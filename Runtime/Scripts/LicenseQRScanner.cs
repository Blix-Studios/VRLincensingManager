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

        // ZXing (reflection)
        private object m_BarcodeReader;
        private MethodInfo m_DecodeMethod;
        private PropertyInfo m_ResultTextProp;
        private bool m_ZxingResolved;

        /// <summary>True while a scan is in progress.</summary>
        public bool IsScanning { get; private set; }

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
            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                Cleanup();
                onUnsupported?.Invoke("No camera available on this headset.");
                yield break;
            }

            m_Webcam = new WebCamTexture(WebCamTexture.devices[0].name, 1024, 1024, 30);
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
            if (m_ZxingResolved) return m_BarcodeReader != null;
            m_ZxingResolved = true;

            try
            {
                Type readerType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    readerType = asm.GetType("ZXing.BarcodeReader");
                    if (readerType != null) break;
                }

                if (readerType == null)
                {
                    Debug.LogWarning("[VR Licensing] ZXing not found. Add 'zxing.unity.dll' to the " +
                        "project to enable QR scanning.");
                    return false;
                }

                m_BarcodeReader = Activator.CreateInstance(readerType);
                m_DecodeMethod = readerType.GetMethod("Decode",
                    new[] { typeof(Color32[]), typeof(int), typeof(int) });

                if (m_DecodeMethod == null)
                {
                    Debug.LogWarning("[VR Licensing] ZXing BarcodeReader.Decode(Color32[],int,int) not found.");
                    m_BarcodeReader = null;
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] Failed to resolve ZXing: {e.Message}");
                m_BarcodeReader = null;
                return false;
            }
        }

        private string TryDecode(Color32[] pixels, int width, int height)
        {
            try
            {
                var result = m_DecodeMethod.Invoke(m_BarcodeReader, new object[] { pixels, width, height });
                if (result == null) return null;

                if (m_ResultTextProp == null)
                    m_ResultTextProp = result.GetType().GetProperty("Text");

                return m_ResultTextProp?.GetValue(result) as string;
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
