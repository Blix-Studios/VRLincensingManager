using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRLicensing.Tests
{
    /// <summary>
    /// The panels are built by code with fixed sizes, so a text can fit its own rect (overflow
    /// checks pass) while a button sits on top of that rect. These tests build the real UI and
    /// compare the rects themselves, in the store and direct-APK variants, which have different
    /// controls. The language does not matter: the rects do not depend on the text.
    /// </summary>
    public class PanelLayoutTests
    {
        private GameObject root;
        private LicenseConfig config;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }

        /// <summary>Panel root and the text under its header that the controls must stay clear of.</summary>
        private static readonly (string panel, string subtitle)[] Panels =
        {
            ("WelcomePanel", "WelcomeSubtitle"),
            ("KeyInputPanel", "KeySubtitle"),
            ("DemoExpiredPanel", "ExpMsg"),
            ("LicenseExpiredPanel", "LicExpMsg"),
        };

        private static IEnumerable<TestCaseData> AllPanels()
        {
            foreach (var p in Panels)
                foreach (bool store in new[] { true, false })
                    yield return new TestCaseData(p.panel, p.subtitle, store)
                        .SetName($"{p.panel}_{(store ? "Store" : "DirectApk")}");
        }

        /// <summary>Builds the UI through the same private path LicenseUIBuilder.Create uses
        /// (Create itself needs Play mode: it calls DontDestroyOnLoad).</summary>
        private RectTransform BuildAndShow(string panelName, bool storeOffers)
        {
            config = ScriptableObject.CreateInstance<LicenseConfig>();
            root = new GameObject("PanelLayoutTests UI");
            var builder = root.AddComponent<LicenseUIBuilder>();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LicenseUIBuilder).GetField("config", flags).SetValue(builder, config);
            typeof(LicenseUIBuilder).GetField("storeOffersEnabled", flags).SetValue(builder, storeOffers);
            typeof(LicenseUIBuilder).GetMethod("BuildUI", flags).Invoke(builder, null);

            var overlay = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Overlay");
            overlay.gameObject.SetActive(true);
            foreach (Transform child in overlay) child.gameObject.SetActive(child.name == panelName);

            var panel = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == panelName);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            foreach (var group in panel.GetComponentsInChildren<LayoutGroup>(false))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            return panel;
        }

        /// <summary>The rect of a transform in the panel's own coordinates.</summary>
        private static Rect RectIn(RectTransform panel, RectTransform t)
        {
            var corners = new Vector3[4];
            t.GetWorldCorners(corners);
            Vector3 a = panel.InverseTransformPoint(corners[0]);
            Vector3 b = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private static List<RectTransform> Controls(RectTransform panel)
        {
            var list = new List<RectTransform>();
            list.AddRange(panel.GetComponentsInChildren<Button>(false).Select(b => (RectTransform)b.transform));
            list.AddRange(panel.GetComponentsInChildren<TMP_InputField>(false).Select(f => (RectTransform)f.transform));
            return list;
        }

        private static string Describe(Rect r) =>
            $"x {r.xMin:0}..{r.xMax:0} y {r.yMin:0}..{r.yMax:0}";

        [TestCaseSource(nameof(AllPanels))]
        public void Subtitle_DoesNotIntersect_AnyControl(string panelName, string subtitleName, bool store)
        {
            var panel = BuildAndShow(panelName, store);
            var subtitle = panel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == subtitleName);
            Rect subtitleRect = RectIn(panel, subtitle.rectTransform);

            var controls = Controls(panel);
            Assert.IsNotEmpty(controls, $"{panelName} has no visible controls: the test would pass for nothing.");
            foreach (var control in controls)
            {
                Rect r = RectIn(panel, control);
                Assert.IsFalse(subtitleRect.Overlaps(r),
                    $"{panelName}: '{subtitleName}' ({Describe(subtitleRect)}) intersects '{control.name}' ({Describe(r)})");
            }
        }

        [TestCaseSource(nameof(AllPanels))]
        public void Controls_DoNotIntersect_EachOther(string panelName, string subtitleName, bool store)
        {
            var panel = BuildAndShow(panelName, store);
            var controls = Controls(panel);
            for (int i = 0; i < controls.Count; i++)
            {
                for (int j = i + 1; j < controls.Count; j++)
                {
                    Rect a = RectIn(panel, controls[i]), b = RectIn(panel, controls[j]);
                    Assert.IsFalse(a.Overlaps(b),
                        $"{panelName}: '{controls[i].name}' ({Describe(a)}) intersects '{controls[j].name}' ({Describe(b)})");
                }
            }
        }

        /// <summary>The same pattern with a text instead of a button: the message box of the
        /// license-expired panel reached down into the column titles.</summary>
        [TestCaseSource(nameof(AllPanels))]
        public void Subtitle_DoesNotIntersect_OtherTexts(string panelName, string subtitleName, bool store)
        {
            var panel = BuildAndShow(panelName, store);
            var texts = panel.GetComponentsInChildren<TMP_Text>(false)
                .Where(t => t.GetComponentInParent<Button>() == null && t.GetComponentInParent<TMP_InputField>() == null)
                .ToList();
            var subtitle = panel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == subtitleName);
            Rect subtitleRect = RectIn(panel, subtitle.rectTransform);

            foreach (var text in texts)
            {
                if (text == subtitle) continue;
                Rect r = RectIn(panel, text.rectTransform);
                Assert.IsFalse(subtitleRect.Overlaps(r),
                    $"{panelName}: '{subtitleName}' ({Describe(subtitleRect)}) intersects '{text.name}' ({Describe(r)})");
            }
        }

        /// <summary>The column must stay between the subtitle and the info line with both button
        /// counts: three on a direct APK, four when the store adds "Subscribe".</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void WelcomeButtons_StayBetween_SubtitleAndInfoLine(bool store)
        {
            var panel = BuildAndShow("WelcomePanel", store);
            var texts = panel.GetComponentsInChildren<TMP_Text>(true);
            Rect subtitle = RectIn(panel, texts.First(t => t.name == "WelcomeSubtitle").rectTransform);
            Rect info = RectIn(panel, texts.First(t => t.name == "WelcomeInfo").rectTransform);

            var buttons = panel.GetComponentsInChildren<Button>(false).Select(b => RectIn(panel, (RectTransform)b.transform)).ToList();
            Assert.AreEqual(store ? 4 : 3, buttons.Count);
            Assert.LessOrEqual(buttons.Max(r => r.yMax), subtitle.yMin, "the top button reaches the subtitle");
            Assert.GreaterOrEqual(buttons.Min(r => r.yMin), info.yMax, "the bottom button reaches the info line");
        }
    }
}
