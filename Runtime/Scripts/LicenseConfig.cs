using System;
using UnityEngine;

namespace VRLicensing
{
    [CreateAssetMenu(fileName = "LicenseConfig", menuName = "VR Licensing/New Configuration")]
    public class LicenseConfig : ScriptableObject
    {
        [HideInInspector]
        public string supabaseUrl = "https://eckpfjebvggzxfpjuzha.supabase.co";

        [HideInInspector]
        public string anonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImVja3BmamVidmdnenhmcGp1emhhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDk5MjM4MTgsImV4cCI6MjA2NTQ5OTgxOH0.U4cb9vPcLVkboKQlptWujbFeG1OcLQyBfoWF-NIpUmI";

        [Header("Product")]
        [Tooltip("Product/simulator ID in the Supabase products table")]
        public int productId;

        [Header("Security")]
        [TextArea(3, 6)]
        [Tooltip("RSA public key in PEM format (for future JWT offline verification)")]
        public string rsaPublicKeyPem;

        [Header("Simulator Settings")]
        [Tooltip("Maximum demo time in seconds (default: 3600 = 1 hour)")]
        public float demoDurationSeconds = 3600f;

        [Tooltip("Maximum hours offline before requiring reconnection (default: 72)")]
        public float maxOfflineHours = 72f;

        [Tooltip("Visible simulator name (displayed in the licensing UI)")]
        public string appDisplayName = "VR Simulator";

        [Header("License UI")]
        [Tooltip("OPT-IN. While the license UI is open, show camera passthrough instead of the " +
                 "game scene (WhatsApp-style device linking). Requires the 'Meta Quest: Session' " +
                 "and 'Meta Quest: Camera (Passthrough)' OpenXR features, AND a rig authored in " +
                 "proper Floor tracking mode. WARNING: starting the AR camera subsystem makes the " +
                 "XR Origin re-run its floor calibration and zero its camera Y offset — in apps " +
                 "whose content was authored with that offset applied, this drops the player " +
                 "through the floor. Leave OFF unless the rig is verified floor-mode. QR scanning " +
                 "does NOT need this: it uses an in-panel camera viewfinder instead.")]
        public bool usePassthroughBackground = false;

        [Header("Store (Meta Horizon Store / Steam)")]
        [Tooltip("Which store this build is distributed through.\n" +
                 "None: company APKs and sideloaded builds (default).\n" +
                 "MetaHorizon: Meta Horizon Store build. Individuals subscribe through Meta in-app " +
                 "subscriptions shown on the demo-expired / license-expired panels; the app also runs " +
                 "Meta's app-ownership (entitlement) check at boot. Requires the Meta XR Platform SDK " +
                 "package (com.meta.xr.sdk.platform); without it this setting logs a warning and " +
                 "behaves like None.\n" +
                 "Steam: reserved for phase 2 (behaves like None for now).")]
        public StorePlatform storePlatform = StorePlatform.None;

        [Tooltip("Subscription SKUs of THIS app in the store dashboard. One subscription per term; " +
                 "SKUs are per app and cannot be edited once created. Leave a field empty to hide " +
                 "that plan. Only used when storePlatform is not None.")]
        public StoreSkus storeSkus = new StoreSkus();

        [Tooltip("Store builds only. ON: when Meta's app-ownership (entitlement) check fails, the " +
                 "app is locked and closes after a short notice (what VRC.Quest.Security.1 expects " +
                 "from a published build). OFF (default): the failure is logged and the app " +
                 "continues in demo / license-key mode — keep it OFF during development: a " +
                 "sideloaded APK on an account that is not an organization member or a test user " +
                 "always fails the check.")]
        public bool enforceEntitlement = false;

        [Header("Purchase Call-To-Action")]
        [Tooltip("Turn OFF for Meta Horizon Store and Steam builds. Store payment policies forbid " +
                 "pointing consumers to off-platform purchases (Meta App Policies 1.1.2: only bulk " +
                 "sales to business customers may happen off-platform). When OFF, the demo-expired " +
                 "panel shows no price, URL, QR code or promotion, only a neutral 'ask your " +
                 "organisation for a license key' message, and the license-expired text no longer " +
                 "mentions the web portal. Leave ON for APKs distributed directly to businesses. " +
                 "Ignored when storePlatform is MetaHorizon (the panel shows Meta subscriptions instead).")]
        public bool showPurchaseCta = true;

        [Tooltip("Store URL shown (as text and as a QR code) when the demo runs out. " +
                 "Keep it short — shorter URLs produce a lower-density QR that is easier " +
                 "to scan with a phone from inside the headset.")]
        public string purchaseUrl = "vrinstructors.com";

        [Tooltip("Optional local promotion shown on the demo-expired panel when the server " +
                 "reports none (server-side product_promos always wins). Leave the code empty " +
                 "to disable. The code must exist in Stripe as an active Promotion Code.")]
        public string promoCode = "";
        public string promoHeadline = "Launch offer";
        public string promoDetail = "50% off your first license";
    }

    /// <summary>
    /// Subscription SKUs of one app in the store dashboard (spec 06-LICENSING-V1.12 §1.4).
    /// Default names follow the catalogue convention: sub_monthly / sub_6m / sub_annual.
    /// </summary>
    [Serializable]
    public class StoreSkus
    {
        [Tooltip("1-month subscription SKU")]
        public string monthly = "sub_monthly";

        [Tooltip("6-month subscription SKU")]
        public string sixMonths = "sub_6m";

        [Tooltip("12-month subscription SKU")]
        public string annual = "sub_annual";

        /// <summary>Human-readable term labels, same order as <see cref="All"/>.</summary>
        public static readonly string[] TermLabels = { "Monthly", "6 months", "Annual" };

        /// <summary>Term names used by the server (<c>term</c> field): monthly / 6m / annual.</summary>
        public static readonly string[] TermIds = { "monthly", "6m", "annual" };

        /// <summary>
        /// The three SKUs in a fixed order (monthly, 6 months, annual). Empty entries are
        /// kept as empty strings so indices stay aligned with <see cref="TermLabels"/>.
        /// </summary>
        public string[] All => new[]
        {
            (monthly ?? "").Trim(),
            (sixMonths ?? "").Trim(),
            (annual ?? "").Trim()
        };

        /// <summary>Configured (non-empty) SKUs only.</summary>
        public string[] Configured
        {
            get
            {
                var list = new System.Collections.Generic.List<string>(3);
                foreach (string s in All)
                    if (!string.IsNullOrEmpty(s) && !list.Contains(s)) list.Add(s);
                return list.ToArray();
            }
        }
    }
}
