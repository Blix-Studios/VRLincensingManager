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
    ///  • The camera clears to transparent black and culls down to a dedicated layer that
    ///    only the license UI occupies, so the whole scene — including the game's own
    ///    canvases, which live on the built-in UI layer — disappears behind passthrough.
    ///  • Controller/hand visuals under the rig stay visible: purely visual GameObjects
    ///    (a Renderer and no physics component) are borrowed onto the render layer.
    ///
    /// Two hard-learned rules this class enforces:
    ///  1. NEVER change the layer of anything with a Collider, Rigidbody or
    ///     CharacterController. Physics layer matrices commonly disable collisions for
    ///     UI-ish layers, and relayering the rig's capsule once sent the player falling
    ///     through the floor.
    ///  2. Don't render on the built-in UI layer (5): every world-space canvas a game
    ///     creates defaults to it, so culling to it leaves the game's menus visible
    ///     (and apparently interactive) through the passthrough.
    ///
    /// Everything is restored exactly on <see cref="Disable"/>. If the project doesn't
    /// have the 'Meta Quest: Camera (Passthrough)' OpenXR feature enabled, the compositor
    /// simply shows black behind the UI — a graceful "dark room" fallback, never an error.
    /// </summary>
    internal sealed class LicensePassthrough
    {
        /// <summary>
        /// Layer used for rendering while passthrough is active. Resolved once per
        /// session: the highest project-unnamed layer, falling back to the built-in
        /// UI layer if the project somehow names all 24 user layers.
        /// </summary>
        public int RenderLayer { get; private set; } = -1;

        public bool IsActive { get; private set; }

        private Camera cam;
        private CameraClearFlags prevClearFlags;
        private Color prevBackground;
        private int prevCullingMask;

        private readonly Dictionary<GameObject, int> prevLayers = new Dictionary<GameObject, int>();

        // Gravity is frozen while passthrough is up. Creating the AR session (and any
        // other tracking hiccup) can momentarily invalidate the camera pose; XRI's
        // gravity then shoves the rig's capsule below the floor and the player falls
        // into the void forever. No gravity while gated → no fall, ever.
        private readonly List<Behaviour> pausedGravity = new List<Behaviour>();
        private Transform rigRoot;
        private Vector3 rigPosAtEnable;

#if HAS_AR_FOUNDATION
        // One AR session for the whole app lifetime. Creating or destroying an ARSession
        // RESTARTS the underlying OpenXR session (verified in logcat: a second "System
        // Startup Completed" fires right after instantiation) — and a mid-experience
        // restart is exactly the tracking hiccup described above. So the session is
        // created once, marked DontDestroyOnLoad, and never torn down; passthrough is
        // toggled purely by enabling/disabling the ARCameraManager.
        private static GameObject sessionGo;
        private ARCameraManager cameraManager;

        /// <summary>
        /// Creates the app-wide AR session if neither we nor the host app made one yet.
        /// Called from the bootstrapper at app start so the one-and-only OpenXR session
        /// restart happens at boot, before the player is in a scene where it matters.
        /// </summary>
        public static void EnsureSessionAlive()
        {
            if (sessionGo != null) return;
            if (Object.FindFirstObjectByType<ARSession>() != null) return; // app owns one

            sessionGo = new GameObject("VRLicensing ARSession");
            Object.DontDestroyOnLoad(sessionGo);
            sessionGo.AddComponent<ARSession>();
        }
#else
        public static void EnsureSessionAlive() { }
#endif

        /// <summary>
        /// Enables passthrough. <paramref name="uiRoots"/> are hierarchies that belong to
        /// the license UI (modal canvas, HUD, spatial keyboard) — they are moved wholesale
        /// onto the render layer for the duration.
        /// </summary>
        public void Enable(Camera targetCam, params GameObject[] uiRoots)
        {
            if (IsActive || targetCam == null) return;
            IsActive = true;
            cam = targetCam;

            if (RenderLayer < 0) RenderLayer = PickRenderLayer();

            prevClearFlags = cam.clearFlags;
            prevBackground = cam.backgroundColor;
            prevCullingMask = cam.cullingMask;

            // Transparent clear → the compositor fills unrendered pixels with passthrough.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << RenderLayer;

            prevLayers.Clear();

            foreach (GameObject root in uiRoots)
            {
                if (root != null)
                    BorrowHierarchy(root);
            }

            // Keep hands/controllers/rays visible over the passthrough feed — but only
            // their visuals. Physics stays untouched (rule 1 above).
            foreach (Renderer r in cam.transform.root.GetComponentsInChildren<Renderer>(true))
            {
                GameObject go = r.gameObject;
                if (go.GetComponent<Collider>() != null) continue;
                if (go.GetComponent<Rigidbody>() != null) continue;
                if (go.GetComponent<CharacterController>() != null) continue;
                Borrow(go);
            }

            // Freeze rig gravity for the duration (see field comment). Resolved by type
            // name so the package works across XRI versions without a hard reference.
            rigRoot = cam.transform.root;
            rigPosAtEnable = rigRoot.position;
            pausedGravity.Clear();
            foreach (Behaviour b in rigRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (b != null && b.enabled && b.GetType().Name == "GravityProvider")
                {
                    b.enabled = false;
                    pausedGravity.Add(b);
                }
            }

#if HAS_AR_FOUNDATION
            EnsureSessionAlive();

            cameraManager = cam.GetComponent<ARCameraManager>();
            if (cameraManager == null)
                cameraManager = cam.gameObject.AddComponent<ARCameraManager>();
            cameraManager.enabled = true;
#endif
        }

        /// <summary>
        /// Moves a late-spawned UI hierarchy (e.g. the global XRI keyboard, which only
        /// exists after the first field is focused) onto the render layer mid-session.
        /// </summary>
        public void BorrowLateHierarchy(GameObject root)
        {
            if (!IsActive || root == null || root.layer == RenderLayer) return;
            BorrowHierarchy(root);
        }

        public void Disable()
        {
            if (!IsActive) return;
            IsActive = false;

#if HAS_AR_FOUNDATION
            // Only the camera manager toggles off — the AR session stays alive for the
            // whole app run so the OpenXR session never restarts mid-experience.
            if (cameraManager != null)
                cameraManager.enabled = false;
#endif

            foreach (Behaviour b in pausedGravity)
            {
                if (b != null) b.enabled = true;
            }
            pausedGravity.Clear();

            // Safety net: if some missed mechanism still dragged the rig down while the
            // modal was up, put the player back where they were. One silent teleport
            // beats an endless fall.
            if (rigRoot != null && rigPosAtEnable.y - rigRoot.position.y > 1f)
            {
                Debug.LogWarning($"[VR Licensing] Rig dropped {rigPosAtEnable.y - rigRoot.position.y:F1}m " +
                                 "while the license UI was open — restoring its position.");
                rigRoot.position = rigPosAtEnable;
            }
            rigRoot = null;

            foreach (var pair in prevLayers)
            {
                if (pair.Key != null)
                    pair.Key.layer = pair.Value;
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

        private void Borrow(GameObject go)
        {
            if (go.layer == RenderLayer || prevLayers.ContainsKey(go)) return;
            prevLayers[go] = go.layer;
            go.layer = RenderLayer;
        }

        private void BorrowHierarchy(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                Borrow(t.gameObject);
        }

        private static int PickRenderLayer()
        {
            // Search from the top so we stay clear of low user layers, which projects
            // tend to allocate first.
            for (int i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                    return i;
            }

            Debug.LogWarning("[VR Licensing] Every user layer is named in this project; " +
                             "passthrough will render on the built-in UI layer and the " +
                             "game's own canvases may stay visible through it.");
            return 5;
        }
    }
}
