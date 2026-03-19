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
                    string errorMsg = $"Error de conexión: {request.error}";
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
                        onError?.Invoke("Clave de licencia no válida o inactiva.");
                        yield break;
                    }

                    LicenseData license = licenses.items[0];

                    // Double-check expiration
                    if (!license.IsValid)
                    {
                        onError?.Invoke($"La licencia ha expirado ({license.expires_at}).");
                        yield break;
                    }

                    Debug.Log($"[VR Licensing] License validated: {license.license_key} " +
                        $"(type: {license.license_type}, expires: {license.expires_at})");

                    onSuccess?.Invoke(license);
                }
                catch (Exception e)
                {
                    string errorMsg = $"Error al procesar la respuesta: {e.Message}";
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
    }
}
