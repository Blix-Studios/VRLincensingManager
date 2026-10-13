#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// EDITOR-ONLY stand-in for the Meta Horizon Store AND for the verify-store-entitlement
    /// Edge Function, so the whole subscription flow (plans with prices → checkout → server
    /// verification → SubscribedViaStore → cache) can be exercised in Play mode before the
    /// server is deployed and without Meta test-user credentials. Never compiled into players.
    /// Toggle: Tools > Blix Studios > Licensing > Editor store stub. Prices here are fake
    /// test strings, not the real catalogue prices (Meta owns those).
    /// </summary>
    public sealed class EditorStoreStub : IStorePlatformProvider
    {
        private const string PREF_ENABLED = "VRLicensing.StoreStub.Enabled";
        private const string PREF_SUBSCRIBED_TERM = "VRLicensing.StoreStub.SubscribedTerm";
        private const string PREF_SERVER_DOWN = "VRLicensing.StoreStub.ServerDown";
        private const string PREF_ENTITLED = "VRLicensing.StoreStub.Entitled";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PREF_ENABLED, false);
            set => EditorPrefs.SetBool(PREF_ENABLED, value);
        }

        /// <summary>Term the stub user is already subscribed to ("" = not subscribed).</summary>
        public static string SubscribedTerm
        {
            get => EditorPrefs.GetString(PREF_SUBSCRIBED_TERM, "");
            set => EditorPrefs.SetString(PREF_SUBSCRIBED_TERM, value ?? "");
        }

        /// <summary>Simulates the Edge Function being unreachable / not deployed.</summary>
        public static bool ServerDown
        {
            get => EditorPrefs.GetBool(PREF_SERVER_DOWN, false);
            set => EditorPrefs.SetBool(PREF_SERVER_DOWN, value);
        }

        /// <summary>Simulates the app-ownership check result.</summary>
        public static bool Entitled
        {
            get => EditorPrefs.GetBool(PREF_ENTITLED, true);
            set => EditorPrefs.SetBool(PREF_ENTITLED, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            if (!Enabled)
            {
                StoreVerification.StubVerifier = null;
                return;
            }
            // Priority 100 beats the real SDK provider when both are present.
            StorePlatformRegistry.Register(StorePlatform.MetaHorizon, () => new EditorStoreStub(), 100, "EDITOR STUB");
            StoreVerification.StubVerifier = VerifyStub;
            Debug.LogWarning("[VR Licensing] EDITOR STORE STUB is ON: fake store + fake server verification. " +
                             "Disable it in Tools > Blix Studios > Licensing before trusting any result.");
        }

        // ── Menu ──

        [MenuItem("Tools/Blix Studios/Licensing/Editor store stub (toggle)")]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Debug.Log($"[VR Licensing] Editor store stub {(Enabled ? "ENABLED" : "disabled")} — takes effect on the next Play.");
        }

        [MenuItem("Tools/Blix Studios/Licensing/Editor store stub (toggle)", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("Tools/Blix Studios/Licensing/Editor store stub (toggle)", Enabled);
            return true;
        }

        [MenuItem("Tools/Blix Studios/Licensing/Store stub: user already subscribed (ANNUAL)")]
        private static void SetSubscribedAnnual() { SubscribedTerm = "ANNUAL"; Debug.Log("[VR Licensing] Stub user subscribed: ANNUAL"); }

        [MenuItem("Tools/Blix Studios/Licensing/Store stub: user not subscribed")]
        private static void SetNotSubscribed() { SubscribedTerm = ""; Debug.Log("[VR Licensing] Stub user not subscribed"); }

        [MenuItem("Tools/Blix Studios/Licensing/Store stub: server down (toggle)")]
        private static void ToggleServerDown() { ServerDown = !ServerDown; Debug.Log($"[VR Licensing] Stub server down: {ServerDown}"); }

        [MenuItem("Tools/Blix Studios/Licensing/Store stub: entitlement fails (toggle)")]
        private static void ToggleEntitled() { Entitled = !Entitled; Debug.Log($"[VR Licensing] Stub entitled: {Entitled}"); }

        [MenuItem("Tools/Blix Studios/Licensing/Clear cached license + store data (PlayerPrefs)")]
        private static void ClearPrefs()
        {
            SecureLicenseStorage.ClearAll();
            Debug.Log("[VR Licensing] Cached license / demo / store data cleared.");
        }

        // ── Fake store ──

        private static readonly Dictionary<string, string> FakePrices = new Dictionary<string, string>
        {
            { "MONTHLY", "US$49.00 (stub)" },
            { "SEMIANNUAL", "US$249.00 (stub)" },
            { "ANNUAL", "US$399.00 (stub)" },
        };

        public StorePlatform Platform => StorePlatform.MetaHorizon;
        public string PlatformId => "meta_quest";
        public bool IsInitialized { get; private set; }
        public bool? IsEntitled { get; private set; }

        public IEnumerator Initialize(Action<bool, string> onDone)
        {
            yield return null;
            IsInitialized = true;
            onDone?.Invoke(true, null);
        }

        public IEnumerator CheckEntitlement(Action<bool, string> onDone)
        {
            yield return null;
            IsEntitled = Entitled;
            onDone?.Invoke(Entitled, Entitled ? null : "stub: entitlement failure requested");
        }

        public IEnumerator GetSubscriptionProduct(string sku, Action<StoreProductInfo, string> onDone)
        {
            yield return null;
            var product = new StoreProductInfo { sku = sku, name = "Stub subscription", isSubscription = true };
            foreach (var kv in FakePrices)
                product.terms.Add(new StoreTermPrice { term = kv.Key, formattedPrice = kv.Value, currency = "USD" });
            onDone?.Invoke(product, null);
        }

        public IEnumerator GetActiveSubscription(string sku, Action<StorePurchaseInfo, string> onDone)
        {
            yield return null;
            string term = SubscribedTerm;
            if (string.IsNullOrEmpty(term)) { onDone?.Invoke(null, null); yield break; }
            onDone?.Invoke(new StorePurchaseInfo
            {
                sku = sku, term = term, purchaseId = "stub-purchase", isSubscription = true,
                expirationUtc = DateTime.UtcNow.AddDays(TermDays(term)), grantUtc = DateTime.UtcNow
            }, null);
        }

        public IEnumerator LaunchCheckout(string checkoutSku, Action<StoreCheckoutResult> onDone)
        {
            yield return new WaitForSecondsRealtime(1f); // pretend the Meta checkout UI was up
            StoreSubscription.ParseCheckoutSku(checkoutSku, out string sku, out string term);
            SubscribedTerm = term;
            Debug.Log($"[VR Licensing] STUB checkout completed: {checkoutSku}");
            onDone?.Invoke(new StoreCheckoutResult
            {
                outcome = StoreCheckoutOutcome.Completed,
                purchase = new StorePurchaseInfo
                {
                    sku = sku, term = term, purchaseId = "stub-purchase", isSubscription = true,
                    expirationUtc = DateTime.UtcNow.AddDays(TermDays(term)), grantUtc = DateTime.UtcNow
                }
            });
        }

        public IEnumerator GetUserIdentity(Action<string, string, string> onDone)
        {
            yield return null;
            onDone?.Invoke("stub-meta-user", Guid.NewGuid().ToString("N"), null);
        }

        // ── Fake server (verify-store-entitlement) ──

        private static StoreVerifyResult VerifyStub(string userId, string nonce, int productId)
        {
            if (ServerDown) return null; // "could not reach the server"
            string term = SubscribedTerm;
            if (string.IsNullOrEmpty(term))
                return new StoreVerifyResult { entitled = false, status = "expired" };
            return new StoreVerifyResult
            {
                entitled = true,
                sku = "sub_access",
                term = term,
                status = "active",
                expiresAt = DateTime.UtcNow.AddDays(TermDays(term)).ToString("o", CultureInfo.InvariantCulture),
                graceUntil = ""
            };
        }

        private static int TermDays(string term)
        {
            switch (StoreSubscription.NormalizeTerm(term))
            {
                case "WEEKLY": return 7;
                case "BIWEEKLY": return 14;
                case "MONTHLY": return 30;
                case "QUARTERLY": return 91;
                case "SEMIANNUAL": return 182;
                case "BIANNUAL": return 730;
                default: return 365;
            }
        }
    }
}
#endif
