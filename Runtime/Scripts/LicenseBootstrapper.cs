using UnityEngine;

namespace VRLicensing
{
    public static class LicenseBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // 1. Search for the specific configuration for this simulator
            var config = Resources.Load<LicenseConfig>("LicenseConfig");

            if (config == null)
            {
                Debug.LogError("[VR Licensing] 'LicenseConfig' not found in the Resources folder. " +
                    "Create one with: Create > VR Licensing > New Configuration and place it in Assets/Resources/.");
                return;
            }

            // 2. Validate that the config has the minimum fields
            if (string.IsNullOrEmpty(config.supabaseUrl) || string.IsNullOrEmpty(config.anonKey))
            {
                Debug.LogError("[VR Licensing] LicenseConfig found but supabaseUrl or anonKey are missing. " +
                    "Configure them in the Inspector.");
                return;
            }

            // 3. Pre-warm the AR session at boot. Creating an ARSession restarts the
            // OpenXR session; doing it here — before any scene is playing — makes that
            // one restart harmless. If it happened lazily when the license modal first
            // opens, the restart's tracking hiccup could let gravity push the player
            // through the floor.
            if (config.usePassthroughBackground)
                LicensePassthrough.EnsureSessionAlive();

            // 4. Instantiate the license system
            var prefab = Resources.Load<GameObject>("LicenseGateUI");
            if (prefab != null)
            {
                // Option A: The project provides a custom UI prefab
                var gate = Object.Instantiate(prefab);
                gate.name = "[VR Licensing System]";
                Object.DontDestroyOnLoad(gate);

                var manager = gate.GetComponent<LicenseManager>();
                if (manager == null)
                {
                    manager = gate.AddComponent<LicenseManager>();
                }
                manager.Initialize(config);

                Debug.Log($"[VR Licensing] System initialized with custom prefab for: {config.appDisplayName}");
            }
            else
            {
                // Option B: Generate all UI via code (no prefabs or external assets)
                Debug.Log("[VR Licensing] Generating UI via code (no 'LicenseGateUI' prefab found).");

                var systemObj = new GameObject("[VR Licensing System]");
                Object.DontDestroyOnLoad(systemObj);

                var manager = systemObj.AddComponent<LicenseManager>();

                // Crear el UI builder — genera Canvas World-Space con toda la interfaz
                var uiBuilder = LicenseUIBuilder.Create(config, manager);
                manager.SetUIBuilder(uiBuilder);

                manager.Initialize(config);

                Debug.Log($"[VR Licensing] System initialized with generated UI for: {config.appDisplayName}");
            }
        }
    }
}