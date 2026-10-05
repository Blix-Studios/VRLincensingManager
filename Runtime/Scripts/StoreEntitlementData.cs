using System;
using System.Globalization;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// A store subscription the SERVER verified for the signed-in store account (spec
    /// 06-LICENSING-V1.12 §1.1–1.2), cached encrypted on the device like a license. The cache
    /// is trusted only until <see cref="cache_until"/> = min(expiresAt (+grace), verifiedAt +
    /// maxOfflineHours); past that the app must re-verify online.
    /// </summary>
    [Serializable]
    public class StoreEntitlementData
    {
        public string platform;        // meta_quest
        public string store_user_id;   // Meta user id
        public int product_id;
        public string sku;             // sub_monthly / sub_6m / sub_annual
        public string term;            // monthly / 6m / annual (server naming)
        public string status;          // active, grace, cancelled_active, expired
        public string expires_at;      // ISO 8601 UTC, end of the paid term
        public string grace_until;     // ISO 8601 UTC or empty
        public string verified_at;     // ISO 8601 UTC, when the server last verified with the store
        public string cache_until;     // ISO 8601 UTC, how long this record may unlock the app offline

        public DateTime ExpiresAt => Parse(expires_at);
        public DateTime GraceUntil => Parse(grace_until);
        public DateTime VerifiedAt => Parse(verified_at);
        public DateTime CacheUntil => Parse(cache_until);

        /// <summary>True while the cached record may still unlock the app without the server.</summary>
        public bool IsCacheValid => DateTime.UtcNow < CacheUntil;

        /// <summary>Entitled per the server rule: active/cancelled_active before expiry, or grace before grace_until.</summary>
        public bool IsEntitledNow
        {
            get
            {
                DateTime now = DateTime.UtcNow;
                if (status == "active" || status == "cancelled_active") return now < ExpiresAt;
                if (status == "grace") return now < GraceUntil;
                return false;
            }
        }

        public TimeSpan TimeRemaining => ExpiresAt - DateTime.UtcNow;

        /// <summary>
        /// Builds the cache record from a verify-store-entitlement response.
        /// </summary>
        public static StoreEntitlementData FromServer(StoreVerifyResult r, string platformId, string storeUserId,
            int productId, float maxOfflineHours)
        {
            DateTime now = DateTime.UtcNow;
            DateTime expires = Parse(r.expiresAt);
            DateTime grace = Parse(r.graceUntil);
            DateTime termEnd = grace > expires ? grace : expires;
            if (termEnd == DateTime.MinValue) termEnd = now; // defensive: unknown expiry → online every launch

            DateTime offlineLimit = now.AddHours(Mathf.Max(1f, maxOfflineHours));
            DateTime cacheUntil = termEnd < offlineLimit ? termEnd : offlineLimit;

            return new StoreEntitlementData
            {
                platform = platformId,
                store_user_id = storeUserId,
                product_id = productId,
                sku = r.sku,
                term = r.term,
                status = r.status,
                expires_at = Format(expires),
                grace_until = grace == DateTime.MinValue ? "" : Format(grace),
                verified_at = Format(now),
                cache_until = Format(cacheUntil)
            };
        }

        public static DateTime Parse(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return DateTime.MinValue;
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToUniversalTime()
                : DateTime.MinValue;
        }

        public static string Format(DateTime utc) => utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }

    /// <summary>Response of the verify-store-entitlement Edge Function (spec §1.1).</summary>
    [Serializable]
    public class StoreVerifyResult
    {
        public bool entitled;
        public string sku;
        public string term;
        public string expiresAt;
        public string graceUntil;
        public string status;
        public string error; // set by the function on failure
    }
}
