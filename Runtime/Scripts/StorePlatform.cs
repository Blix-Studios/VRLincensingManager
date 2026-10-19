using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>Store a build is distributed through (LicenseConfig.storePlatform).</summary>
    public enum StorePlatform
    {
        /// <summary>Company APKs / sideloaded builds. No store integration.</summary>
        None = 0,
        /// <summary>Meta Horizon Store: entitlement check + Meta in-app subscriptions.</summary>
        MetaHorizon = 1,
        /// <summary>Reserved for phase 2. Behaves like None for now.</summary>
        Steam = 2
    }

    /// <summary>One billing plan (term) of a subscription product, priced by the store.</summary>
    public sealed class StoreTermPrice
    {
        /// <summary>Meta OfferTerm token: MONTHLY, SEMIANNUAL, ANNUAL…</summary>
        public string term;
        /// <summary>Localized price string exactly as the store formats it (e.g. "$49.99").</summary>
        public string formattedPrice;
        /// <summary>Numeric price in hundredths as the store reports it (Price.AmountInHundredths); 0 = not provided.</summary>
        public uint amountInHundredths;
        public string currency;
        public bool hasTrial;

        /// <summary>
        /// Numeric price: the store's amount when it gave one, otherwise a robust parse of the
        /// formatted string. False when neither is usable.
        /// </summary>
        public bool TryGetAmount(out decimal amount)
        {
            if (amountInHundredths > 0)
            {
                amount = amountInHundredths / 100m;
                return true;
            }
            return StorePriceParser.TryParse(formattedPrice, out amount);
        }
    }

    /// <summary>
    /// Reads a store-formatted price ("US$399.99", "399,99 €", "1.399,99 €", "¥39,999",
    /// "$1,399.99", "CHF 399.–") into a decimal without knowing the locale. Currency symbols,
    /// codes and spaces are ignored; the LAST of ',' / '.' is the decimal separator when it is
    /// followed by 1–2 digits, otherwise every separator is a thousands separator.
    /// </summary>
    public static class StorePriceParser
    {
        public static bool TryParse(string formatted, out decimal amount)
        {
            amount = 0m;
            if (string.IsNullOrWhiteSpace(formatted)) return false;

            // Keep digits and the two candidate separators only. Non-ASCII digits are not
            // expected from Meta; anything else (symbols, codes, nbsp, "–") is noise.
            var sb = new System.Text.StringBuilder(formatted.Length);
            foreach (char c in formatted)
            {
                if (c >= '0' && c <= '9') sb.Append(c);
                else if (c == ',' || c == '.') sb.Append(c);
            }
            string s = sb.ToString().Trim(',', '.');
            if (s.Length == 0) return false;

            int lastComma = s.LastIndexOf(',');
            int lastDot = s.LastIndexOf('.');
            int last = Math.Max(lastComma, lastDot);

            string integerPart, fractionPart = "";
            if (last < 0)
            {
                integerPart = s;
            }
            else
            {
                char sep = s[last];
                int digitsAfter = s.Length - last - 1;
                int sepCount = 0;
                foreach (char c in s) if (c == sep) sepCount++;

                bool isDecimal = digitsAfter >= 1 && digitsAfter <= 2 && sepCount == 1;
                if (isDecimal)
                {
                    integerPart = s.Substring(0, last);
                    fractionPart = s.Substring(last + 1);
                }
                else
                {
                    integerPart = s; // all separators are grouping separators
                }
            }

            integerPart = integerPart.Replace(",", "").Replace(".", "");
            if (integerPart.Length == 0) integerPart = "0";
            if (!decimal.TryParse(integerPart, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal whole))
                return false;

            decimal fraction = 0m;
            if (fractionPart.Length > 0)
            {
                if (!decimal.TryParse(fractionPart, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal f))
                    return false;
                fraction = f / (fractionPart.Length == 1 ? 10m : 100m);
            }

            amount = whole + fraction;
            return amount > 0m;
        }
    }

    /// <summary>A subscription product as reported by the store (spec §1.0: one SKU per tier, terms as billing plans).</summary>
    public sealed class StoreProductInfo
    {
        public string sku;
        public string name;
        public bool isSubscription;
        public readonly List<StoreTermPrice> terms = new List<StoreTermPrice>();

        public StoreTermPrice FindTerm(string term)
        {
            if (string.IsNullOrEmpty(term)) return null;
            foreach (var t in terms)
                if (t != null && string.Equals(t.term, term, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
    }

    /// <summary>A purchase / subscription the store reports for the signed-in user.</summary>
    public sealed class StorePurchaseInfo
    {
        public string sku;
        public string term;        // when the store reports it (checkout result), else empty
        public string purchaseId;
        /// <summary>UTC expiration reported by the store; DateTime.MaxValue when the store reports none.</summary>
        public DateTime expirationUtc = DateTime.MaxValue;
        public DateTime grantUtc = DateTime.MinValue;
        public bool isSubscription;

        public bool IsActiveNow => DateTime.UtcNow < expirationUtc;
    }

    public enum StoreCheckoutOutcome { Completed, Cancelled, Failed }

    public sealed class StoreCheckoutResult
    {
        public StoreCheckoutOutcome outcome;
        public StorePurchaseInfo purchase;
        public string error;
    }

    /// <summary>
    /// Thin, SDK-agnostic view of a store platform. Implemented in an optional assembly per
    /// store (VRLicensing.MetaStore for the Meta XR Platform SDK) so the runtime assembly
    /// never references a store SDK. All calls are coroutines driven by LicenseManager.
    /// </summary>
    public interface IStorePlatformProvider
    {
        StorePlatform Platform { get; }

        /// <summary>Identifier sent to the server: "meta_quest", later "steam".</summary>
        string PlatformId { get; }

        bool IsInitialized { get; }

        /// <summary>null = not checked yet / unknown.</summary>
        bool? IsEntitled { get; }

        /// <summary>Platform (SDK) initialization. Never throws; errors go to the callback.</summary>
        IEnumerator Initialize(Action<bool, string> onDone);

        /// <summary>App-ownership (entitlement) check for the signed-in account.</summary>
        IEnumerator CheckEntitlement(Action<bool, string> onDone);

        /// <summary>
        /// The subscription product for one SKU with its billing plans (term + localized price).
        /// Null product (no error) when the store does not know the SKU.
        /// </summary>
        IEnumerator GetSubscriptionProduct(string sku, Action<StoreProductInfo, string> onDone);

        /// <summary>
        /// The viewer's currently active subscription for the SKU (latest expiration wins), or
        /// null when there is none. Error only when the store call itself failed.
        /// </summary>
        IEnumerator GetActiveSubscription(string sku, Action<StorePurchaseInfo, string> onDone);

        /// <summary>Opens the store's own checkout UI. <paramref name="checkoutSku"/> = "{sku}:SUBSCRIPTION__{TERM}".</summary>
        IEnumerator LaunchCheckout(string checkoutSku, Action<StoreCheckoutResult> onDone);

        /// <summary>
        /// Signed-in store user id plus a one-time proof (nonce) the server validates with the
        /// store. Used only by the server-authoritative subscription verification.
        /// </summary>
        IEnumerator GetUserIdentity(Action<string, string, string> onDone);
    }

    /// <summary>
    /// Registry the optional store assemblies register into at AfterAssembliesLoaded (which runs
    /// before LicenseBootstrapper's BeforeSceneLoad). The runtime resolves providers from here
    /// without any static reference to a store SDK. Higher priority wins (the editor stub uses it).
    /// </summary>
    public static class StorePlatformRegistry
    {
        private sealed class Entry { public Func<IStorePlatformProvider> factory; public int priority; public string name; }

        private static readonly Dictionary<StorePlatform, Entry> entries = new Dictionary<StorePlatform, Entry>();

        public static void Register(StorePlatform platform, Func<IStorePlatformProvider> factory, int priority = 0, string name = null)
        {
            if (factory == null) return;
            if (entries.TryGetValue(platform, out var existing) && existing.priority > priority) return;
            entries[platform] = new Entry { factory = factory, priority = priority, name = name ?? "provider" };
            Debug.Log($"[VR Licensing] Store provider registered: {platform} ({entries[platform].name})");
        }

        public static bool IsAvailable(StorePlatform platform) =>
            platform != StorePlatform.None && entries.ContainsKey(platform);

        /// <summary>
        /// The platform the build will actually behave as: the requested one when a provider is
        /// registered, otherwise None (Steam is always None until phase 2).
        /// </summary>
        public static StorePlatform Effective(StorePlatform requested) =>
            IsAvailable(requested) ? requested : StorePlatform.None;

        public static IStorePlatformProvider Create(StorePlatform platform)
        {
            if (!entries.TryGetValue(platform, out var entry)) return null;
            try
            {
                return entry.factory();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] Store provider for {platform} failed to create: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Why a requested platform is not available, for the console warning. Null when it is.
        /// </summary>
        public static string UnavailableReason(StorePlatform requested)
        {
            switch (requested)
            {
                case StorePlatform.None:
                    return null;
                case StorePlatform.MetaHorizon:
                    return IsAvailable(requested) ? null :
                        "LicenseConfig.storePlatform = MetaHorizon but the Meta XR Platform SDK is not " +
                        "installed (package com.meta.xr.sdk.platform → scripting define VRL_META_PLATFORM). " +
                        "Behaving as storePlatform = None: no entitlement check and no in-app subscriptions.";
                case StorePlatform.Steam:
                    return "LicenseConfig.storePlatform = Steam is reserved for phase 2. Behaving as None.";
                default:
                    return $"Unknown storePlatform {requested}. Behaving as None.";
            }
        }
    }

    /// <summary>
    /// Store subscription helpers shared by the manager, the UI and the providers.
    /// </summary>
    public static class StoreSubscription
    {
        /// <summary>Checkout string per spec §1.0: "{sku}:SUBSCRIPTION__{TERM}" (Meta's own example: TEST_SKU:SUBSCRIPTION__MONTHLY).</summary>
        public static string CheckoutSku(string sku, string term) =>
            (sku ?? "").Trim() + ":SUBSCRIPTION__" + NormalizeTerm(term);

        public static string NormalizeTerm(string term) => (term ?? "").Trim().ToUpperInvariant();

        /// <summary>
        /// Display order: ANNUAL first (Meta requires the best-value plan to be the most visible),
        /// then the configured order. Empty / duplicate tokens are dropped.
        /// </summary>
        public static List<string> OrderedTerms(IList<string> configured)
        {
            var result = new List<string>();
            if (configured == null) return result;
            foreach (string raw in configured)
            {
                string t = NormalizeTerm(raw);
                if (t.Length > 0 && !result.Contains(t)) result.Add(t);
            }
            int annual = result.IndexOf("ANNUAL");
            if (annual > 0)
            {
                result.RemoveAt(annual);
                result.Insert(0, "ANNUAL");
            }
            return result;
        }

        /// <summary>Splits "sku:SUBSCRIPTION__TERM" (or a bare sku) into its parts.</summary>
        public static void ParseCheckoutSku(string checkoutSku, out string sku, out string term)
        {
            sku = checkoutSku ?? "";
            term = "";
            int i = sku.IndexOf(":SUBSCRIPTION__", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return;
            term = NormalizeTerm(sku.Substring(i + ":SUBSCRIPTION__".Length));
            sku = sku.Substring(0, i);
        }

        /// <summary>True when a purchase's SKU is this subscription (bare or with a term suffix).</summary>
        public static bool MatchesSku(string purchaseSku, string sku)
        {
            if (string.IsNullOrEmpty(purchaseSku) || string.IsNullOrEmpty(sku)) return false;
            if (string.Equals(purchaseSku, sku, StringComparison.OrdinalIgnoreCase)) return true;
            return purchaseSku.StartsWith(sku + ":", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Length of a Meta OfferTerm in months (weeks as fractions). 0 = unknown token.</summary>
        public static decimal TermMonths(string term)
        {
            switch (NormalizeTerm(term))
            {
                case "WEEKLY": return 7m / 30m;
                case "BIWEEKLY": return 14m / 30m;
                case "MONTHLY": return 1m;
                case "QUARTERLY": return 3m;
                case "SEMIANNUAL": return 6m;
                case "ANNUAL": return 12m;
                case "BIANNUAL": return 24m;
                default: return 0m;
            }
        }

        /// <summary>
        /// Whether the "Best value" badge may be shown on ANNUAL: only when its price per month is
        /// STRICTLY lower than the per-month price of every other configured term, computed from
        /// the prices the store reports. Any configured term without a readable price (missing
        /// billing plan, unparsable string, unknown term length) → no badge.
        /// </summary>
        public static bool IsAnnualBestValue(StoreProductInfo product, IList<string> configuredTerms, out string reason)
        {
            reason = null;
            var terms = OrderedTerms(configuredTerms);
            if (product == null || !terms.Contains("ANNUAL"))
            {
                reason = "no product or ANNUAL not configured";
                return false;
            }
            if (terms.Count < 2)
            {
                reason = "ANNUAL is the only configured term";
                return false;
            }

            if (!PerMonth(product, "ANNUAL", out decimal annualPerMonth, out reason)) return false;

            foreach (string term in terms)
            {
                if (term == "ANNUAL") continue;
                if (!PerMonth(product, term, out decimal otherPerMonth, out reason)) return false;
                if (annualPerMonth >= otherPerMonth)
                {
                    reason = $"ANNUAL {annualPerMonth:0.####}/month is not below {term} {otherPerMonth:0.####}/month";
                    return false;
                }
            }
            return true;
        }

        private static bool PerMonth(StoreProductInfo product, string term, out decimal perMonth, out string reason)
        {
            perMonth = 0m;
            reason = null;
            StoreTermPrice plan = product.FindTerm(term);
            if (plan == null) { reason = $"no billing plan for {term}"; return false; }
            if (!plan.TryGetAmount(out decimal amount)) { reason = $"unreadable price for {term} ('{plan.formattedPrice}')"; return false; }
            decimal months = TermMonths(term);
            if (months <= 0m) { reason = $"unknown term length for {term}"; return false; }
            perMonth = amount / months;
            return true;
        }
    }

    /// <summary>
    /// Server-authoritative verification of store subscriptions (spec §1.1). The client always
    /// asks the server; the only exception is an EDITOR stub for testing the flow before the
    /// server functions are deployed (see <see cref="StubVerifier"/>, never set in players).
    /// </summary>
    public static class StoreVerification
    {
        /// <summary>Meta's grace period after a failed renewal, added to expiresAt for the cache.</summary>
        public const int GraceDays = 3;

        /// <summary>
        /// Editor-only stand-in for the verify-store-entitlement Edge Function:
        /// (storeUserId, nonce, productId) → result. Null in players and whenever no stub is active.
        /// </summary>
        public static Func<string, string, int, StoreVerifyResult> StubVerifier;
    }
}
