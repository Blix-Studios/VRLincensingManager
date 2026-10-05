using System;

namespace VRLicensing
{
    /// <summary>
    /// Represents a license record from the Supabase user_licenses table.
    /// Used for both API responses and local cache.
    /// </summary>
    [Serializable]
    public class LicenseData
    {
        public string id;
        public string license_key;
        public string license_type; // weekly, monthly, annual
        public string status;       // active, expired, cancelled, suspended
        public int product_id;
        public string starts_at;
        public string expires_at;
        public string device_unique_id; // Device binding — null means unbound

        /// <summary>
        /// Returns the parsed expiration date.
        /// </summary>
        public DateTime ExpirationDate
        {
            get
            {
                if (DateTime.TryParse(expires_at, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToUniversalTime();
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Status values the server may send. Anything else (including values added later,
        /// e.g. "revoked", or a typo) is simply "not active": no exception, no access.
        /// </summary>
        public const string STATUS_ACTIVE = "active";

        /// <summary>True only for the exact server status "active" (case-sensitive, trimmed).</summary>
        public bool IsStatusActive => status != null && status.Trim() == STATUS_ACTIVE;

        /// <summary>True when the expiration date has passed (or could not be parsed).</summary>
        public bool IsExpiredByDate => DateTime.UtcNow >= ExpirationDate;

        /// <summary>
        /// Returns true if the license is currently active and not expired. Unknown or empty
        /// statuses count as invalid.
        /// </summary>
        public bool IsValid => IsStatusActive && !IsExpiredByDate;

        /// <summary>
        /// Why <see cref="IsValid"/> is false, for the user: the status when it is not "active"
        /// (e.g. "revoked", "suspended", "cancelled"), otherwise "expired". Null when valid.
        /// </summary>
        public string InvalidReason
        {
            get
            {
                if (IsValid) return null;
                if (!IsStatusActive) return string.IsNullOrWhiteSpace(status) ? "inactive" : status.Trim();
                return "expired";
            }
        }

        /// <summary>
        /// Returns the remaining time before expiration. Negative if already expired.
        /// </summary>
        public TimeSpan TimeRemaining => ExpirationDate - DateTime.UtcNow;
    }

    /// <summary>
    /// Wrapper for Supabase REST API responses that return an array.
    /// </summary>
    [Serializable]
    public class LicenseDataArray
    {
        public LicenseData[] items;

        public static LicenseDataArray FromJson(string json)
        {
            // Supabase REST returns a JSON array directly, wrap it for JsonUtility
            string wrapped = "{\"items\":" + json + "}";
            return UnityEngine.JsonUtility.FromJson<LicenseDataArray>(wrapped);
        }
    }
}
