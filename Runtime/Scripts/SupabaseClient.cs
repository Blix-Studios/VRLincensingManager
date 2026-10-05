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
    /// v1.12 adds get_license_branding / report_telemetry (with a fallback to the
    /// pre-1.12 direct table access while the functions are not deployed), the
    /// verify-store-entitlement Edge Function, p_store_user_id and p_events.
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
            return BuildPost($"{supabaseUrl}/rest/v1/rpc/{function}", jsonBody, 10);
        }

        private UnityWebRequest BuildPost(string url, string jsonBody, int timeoutSeconds)
        {
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", anonKey);
            request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = timeoutSeconds;
            return request;
        }

        /// <summary>
        /// PostgREST answers 404 (PGRST202) when an RPC does not exist or its signature does not
        /// match. That is how the client tells "function not deployed yet" from a real failure
        /// and falls back to the pre-1.12 path.
        /// </summary>
        private static bool IsFunctionMissing(UnityWebRequest request) => request.responseCode == 404;

        private static string Snippet(UnityWebRequest request)
        {
            string text = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > 160 ? text.Substring(0, 160) + "…" : text;
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
        //    v1.12: optional p_store_user_id adds the "store" block to the response
        //    (read-only view of store_entitlements; no store call server-side).
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator GetDeviceStatus(int productId,
            Action<DeviceStatus> onSuccess, Action<string> onError)
        {
            return GetDeviceStatus(productId, null, onSuccess, onError);
        }

        public IEnumerator GetDeviceStatus(int productId, string storeUserId,
            Action<DeviceStatus> onSuccess, Action<string> onError)
        {
            string body = string.IsNullOrEmpty(storeUserId)
                ? JsonUtility.ToJson(new ReqStatus
                {
                    p_device_id = SystemInfo.deviceUniqueIdentifier,
                    p_product_id = productId
                })
                : JsonUtility.ToJson(new ReqStatusStore
                {
                    p_device_id = SystemInfo.deviceUniqueIdentifier,
                    p_product_id = productId,
                    p_store_user_id = storeUserId
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
        //    v1.12: optional p_events (JSON array) carries store analytics events.
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator ReportDeviceSession(int productId, float demoDeltaSeconds,
            string lastLicenseKey, Action<DeviceStatus> onSuccess, Action<string> onError,
            string eventsJsonArray = null)
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

            // JsonUtility cannot emit a free-form jsonb value; splice the pre-built array in.
            if (!string.IsNullOrEmpty(eventsJsonArray))
                body = body.Substring(0, body.Length - 1) + ",\"p_events\":" + eventsJsonArray + "}";

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
        // 4. Branding — v1.12: get_license_branding RPC (SECURITY DEFINER; the
        //    server checks the key is valid for the product and bound to this
        //    device). Falls back to the direct license_branding read while the
        //    function is not deployed (onError reports notFound = true).
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches branding through the get_license_branding RPC.
        /// </summary>
        /// <param name="onSuccess">BrandingData, or null when the client configured none.</param>
        /// <param name="onError">(functionMissing, message). functionMissing = the RPC is not deployed.</param>
        public IEnumerator FetchBrandingRpc(string licenseKey, int productId, string deviceId,
            Action<BrandingData> onSuccess, Action<bool, string> onError)
        {
            var body = JsonUtility.ToJson(new ReqBranding
            {
                p_license_key = licenseKey,
                p_product_id = productId,
                p_device_id = deviceId
            });

            using (var request = BuildRpc("get_license_branding", body))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(IsFunctionMissing(request),
                        $"get_license_branding failed: {request.error} (HTTP {request.responseCode}) {Snippet(request)}");
                    yield break;
                }

                try
                {
                    string text = request.downloadHandler.text?.Trim();
                    if (string.IsNullOrEmpty(text) || text == "null" || text == "{}")
                    {
                        onSuccess?.Invoke(null);
                        yield break;
                    }

                    var result = JsonUtility.FromJson<RpcBrandingResult>(text);
                    if (result == null || (string.IsNullOrEmpty(result.brandName) && string.IsNullOrEmpty(result.logoUrl)))
                    {
                        if (result != null && !string.IsNullOrEmpty(result.error))
                            Debug.LogWarning($"[VR Licensing] get_license_branding: {result.error}");
                        onSuccess?.Invoke(null);
                        yield break;
                    }

                    onSuccess?.Invoke(new BrandingData
                    {
                        brand_name = result.brandName,
                        logo_url = result.logoUrl
                    });
                }
                catch (Exception e)
                {
                    onError?.Invoke(false, $"Error parsing branding response: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Pre-1.12 branding read (direct select on license_branding by license id). Kept as the
        /// fallback until get_license_branding is deployed and the anon policy is removed.
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
        // 5. Telemetry — v1.12: report_telemetry RPC (SECURITY DEFINER; server
        //    checks the key is bound to this device, caps size and rate). Falls
        //    back to the direct insert while the function is not deployed.
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends telemetry through the report_telemetry RPC. Fire-and-forget; errors are
        /// logged only, except that a missing function triggers <paramref name="onFunctionMissing"/>
        /// so the caller can fall back to <see cref="SendTelemetry"/>.
        /// </summary>
        public IEnumerator SendTelemetryRpc(string licenseKey, int productId, string deviceId,
            TelemetryPayload payload, Action onFunctionMissing)
        {
            string head = JsonUtility.ToJson(new ReqTelemetryHead
            {
                p_license_key = licenseKey,
                p_product_id = productId,
                p_device_id = deviceId
            });
            string body = head.Substring(0, head.Length - 1) + ",\"p_payload\":" + payload.ToJson() + "}";

            using (var request = BuildRpc("report_telemetry", body))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success) yield break;

                if (IsFunctionMissing(request))
                {
                    Debug.Log("[VR Licensing] report_telemetry not deployed yet — falling back to the direct insert.");
                    onFunctionMissing?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[VR Licensing] report_telemetry failed (non-critical): {request.error} " +
                                     $"(HTTP {request.responseCode}) {Snippet(request)}");
                }
            }
        }

        /// <summary>
        /// Pre-1.12 telemetry path (anon INSERT into device_telemetry). Kept as the fallback.
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

        // ────────────────────────────────────────────────────────────────────
        // 6. Store subscription verification — Edge Function (spec §1.1, phase B).
        //    The function validates the nonce with the store, reads the user's
        //    subscription through the store's S2S API and upserts store_entitlements.
        //    The client only ever receives the verdict.
        // ────────────────────────────────────────────────────────────────────

        public IEnumerator VerifyStoreEntitlement(string storeUserId, string nonce, int productId,
            string deviceId, string platform, Action<StoreVerifyResult> onSuccess, Action<string> onError)
        {
            var body = JsonUtility.ToJson(new ReqVerifyStore
            {
                metaUserId = storeUserId,
                nonce = nonce,
                productId = productId,
                deviceId = deviceId,
                platform = platform
            });

            using (var request = BuildPost($"{supabaseUrl}/functions/v1/verify-store-entitlement", body, 20))
            {
                Debug.Log("[VR Licensing] Verifying store subscription with the server...");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"verify-store-entitlement failed: {request.error} " +
                                    $"(HTTP {request.responseCode}) {Snippet(request)}");
                    yield break;
                }

                StoreVerifyResult result;
                try
                {
                    result = JsonUtility.FromJson<StoreVerifyResult>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error parsing verify-store-entitlement response: {e.Message}");
                    yield break;
                }

                if (result == null)
                {
                    onError?.Invoke("verify-store-entitlement returned an empty response.");
                    yield break;
                }

                if (!result.entitled && !string.IsNullOrEmpty(result.error))
                {
                    onError?.Invoke(result.error);
                    yield break;
                }

                onSuccess?.Invoke(result);
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
    internal class ReqStatusStore
    {
        public string p_device_id;
        public int p_product_id;
        public string p_store_user_id;
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

    [Serializable]
    internal class ReqBranding
    {
        public string p_license_key;
        public int p_product_id;
        public string p_device_id;
    }

    [Serializable]
    internal class ReqTelemetryHead
    {
        public string p_license_key;
        public int p_product_id;
        public string p_device_id;
    }

    [Serializable]
    internal class ReqVerifyStore
    {
        public string metaUserId;
        public string nonce;
        public int productId;
        public string deviceId;
        public string platform;
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

    /// <summary>Result of get_license_branding RPC (v1.12).</summary>
    [Serializable]
    public class RpcBrandingResult
    {
        public string brandName;
        public string logoUrl;
        public string error;
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
        public PromoData promo;         // active promotion for this product, or empty
        public StoreStatus store;       // v1.12: only when p_store_user_id was sent
    }

    /// <summary>
    /// Read-only view of store_entitlements for the signed-in store user (v1.12, spec §1.3).
    /// No store call happens server-side for this; it reflects the last verification.
    /// </summary>
    [Serializable]
    public class StoreStatus
    {
        public bool entitled;
        public string sku;
        public string expiresAt;
        public string graceUntil;
        public string status;
        public string lastVerifiedAt;

        public bool HasData => !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(sku);
    }

    /// <summary>
    /// Promotion the app should advertise (e.g. on the demo-expired panel). Configured
    /// server-side in product_promos so the offer can change without a rebuild. The
    /// code must also exist in Stripe — this only tells the app what to show.
    /// </summary>
    [Serializable]
    public class PromoData
    {
        public string code;
        public string headline;
        public string detail;
        public int discount_percent;
        public string url;

        public bool IsValid => !string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(url);
    }
}
