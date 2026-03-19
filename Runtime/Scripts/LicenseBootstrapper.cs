using UnityEngine;

namespace VRLicensing
{
    public static class LicenseBootstrapper
    {
        // Esto hace que el script se ejecute automáticamente antes de cargar la primera escena
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // 1. Buscar la configuración específica de este simulador
            var config = Resources.Load<LicenseConfig>("LicenseConfig");
            
            if (config == null)
            {
                Debug.LogError("[VR Licensing] CUIDADO: No se encontró 'LicenseConfig' en la carpeta Resources del proyecto. El sistema de licencias está inactivo.");
                return;
            }

            // 2. Instanciar el sistema (Asegúrate de tener un prefab llamado LicenseGateUI en Resources o empaquetado)
            var prefab = Resources.Load<GameObject>("LicenseGateUI");
            if (prefab != null)
            {
                var gate = Object.Instantiate(prefab);
                Object.DontDestroyOnLoad(gate);
                
                // Aquí inicializarías tu LicenseManager
                // gate.GetComponent<LicenseManager>().Init(config);
            }
            else
            {
                Debug.LogError("[VR Licensing] No se encontró el prefab de la UI de licencias.");
            }
        }
    }
}