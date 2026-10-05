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

    /// <summary>A purchasable product as reported by the store (price already localized by the store).</summary>
    public sealed class StoreProductInfo
    {
        public string sku;
        public string name;
        /// <summary>Localized price string exactly as the store formats it (e.g. "$49.99").</summary>
        public string formattedPrice;
        public bool isSubscription;
    }

    /// <summary>A purchase / subscription the store reports for the signed-in user.</summary>
    public sealed class StorePurchaseInfo
    {
        public string sku;
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

        /// <summary>Products for the given SKUs. SKUs the store does not know are simply absent.</summary>
        IEnumerator GetProducts(IList<string> skus, Action<List<StoreProductInfo>, string> onDone);

        /// <summary>
        /// The viewer's currently active subscription among the given SKUs (latest expiration
        /// wins), or null when there is none. Error only when the store call itself failed.
        /// </summary>
        IEnumerator GetActiveSubscription(IList<string> skus, Action<StorePurchaseInfo, string> onDone);

        /// <summary>Opens the store's own checkout UI for one SKU.</summary>
        IEnumerator LaunchCheckout(string sku, Action<StoreCheckoutResult> onDone);

        /// <summary>
        /// Signed-in store user id plus a one-time proof (nonce) the server validates with the
        /// store. Used by the server-authoritative subscription verification (phase B).
        /// </summary>
        IEnumerator GetUserIdentity(Action<string, string, string> onDone);
    }

    /// <summary>
    /// Registry the optional store assemblies register into at AfterAssembliesLoaded (which runs
    /// before LicenseBootstrapper's BeforeSceneLoad). The runtime resolves providers from here
    /// without any static reference to a store SDK.
    /// </summary>
    public static class StorePlatformRegistry
    {
        private static readonly Dictionary<StorePlatform, Func<IStorePlatformProvider>> factories =
            new Dictionary<StorePlatform, Func<IStorePlatformProvider>>();

        public static void Register(StorePlatform platform, Func<IStorePlatformProvider> factory)
        {
            if (factory == null) return;
            factories[platform] = factory;
            Debug.Log($"[VR Licensing] Store provider registered: {platform}");
        }

        public static bool IsAvailable(StorePlatform platform) =>
            platform != StorePlatform.None && factories.ContainsKey(platform);

        /// <summary>
        /// The platform the build will actually behave as: the requested one when a provider is
        /// registered, otherwise None (Steam is always None until phase 2).
        /// </summary>
        public static StorePlatform Effective(StorePlatform requested) =>
            IsAvailable(requested) ? requested : StorePlatform.None;

        public static IStorePlatformProvider Create(StorePlatform platform)
        {
            if (!factories.TryGetValue(platform, out var factory)) return null;
            try
            {
                return factory();
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
    /// Phase B switch (spec 06-LICENSING-V1.12 §1.1): server-authoritative verification of store
    /// subscriptions through the verify-store-entitlement Edge Function, plus p_store_user_id on
    /// get_device_status and p_events on report_device_session. Everything is wired; it is turned
    /// on once the server side is deployed. While OFF, a completed store checkout cannot unlock the
    /// app (the client never decides it is subscribed on its own word).
    /// </summary>
    public static class StoreVerification
    {
        public static readonly bool ServerEnabled = false;

        /// <summary>Meta's grace period after a failed renewal, added to expiresAt for the cache.</summary>
        public const int GraceDays = 3;
    }
}
