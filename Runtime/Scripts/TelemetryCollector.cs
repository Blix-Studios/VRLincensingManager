using System;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Collects device hardware, session, and environment data for telemetry.
    /// All data collection uses Unity's built-in SystemInfo APIs — no special
    /// permissions required on Meta Quest devices.
    /// </summary>
    public static class TelemetryCollector
    {
        /// <summary>
        /// Collects a snapshot of the current device and session state.
        /// </summary>
        /// <param name="licenseId">The active license ID to associate telemetry with.</param>
        /// <param name="sessionDuration">Total seconds the current session has been active.</param>
        /// <param name="demoUsed">Total demo seconds consumed across all sessions.</param>
        /// <param name="sessionCount">Total number of sessions recorded.</param>
        /// <param name="avgFps">Average FPS during this session (0 if not tracked).</param>
        public static TelemetryPayload Collect(
            string licenseId,
            float sessionDuration = 0f,
            float demoUsed = 0f,
            int sessionCount = 0,
            float avgFps = 0f)
        {
            var payload = new TelemetryPayload
            {
                license_id = licenseId,
                device_model = SystemInfo.deviceModel,
                device_name = SystemInfo.deviceName,
                os_version = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName,
                ram_mb = SystemInfo.systemMemorySize,
                battery_level = SystemInfo.batteryLevel,
                app_version = Application.version,
                system_language = Application.systemLanguage.ToString(),
                display_refresh_rate = GetDisplayRefreshRate(),
                avg_fps = avgFps,
                session_duration_seconds = sessionDuration,
                demo_used_seconds = demoUsed,
                session_count = sessionCount,
                network_type = GetNetworkType(),
            };

            return payload;
        }

        /// <summary>
        /// Attempts to get the display refresh rate via OVRManager reflection.
        /// Falls back to the target frame rate or 0 if unavailable.
        /// </summary>
        private static float GetDisplayRefreshRate()
        {
            try
            {
                // Try OVRManager.display.displayFrequency via reflection
                var ovrManagerType = Type.GetType("OVRManager, Oculus.VR");
                if (ovrManagerType != null)
                {
                    var displayProp = ovrManagerType.GetProperty("display",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (displayProp != null)
                    {
                        var display = displayProp.GetValue(null);
                        if (display != null)
                        {
                            var freqProp = display.GetType().GetProperty("displayFrequency");
                            if (freqProp != null)
                            {
                                return (float)freqProp.GetValue(display);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] Could not get display refresh rate: {e.Message}");
            }

            // Fallback to target frame rate
            return Application.targetFrameRate > 0 ? Application.targetFrameRate : 0f;
        }

        private static string GetNetworkType()
        {
            return Application.internetReachability switch
            {
                NetworkReachability.NotReachable => "None",
                NetworkReachability.ReachableViaLocalAreaNetwork => "WiFi",
                NetworkReachability.ReachableViaCarrierDataNetwork => "Cellular",
                _ => "Unknown",
            };
        }
    }

    /// <summary>
    /// JSON-serializable payload sent to Supabase device_telemetry table.
    /// Field names match the database column names exactly.
    /// </summary>
    [Serializable]
    public class TelemetryPayload
    {
        public string license_id;
        public string device_model;
        public string device_name;
        public string os_version;
        public string processor;
        public string gpu;
        public int ram_mb;
        public float battery_level;
        public string app_version;
        public string system_language;
        public float display_refresh_rate;
        public float avg_fps;
        public float session_duration_seconds;
        public float demo_used_seconds;
        public int session_count;
        public string network_type;

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
