using System;

namespace VRLicensing
{
    /// <summary>
    /// Represents branding customization data answered by the get_license_branding RPC.
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
}
