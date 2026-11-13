using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.18: every label of every license panel is drawn and fits, in the 16 languages.
    /// VR-Forklift QA on pre.17: in Arabic and Urdu the column titles of the demo-expired panel
    /// were not drawn at all — a 16 pt line of an Arabic face needs about 34 units, the rects are
    /// 26 high, and TMP's Ellipsis mode draws nothing when not even one line fits.
    /// The panels are built for real, with fonts that have the scripts (system fonts; the test
    /// is skipped where they are missing), once with the fonts' own metrics and once with the
    /// line made as tall as the face that showed the bug (2.11 x the font size).
    /// </summary>
    public class LabelFitTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Line height of the Arabic face in VR-Forklift, per unit of font size (33.8 at 16 pt).</summary>
        private const float TallLine = 2.11f;

        private static readonly string[] Variants = { "StoreWithSku", "FreeStoreApp", "DirectApk" };

        private static TMP_FontAsset normalFont, tallFont;
        private static string fontProblem;
        private TMP_FontAsset defaultBefore;
        private string languageBefore;
        private GameObject root;
        private LicenseConfig config;

        // ───────────── fonts ─────────────

        private static readonly string[] FontFiles =
        {
            "arial.ttf",      // Latin, Cyrillic, Arabic + presentation forms (primary)
            "tahoma.ttf",     // more Arabic-script coverage (Urdu)
            "Nirmala.ttc",    // Devanagari
            "malgun.ttf",     // Korean
            "YuGothM.ttc",    // Japanese
            "msgothic.ttc",   // Japanese (older systems)
            "msyh.ttc",       // Chinese
            "simsun.ttc",     // Chinese (older systems)
        };

        private static TMP_FontAsset Load(string file, float lineFactor)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
            if (!File.Exists(path)) return null;
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(path, 0, 40, 4, GlyphRenderMode.SDFAA, 2048, 2048);
            if (font == null) return null;
            font.name = file + (lineFactor > 0f ? " (tall line)" : "");
            font.isMultiAtlasTexturesEnabled = true;
            font.hideFlags = HideFlags.DontSave;
            if (lineFactor > 0f)
            {
                // Same glyphs, the vertical metrics of a face with a tall line.
                FaceInfo face = font.faceInfo;
                float em = face.pointSize;
                face.ascentLine = em * lineFactor * 0.69f;
                face.descentLine = -em * lineFactor * 0.31f;
                face.lineHeight = em * lineFactor;
                font.faceInfo = face;
            }
            return font;
        }

        private static TMP_FontAsset Compose(float lineFactor)
        {
            TMP_FontAsset main = null;
            var fallbacks = new List<TMP_FontAsset>();
            foreach (string file in FontFiles)
            {
                TMP_FontAsset font = Load(file, lineFactor);
                if (font == null) continue;
                if (main == null) main = font; else fallbacks.Add(font);
            }
            if (main != null) main.fallbackFontAssetTable = fallbacks;
            return main;
        }

        [OneTimeSetUp]
        public void CreateFonts()
        {
            try
            {
                if (TMP_Settings.instance == null) { fontProblem = "the project has no TMP settings (import TMP Essential Resources)"; return; }
                normalFont = Compose(0f);
                tallFont = Compose(TallLine);
                if (normalFont == null || tallFont == null) fontProblem = "no system font file found (" + FontFiles[0] + "…)";
            }
            catch (Exception e)
            {
                fontProblem = "fonts could not be created: " + e.Message;
            }
        }

        [OneTimeTearDown]
        public void DestroyFonts()
        {
            foreach (TMP_FontAsset main in new[] { normalFont, tallFont })
            {
                if (main == null) continue;
                foreach (TMP_FontAsset f in main.fallbackFontAssetTable) if (f != null) UnityEngine.Object.DestroyImmediate(f);
                UnityEngine.Object.DestroyImmediate(main);
            }
            normalFont = tallFont = null;
        }

        [SetUp]
        public void SetUp()
        {
            languageBefore = LicenseStrings.LanguageOverride;
            defaultBefore = TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
        }

        [TearDown]
        public void TearDown()
        {
            LicenseStrings.LanguageOverride = languageBefore;
            if (TMP_Settings.instance != null) TMP_Settings.defaultFontAsset = defaultBefore;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        // ───────────── building and measuring ─────────────

        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (string lang in LicenseStrings.SupportedLanguages)
                foreach (string variant in Variants)
                    yield return new TestCaseData(lang, variant).SetName("{m}(" + lang + "," + variant + ")");
        }

        private LicenseUIBuilder Build(string language, string variant, TMP_FontAsset font)
        {
            // What a host does when it has a font per language: it becomes TMP's default.
            TMP_Settings.defaultFontAsset = font;
            LicenseStrings.LanguageOverride = language;

            config = ScriptableObject.CreateInstance<LicenseConfig>();
            config.appDisplayName = "Forklift VR Instructor";
            StorePlatform platform = variant == "DirectApk" ? StorePlatform.None : StorePlatform.MetaHorizon;
            config.storePlatform = platform;
            config.storeSubscriptionSku = variant == "StoreWithSku" ? "sub_access" : "";
            config.showPurchaseCta = variant == "DirectApk";

            root = new GameObject("LabelFitTests UI");
            var builder = root.AddComponent<LicenseUIBuilder>();
            typeof(LicenseUIBuilder).GetField("config", Private).SetValue(builder, config);
            typeof(LicenseUIBuilder).GetMethod("ApplyStoreSettings", Private).Invoke(builder, new object[] { platform });
            typeof(LicenseUIBuilder).GetMethod("BuildUI", Private).Invoke(builder, null);
            return builder;
        }

        private static int VisibleCharacters(TMP_Text label)
        {
            TMP_TextInfo info = label.textInfo;
            int n = 0;
            for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
                if (info.characterInfo[i].isVisible) n++;
            return n;
        }

        private sealed class Finding
        {
            public string where;
            public bool invisible, overflowing;
            public override string ToString() => where;
        }

        /// <summary>Shows every panel in turn and measures every label that has text.</summary>
        private List<Finding> Measure(LicenseUIBuilder builder, out int measured)
        {
            var findings = new List<Finding>();
            measured = 0;
            MethodInfo sync = typeof(LicenseUIBuilder).GetMethod("SyncRtl", Private);
            typeof(LicenseUIBuilder).GetMethod("RefreshWelcomeInfo", Private).Invoke(builder, null);

            // Texts that are only written when their panel is shown.
            var licExpMsg = root.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(l => l.name == "LicExpMsg");
            MethodInfo licenseExpiredMessage = typeof(LicenseUIBuilder).GetMethod("LicenseExpiredMessage", Private);

            var overlay = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Overlay");
            overlay.gameObject.SetActive(true);
            var panels = new List<Transform>();
            foreach (Transform child in overlay) panels.Add(child);

            foreach (Transform panel in panels)
            {
                foreach (Transform other in panels) other.gameObject.SetActive(other == panel);

                var passes = new List<string> { "" };
                if (panel.name == "LicenseExpiredPanel") passes = new List<string> { "key expired", "key revoked", "subscription ended" };
                foreach (string pass in passes)
                {
                    if (licExpMsg != null && pass != "")
                        licExpMsg.text = (string)licenseExpiredMessage.Invoke(builder,
                            new object[] { pass == "subscription ended", pass == "key revoked" ? "revoked" : null });

                    Canvas.ForceUpdateCanvases();
                    foreach (var group in panel.GetComponentsInChildren<LayoutGroup>(false))
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);

                    var labels = panel.GetComponentsInChildren<TextMeshProUGUI>(false);
                    foreach (var label in labels) label.ForceMeshUpdate(true, true);
                    sync.Invoke(builder, null);                       // RTL mode + rescue, as every frame at runtime
                    foreach (var label in labels) label.ForceMeshUpdate(true, true);

                    foreach (var label in labels)
                    {
                        if (string.IsNullOrWhiteSpace(label.text)) continue;
                        measured++;
                        bool invisible = VisibleCharacters(label) == 0;
                        bool overflowing = label.isTextOverflowing;
                        if (!invisible && !overflowing) continue;
                        findings.Add(new Finding
                        {
                            invisible = invisible,
                            overflowing = overflowing,
                            where = $"{panel.name}/{label.name}{(pass == "" ? "" : " [" + pass + "]")} " +
                                    $"({(invisible ? "NOT DRAWN" : "overflows")}, {label.fontSize:0.#} pt of {label.fontSizeMax:0.#}, " +
                                    $"rect {label.rectTransform.rect.width:0}x{label.rectTransform.rect.height:0}, " +
                                    $"needs {label.preferredHeight:0.#} high) \"{Short(label.text)}\"",
                        });
                    }
                }
            }
            return findings;
        }

        private static string Short(string text)
        {
            text = text.Replace("\n", " / ");
            return text.Length > 46 ? text.Substring(0, 46) + "…" : text;
        }

        // ───────────── the tests ─────────────

        [TestCaseSource(nameof(Cases))]
        public void EveryLabel_IsDrawn_AndFits(string language, string variant)
        {
            if (fontProblem != null) Assert.Ignore(fontProblem);
            var builder = Build(language, variant, normalFont);

            // The panel must really be in the language under test, not in the English fallback.
            var subtitle = root.GetComponentsInChildren<TextMeshProUGUI>(true).First(l => l.name == "WelcomeSubtitle");
            if (subtitle.text != LicenseStrings.Get(LicenseStrings.Key.WelcomeSubtitle, language))
                Assert.Ignore($"no system font on this machine draws '{language}'");

            List<Finding> findings = Measure(builder, out int measured);

            Assert.Greater(measured, 25, "labels measured");
            Assert.IsEmpty(findings.Where(f => f.invisible).ToList(), "labels with text that draw nothing");
            Assert.IsEmpty(findings.Where(f => f.overflowing).ToList(), "labels whose text does not fit");
        }

        [TestCaseSource(nameof(Cases))]
        public void EveryLabel_IsDrawn_WithATallLineFont(string language, string variant)
        {
            // The pre.17 defect: with a face whose line is 2.11 x the font size, the 16 pt titles
            // in 26-high rects were not drawn. Now they shrink to fit; nothing may disappear.
            if (fontProblem != null) Assert.Ignore(fontProblem);
            var builder = Build(language, variant, tallFont);

            List<Finding> findings = Measure(builder, out int measured);

            Assert.Greater(measured, 25, "labels measured");
            Assert.IsEmpty(findings.Where(f => f.invisible).ToList(), "labels with text that draw nothing");

            // The single-line titles named in the defect report must also fit (shrunk), not just show.
            string[] reported = { "ExpOrgTitle", "ExpRedeemTitle", "LicExpRedeemTitle", "ExpStoreTitle", "LicExpStoreTitle",
                                  "PlansStoreTitle", "ExpBuyTitle", "KeySubtitle" };
            var titles = findings.Where(f => f.overflowing && reported.Any(name => f.where.Contains("/" + name + " "))).ToList();
            Assert.IsEmpty(titles, "titles that still do not fit");
        }

        [Test]
        public void ReportedTitles_ShrinkInsteadOfDisappearing()
        {
            if (fontProblem != null) Assert.Ignore(fontProblem);
            var builder = Build("es", "FreeStoreApp", tallFont);
            Measure(builder, out _);

            var overlay = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Overlay");
            foreach (Transform child in overlay) child.gameObject.SetActive(child.name == "DemoExpiredPanel");
            Canvas.ForceUpdateCanvases();

            foreach (string name in new[] { "ExpOrgTitle", "ExpRedeemTitle" })
            {
                var label = root.GetComponentsInChildren<TextMeshProUGUI>(true).First(l => l.name == name);
                label.ForceMeshUpdate(true, true);
                Assert.IsTrue(label.enableAutoSizing, name);
                Assert.AreEqual(16f, label.fontSizeMax, name + ": design size");
                Assert.Less(label.fontSize, 16f, name + " shrank: one 16 pt line is " + (16f * TallLine) + " high, the rect 26");
                Assert.GreaterOrEqual(label.fontSize, label.fontSizeMin, name);
                Assert.Greater(VisibleCharacters(label), 0, name + " is drawn");
                Assert.IsFalse(label.isTextOverflowing, name + " fits");
            }
        }

        [Test]
        public void LabelWithNoRoomForOneLine_IsDrawnAnyway()
        {
            // Last resort: even at the smallest size the line does not fit → Overflow, not nothing.
            if (fontProblem != null) Assert.Ignore(fontProblem);
            var builder = Build("en", "FreeStoreApp", tallFont);
            var label = root.GetComponentsInChildren<TextMeshProUGUI>(true).First(l => l.name == "ExpOrgTitle");
            var overlay = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Overlay");
            overlay.gameObject.SetActive(true);
            foreach (Transform child in overlay) child.gameObject.SetActive(child.name == "DemoExpiredPanel");

            label.rectTransform.sizeDelta = new Vector2(410, 6);   // nothing fits in 6 units
            label.ForceMeshUpdate(true, true);
            Assert.AreEqual(0, VisibleCharacters(label), "this is the state the rescue is for");

            typeof(LicenseUIBuilder).GetMethod("SyncRtl", Private).Invoke(builder, null);
            label.ForceMeshUpdate(true, true);

            Assert.AreEqual(TextOverflowModes.Overflow, label.overflowMode);
            Assert.Greater(VisibleCharacters(label), 0, "drawn across the rect's edge rather than not at all");
        }

        [Test]
        public void MinimumSize_IsAFractionOfTheDesignSize()
        {
            var min = typeof(LicenseUIBuilder).GetMethod("MinFontSize", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(8f, (float)min.Invoke(null, new object[] { 16f }));
            Assert.AreEqual(7f, (float)min.Invoke(null, new object[] { 13f }));
            Assert.AreEqual(7f, (float)min.Invoke(null, new object[] { 10f }), "never under 7");
            Assert.AreEqual(13f, (float)min.Invoke(null, new object[] { 24f }));
        }

        [Test]
        public void Urdu_SaysKaleedForALicenseKey()
        {
            // «کی» (the loanword "key") reads as the genitive particle; «کلید» is unambiguous.
            string[] keys =
            {
                LicenseStrings.Key.EnterKey, LicenseStrings.Key.EnterKeyTitle, LicenseStrings.Key.HaveKeyTitle,
                LicenseStrings.Key.KeyHint, LicenseStrings.Key.EnterFullKey, LicenseStrings.Key.EnterKeyPrompt,
                LicenseStrings.Key.InvalidKey, LicenseStrings.Key.LicenseNotActive, LicenseStrings.Key.ChoosePlanOrKey,
                LicenseStrings.Key.ChoosePlanOrNewKey, LicenseStrings.Key.RenewOnWeb, LicenseStrings.Key.AskOrgForKey,
                LicenseStrings.Key.KeyRedeemedTitle, LicenseStrings.Key.ScanSubtitle, LicenseStrings.Key.EnterKeyManually,
                LicenseStrings.Key.CameraNotShared, LicenseStrings.Key.NoQrDetected, LicenseStrings.Key.OrgBody,
            };
            foreach (string key in keys)
                StringAssert.Contains("کلید", LicenseStrings.Get(key, "ur"), key);
            StringAssert.Contains("کلید", LicenseStrings.CompanyHint("ur"));
            Assert.AreEqual("کیا آپ کے پاس لائسنس کلید ہے؟", LicenseStrings.Get(LicenseStrings.Key.HaveKeyTitle, "ur"));

            // No string is left where «کی» is followed by the verbs that only a "key" takes.
            foreach (string key in LicenseStrings.UiKeys)
            {
                string ur = LicenseStrings.Get(key, "ur");
                foreach (string wrong in new[] { "کی درج", "کی طلب کریں", "کی خود", "کی ہے؟", "کی غلط" })
                    StringAssert.DoesNotContain(wrong, ur, key);
            }
        }
    }
}
