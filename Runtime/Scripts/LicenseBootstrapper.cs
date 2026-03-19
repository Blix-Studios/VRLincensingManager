using UnityEngine;

namespace VRLicensing
{
    public static class LicenseBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // 1. Buscar la configuración específica de este simulador
            var config = Resources.Load<LicenseConfig>("LicenseConfig");

            if (config == null)
            {
                Debug.LogError("[VR Licensing] No se encontró 'LicenseConfig' en la carpeta Resources. " +
                    "Crea uno con: Create > VR Licensing > Nueva Configuracion y colócalo en Assets/Resources/.");
                return;
            }

            // 2. Validar que la config tiene los campos mínimos
            if (string.IsNullOrEmpty(config.supabaseUrl) || string.IsNullOrEmpty(config.anonKey))
            {
                Debug.LogError("[VR Licensing] LicenseConfig encontrado pero faltan supabaseUrl o anonKey. " +
                    "Configúralos en el Inspector.");
                return;
            }

            // 3. Instanciar el sistema de licencias
            var prefab = Resources.Load<GameObject>("LicenseGateUI");
            if (prefab != null)
            {
                var gate = Object.Instantiate(prefab);
                gate.name = "[VR Licensing System]";
                Object.DontDestroyOnLoad(gate);

                // Inicializar el manager
                var manager = gate.GetComponent<LicenseManager>();
                if (manager == null)
                {
                    manager = gate.AddComponent<LicenseManager>();
                }
                manager.Initialize(config);

                Debug.Log($"[VR Licensing] Sistema inicializado para: {config.appDisplayName}");
            }
            else
            {
                // Fallback: crear un GameObject vacío con solo la lógica (sin UI prefab)
                Debug.LogWarning("[VR Licensing] No se encontró el prefab 'LicenseGateUI' en Resources. " +
                    "Creando sistema sin UI. Puedes agregar el prefab después.");

                var systemObj = new GameObject("[VR Licensing System]");
                Object.DontDestroyOnLoad(systemObj);

                var manager = systemObj.AddComponent<LicenseManager>();
                manager.Initialize(config);
            }
        }
    }
}