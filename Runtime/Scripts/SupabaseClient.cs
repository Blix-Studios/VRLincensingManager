using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VRLicensing
{
    /// <summary>
    /// Handles HTTP communication with Supabase.
    /// License validation, device binding and demo accounting go through
    /// SECURITY DEFINER RPCs (see migration 20260706120000_add_vr_licensing_rpcs.sql)
    /// so the anon key can no longer read/enumerate the tables directly.
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

        // ────────────────────────────────────────────────────────────────────
        // RPC helpers
        // ────────────────────────────────────────────────────────────────────

        private UnityWebRequest BuildRpc(string function, string jsonBody)
        {
            var request = new UnityWebRequest($"{supabaseUrl}/rest/v1/rpc/{function}", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", anonKey);
            request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = 10;
            return request;
        }

        // ────────────────────────────────────────────────────────────────────
        // 1. Validate + bind license (atomic, server-side).
        //    Replaces the old GET user_licenses + PATCH device_unique_id.
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates a license key and binds it to this device in a single
        /// atomic server call. The caller must know the exact key — the server
        /// never returns a list, so enumeration is impossible.
        /// </summary>
        /// <param name="onSuccess">Called with the validated LicenseData.</param>
        /// <param name="onError">Called with (code, message). Codes: not_found,
        /// inactive, expired, bound_other_device, invalid, error.</param>
        public IEnumerator ValidateAndBindLicense(string licenseKey, int productId, string deviceId,
            Action<LicenseData> onSuccess, Action<string, string> onError)
        {
            var body = JsonUtility.ToJson(new ReqValidate
            {
                p_license_key = licenseKey,
                p_product_id = productId,
                p_device_id = deviceId
            });

            using (var request = BuildRpc("validate_and_bind_license", body))
            {
                Debug.Log("[VR Licensing] Validating license key (RPC)...");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke("error", $"Connection error: {request.error}");
                    yield break;
                }

                RpcLicenseResult result;
                try
                {
                    result = JsonUtility.FromJson<RpcLicenseResult>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    onError?.Invoke("error", $"Error processing response: {e.Message}");
                    yield break;
                }

                if (result != null && result.success && result.license != null)
                {
                    Debug.Log($"[VR Licensing] License validated + bound: {result.license.license_key} " +
                        $"(type: {result.license.license_type}, expires: {result.license.expires_at})");
                    onSuccess?.Invoke(result.license);
                }
                else
                {
                    string code = result?.code ?? "error";
                    string msg = result?.error ?? "Invalid or inactive license key.";
                    Debug.LogWarning($"[VR Licensing] License rejected ({code}): {msg}");
                    onError?.Invoke(code, msg);
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // 2. Read THIS device's demo status (scoped, no enumeration).
        //    Replaces GET device_registry?device_unique_id=eq...
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator GetDeviceStatus(int productId,
            Action<DeviceStatus> onSuccess, Action<string> onError)
        {
            var body = JsonUtility.ToJson(new ReqStatus
            {
                p_device_id = SystemInfo.deviceUniqueIdentifier,
                p_product_id = productId
            });

            using (var request = BuildRpc("get_device_status", body))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Device status check failed: {request.error}");
                    yield break;
                }

                try
                {
                    var status = JsonUtility.FromJson<DeviceStatus>(request.downloadHandler.text);
                    onSuccess?.Invoke(status);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error parsing device status: {e.Message}");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // 3. Report a device session — ADDITIVE demo accounting.
        //    The server ADDS the positive delta; the client can never lower the
        //    counter nor touch demo_blocked. Replaces the upsert that trusted a
        //    client-supplied total. Send the PER-SESSION delta, not a total.
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator ReportDeviceSession(int productId, float demoDeltaSeconds,
            string lastLicenseKey, Action<DeviceStatus> onSuccess, Action<string> onError)
        {
            var body = JsonUtility.ToJson(new ReqReport
            {
                p_device_id = SystemInfo.deviceUniqueIdentifier,
                p_product_id = productId,
                p_demo_delta_seconds = Mathf.Max(0f, demoDeltaSeconds),
                p_device_model = SystemInfo.deviceModel,
                p_device_name = SystemInfo.deviceName,
                p_last_license_key = lastLicenseKey ?? ""
            });

            using (var request = BuildRpc("report_device_session", body))
            {
                Debug.Log($"[VR Licensing] Reporting device session (delta {Mathf.Max(0f, demoDeltaSeconds):F0}s)...");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Device session report failed: {request.error}");
                    yield break;
                }

                try
                {
                    var status = JsonUtility.FromJson<DeviceStatus>(request.downloadHandler.text);
                    onSuccess?.Invoke(status);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error parsing report response: {e.Message}");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Connectivity (unchanged)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks if the device has internet connectivity by pinging Supabase.
        /// </summary>
        public IEnumerator CheckConnectivity(Action<bool> callback)
        {
            using (var request = UnityWebRequest.Head($"{supabaseUrl}/rest/v1/"))
            {
                request.SetRequestHeader("apikey", anonKey);
                request.timeout = 5;

                yield return request.SendWebRequest();

                bool isConnected = request.result == UnityWebRequest.Result.Success;
                callback?.Invoke(isConnected);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Branding (unchanged — read of license_branding by license id)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches branding data for a license from the license_branding table.
        /// Returns null if no branding is configured by the client.
        /// </summary>
        public IEnumerator FetchBranding(string licenseId,
            Action<BrandingData> onSuccess, Action<string> onError)
        {
            string endpoint = $"{supabaseUrl}/rest/v1/license_branding";
            string query = $"?license_id=eq.{Uri.EscapeDataString(licenseId)}" +
                           $"&select=id,license_id,brand_name,logo_url";

            using (var request = UnityWebRequest.Get(endpoint + query))
            {
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Error fetching branding: {request.error}");
                    yield break;
                }

                try
                {
                    var brandingArray = BrandingDataArray.FromJson(request.downloadHandler.text);

                    if (brandingArray.items == null || brandingArray.items.Length == 0)
                    {
                        onSuccess?.Invoke(null);
                        yield break;
                    }

                    onSuccess?.Invoke(brandingArray.items[0]);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error parsing branding response: {e.Message}");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Telemetry (unchanged — anon INSERT into device_telemetry)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends device telemetry. Fire-and-forget; errors are logged only.
        /// </summary>
        public IEnumerator SendTelemetry(TelemetryPayload payload)
        {
            string url = $"{supabaseUrl}/rest/v1/device_telemetry";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToJson()));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Prefer", "return=minimal");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning($"[VR Licensing] Telemetry send failed (non-critical): {request.error}");
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // RPC request/response models
    // ════════════════════════════════════════════════════════════════════════

    [Serializable]
    internal class ReqValidate
    {
        public string p_license_key;
        public int p_product_id;
        public string p_device_id;
    }

    [Serializable]
    internal class ReqStatus
    {
        public string p_device_id;
        public int p_product_id;
    }

    [Serializable]
    internal class ReqReport
    {
        public string p_device_id;
        public int p_product_id;
        public float p_demo_delta_seconds;
        public string p_device_model;
        public string p_device_name;
        public string p_last_license_key;
    }

    /// <summary>Result of validate_and_bind_license RPC.</summary>
    [Serializable]
    public class RpcLicenseResult
    {
        public bool success;
        public string code;   // ok, not_found, inactive, expired, bound_other_device, invalid, error
        public string error;
        public LicenseData license;
    }

    /// <summary>
    /// Authoritative device/demo status returned by get_device_status and
    /// report_device_session RPCs.
    /// </summary>
    [Serializable]
    public class DeviceStatus
    {
        public bool found;              // get_device_status only
        public bool ok;                 // report_device_session only
        public float demo_used_seconds;
        public float demo_limit_seconds;
        public bool demo_blocked;
        public int session_count;
    }
}
