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
                // Opción A: El proyecto provee un prefab custom de UI
                var gate = Object.Instantiate(prefab);
                gate.name = "[VR Licensing System]";
                Object.DontDestroyOnLoad(gate);

                var manager = gate.GetComponent<LicenseManager>();
                if (manager == null)
                {
                    manager = gate.AddComponent<LicenseManager>();
                }
                manager.Initialize(config);

                Debug.Log($"[VR Licensing] Sistema inicializado con prefab custom para: {config.appDisplayName}");
            }
            else
            {
                // Opción B: Generar toda la UI por código (sin prefabs ni assets externos)
                Debug.Log("[VR Licensing] Generando UI por código (no se encontró prefab 'LicenseGateUI').");

                var systemObj = new GameObject("[VR Licensing System]");
                Object.DontDestroyOnLoad(systemObj);

                var manager = systemObj.AddComponent<LicenseManager>();

                // Crear el UI builder — genera Canvas World-Space con toda la interfaz
                var uiBuilder = LicenseUIBuilder.Create(config, manager);
                manager.SetUIBuilder(uiBuilder);

                manager.Initialize(config);

                Debug.Log($"[VR Licensing] Sistema inicializado con UI generada para: {config.appDisplayName}");
            }
        }
    }
}