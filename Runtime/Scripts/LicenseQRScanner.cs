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
        /// CPU-built preview, refreshed on every decode tick from the very pixel buffer
        /// ZXing reads. Bind THIS in the viewfinder rather than the WebCamTexture:
        /// rendering the WebCamTexture directly shows black under Vulkan on Quest, while
        /// the CPU pixel path works — and this way the preview is by construction exactly
        /// what the decoder sees.
        /// </summary>
        public Texture2D PreviewTexture { get; private set; }

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
            // (REVOKE_WHEN_REQUESTED) - so request both, every time.
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

            // 3. Camera devices (empty on headsets without camera access, e.g. Quest 2)
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

            // 4. Scan, cycling devices: the OS exposes several camera nodes and some of
            // them serve only privacy-blacked frames. Try each until one delivers a real
            // image; if every node is black, raw camera access is disabled on this OS and
            // scanning cannot work (measured: avg luminance 0 on every frame).
            const float decodeInterval = 0.2f;
            const int blackTicksPerDevice = 10; // ~2s of pure black -> try the next node
            float elapsed = 0f;
            bool anyLight = false;

            for (int d = 0; d < devices.Length && IsScanning && elapsed < timeoutSeconds; d++)
            {
                ReleaseWebcam();
                // Let the OS pick the camera's native mode: forcing an unsupported
                // resolution silently delivers black frames.
                m_Webcam = new WebCamTexture(devices[d].name);
                m_Webcam.Play();
                onStatus?.Invoke("Point the QR code into view...");

                float startWait = 0f;
                while (m_Webcam.width <= 16 && startWait < 5f)
                {
                    startWait += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (m_Webcam.width <= 16)
                {
                    Debug.Log($"[VR Licensing] Camera did not start: {devices[d].name}");
                    continue;
                }

                Debug.Log($"[VR Licensing] Camera streaming ({devices[d].name}): " +
                          $"{m_Webcam.width}x{m_Webcam.height} rotation={m_Webcam.videoRotationAngle}");

                int blackTicks = 0;
                bool deviceLit = false;
                float sinceDecode = decodeInterval;

                while (IsScanning && elapsed < timeoutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    sinceDecode += Time.unscaledDeltaTime;

                    if (sinceDecode >= decodeInterval && m_Webcam.isPlaying && m_Webcam.width > 16)
                    {
                        sinceDecode = 0f;
                        int w = m_Webcam.width, h = m_Webcam.height;
                        Color32[] pixels = m_Webcam.GetPixels32();

                        long sum = 0; int count = 0;
                        for (int i = 0; i < pixels.Length; i += 997) { sum += pixels[i].g; count++; }
                        long luma = count > 0 ? sum / count : 0;

                        if (luma < 3)
                        {
                            if (!deviceLit && ++blackTicks >= blackTicksPerDevice)
                            {
                                Debug.Log($"[VR Licensing] Only black frames from {devices[d].name} " +
                                          "- trying the next device.");
                                break; // next device
                            }
                        }
                        else
                        {
                            if (!deviceLit)
                                Debug.Log($"[VR Licensing] Real image from {devices[d].name} (luma {luma}).");
                            deviceLit = true;
                            anyLight = true;
                            blackTicks = 0;
                        }

                        UpdatePreview(pixels, w, h);

                        string decoded = TryDecode(pixels, w, h);
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
            }

            bool cancelled = !IsScanning;
            Cleanup();

            if (cancelled)
                yield break; // StopScan already gave feedback

            if (!anyLight)
                onUnsupported?.Invoke("This headset is not sharing the camera image. " +
                                      "Update HorizonOS, or enter the key manually.");
            else
                onStatus?.Invoke("No QR code detected. Try again or enter the key manually.");
        }

        private void ReleaseWebcam()
        {
            if (m_Webcam == null) return;
            if (m_Webcam.isPlaying) m_Webcam.Stop();
            Destroy(m_Webcam);
            m_Webcam = null;
        }

        /// <summary>Copies the decode buffer into the preview texture (created lazily).</summary>
        private void UpdatePreview(Color32[] pixels, int width, int height)
        {
            if (PreviewTexture == null || PreviewTexture.width != width || PreviewTexture.height != height)
            {
                if (PreviewTexture != null) Destroy(PreviewTexture);
                PreviewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            PreviewTexture.SetPixels32(pixels);
            PreviewTexture.Apply(false, false);
        }

        private void Cleanup()
        {
            IsScanning = false;

            ReleaseWebcam();

            if (PreviewTexture != null)
            {
                Destroy(PreviewTexture);
                PreviewTexture = null;
            }

            m_ScanRoutine = null;
        }

        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
    }
}
