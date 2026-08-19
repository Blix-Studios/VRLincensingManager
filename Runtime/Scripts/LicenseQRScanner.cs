using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Android;

namespace VRLicensing
{
    /// <summary>
    /// Scans a QR code from the headset passthrough camera and returns the decoded text.
    /// Uses <see cref="WebCamTexture"/> for camera frames and ZXing (resolved via reflection,
    /// so the package keeps no hard dependency) to decode.
    ///
    /// Camera access on Meta Quest requires **Quest 3 / 3S on OS v74+** with
    /// <c>android.permission.CAMERA</c> (and <c>horizonos.permission.HEADSET_CAMERA</c> in the
    /// AndroidManifest). On headsets without camera access (e.g. Quest 2) or without the ZXing
    /// library, StartScan reports <c>onUnsupported</c> so the caller can hide the QR option.
    /// </summary>
    public class LicenseQRScanner : MonoBehaviour
    {
        private WebCamTexture m_Webcam;
        private Coroutine m_ScanRoutine;

        // ZXing — referenced directly: the package ships zxing.unity.dll, so there is no
        // optional dependency to soften with reflection anymore. Direct usage also means
        // the IL2CPP linker sees the assembly as used (reflection-only usage got it
        // stripped from player builds, which silently killed scanning on device).
        private ZXing.BarcodeReader m_BarcodeReader;

        /// <summary>True while a scan is in progress.</summary>
        public bool IsScanning { get; private set; }

        /// <summary>
        /// The live camera feed while scanning (null otherwise). The UI shows this in a
        /// viewfinder so the user can aim — WebCamTexture needs no AR/passthrough at all.
        /// </summary>
        public WebCamTexture CameraTexture => m_Webcam;

        /// <summary>
        /// Starts scanning. Exactly one of the callbacks fires per scan session.
        /// </summary>
        /// <param name="timeoutSeconds">Give up after this many seconds without a hit.</param>
        /// <param name="onDecoded">Fires with the decoded/extracted license key.</param>
        /// <param name="onStatus">Progress messages for the UI (non-terminal).</param>
        /// <param name="onUnsupported">Terminal: camera/permission/ZXing not available.</param>
        public void StartScan(float timeoutSeconds, Action<string> onDecoded,
            Action<string> onStatus, Action<string> onUnsupported)
        {
            if (IsScanning) return;
            m_ScanRoutine = StartCoroutine(ScanRoutine(timeoutSeconds, onDecoded, onStatus, onUnsupported));
        }

        /// <summary>Stops an in-progress scan and releases the camera.</summary>
        public void StopScan()
        {
            if (!IsScanning) return;
            Cleanup();
        }

        private IEnumerator ScanRoutine(float timeoutSeconds, Action<string> onDecoded,
            Action<string> onStatus, Action<string> onUnsupported)
        {
            IsScanning = true;

            // 1. Camera permissions (Android only). HorizonOS v74+ gates the passthrough
            // camera behind BOTH android.permission.CAMERA and its own HEADSET_CAMERA
            // permission, and pre-grants the latter only until the app explicitly asks
            // (REVOKE_WHEN_REQUESTED) — so request both, every time.
#if UNITY_ANDROID && !UNITY_EDITOR
            string[] required = { Permission.Camera, "horizonos.permission.HEADSET_CAMERA" };
            foreach (string permission in required)
            {
                if (Permission.HasUserAuthorizedPermission(permission)) continue;

                onStatus?.Invoke("Requesting camera permission...");
                Permission.RequestUserPermission(permission);

                float waited = 0f;
                while (!Permission.HasUserAuthorizedPermission(permission) && waited < 20f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!Permission.HasUserAuthorizedPermission(permission))
                {
                    Cleanup();
                    onUnsupported?.Invoke("Camera permission denied.");
                    yield break;
                }
            }
#endif

            // 2. ZXing decoder
            if (!TryResolveZXing())
            {
                Cleanup();
                onUnsupported?.Invoke("QR decoder (ZXing) not available.");
                yield break;
            }

            // 3. Camera device (empty on headsets without camera access, e.g. Quest 2)
            var devices = WebCamTexture.devices;
            Debug.Log($"[VR Licensing] WebCamTexture devices: {(devices == null ? 0 : devices.Length)}" +
                      (devices != null && devices.Length > 0
                          ? " (" + string.Join(", ", System.Array.ConvertAll(devices, d => d.name)) + ")"
                          : ""));
            if (devices == null || devices.Length == 0)
            {
                Cleanup();
                onUnsupported?.Invoke("No camera available on this headset.");
                yield break;
            }

            // Let the OS pick the camera's native mode. Forcing a resolution the driver
            // doesn't support (e.g. 1024x1024) can silently deliver black frames on Quest.
            m_Webcam = new WebCamTexture(devices[0].name);
            m_Webcam.Play();
            onStatus?.Invoke("Point the QR code into view...");

            // Wait for the first real frame
            float startWait = 0f;
            while (m_Webcam.width <= 16 && startWait < 5f)
            {
                startWait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (m_Webcam.width <= 16)
            {
                Cleanup();
                onUnsupported?.Invoke("Camera did not start on this headset.");
                yield break;
            }

            Debug.Log($"[VR Licensing] Camera streaming: {m_Webcam.width}x{m_Webcam.height} " +
                      $"rotation={m_Webcam.videoRotationAngle} mirrored={m_Webcam.videoVerticallyMirrored}");

            // 4. Decode loop
            const float decodeInterval = 0.4f;
            float elapsed = 0f;
            float sinceDecode = decodeInterval;

            while (IsScanning && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                sinceDecode += Time.unscaledDeltaTime;

                if (sinceDecode >= decodeInterval && m_Webcam.isPlaying && m_Webcam.width > 16)
                {
                    sinceDecode = 0f;
                    string decoded = TryDecode(m_Webcam.GetPixels32(), m_Webcam.width, m_Webcam.height);
                    if (!string.IsNullOrEmpty(decoded))
                    {
                        string key = ExtractKey(decoded);
                        Cleanup();
                        onDecoded?.Invoke(key);
                        yield break;
                    }
                }

                yield return null;
            }

            Cleanup();
            onStatus?.Invoke("No QR code detected. Try again or enter the key manually.");
        }

        private bool TryResolveZXing()
        {
            if (m_BarcodeReader != null) return true;

            try
            {
                m_BarcodeReader = new ZXing.BarcodeReader();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] Failed to create ZXing reader: {e.Message}");
                return false;
            }
        }

        private string TryDecode(Color32[] pixels, int width, int height)
        {
            try
            {
                var result = m_BarcodeReader.Decode(pixels, width, height);
                return result?.Text;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] QR decode error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts a license key from the decoded payload. Accepts a raw key
        /// (XXXX-XXXX-XXXX-XXXX) or a URL/text containing one; falls back to the raw text.
        /// </summary>
        private static string ExtractKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            raw = raw.Trim();

            var match = System.Text.RegularExpressions.Regex.Match(
                raw.ToUpperInvariant(), @"[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}");

            return match.Success ? match.Value : raw;
        }

        private void Cleanup()
        {
            IsScanning = false;

            if (m_Webcam != null)
            {
                if (m_Webcam.isPlaying) m_Webcam.Stop();
                Destroy(m_Webcam);
                m_Webcam = null;
            }

            m_ScanRoutine = null;
        }

        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
    }
}
