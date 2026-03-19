using UnityEngine;

namespace VRLicensing
{
    [CreateAssetMenu(fileName = "LicenseConfig", menuName = "VR Licensing/Nueva Configuracion")]
    public class LicenseConfig : ScriptableObject
    {
        [Header("Conexión Supabase")]
        [Tooltip("URL de tu proyecto Supabase (ej: https://xxx.supabase.co)")]
        public string supabaseUrl;

        [Tooltip("Clave pública anon de Supabase")]
        public string anonKey;

        [Header("Producto")]
        [Tooltip("ID del producto/simulador en la tabla products de Supabase")]
        public int productId;

        [Header("Seguridad")]
        [TextArea(3, 6)]
        [Tooltip("Clave pública RSA en formato PEM (para futura verificación JWT)")]
        public string rsaPublicKeyPem;

        [Header("Ajustes del Simulador")]
        [Tooltip("Tiempo máximo de demo en segundos (default: 3600 = 1 hora)")]
        public float demoDurationSeconds = 3600f;

        [Tooltip("Horas máximas offline antes de exigir reconexión (default: 72)")]
        public float maxOfflineHours = 72f;

        [Tooltip("Nombre visible del simulador (se muestra en la UI de licencias)")]
        public string appDisplayName = "Simulador VR";
    }
}