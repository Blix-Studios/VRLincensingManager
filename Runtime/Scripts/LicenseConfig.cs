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
    }
}