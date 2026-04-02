using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VRLicensing
{
    /// <summary>
    /// Handles HTTP communication with Supabase REST API.
    /// Validates license keys directly against the user_licenses table.
    /// </summary>
    public class SupabaseClient : MonoBehaviour
    {
        private string supabaseUrl;
        private string anonKey;

        /// <summary>
        /// Initialize with config values.
        /// </summary>
        public void Initialize(LicenseConfig config)
        {
            supabaseUrl = config.supabaseUrl.TrimEnd('/');
            anonKey = config.anonKey;
        }

        /// <summary>
        /// Validates a license key against Supabase.
        /// Queries user_licenses where license_key matches, status is active,
        /// and optionally filters by product_id.
        /// </summary>
        /// <param name="licenseKey">The license key entered by the user (format: XXXX-XXXX-XXXX-XXXX)</param>
        /// <param name="productId">The product ID for this simulator (from LicenseConfig). Pass 0 to skip product filter.</param>
        /// <param name="onSuccess">Called with the LicenseData if the key is valid and active.</param>
        /// <param name="onError">Called with an error message if validation fails.</param>
        public IEnumerator ValidateLicenseKey(string licenseKey, int productId,
            Action<LicenseData> onSuccess, Action<string> onError)
        {
            // Build the Supabase REST API query
            // GET /rest/v1/user_licenses?license_key=eq.{key}&status=eq.active&select=...
            string endpoint = $"{supabaseUrl}/rest/v1/user_licenses";
            string query = $"?license_key=eq.{Uri.EscapeDataString(licenseKey)}" +
                           $"&status=eq.active" +
                           $"&select=id,license_key,license_type,status,product_id,starts_at,expires_at";

            // Optionally filter by product_id
            if (productId > 0)
            {
                query += $"&product_id=eq.{productId}";
            }

            string url = endpoint + query;

            using (var request = UnityWebRequest.Get(url))
            {
                // Supabase REST API headers
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Accept", "application/json");

                Debug.Log($"[VR Licensing] Validating license key...");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"Connection error: {request.error}";
                    Debug.LogError($"[VR Licensing] {errorMsg}");
                    onError?.Invoke(errorMsg);
                    yield break;
                }

                string responseBody = request.downloadHandler.text;
                Debug.Log($"[VR Licensing] Response: {responseBody}");

                try
                {
                    // Parse the response array
                    var licenses = LicenseDataArray.FromJson(responseBody);

                    if (licenses.items == null || licenses.items.Length == 0)
                    {
                        onError?.Invoke("Invalid or inactive license key.");
                        yield break;
                    }

                    LicenseData license = licenses.items[0];

                    // Double-check expiration
                    if (!license.IsValid)
                    {
                        onError?.Invoke($"License has expired ({license.expires_at}).");
                        yield break;
                    }

                    Debug.Log($"[VR Licensing] License validated: {license.license_key} " +
                        $"(type: {license.license_type}, expires: {license.expires_at})");

                    onSuccess?.Invoke(license);
                }
                catch (Exception e)
                {
                    string errorMsg = $"Error processing response: {e.Message}";
                    Debug.LogError($"[VR Licensing] {errorMsg}");
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Checks if the device has internet connectivity by pinging the Supabase URL.
        /// </summary>
        public IEnumerator CheckConnectivity(Action<bool> callback)
        {
            string url = $"{supabaseUrl}/rest/v1/";

            using (var request = UnityWebRequest.Head(url))
            {
                request.SetRequestHeader("apikey", anonKey);
                request.timeout = 5; // 5 second timeout

                yield return request.SendWebRequest();

                bool isConnected = request.result == UnityWebRequest.Result.Success;
                callback?.Invoke(isConnected);
            }
        }

        /// <summary>
        /// Fetches branding data for a license from the license_branding table.
        /// Returns null if no branding is configured by the client.
        /// </summary>
        /// <param name="licenseId">The license UUID to fetch branding for.</param>
        /// <param name="onSuccess">Called with BrandingData if found (null if not configured).</param>
        /// <param name="onError">Called with error message on failure.</param>
        public IEnumerator FetchBranding(string licenseId,
            Action<BrandingData> onSuccess, Action<string> onError)
        {
            string endpoint = $"{supabaseUrl}/rest/v1/license_branding";
            string query = $"?license_id=eq.{Uri.EscapeDataString(licenseId)}" +
                           $"&select=id,license_id,brand_name,logo_url";

            string url = endpoint + query;

            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = 10;

                Debug.Log($"[VR Licensing] Fetching branding for license {licenseId}...");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"Error fetching branding: {request.error}";
                    Debug.LogWarning($"[VR Licensing] {errorMsg}");
                    onError?.Invoke(errorMsg);
                    yield break;
                }

                string responseBody = request.downloadHandler.text;

                try
                {
                    var brandingArray = BrandingDataArray.FromJson(responseBody);

                    if (brandingArray.items == null || brandingArray.items.Length == 0)
                    {
                        Debug.Log("[VR Licensing] No branding configured for this license.");
                        onSuccess?.Invoke(null);
                        yield break;
                    }

                    BrandingData branding = brandingArray.items[0];
                    Debug.Log($"[VR Licensing] Branding loaded: \"{branding.brand_name}\" " +
                        $"(logo: {(string.IsNullOrEmpty(branding.logo_url) ? "none" : "yes")})");

                    onSuccess?.Invoke(branding);
                }
                catch (Exception e)
                {
                    string errorMsg = $"Error parsing branding response: {e.Message}";
                    Debug.LogError($"[VR Licensing] {errorMsg}");
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Sends device telemetry data to the device_telemetry table.
        /// Fire-and-forget: errors are logged but do not affect licensing flow.
        /// </summary>
        /// <param name="payload">The telemetry data collected by TelemetryCollector.</param>
        public IEnumerator SendTelemetry(TelemetryPayload payload)
        {
            string url = $"{supabaseUrl}/rest/v1/device_telemetry";
            string jsonBody = payload.ToJson();

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Prefer", "return=minimal");
                request.timeout = 10;

                Debug.Log("[VR Licensing] Sending device telemetry...");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VR Licensing] Telemetry send failed (non-critical): {request.error}");
                }
                else
                {
                    Debug.Log("[VR Licensing] Telemetry sent successfully.");
                }
            }
        }

        /// <summary>
        /// Registers or updates this device in the device_registry table.
        /// Uses Supabase upsert (on conflict device_unique_id + product_id).
        /// Returns the server-side device record.
        /// </summary>
        public IEnumerator RegisterDevice(int productId, float demoUsedSeconds, int sessionCount,
            string lastLicenseKey, Action<DeviceRegistryData> onSuccess, Action<string> onError)
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string url = $"{supabaseUrl}/rest/v1/device_registry";

            // Build upsert payload
            var payload = new DeviceRegistryUpsert
            {
                device_unique_id = deviceId,
                product_id = productId,
                device_model = SystemInfo.deviceModel,
                device_name = SystemInfo.deviceName,
                demo_used_seconds = demoUsedSeconds,
                session_count = sessionCount,
                last_license_key = lastLicenseKey ?? "",
                last_seen_at = DateTime.UtcNow.ToString("o")
            };

            string jsonBody = JsonUtility.ToJson(payload);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Content-Type", "application/json");
                // Upsert on conflict, return the record
                request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=representation");
                request.timeout = 10;

                Debug.Log($"[VR Licensing] Registering device {deviceId} for product {productId}...");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"Device registration failed: {request.error}";
                    Debug.LogWarning($"[VR Licensing] {errorMsg}");
                    onError?.Invoke(errorMsg);
                    yield break;
                }

                string responseBody = request.downloadHandler.text;

                try
                {
                    var records = DeviceRegistryArray.FromJson(responseBody);
                    if (records.items != null && records.items.Length > 0)
                    {
                        Debug.Log($"[VR Licensing] Device registered. Server demo used: {records.items[0].demo_used_seconds}s, " +
                            $"blocked: {records.items[0].demo_blocked}, sessions: {records.items[0].session_count}");
                        onSuccess?.Invoke(records.items[0]);
                    }
                    else
                    {
                        onError?.Invoke("Empty response from device registry.");
                    }
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error parsing device registry response: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Checks the server-side demo status for this device.
        /// Returns the device record if found, null otherwise.
        /// </summary>
        public IEnumerator CheckDeviceDemo(int productId,
            Action<DeviceRegistryData> onSuccess, Action<string> onError)
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string url = $"{supabaseUrl}/rest/v1/device_registry" +
                $"?device_unique_id=eq.{Uri.EscapeDataString(deviceId)}" +
                $"&product_id=eq.{productId}" +
                $"&select=*";

            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Device check failed: {request.error}");
                    yield break;
                }

                string responseBody = request.downloadHandler.text;

                try
                {
                    var records = DeviceRegistryArray.FromJson(responseBody);
                    if (records.items != null && records.items.Length > 0)
                    {
                        onSuccess?.Invoke(records.items[0]);
                    }
                    else
                    {
                        onSuccess?.Invoke(null); // Device not yet registered
                    }
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error parsing device check response: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Data model for device_registry table rows.
    /// </summary>
    [Serializable]
    public class DeviceRegistryData
    {
        public string id;
        public string device_unique_id;
        public int product_id;
        public string device_model;
        public string device_name;
        public float demo_used_seconds;
        public float demo_limit_seconds;
        public bool demo_blocked;
        public string demo_reset_at;
        public int session_count;
        public string last_license_key;
        public string first_seen_at;
        public string last_seen_at;
    }

    [Serializable]
    public class DeviceRegistryArray
    {
        public DeviceRegistryData[] items;

        public static DeviceRegistryArray FromJson(string json)
        {
            // Supabase returns a JSON array, wrap it
            string wrapped = "{\"items\":" + json + "}";
            return JsonUtility.FromJson<DeviceRegistryArray>(wrapped);
        }
    }

    /// <summary>
    /// Payload for upserting into device_registry.
    /// </summary>
    [Serializable]
    public class DeviceRegistryUpsert
    {
        public string device_unique_id;
        public int product_id;
        public string device_model;
        public string device_name;
        public float demo_used_seconds;
        public int session_count;
        public string last_license_key;
        public string last_seen_at;
    }
}
