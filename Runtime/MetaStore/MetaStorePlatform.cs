// Compiled ONLY when the Meta XR Platform SDK (com.meta.xr.sdk.platform) is installed:
// this assembly carries defineConstraints [VRL_META_PLATFORM], which the asmdef's own
// versionDefines entry sets when the package is present. Projects without the SDK never
// compile this file and the runtime assembly never references it.
using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;
using UnityEngine.Scripting;

namespace VRLicensing.MetaStore
{
    /// <summary>
    /// IStorePlatformProvider for the Meta Horizon Store, on top of the Meta XR Platform SDK:
    /// platform init, app-ownership (entitlement) check, subscription products, the viewer's
    /// purchases, Meta's checkout flow and the user proof (nonce) for server verification.
    /// </summary>
    [Preserve]
    public sealed class MetaStorePlatform : IStorePlatformProvider
    {
        // Runs before LicenseBootstrapper (BeforeSceneLoad), so the registry is populated by
        // the time the licensing system resolves its provider. The attribute also keeps this
        // class alive under IL2CPP stripping without any link.xml entry.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            StorePlatformRegistry.Register(StorePlatform.MetaHorizon, () => new MetaStorePlatform());
        }

        public StorePlatform Platform => StorePlatform.MetaHorizon;
        public string PlatformId => "meta_quest";
        public bool IsInitialized { get; private set; }
        public bool? IsEntitled { get; private set; }

        // ────────────────────────────────────────────────────────────────────
        // Init + entitlement
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator Initialize(Action<bool, string> onDone)
        {
            if (Core.IsInitialized())
            {
                IsInitialized = true;
                onDone?.Invoke(true, null);
                yield break;
            }

            bool done = false, ok = false;
            string error = null;

            // Core.AsyncInitialize throws (UnityException) when no App ID is configured in
            // Meta > Platform > Edit Settings. On Android it reads MobileAppID; in the Editor it
            // additionally needs test-user credentials in OculusPlatformSettings.
            try
            {
                Core.AsyncInitialize().OnComplete(msg =>
                {
                    done = true;
                    if (msg.IsError)
                        error = DescribeError(msg.GetError(), "platform init failed");
                    else if (msg.Data == null || msg.Data.Result != PlatformInitializeResult.Success)
                        error = "platform init result: " + (msg.Data != null ? msg.Data.Result.ToString() : "null");
                    else
                        ok = true;
                });
            }
            catch (Exception e)
            {
                onDone?.Invoke(false, "platform init exception: " + e.Message);
                yield break;
            }

            while (!done) yield return null;

            IsInitialized = ok;
            onDone?.Invoke(ok, error);
        }

        public IEnumerator CheckEntitlement(Action<bool, string> onDone)
        {
            if (!IsInitialized)
            {
                onDone?.Invoke(false, "platform not initialized");
                yield break;
            }

            bool done = false, ok = false;
            string error = null;

            try
            {
                Entitlements.IsUserEntitledToApplication().OnComplete(msg =>
                {
                    done = true;
                    if (msg.IsError) error = DescribeError(msg.GetError(), "not entitled");
                    else ok = true;
                });
            }
            catch (Exception e)
            {
                onDone?.Invoke(false, "entitlement exception: " + e.Message);
                yield break;
            }

            while (!done) yield return null;

            IsEntitled = ok;
            onDone?.Invoke(ok, error);
        }

        // ────────────────────────────────────────────────────────────────────
        // IAP
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator GetProducts(IList<string> skus, Action<List<StoreProductInfo>, string> onDone)
        {
            var result = new List<StoreProductInfo>();
            if (!IsInitialized)
            {
                onDone?.Invoke(result, "platform not initialized");
                yield break;
            }
            if (skus == null || skus.Count == 0)
            {
                onDone?.Invoke(result, null);
                yield break;
            }

            bool done = false;
            string error = null;
            var skuArray = new string[skus.Count];
            skus.CopyTo(skuArray, 0);

            try
            {
                IAP.GetProductsBySKU(skuArray).OnComplete(msg =>
                {
                    done = true;
                    if (msg.IsError)
                    {
                        error = DescribeError(msg.GetError(), "GetProductsBySKU failed");
                        return;
                    }
                    if (msg.Data == null) return;
                    foreach (Product p in msg.Data)
                    {
                        if (p == null || string.IsNullOrEmpty(p.Sku)) continue;
                        result.Add(new StoreProductInfo
                        {
                            sku = p.Sku,
                            name = p.Name,
                            formattedPrice = !string.IsNullOrEmpty(p.FormattedPrice)
                                ? p.FormattedPrice
                                : (p.Price != null ? p.Price.Formatted : null),
                            isSubscription = p.Type == ProductType.Subscription
                        });
                    }
                });
            }
            catch (Exception e)
            {
                onDone?.Invoke(result, "GetProductsBySKU exception: " + e.Message);
                yield break;
            }

            while (!done) yield return null;
            onDone?.Invoke(result, error);
        }

        public IEnumerator GetActiveSubscription(IList<string> skus, Action<StorePurchaseInfo, string> onDone)
        {
            if (!IsInitialized)
            {
                onDone?.Invoke(null, "platform not initialized");
                yield break;
            }

            bool done = false;
            string error = null;
            StorePurchaseInfo best = null;

            try
            {
                IAP.GetViewerPurchases().OnComplete(msg =>
                {
                    done = true;
                    if (msg.IsError)
                    {
                        error = DescribeError(msg.GetError(), "GetViewerPurchases failed");
                        return;
                    }
                    if (msg.Data == null) return;
                    foreach (Purchase p in msg.Data)
                    {
                        if (p == null || string.IsNullOrEmpty(p.Sku)) continue;
                        if (skus != null && skus.Count > 0 && !ContainsSku(skus, p.Sku)) continue;

                        var info = ToPurchaseInfo(p);
                        if (!info.IsActiveNow) continue;
                        if (best == null || info.expirationUtc > best.expirationUtc) best = info;
                    }
                });
            }
            catch (Exception e)
            {
                onDone?.Invoke(null, "GetViewerPurchases exception: " + e.Message);
                yield break;
            }

            while (!done) yield return null;
            onDone?.Invoke(best, error);
        }

        public IEnumerator LaunchCheckout(string sku, Action<StoreCheckoutResult> onDone)
        {
            var result = new StoreCheckoutResult { outcome = StoreCheckoutOutcome.Failed };
            if (!IsInitialized)
            {
                result.error = "platform not initialized";
                onDone?.Invoke(result);
                yield break;
            }

            bool done = false;
            try
            {
                IAP.LaunchCheckoutFlow(sku).OnComplete(msg =>
                {
                    done = true;
                    if (msg.IsError)
                    {
                        Error err = msg.GetError();
                        string text = err != null ? err.Message ?? "" : "";
                        // The SDK reports a user-dismissed checkout as an error; tell it apart
                        // from real failures so the panel can stay quiet about it.
                        result.outcome = text.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0
                            ? StoreCheckoutOutcome.Cancelled
                            : StoreCheckoutOutcome.Failed;
                        result.error = DescribeError(err, "checkout failed");
                        return;
                    }
                    result.outcome = StoreCheckoutOutcome.Completed;
                    result.purchase = msg.Data != null ? ToPurchaseInfo(msg.Data) : null;
                });
            }
            catch (Exception e)
            {
                result.error = "LaunchCheckoutFlow exception: " + e.Message;
                onDone?.Invoke(result);
                yield break;
            }

            while (!done) yield return null;
            onDone?.Invoke(result);
        }

        // ────────────────────────────────────────────────────────────────────
        // Identity (phase B: server-authoritative verification)
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator GetUserIdentity(Action<string, string, string> onDone)
        {
            if (!IsInitialized)
            {
                onDone?.Invoke(null, null, "platform not initialized");
                yield break;
            }

            bool userDone = false;
            string userId = null, error = null;
            try
            {
                Users.GetLoggedInUser().OnComplete(msg =>
                {
                    userDone = true;
                    if (msg.IsError) error = DescribeError(msg.GetError(), "GetLoggedInUser failed");
                    else if (msg.Data != null) userId = msg.Data.ID.ToString();
                });
            }
            catch (Exception e)
            {
                onDone?.Invoke(null, null, "GetLoggedInUser exception: " + e.Message);
                yield break;
            }
            while (!userDone) yield return null;

            if (string.IsNullOrEmpty(userId))
            {
                onDone?.Invoke(null, null, error ?? "no logged-in user");
                yield break;
            }

            bool proofDone = false;
            string nonce = null;
            try
            {
                Users.GetUserProof().OnComplete(msg =>
                {
                    proofDone = true;
                    if (msg.IsError) error = DescribeError(msg.GetError(), "GetUserProof failed");
                    else if (msg.Data != null) nonce = msg.Data.Value;
                });
            }
            catch (Exception e)
            {
                onDone?.Invoke(userId, null, "GetUserProof exception: " + e.Message);
                yield break;
            }
            while (!proofDone) yield return null;

            onDone?.Invoke(userId, nonce, string.IsNullOrEmpty(nonce) ? (error ?? "empty nonce") : null);
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────

        private static StorePurchaseInfo ToPurchaseInfo(Purchase p)
        {
            var info = new StorePurchaseInfo
            {
                sku = p.Sku,
                purchaseId = p.ID,
                isSubscription = p.Type == ProductType.Subscription
            };
            // The SDK leaves ExpirationTime at default for purchases that never expire.
            if (p.ExpirationTime > DateTime.MinValue.AddYears(1))
                info.expirationUtc = p.ExpirationTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(p.ExpirationTime, DateTimeKind.Utc)
                    : p.ExpirationTime.ToUniversalTime();
            if (p.GrantTime > DateTime.MinValue.AddYears(1))
                info.grantUtc = p.GrantTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(p.GrantTime, DateTimeKind.Utc)
                    : p.GrantTime.ToUniversalTime();
            return info;
        }

        private static bool ContainsSku(IList<string> skus, string sku)
        {
            for (int i = 0; i < skus.Count; i++)
                if (string.Equals(skus[i], sku, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string DescribeError(Error err, string fallback)
        {
            if (err == null) return fallback;
            string msg = string.IsNullOrEmpty(err.Message) ? fallback : err.Message;
            return $"{msg} (code {err.Code}, http {err.HttpCode})";
        }
    }
}
