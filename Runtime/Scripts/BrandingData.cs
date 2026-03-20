using System;

namespace VRLicensing
{
    /// <summary>
    /// Represents branding customization data fetched from Supabase.
    /// Set by the client via the web portal, consumed by the VR app
    /// to display client-specific branding (logo, company name, etc.).
    /// </summary>
    [Serializable]
    public class BrandingData
    {
        public string id;
        public string license_id;
        public string brand_name;
        public string logo_url;

        /// <summary>
        /// Returns true if any branding has been configured by the client.
        /// </summary>
        public bool HasBranding => !string.IsNullOrEmpty(brand_name) || !string.IsNullOrEmpty(logo_url);
    }

    /// <summary>
    /// Wrapper for Supabase REST API responses that return an array of BrandingData.
    /// </summary>
    [Serializable]
    public class BrandingDataArray
    {
        public BrandingData[] items;

        public static BrandingDataArray FromJson(string json)
        {
            // Supabase REST returns a JSON array directly, wrap it for JsonUtility
            string wrapped = "{\"items\":" + json + "}";
            return UnityEngine.JsonUtility.FromJson<BrandingDataArray>(wrapped);
        }
    }
}
