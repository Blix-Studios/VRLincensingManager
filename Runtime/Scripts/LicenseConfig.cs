using UnityEngine;

namespace VRLicensing
{
    [CreateAssetMenu(fileName = "LicenseConfig", menuName = "VR Licensing/Nueva Configuracion")]
    public class LicenseConfig : ScriptableObject
    {
        [Header("Conexión Supabase")]
        public string supabaseUrl;
        public string anonKey;

        [Header("Seguridad")]
        [TextArea(3, 6)] 
        public string rsaPublicKeyPem;
        
        [Header("Ajustes del Simulador")]
        public float demoDurationSeconds = 3600f; // 1 hora por defecto
        public string appDisplayName = "Simulador VR";
    }
}