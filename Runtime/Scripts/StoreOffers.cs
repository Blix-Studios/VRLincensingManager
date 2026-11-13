namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.17 - the one place that decides whether this build may offer store
    /// subscriptions and whether it may show the web purchase call-to-action.
    /// A store build with no subscription SKU is a FREE app: it has nothing to sell, so it shows
    /// no "Subscribe" button, never opens the plans panel and queues no plans/checkout events.
    /// </summary>
    public static class StoreOffers
    {
        /// <summary>The configured SKU, trimmed; "" when none.</summary>
        public static string Sku(LicenseConfig config)
        {
            return config == null || config.storeSubscriptionSku == null ? "" : config.storeSubscriptionSku.Trim();
        }

        /// <summary>
        /// True when subscription plans can be offered: the build behaves as a Meta Horizon
        /// Store build AND a subscription SKU is configured.
        /// </summary>
        public static bool Enabled(StorePlatform effectivePlatform, string sku)
        {
            return effectivePlatform == StorePlatform.MetaHorizon && !string.IsNullOrWhiteSpace(sku);
        }

        /// <summary>
        /// True when the web purchase call-to-action (URL, QR, price, promo) may be shown: only
        /// builds that are not on a store (Meta App Policies 1.1.2). A store build never shows
        /// it, with or without a SKU, whatever <see cref="LicenseConfig.showPurchaseCta"/> says.
        /// </summary>
        public static bool WebPurchaseCta(StorePlatform effectivePlatform, bool showPurchaseCta)
        {
            return showPurchaseCta && effectivePlatform == StorePlatform.None;
        }
    }
}
