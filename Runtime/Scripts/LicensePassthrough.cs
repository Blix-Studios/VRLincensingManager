using System.Collections.Generic;
using UnityEngine;
#if HAS_AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

namespace VRLicensing
{
    /// <summary>
    /// WhatsApp-style passthrough background for the license UI: while the modal is open,
    /// the game scene is hidden and the headset shows the real room instead, so the user
    /// can read a license key from their phone or a printed card without taking the
    /// headset off.
    ///
    /// How it works (Meta OpenXR + AR Foundation):
    ///  • An <see cref="ARSession"/> + <see cref="ARCameraManager"/> drive the passthrough
    ///    compositing layer. This is SYSTEM-composited video — the app never sees camera
    ///    frames, so no camera permission is involved (unlike the QR scanner).
    ///  • The camera clears to transparent black, and its culling mask is narrowed to the
    ///    UI layer, so the environment disappears and passthrough shows through.
    ///  • The whole XR rig root is moved to the UI layer while active, keeping controller
    ///    models and interaction rays visible over the passthrough feed.
    ///
    /// Everything is restored exactly on <see cref="Disable"/>. If the project doesn't have
    /// the 'Meta Quest: Camera (Passthrough)' OpenXR feature enabled, the compositor simply
    /// shows black behind the UI — a graceful "dark room" fallback, never an error.
    /// </summary>
    internal sealed class LicensePassthrough
    {
        /// <summary>Layer the license UI lives on; also what the camera renders while active.</summary>
        public const int UiLayer = 5; // Unity's built-in "UI" layer

        private bool active;

        private Camera cam;
        private CameraClearFlags prevClearFlags;
        private Color prevBackground;
        private int prevCullingMask;

        private readonly Dictionary<Transform, int> prevLayers = new Dictionary<Transform, int>();

#if HAS_AR_FOUNDATION
        private GameObject sessionGo;      // created by us, destroyed on Disable
        private ARCameraManager addedCameraManager; // added by us, removed on Disable
#endif

        public void Enable(Camera targetCam)
        {
            if (active || targetCam == null) return;
            active = true;
            cam = targetCam;

            prevClearFlags = cam.clearFlags;
            prevBackground = cam.backgroundColor;
            prevCullingMask = cam.cullingMask;

            // Transparent clear → the compositor fills unrendered pixels with passthrough.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << UiLayer;

            // Keep hands/controllers/rays visible: they live under the rig root.
            prevLayers.Clear();
            Transform rigRoot = cam.transform.root;
            foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                prevLayers[t] = t.gameObject.layer;
                t.gameObject.layer = UiLayer;
            }

#if HAS_AR_FOUNDATION
            if (Object.FindFirstObjectByType<ARSession>() == null)
            {
                sessionGo = new GameObject("VRLicensing ARSession (passthrough)");
                sessionGo.AddComponent<ARSession>();
            }

            if (cam.GetComponent<ARCameraManager>() == null)
                addedCameraManager = cam.gameObject.AddComponent<ARCameraManager>();
#endif
        }

        public void Disable()
        {
            if (!active) return;
            active = false;

#if HAS_AR_FOUNDATION
            if (addedCameraManager != null)
            {
                Object.Destroy(addedCameraManager);
                addedCameraManager = null;
            }
            if (sessionGo != null)
            {
                Object.Destroy(sessionGo);
                sessionGo = null;
            }
#endif

            foreach (var pair in prevLayers)
            {
                if (pair.Key != null)
                    pair.Key.gameObject.layer = pair.Value;
            }
            prevLayers.Clear();

            if (cam != null)
            {
                cam.clearFlags = prevClearFlags;
                cam.backgroundColor = prevBackground;
                cam.cullingMask = prevCullingMask;
            }
            cam = null;
        }
    }
}
