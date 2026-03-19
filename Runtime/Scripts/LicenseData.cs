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
        /// Returns true if the license is currently active and not expired.
        /// </summary>
        public bool IsValid => status == "active" && DateTime.UtcNow < ExpirationDate;

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
