using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.17: a store build with no subscription SKU is a free app. It must show no
    /// "Subscribe" button, build no plans panel or plans column, and never open the plans panel;
    /// the same build with a SKU keeps all of them. A store build never shows the web purchase
    /// CTA (URL / QR / price), with or without a SKU.
    /// </summary>
    public class StoreOffersTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject root;
        private GameObject managerGo;
        private LicenseConfig config;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (managerGo != null) Object.DestroyImmediate(managerGo);
            if (config != null) Object.DestroyImmediate(config);
        }

        // ───────────── the rule ─────────────

        [TestCase(StorePlatform.MetaHorizon, "sub_access", true)]
        [TestCase(StorePlatform.MetaHorizon, "", false)]
        [TestCase(StorePlatform.MetaHorizon, "   ", false)]
        [TestCase(StorePlatform.MetaHorizon, null, false)]
        [TestCase(StorePlatform.None, "sub_access", false)]
        [TestCase(StorePlatform.Steam, "sub_access", false)]
        public void OffersNeedAStoreAndASku(StorePlatform platform, string sku, bool expected)
        {
            Assert.AreEqual(expected, StoreOffers.Enabled(platform, sku));
        }

        [TestCase(StorePlatform.None, true, true)]
        [TestCase(StorePlatform.None, false, false)]
        [TestCase(StorePlatform.MetaHorizon, true, false)]
        [TestCase(StorePlatform.MetaHorizon, false, false)]
        public void WebPurchaseCtaOnlyOutsideAStore(StorePlatform platform, bool showPurchaseCta, bool expected)
        {
            Assert.AreEqual(expected, StoreOffers.WebPurchaseCta(platform, showPurchaseCta));
        }

        [Test]
        public void SkuIsTrimmed_AndNullConfigIsEmpty()
        {
            config = ScriptableObject.CreateInstance<LicenseConfig>();
            config.storeSubscriptionSku = "  sub_access ";
            Assert.AreEqual("sub_access", StoreOffers.Sku(config));
            config.storeSubscriptionSku = null;
            Assert.AreEqual("", StoreOffers.Sku(config));
            Assert.AreEqual("", StoreOffers.Sku(null));
        }

        // ───────────── the real UI, both configs ─────────────

        /// <summary>Builds the UI as LicenseUIBuilder.Create does (Create itself needs Play mode).</summary>
        private LicenseUIBuilder Build(StorePlatform effective, string sku, bool showPurchaseCta)
        {
            config = ScriptableObject.CreateInstance<LicenseConfig>();
            config.storePlatform = effective;
            config.storeSubscriptionSku = sku;
            config.showPurchaseCta = showPurchaseCta;

            root = new GameObject("StoreOffersTests UI");
            var builder = root.AddComponent<LicenseUIBuilder>();
            typeof(LicenseUIBuilder).GetField("config", Private).SetValue(builder, config);
            typeof(LicenseUIBuilder).GetMethod("ApplyStoreSettings", Private).Invoke(builder, new object[] { effective });
            typeof(LicenseUIBuilder).GetMethod("BuildUI", Private).Invoke(builder, null);
            return builder;
        }

        private bool Has(string name) =>
            root.GetComponentsInChildren<Transform>(true).Any(t => t.name == name);

        [Test]
        public void StoreBuildWithSku_HasEverySubscribeEntryPoint()
        {
            Build(StorePlatform.MetaHorizon, "sub_access", showPurchaseCta: false);

            Assert.IsTrue(Has("SubscribeBtn"), "welcome panel: Subscribe button");
            Assert.IsTrue(Has("PlansPanel"), "plans panel");
            Assert.IsTrue(Has("ExpStoreColumn"), "demo-expired panel: plans column");
            Assert.IsFalse(Has("ExpBuyTitle") || Has("ExpQrFrame"), "no web CTA in a store build");
        }

        [Test]
        public void FreeStoreBuild_NoSku_HasNoSubscribeEntryPoint()
        {
            var builder = Build(StorePlatform.MetaHorizon, "", showPurchaseCta: false);

            Assert.IsFalse(Has("SubscribeBtn"), "welcome panel must not offer Subscribe");
            Assert.IsFalse(Has("PlansPanel"), "the plans panel must not even exist");
            Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).Any(t => t.name.EndsWith("StoreColumn")),
                "no plans column on the expired panels");
            Assert.IsTrue(Has("ExpOrgTitle"), "the demo-expired panel shows the neutral key hint instead");

            // Asking the UI for the plans anyway does nothing (there is no panel to show).
            Assert.DoesNotThrow(() => builder.ShowPlans(false));
            Assert.IsFalse(Has("PlansPanel"));

            // Welcome: demo, key and QR only.
            var welcome = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "WelcomePanel");
            var buttons = welcome.GetComponentsInChildren<UnityEngine.UI.Button>(true).Select(b => b.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "StartDemoBtn", "EnterKeyBtn", "ScanQRBtn" }, buttons);
        }

        [Test]
        public void FreeStoreBuild_NeverShowsTheWebCta_EvenIfTheConfigAsksForIt()
        {
            // showPurchaseCta defaults to true: a free store build must not fall into the web CTA.
            Build(StorePlatform.MetaHorizon, "", showPurchaseCta: true);

            Assert.IsFalse(Has("ExpBuyTitle"), "no purchase title");
            Assert.IsFalse(Has("ExpQrFrame"), "no QR");
            Assert.IsTrue(Has("ExpOrgTitle"));
        }

        [Test]
        public void DirectApk_KeepsItsWebCta_AndHasNoPlans()
        {
            Build(StorePlatform.None, "sub_access", showPurchaseCta: true);

            Assert.IsFalse(Has("SubscribeBtn"));
            Assert.IsFalse(Has("PlansPanel"));
            Assert.IsTrue(Has("ExpBuyTitle"), "company APKs keep the web purchase CTA");
        }

        // ───────────── the manager (what hosts and pause-menu helpers ask) ─────────────

        private LicenseManager Manager(StorePlatform effective, string sku)
        {
            config = ScriptableObject.CreateInstance<LicenseConfig>();
            config.storePlatform = effective;
            config.storeSubscriptionSku = sku;

            managerGo = new GameObject("StoreOffersTests Manager");
            var manager = managerGo.AddComponent<LicenseManager>();
            typeof(LicenseManager).GetField("config", Private).SetValue(manager, config);
            typeof(LicenseManager).GetField("effectiveStorePlatform", Private).SetValue(manager, effective);
            return manager;
        }

        [Test]
        public void Manager_StoreOffersEnabled_FollowsTheSku()
        {
            Assert.IsTrue(Manager(StorePlatform.MetaHorizon, "sub_access").StoreOffersEnabled);
            Object.DestroyImmediate(managerGo); Object.DestroyImmediate(config);

            Assert.IsFalse(Manager(StorePlatform.MetaHorizon, "").StoreOffersEnabled,
                "hosts hide their own Subscribe buttons (pause menu) on this");
        }

        [Test]
        public void Manager_ShowPlans_IsANoOp_WithoutSku()
        {
            var manager = Manager(StorePlatform.MetaHorizon, "");
            var builder = Build2(manager);

            LogAssert.Expect(LogType.Log, "[VR Licensing] ShowPlans ignored — no store platform, no subscription SKU or no UI.");
            manager.ShowPlans();

            var overlay = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Overlay");
            Assert.IsFalse(overlay.gameObject.activeSelf, "nothing was opened");
            Assert.IsNotNull(builder);
        }

        [Test]
        public void Manager_SubscribeViaStore_Refuses_WithoutSku()
        {
            var manager = Manager(StorePlatform.MetaHorizon, "");
            bool? ok = null;
            manager.SubscribeViaStore("ANNUAL", (success, message) => ok = success);
            Assert.AreEqual(false, ok, "no checkout can be opened without a SKU");
        }

        /// <summary>A UI built from the manager's own config, attached to it like at runtime.</summary>
        private LicenseUIBuilder Build2(LicenseManager manager)
        {
            root = new GameObject("StoreOffersTests UI");
            var builder = root.AddComponent<LicenseUIBuilder>();
            typeof(LicenseUIBuilder).GetField("config", Private).SetValue(builder, config);
            typeof(LicenseUIBuilder).GetField("manager", Private).SetValue(builder, manager);
            typeof(LicenseUIBuilder).GetMethod("ApplyStoreSettings", Private)
                .Invoke(builder, new object[] { manager.EffectiveStorePlatform });
            typeof(LicenseUIBuilder).GetMethod("BuildUI", Private).Invoke(builder, null);
            typeof(LicenseManager).GetField("uiBuilder", Private).SetValue(manager, builder);
            return builder;
        }
    }
}
