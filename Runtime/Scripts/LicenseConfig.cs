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

        [Header("Purchase Call-To-Action")]
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
}