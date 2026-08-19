using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace VRLicensing
{
    /// <summary>
    /// Builds the entire license UI via code — no prefabs, no external assets.
    /// Creates a World-Space Canvas compatible with VR (XR Interaction Toolkit)
    /// and desktop/Editor environments.
    /// </summary>
    public class LicenseUIBuilder : MonoBehaviour
    {
        // ─────────────────────── Design Constants ───────────────────────
        // Modal scale sized for readability from 2m in-headset: at 0.0018 the 1000px canvas
        // spans ~1.8m (~48° of view), comparable to HorizonOS system dialogs. The original
        // 0.001 was legible on a monitor but far too small through lenses.
        private const float CANVAS_SCALE = 0.0018f;
        private const float HUD_SCALE = 0.001f; // demo HUD keeps its compact size
        private const float CANVAS_DISTANCE = 2.0f;
        // Wide enough to hold the two-column "Demo Expired" panel (900px) with margin.
        // Panels are not clipped by the overlay, so anything wider would visibly spill
        // outside the dimmed backdrop.
        private const int CANVAS_PX_WIDTH = 1000;
        private const int CANVAS_PX_HEIGHT = 560;

        // Colors — dark, premium palette
        private static readonly Color COLOR_BG_OVERLAY = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color COLOR_PANEL_BG = new Color(0.10f, 0.10f, 0.14f, 0.95f);
        private static readonly Color COLOR_PANEL_HEADER = new Color(0.13f, 0.13f, 0.18f, 1f);
        private static readonly Color COLOR_ACCENT = new Color(0.29f, 0.56f, 1f, 1f);
        private static readonly Color COLOR_ACCENT_HOVER = new Color(0.40f, 0.65f, 1f, 1f);
        private static readonly Color COLOR_GREEN = new Color(0.18f, 0.72f, 0.35f, 1f);
        private static readonly Color COLOR_GREEN_HOVER = new Color(0.25f, 0.82f, 0.42f, 1f);
        private static readonly Color COLOR_SECONDARY = new Color(0.30f, 0.30f, 0.38f, 1f);
        private static readonly Color COLOR_SECONDARY_HOVER = new Color(0.40f, 0.40f, 0.48f, 1f);
        private static readonly Color COLOR_ERROR = new Color(0.95f, 0.30f, 0.30f, 1f);
        private static readonly Color COLOR_SUCCESS = new Color(0.30f, 0.85f, 0.45f, 1f);
        private static readonly Color COLOR_TEXT = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color COLOR_TEXT_DIM = new Color(0.55f, 0.55f, 0.62f, 1f);
        private static readonly Color COLOR_INPUT_BG = new Color(0.15f, 0.15f, 0.20f, 1f);
        private static readonly Color COLOR_INPUT_BORDER = new Color(0.25f, 0.25f, 0.32f, 1f);

        // ─────────────────────── UI References ───────────────────────
        private Canvas canvas;
        private GameObject overlayPanel;

        // Welcome Panel
        private GameObject welcomePanel;
        private TextMeshProUGUI welcomeInfoText;

        // Key Input Panel
        private GameObject keyInputPanel;
        private TMP_InputField keyField;
        private Button activateButton;
        private TextMeshProUGUI activateButtonText;
        private TextMeshProUGUI statusText;

        // Demo Expired Panel
        private GameObject demoExpiredPanel;
        private TMP_InputField expiredKeyField;
        private Button expiredActivateButton;
        private TextMeshProUGUI expiredActivateButtonText;
        private TextMeshProUGUI expiredStatusText;

        // License Expired Panel
        private GameObject licenseExpiredPanel;
        private TMP_InputField licExpiredKeyField;
        private Button licExpiredActivateButton;
        private TextMeshProUGUI licExpiredActivateButtonText;
        private TextMeshProUGUI licExpiredStatusText;

        // Success Panel
        private GameObject successPanel;
        private TextMeshProUGUI successTitleText;
        private TextMeshProUGUI successSubtitleText;
        private bool successAnimating;

        // Demo timer HUD — its own world-space canvas, head-locked to the bottom-right of view
        private Canvas demoTimerCanvas;
        private GameObject demoTimerPanel;
        private TextMeshProUGUI demoTimerText;
        private DemoModeManager demoManagerRef;

        // Runtime-generated purchase QR — released in OnDestroy (textures are not GC'd).
        private Texture2D purchaseQrTexture;

        // Opt-in WhatsApp-style passthrough background while the license modal is open.
        // The QR scan flow deliberately does NOT use it: it renders the WebCamTexture in
        // an in-panel viewfinder instead, which needs no AR subsystem (starting the AR
        // camera makes the XR Origin re-run floor calibration, which breaks rigs authored
        // against an applied camera offset — measured on device as a fall through the floor).
        private readonly LicensePassthrough passthrough = new LicensePassthrough();
        private bool passthroughActive;
        private Image overlayImage;

        // Scan-mode HUD: floating instructions over passthrough while a QR scan runs.
        private GameObject viewfinderPanel;
        private Camera hudCam;

        // QR scanning (Quest 3/3S camera access; buttons hidden on unsupported headsets)
        private LicenseQRScanner qrScanner;
        private bool qrSupported = true;
        private readonly System.Collections.Generic.List<GameObject> scanButtons =
            new System.Collections.Generic.List<GameObject>();

        // HUD placement relative to the camera (camera-local metres). Tune to taste.
        private const float DEMO_HUD_RIGHT = 0.42f;    // + = right
        private const float DEMO_HUD_DOWN = -0.30f;    // - = down
        private const float DEMO_HUD_FORWARD = 1.0f;   // distance in front of the eyes
        private const float DEMO_HUD_FOLLOW_SPEED = 12f; // higher = more rigidly head-locked

        // Main license modal — lazy "dead-zone" follow so it can't be ignored, yet stays
        // stable while the user aims at its buttons.
        private bool recenteringModal;
        private const float MODAL_RECENTER_ENTER_ANGLE = 32f; // start recentering past this off-center angle
        private const float MODAL_RECENTER_EXIT_ANGLE = 5f;   // settle once re-centered within this angle
        private const float MODAL_MAX_DISTANCE = CANVAS_DISTANCE * 1.8f; // also recenter if the user walks away
        private const float MODAL_FOLLOW_SPEED = 5f;          // gentle for comfort

        private LicenseConfig config;
        private LicenseManager manager;
        private bool isPositioned;
        private Component lazyFollowInstance;

        // ─────────────────── XR Keyboard Cache ───────────────────
        private Type xrKeyboardDisplayType;

        // ─────────────────────── Factory ───────────────────────

        public static LicenseUIBuilder Create(LicenseConfig licenseConfig, LicenseManager licenseManager)
        {
            var go = new GameObject("[VR Licensing UI]");
            DontDestroyOnLoad(go);

            var builder = go.AddComponent<LicenseUIBuilder>();
            builder.config = licenseConfig;
            builder.manager = licenseManager;
            builder.BuildUI();

            return builder;
        }

        // ─────────────────────── Public API ───────────────────────

        /// <summary>Shows the Welcome panel with 3 options: Demo, License, QR.</summary>
        public void ShowWelcome()
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            welcomePanel.SetActive(true);
            keyInputPanel.SetActive(false);
            demoExpiredPanel.SetActive(false);
            licenseExpiredPanel.SetActive(false);
            successPanel.SetActive(false);

            float used = 0f;
            if (manager != null)
            {
                var dm = manager.GetComponent<DemoModeManager>();
                if (dm != null) used = dm.TotalDemoUsedSeconds;
            }
            float remaining = Mathf.Max(0f, config.demoDurationSeconds - used);
            welcomeInfoText.text =
                $"Free demo: {FormatDuration(remaining)} remaining of {FormatDuration(config.demoDurationSeconds)}";
        }

        /// <summary>Shows the Key Input panel for entering a license key.</summary>
        public void ShowKeyInput()
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            welcomePanel.SetActive(false);
            keyInputPanel.SetActive(true);
            demoExpiredPanel.SetActive(false);
            licenseExpiredPanel.SetActive(false);
            successPanel.SetActive(false);
            statusText.text = "";
            ClearKeyField(keyField);
        }

        /// <summary>Shows the Demo Expired panel (only license key option).</summary>
        public void ShowDemoExpired()
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            welcomePanel.SetActive(false);
            keyInputPanel.SetActive(false);
            demoExpiredPanel.SetActive(true);
            licenseExpiredPanel.SetActive(false);
            successPanel.SetActive(false);
            expiredStatusText.text = "";
            ClearKeyField(expiredKeyField);
        }

        /// <summary>Shows the License Expired panel (renewal option).</summary>
        public void ShowLicenseExpired()
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            welcomePanel.SetActive(false);
            keyInputPanel.SetActive(false);
            demoExpiredPanel.SetActive(false);
            licenseExpiredPanel.SetActive(true);
            successPanel.SetActive(false);
            licExpiredStatusText.text = "";
            ClearKeyField(licExpiredKeyField);
        }

        /// <summary>Hides all UI (license valid or demo running).</summary>
        public void HideAll()
        {
            overlayPanel.SetActive(false);
            if (qrScanner != null && qrScanner.IsScanning)
                qrScanner.StopScan();
        }

        /// <summary>
        /// Shows the "license active" confirmation, then hides everything.
        /// </summary>
        /// <param name="alreadyActive">
        /// true when the license was already stored on this device (cached) — shows an
        /// "all good" notice and auto-closes after 8s. false for a fresh key redemption.
        /// </param>
        public void ShowLicensed(bool alreadyActive = false)
        {
            HideDemoTimer();
            EnsurePositioned();
            if (successAnimating) return;

            string title = alreadyActive ? "License active" : "¡Key redeemed successfully!";
            string subtitle = alreadyActive
                ? "This device already has a valid license.\nEverything's good — enjoy the simulator."
                : "Your license has been activated successfully.\nThe simulator will unlock automatically.";
            float hold = alreadyActive ? 8f : 2.5f;

            StartCoroutine(ShowSuccessAndHide(title, subtitle, hold));
        }

        /// <summary>Shows an error message on the active panel.</summary>
        public void ShowError(string message)
        {
            if (keyInputPanel.activeSelf)
            {
                statusText.text = message;
                statusText.color = COLOR_ERROR;
            }
            else if (demoExpiredPanel.activeSelf)
            {
                expiredStatusText.text = message;
                expiredStatusText.color = COLOR_ERROR;
            }
            else if (licenseExpiredPanel.activeSelf)
            {
                licExpiredStatusText.text = message;
                licExpiredStatusText.color = COLOR_ERROR;
            }
        }

        /// <summary>Shows a success message on the active panel.</summary>
        public void ShowSuccess(string message = "License activated successfully")
        {
            if (keyInputPanel.activeSelf)
            {
                statusText.text = message;
                statusText.color = COLOR_SUCCESS;
            }
            else if (demoExpiredPanel.activeSelf)
            {
                expiredStatusText.text = message;
                expiredStatusText.color = COLOR_SUCCESS;
            }
            else if (licenseExpiredPanel.activeSelf)
            {
                licExpiredStatusText.text = message;
                licExpiredStatusText.color = COLOR_SUCCESS;
            }
        }

        /// <summary>Sets loading state on activate buttons.</summary>
        public void SetLoading(bool loading)
        {
            if (activateButton != null)
            {
                activateButton.interactable = !loading;
                activateButtonText.text = loading ? "Validating..." : "Activate License";
            }
            if (expiredActivateButton != null)
            {
                expiredActivateButton.interactable = !loading;
                expiredActivateButtonText.text = loading ? "Validating..." : "Activate License";
            }
            if (licExpiredActivateButton != null)
            {
                licExpiredActivateButton.interactable = !loading;
                licExpiredActivateButtonText.text = loading ? "Validating..." : "Activate License";
            }
        }

        // ─────────────────────── Build Methods ───────────────────────

        private void BuildUI()
        {
            BuildCanvas();
            BuildOverlay();
            BuildWelcomePanel();
            BuildKeyInputPanel();
            BuildDemoExpiredPanel();
            BuildLicenseExpiredPanel();
            BuildSuccessPanel();
            BuildDemoTimer();

            // Idiomatic UI layer for normal rendering. While passthrough is active the
            // hierarchy is borrowed onto a dedicated render layer instead (see
            // LicensePassthrough) — the built-in UI layer can't be used for that because
            // every game canvas defaults to it too.
            SetLayerRecursively(canvas.gameObject, 5);
            if (demoTimerCanvas != null)
                SetLayerRecursively(demoTimerCanvas.gameObject, 5);

            overlayImage = overlayPanel.GetComponent<Image>();

            BuildViewfinder();

            // Start hidden
            overlayPanel.SetActive(false);
        }

        /// <summary>
        /// Scan-mode HUD, WhatsApp device-linking style: no panel, no viewfinder — just
        /// floating text over passthrough telling the user to look at the QR code, plus a
        /// Cancel button. The user aims with their own eyes through passthrough (the
        /// headset camera scans whatever the head faces), so no preview is needed — and
        /// on top of that, the OS blacks out raw camera frames for apps on some HorizonOS
        /// versions, so a preview couldn't be trusted anyway.
        /// </summary>
        private void BuildViewfinder()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            viewfinderPanel = CreatePanel("ScanHud", overlayRt, Color.clear);
            var rt = viewfinderPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700, 300);
            rt.anchoredPosition = new Vector2(0, 40);

            CreateTMPText("ScanTitle", rt,
                "Look at the QR code",
                26, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(680, 40), pos: new Vector2(0, -20));

            CreateTMPText("ScanSubtitle", rt,
                "Hold it steady in front of you — or cancel and type the key manually.",
                15, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(680, 30), pos: new Vector2(0, -66));

            CreatePositionedButton("ScanCancel", rt,
                "Cancel", new Vector2(180, 40), new Vector2(0, 30),
                COLOR_ERROR, COLOR_ERROR, () =>
                {
                    if (qrScanner != null && qrScanner.IsScanning)
                    {
                        qrScanner.StopScan();
                        SetScanStatus("Scan cancelled.", COLOR_TEXT_DIM);
                    }
                },
                anchorAtBottom: true);

            viewfinderPanel.SetActive(false);
        }

        // Panels hidden while scan mode is active, restored exactly afterwards.
        private readonly System.Collections.Generic.List<GameObject> panelsHiddenForScan =
            new System.Collections.Generic.List<GameObject>();

        /// <summary>
        /// Enters/leaves scan mode: while scanning, every modal panel hides so only the
        /// floating instructions remain over passthrough; on exit the previous panel
        /// comes back untouched.
        /// </summary>
        private void SyncViewfinder()
        {
            if (viewfinderPanel == null) return;

            bool scanning = qrScanner != null && qrScanner.IsScanning;
            if (viewfinderPanel.activeSelf == scanning) return;

            viewfinderPanel.SetActive(scanning);

            if (scanning)
            {
                panelsHiddenForScan.Clear();
                foreach (GameObject panel in new[]
                         { welcomePanel, keyInputPanel, demoExpiredPanel, licenseExpiredPanel, successPanel })
                {
                    if (panel != null && panel.activeSelf)
                    {
                        panel.SetActive(false);
                        panelsHiddenForScan.Add(panel);
                    }
                }
            }
            else
            {
                foreach (GameObject panel in panelsHiddenForScan)
                {
                    if (panel != null) panel.SetActive(true);
                }
                panelsHiddenForScan.Clear();
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("LicenseCanvas");
            canvasGo.transform.SetParent(transform);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 10f;
            canvasScaler.referencePixelsPerUnit = 100f;

            // Add TrackedDeviceGraphicRaycaster via reflection (required for VR interaction)
            bool addedXR = false;
            var xrRaycasterType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (xrRaycasterType != null)
            {
                canvasGo.AddComponent(xrRaycasterType);
                addedXR = true;
                Debug.Log("[VR Licensing] TrackedDeviceGraphicRaycaster added.");
            }
            if (!addedXR)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
                Debug.Log("[VR Licensing] Standard GraphicRaycaster added.");
            }

            // Canvas size
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_PX_WIDTH, CANVAS_PX_HEIGHT);
            canvasGo.transform.localScale = Vector3.one * CANVAS_SCALE;

            // LazyFollow for VR head tracking
            AddLazyFollow(canvasGo);
        }

        private void BuildOverlay()
        {
            overlayPanel = CreatePanel("Overlay", canvas.GetComponent<RectTransform>(),
                COLOR_BG_OVERLAY, stretch: true);
        }

        // ─────────────────── WELCOME PANEL ───────────────────

        private void BuildWelcomePanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            welcomePanel = CreatePanel("WelcomePanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = welcomePanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(600, 400);
            panelRt.anchoredPosition = Vector2.zero;

            // Header
            var header = CreatePanel("WelcomeHeader", panelRt, COLOR_PANEL_HEADER);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 70);
            headerRt.anchoredPosition = Vector2.zero;

            CreateTMPText("WelcomeTitle", headerRt,
                config.appDisplayName,
                24, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(20, 20, 0, 0));

            // Subtitle
            CreateTMPText("WelcomeSubtitle", panelRt,
                "Select an option to continue",
                14, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(500, 30), pos: new Vector2(0, -85));

            // Buttons container
            var btnContainer = CreatePanel("BtnContainer", panelRt, Color.clear);
            var btnContRt = btnContainer.GetComponent<RectTransform>();
            btnContRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnContRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnContRt.sizeDelta = new Vector2(400, 160);
            btnContRt.anchoredPosition = new Vector2(0, -10);

            var vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Button 1: Start Demo (green)
            CreateLayoutButton("StartDemoBtn", btnContRt,
                "Start Free Demo", 45,
                COLOR_GREEN, COLOR_GREEN_HOVER,
                OnStartDemoClicked);

            // Button 2: Enter License Key (blue accent)
            CreateLayoutButton("EnterKeyBtn", btnContRt,
                "Enter License Key", 45,
                COLOR_ACCENT, COLOR_ACCENT_HOVER,
                OnEnterKeyClicked);

            // Button 3: Scan QR (secondary) — captured so it can be hidden on unsupported headsets
            scanButtons.Add(CreateLayoutButton("ScanQRBtn", btnContRt,
                "Scan QR", 45,
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER,
                OnScanQRClicked));

            // Info text
            welcomeInfoText = CreateTMPText("WelcomeInfo", panelRt,
                "", 12, FontStyles.Italic, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
                size: new Vector2(400, 25), pos: new Vector2(0, 20));
        }

        // ─────────────────── KEY INPUT PANEL ───────────────────

        private void BuildKeyInputPanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            keyInputPanel = CreatePanel("KeyInputPanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = keyInputPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(650, 350);
            panelRt.anchoredPosition = Vector2.zero;

            // Header
            var header = CreatePanel("KeyHeader", panelRt, COLOR_PANEL_HEADER);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 60);
            headerRt.anchoredPosition = Vector2.zero;

            CreateTMPText("KeyTitle", headerRt,
                "Enter License Key",
                20, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(20, 20, 0, 0));

            // Subtitle
            CreateTMPText("KeySubtitle", panelRt,
                "Enter your license key (format XXXX-XXXX-XXXX-XXXX)",
                13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(550, 25), pos: new Vector2(0, -72));

            // Key fields
            keyField = BuildKeyField("KeyField", panelRt, new Vector2(0, 15));

            // Activate button
            activateButton = CreatePositionedButton("ActivateBtn", panelRt,
                "Activate License", new Vector2(260, 42), new Vector2(0, -40),
                COLOR_ACCENT, COLOR_ACCENT_HOVER, OnActivateClicked);
            activateButtonText = activateButton.GetComponentInChildren<TextMeshProUGUI>();

            // Status text
            statusText = CreateTMPText("KeyStatusText", panelRt,
                "", 13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
                size: new Vector2(500, 25), pos: new Vector2(0, 50));

            // Back button
            CreatePositionedButton("BackBtn", panelRt,
                "Back", new Vector2(120, 35), new Vector2(0, 15),
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER, OnBackClicked,
                anchorAtBottom: true);

            keyInputPanel.SetActive(false);
        }

        // ─────────────────── DEMO EXPIRED PANEL ───────────────────

        private void BuildDemoExpiredPanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            // Two columns: "buy it" on the left, "already bought it" on the right.
            // The demo ending is the one moment we have the user's full attention, so the
            // purchase path gets equal billing with the key-entry path instead of being
            // an afterthought.
            demoExpiredPanel = CreatePanel("DemoExpiredPanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = demoExpiredPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(900, 430);
            panelRt.anchoredPosition = Vector2.zero;

            // Header
            var header = CreatePanel("ExpHeader", panelRt, COLOR_PANEL_HEADER);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 60);
            headerRt.anchoredPosition = Vector2.zero;

            CreateTMPText("ExpTitle", headerRt,
                "Demo Expired",
                20, FontStyles.Bold, COLOR_ERROR, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(20, 20, 0, 0));

            // Message
            CreateTMPText("ExpMsg", panelRt,
                $"Your free trial of {config.appDisplayName} has ended.",
                14, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(820, 30), pos: new Vector2(0, -74));

            // Vertical divider between the two columns
            var divider = CreatePanel("ExpDivider", panelRt, COLOR_TEXT_DIM * 0.4f);
            var divRt = divider.GetComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.5f, 0.5f);
            divRt.anchorMax = new Vector2(0.5f, 0.5f);
            divRt.sizeDelta = new Vector2(2, 250);
            divRt.anchoredPosition = new Vector2(0, -30);

            BuildPurchaseCta(panelRt, columnX: -225f);

            // ── Right column: redeem an existing key ──
            CreateTMPText("ExpRedeemTitle", panelRt,
                "Already have a license key?",
                16, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(410, 26), pos: new Vector2(225, 85));

            expiredKeyField = BuildKeyField("ExpKeyField", panelRt, new Vector2(225, 25), width: 400f);

            expiredActivateButton = CreatePositionedButton("ExpActivateBtn", panelRt,
                "Activate License", new Vector2(260, 42), new Vector2(225, -40),
                COLOR_ACCENT, COLOR_ACCENT_HOVER, OnExpiredActivateClicked);
            expiredActivateButtonText = expiredActivateButton.GetComponentInChildren<TextMeshProUGUI>();

            // Scan QR button — captured so it can be hidden on unsupported headsets
            scanButtons.Add(CreatePositionedButton("ExpScanQR", panelRt,
                "Scan QR", new Vector2(260, 35), new Vector2(225, -95),
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER, OnScanQRClicked).gameObject);

            expiredStatusText = CreateTMPText("ExpStatusText", panelRt,
                "", 13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(410, 40), pos: new Vector2(225, -145));

            demoExpiredPanel.SetActive(false);
        }

        /// <summary>
        /// Left column of the "Demo Expired" panel: where to buy, as text and as a QR the
        /// user can scan with their phone without taking the headset off.
        /// </summary>
        private void BuildPurchaseCta(RectTransform panelRt, float columnX)
        {
            CreateTMPText("ExpBuyTitle", panelRt,
                "Get the full version",
                16, FontStyles.Bold, COLOR_GREEN, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(410, 26), pos: new Vector2(columnX, 85));

            string url = string.IsNullOrWhiteSpace(config.purchaseUrl)
                ? "vrinstructors.com"
                : config.purchaseUrl.Trim();

            Texture2D qr = TryCreateQRTexture(url);

            if (qr != null)
            {
                purchaseQrTexture = qr;

                // White quiet-zone backing so the code stays scannable on the dark panel.
                var frame = CreatePanel("ExpQrFrame", panelRt, Color.white);
                var frameRt = frame.GetComponent<RectTransform>();
                frameRt.anchorMin = new Vector2(0.5f, 0.5f);
                frameRt.anchorMax = new Vector2(0.5f, 0.5f);
                frameRt.sizeDelta = new Vector2(180, 180);
                frameRt.anchoredPosition = new Vector2(columnX, -20);

                var qrGo = new GameObject("ExpQrImage");
                qrGo.transform.SetParent(frameRt, false);
                var raw = qrGo.AddComponent<RawImage>();
                raw.texture = qr;
                raw.raycastTarget = false;
                var qrRt = raw.rectTransform;
                qrRt.anchorMin = Vector2.zero;
                qrRt.anchorMax = Vector2.one;
                qrRt.offsetMin = Vector2.zero;
                qrRt.offsetMax = Vector2.zero;
            }

            CreateTMPText("ExpBuyUrl", panelRt,
                url,
                17, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(410, 26), pos: new Vector2(columnX, -128));

            CreateTMPText("ExpBuyHint", panelRt,
                qr != null
                    ? "Scan with your phone, or visit the address above."
                    : "Visit the address above to purchase a license.",
                12, FontStyles.Italic, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(410, 36), pos: new Vector2(columnX, -158));
        }

        /// <summary>
        /// Renders <paramref name="payload"/> as a crisp, point-filtered QR texture.
        /// Returns null (and logs) if the payload cannot be encoded, so the CTA degrades
        /// to text instead of breaking the panel.
        /// </summary>
        private static Texture2D TryCreateQRTexture(string payload, int quietZone = 4)
        {
            try
            {
                bool[,] matrix = QRCodeEncoder.Encode(payload);
                int modules = matrix.GetLength(0);
                int size = modules + quietZone * 2;

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, // keep module edges hard when scaled up
                    wrapMode = TextureWrapMode.Clamp
                };

                var light = new Color32(255, 255, 255, 255);
                var dark = new Color32(0, 0, 0, 255);
                var pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    // Texture rows run bottom-up; matrix row 0 is the top of the symbol.
                    int row = modules - 1 - (y - quietZone);
                    for (int x = 0; x < size; x++)
                    {
                        int col = x - quietZone;
                        bool isDark = row >= 0 && row < modules
                                   && col >= 0 && col < modules
                                   && matrix[row, col];
                        pixels[y * size + x] = isDark ? dark : light;
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] Could not build purchase QR for '{payload}': {e.Message}");
                return null;
            }
        }

        // ─────────────────── LICENSE EXPIRED PANEL ───────────────────

        private void BuildLicenseExpiredPanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            licenseExpiredPanel = CreatePanel("LicenseExpiredPanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = licenseExpiredPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(650, 400);
            panelRt.anchoredPosition = Vector2.zero;

            // Header with amber/orange warning color
            var header = CreatePanel("LicExpHeader", panelRt, COLOR_PANEL_HEADER);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 60);
            headerRt.anchoredPosition = Vector2.zero;

            CreateTMPText("LicExpTitle", headerRt,
                "License Expired",
                20, FontStyles.Bold, new Color(1f, 0.65f, 0.15f, 1f), TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(20, 20, 0, 0));

            // Message
            CreateTMPText("LicExpMsg", panelRt,
                $"Your license for {config.appDisplayName} has expired.\nRenew on the web portal or enter a new key.",
                13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(550, 45), pos: new Vector2(0, -72));

            // Key fields
            licExpiredKeyField = BuildKeyField("LicExpKeyField", panelRt, new Vector2(0, 10));

            // Activate button
            licExpiredActivateButton = CreatePositionedButton("LicExpActivateBtn", panelRt,
                "Activate License", new Vector2(260, 42), new Vector2(0, -45),
                COLOR_ACCENT, COLOR_ACCENT_HOVER, OnLicExpiredActivateClicked);
            licExpiredActivateButtonText = licExpiredActivateButton.GetComponentInChildren<TextMeshProUGUI>();

            // Status
            licExpiredStatusText = CreateTMPText("LicExpStatusText", panelRt,
                "", 13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
                size: new Vector2(500, 25), pos: new Vector2(0, 55));

            // Scan QR button — captured so it can be hidden on unsupported headsets
            scanButtons.Add(CreatePositionedButton("LicExpScanQR", panelRt,
                "Scan QR", new Vector2(260, 35), new Vector2(0, 18),
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER, OnScanQRClicked,
                anchorAtBottom: true).gameObject);

            licenseExpiredPanel.SetActive(false);
        }

        // ─────────────────── SUCCESS PANEL ───────────────────

        private CanvasGroup successCanvasGroup;
        private RectTransform successContentRt;

        private void BuildSuccessPanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            // Main container panel with dark semi-transparent bg
            successPanel = CreatePanel("SuccessPanel", overlayRt, new Color(0.04f, 0.06f, 0.10f, 0.92f));
            var panelRt = successPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // CanvasGroup for fade animation
            successCanvasGroup = successPanel.AddComponent<CanvasGroup>();
            successCanvasGroup.alpha = 0f;

            // Content container (this is what scales up)
            var content = CreatePanel("SuccessContent", panelRt, Color.clear);
            successContentRt = content.GetComponent<RectTransform>();
            successContentRt.anchorMin = new Vector2(0.5f, 0.5f);
            successContentRt.anchorMax = new Vector2(0.5f, 0.5f);
            successContentRt.sizeDelta = new Vector2(500, 320);
            successContentRt.anchoredPosition = Vector2.zero;
            successContentRt.localScale = Vector3.one * 0.5f;

            // ── Green circle background for checkmark ──
            var circleBg = new GameObject("CheckCircle");
            circleBg.transform.SetParent(successContentRt, false);
            var circleRt = circleBg.AddComponent<RectTransform>();
            circleRt.anchorMin = new Vector2(0.5f, 1f);
            circleRt.anchorMax = new Vector2(0.5f, 1f);
            circleRt.pivot = new Vector2(0.5f, 1f);
            circleRt.sizeDelta = new Vector2(100, 100);
            circleRt.anchoredPosition = new Vector2(0, -20);

            var circleImg = circleBg.AddComponent<Image>();
            circleImg.color = COLOR_SUCCESS;
            circleImg.raycastTarget = false;
            // Note: Image is a square, but visually works well as a badge

            // ── Checkmark text (using Unicode ✓) ──
            CreateTMPText("CheckMark", circleRt,
                "\u2714",
                52, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(10, 10, 10, 10));

            // ── Title text ──
            successTitleText = CreateTMPText("SuccessTitle", successContentRt,
                "\u00a1Key redeemed successfully!",
                26, FontStyles.Bold, COLOR_SUCCESS, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(480, 40), pos: new Vector2(0, -135));

            // ── Subtitle text ──
            successSubtitleText = CreateTMPText("SuccessSubtitle", successContentRt,
                "Your license has been activated successfully.\nThe simulator will unlock automatically.",
                14, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(420, 50), pos: new Vector2(0, -182));

            // ── Decorative glow bar ──
            var glowBar = new GameObject("GlowBar");
            glowBar.transform.SetParent(successContentRt, false);
            var glowRt = glowBar.AddComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0.5f, 1f);
            glowRt.anchorMax = new Vector2(0.5f, 1f);
            glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = new Vector2(200, 3);
            glowRt.anchoredPosition = new Vector2(0, -240);
            var glowImg = glowBar.AddComponent<Image>();
            glowImg.color = new Color(COLOR_SUCCESS.r, COLOR_SUCCESS.g, COLOR_SUCCESS.b, 0.4f);
            glowImg.raycastTarget = false;

            successPanel.SetActive(false);
        }

        // ─────────────────── Button Handlers ───────────────────

        private void OnStartDemoClicked()
        {
            manager.StartDemoMode();
        }

        private void OnEnterKeyClicked()
        {
            ShowKeyInput();
        }

        private void OnScanQRClicked()
        {
            if (!qrSupported) return;

            // A second press while scanning cancels.
            if (qrScanner != null && qrScanner.IsScanning)
            {
                qrScanner.StopScan();
                SetScanStatus("Scan cancelled.", COLOR_TEXT_DIM);
                return;
            }

            if (qrScanner == null)
                qrScanner = gameObject.AddComponent<LicenseQRScanner>();

            // The in-panel viewfinder (SyncViewfinder) gives the user the camera feed to
            // aim with — no passthrough involved.
            SetScanStatus("Starting camera...", COLOR_TEXT_DIM);

            qrScanner.StartScan(30f,
                onDecoded: OnQRDecoded,
                onStatus: msg => SetScanStatus(msg, COLOR_TEXT_DIM),
                onUnsupported: msg =>
                {
                    // Surface the exact reason in logcat — the UI status is easy to miss
                    // and this is the only trace of WHY scanning got disabled.
                    Debug.LogWarning("[VR Licensing] QR scan unsupported: " + msg);
                    qrSupported = false;
                    HideScanButtons(); // this headset can't scan — remove the option entirely
                    SetScanStatus(msg + " Enter your key manually.", COLOR_ERROR);
                });
        }

        private void OnQRDecoded(string key)
        {
            SetScanStatus("QR detected — validating...", COLOR_SUCCESS);

            // Mirror the key into the visible fields (if a key-entry panel is open) for feedback.
            if (keyInputPanel.activeSelf) FillKeyField(keyField, key);
            else if (demoExpiredPanel.activeSelf) FillKeyField(expiredKeyField, key);
            else if (licenseExpiredPanel.activeSelf) FillKeyField(licExpiredKeyField, key);

            SubmitKey(key);
        }

        private void HideScanButtons()
        {
            foreach (var b in scanButtons)
                if (b != null) b.SetActive(false);
        }

        private void FillKeyField(TMP_InputField field, string key)
        {
            if (field != null && !string.IsNullOrEmpty(key))
                field.text = NormalizeKey(key);
        }

        private void SetScanStatus(string msg, Color color)
        {
            if (keyInputPanel.activeSelf) { statusText.text = msg; statusText.color = color; }
            else if (demoExpiredPanel.activeSelf) { expiredStatusText.text = msg; expiredStatusText.color = color; }
            else if (licenseExpiredPanel.activeSelf) { licExpiredStatusText.text = msg; licExpiredStatusText.color = color; }
            else if (welcomePanel.activeSelf && welcomeInfoText != null) { welcomeInfoText.text = msg; }
        }

        private void OnBackClicked()
        {
            ShowWelcome();
        }

        private void OnActivateClicked()
        {
            string key = GetKey(keyField);
            SubmitKey(key);
        }

        private void OnExpiredActivateClicked()
        {
            string key = GetKey(expiredKeyField);
            SubmitKey(key);
        }

        private void OnLicExpiredActivateClicked()
        {
            string key = GetKey(licExpiredKeyField);
            SubmitKey(key);
        }

        private void SubmitKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Replace("-", "").Length < 16)
            {
                ShowError("Enter the full key (XXXX-XXXX-XXXX-XXXX)");
                return;
            }

            SetLoading(true);
            statusText.text = "";
            expiredStatusText.text = "";
            licExpiredStatusText.text = "";

            manager.SubmitLicenseKey(key, (success, error) =>
            {
                SetLoading(false);
                if (success)
                {
                    ShowSuccess();
                    ShowLicensed();
                }
                else
                {
                    ShowError(error ?? "Error validating the license.");
                }
            });
        }

        // ─────────────────── LazyFollow ───────────────────

        private void AddLazyFollow(GameObject target)
        {
            var lazyFollowType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.BodyUI.LazyFollow, Unity.XR.Interaction.Toolkit");

            if (lazyFollowType != null)
            {
                lazyFollowInstance = target.AddComponent(lazyFollowType);

                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public;

                // Position Follow Mode = Follow (1)
                SetNestedXRI3Field(lazyFollowType, lazyFollowInstance, "m_PositionFollowParams", "m_PositionFollowMode", "positionFollowMode", 1, flags);

                // Rotation Follow Mode = LookAtWithWorldUp / LookAt (2)
                SetNestedXRI3Field(lazyFollowType, lazyFollowInstance, "m_RotationFollowParams", "m_RotationFollowMode", "rotationFollowMode", 2, flags);

                // Movement Speed
                SetNestedXRI3Field(lazyFollowType, lazyFollowInstance, "m_GeneralFollowParams", "m_MovementSpeed", "speed", 5f, flags);

                // Target Offset — Z = distance in front of camera
                SetNestedXRI3Field(lazyFollowType, lazyFollowInstance, "m_TargetConfig", "m_TargetOffset", "targetOffset",
                    new Vector3(0f, 0f, CANVAS_DISTANCE), flags);

                // Snap On Enable
                SetNestedXRI3Field(lazyFollowType, lazyFollowInstance, "m_GeneralFollowParams", "m_SnapOnEnable", "snapOnEnable", true, flags);

                Debug.Log($"[VR Licensing] LazyFollow added (targetOffset.z = {CANVAS_DISTANCE}).");
            }
            else
            {
                Debug.Log("[VR Licensing] LazyFollow not available, using manual positioning.");
            }
        }

        private void SetNestedXRI3Field(Type type, object instance, string groupField, string fieldName, string propName, object value, System.Reflection.BindingFlags flags)
        {
            // First, try to see if it's hidden inside an XRI 3.x params struct
            var group = type.GetField(groupField, flags);
            if (group != null)
            {
                var groupVal = group.GetValue(instance);
                if (groupVal != null)
                {
                    SetFieldOrProperty(groupVal.GetType(), groupVal, fieldName, propName, value, flags);
                    // Since it's a struct, we must set it back onto the component
                    if (group.FieldType.IsValueType)
                    {
                        group.SetValue(instance, groupVal);
                    }
                    return;
                }
            }

            // Fallback: XRI 2.x standard fields/properties
            SetFieldOrProperty(type, instance, fieldName, propName, value, flags);
        }

        /// <summary>
        /// Tries serialized field first (m_ prefix), then public property.
        /// Handles enum conversion for mode fields.
        /// </summary>
        private void SetFieldOrProperty(Type type, object instance, string fieldName, string propName, object value, System.Reflection.BindingFlags flags)
        {
            // Try field first
            var field = type.GetField(fieldName, flags);
            if (field != null)
            {
                if (field.FieldType.IsEnum)
                    field.SetValue(instance, Enum.ToObject(field.FieldType, value));
                else
                    field.SetValue(instance, value);
                return;
            }

            // Fallback to property
            var prop = type.GetProperty(propName, flags);
            if (prop != null && prop.CanWrite)
            {
                if (prop.PropertyType.IsEnum)
                    prop.SetValue(instance, Enum.ToObject(prop.PropertyType, value));
                else
                    prop.SetValue(instance, value);
            }
        }

        // ─────────────────── Positioning ───────────────────

        private void EnsurePositioned()
        {
            if (isPositioned) return;

            // Try synchronous positioning first
            var cam = Camera.main;
            if (cam != null)
            {
                SetupCameraTarget(cam);
                isPositioned = true;
            }
            else
            {
                StartCoroutine(PositionWhenCameraReady());
            }
        }

        private IEnumerator PositionWhenCameraReady()
        {
            while (Camera.main == null)
                yield return null;

            SetupCameraTarget(Camera.main);
            isPositioned = true;
        }

        private void SetupCameraTarget(Camera cam)
        {
            if (lazyFollowInstance != null)
            {
                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public;

                // In XRI 3.x, target is often in m_TargetConfig struct
                var lazyFollowType = lazyFollowInstance.GetType();
                var groupField = lazyFollowType.GetField("m_TargetConfig", flags);
                
                bool targetSet = false;
                if (groupField != null)
                {
                    var groupVal = groupField.GetValue(lazyFollowInstance);
                    if (groupVal != null)
                    {
                        var targetField = groupVal.GetType().GetField("m_Target", flags) ?? groupVal.GetType().GetField("target", flags);
                        if (targetField != null)
                        {
                            targetField.SetValue(groupVal, cam.transform);
                            if (groupField.FieldType.IsValueType)
                                groupField.SetValue(lazyFollowInstance, groupVal); // set struct back
                            targetSet = true;
                        }
                    }
                }
                
                if (!targetSet)
                {
                    // Fallback to top-level XRI 2.x
                    var targetField = lazyFollowType.GetField("m_Target", flags);
                    if (targetField != null)
                        targetField.SetValue(lazyFollowInstance, cam.transform);
                    else
                    {
                        var targetProp = lazyFollowType.GetProperty("target", flags);
                        if (targetProp != null)
                            targetProp.SetValue(lazyFollowInstance, cam.transform);
                    }
                }

                // Initial position — use eye-level height (1.5m minimum if camera is at origin)
                var ct = canvas.transform;
                var camPos = cam.transform.position;
                if (camPos.y < 0.5f) camPos.y = 1.5f; // VR eye-level fallback
                ct.position = camPos + cam.transform.forward * CANVAS_DISTANCE;
                ct.rotation = Quaternion.LookRotation(ct.position - camPos, Vector3.up);
            }
            else
            {
                // Manual positioning fallback — use eye-level height
                var ct = canvas.transform;
                var camPos = cam.transform.position;
                if (camPos.y < 0.5f) camPos.y = 1.5f; // VR eye-level fallback
                ct.position = camPos + cam.transform.forward * CANVAS_DISTANCE;
                ct.rotation = Quaternion.LookRotation(ct.position - camPos, Vector3.up);
            }
        }

        // ─────────────────── Key Field Builders ───────────────────

        // A single input field for the whole key (XXXX-XXXX-XXXX-XXXX). One field paired
        // with one VR keyboard makes typing/backspace/editing work naturally — the 4-field
        // layout fought the shared global keyboard (desync, stuck backspace, carry-over).
        private TMP_InputField BuildKeyField(string name, RectTransform parent, Vector2 position,
            float width = 520f)
        {
            var field = CreateKeyInputField(name, parent, width,
                charLimit: 19, placeholderText: "XXXX-XXXX-XXXX-XXXX", allowDashes: true);

            var rt = field.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, 50);
            rt.anchoredPosition = position;
            return field;
        }

        private TMP_InputField CreateKeyInputField(string name, RectTransform parent, float width,
            int charLimit = 4, string placeholderText = "XXXX", bool allowDashes = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 45);

            var bgImg = go.AddComponent<Image>();
            bgImg.color = COLOR_INPUT_BG;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = COLOR_INPUT_BORDER;
            outline.effectDistance = new Vector2(1, -1);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 1;
            le.preferredHeight = 45;

            // Text area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(8, 4);
            textAreaRt.offsetMax = new Vector2(-8, -4);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholderText;
            ph.fontSize = 20;
            ph.color = new Color(0.35f, 0.35f, 0.42f, 0.5f);
            ph.alignment = TextAlignmentOptions.Center;
            ph.enableWordWrapping = false;
            SetRectFill(ph.rectTransform);

            // Input text
            var itGo = new GameObject("Text");
            itGo.transform.SetParent(textArea.transform, false);
            var it = itGo.AddComponent<TextMeshProUGUI>();
            it.fontSize = 20;
            it.fontStyle = FontStyles.Bold;
            it.color = COLOR_TEXT;
            it.alignment = TextAlignmentOptions.Center;
            it.enableWordWrapping = false;
            SetRectFill(it.rectTransform);

            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = it;
            inputField.placeholder = ph;
            inputField.characterLimit = charLimit;
            if (allowDashes)
            {
                // Single full-key field: allow the dash separators, free caret editing.
                inputField.contentType = TMP_InputField.ContentType.Standard;
                inputField.characterValidation = TMP_InputField.CharacterValidation.None;
                inputField.onFocusSelectAll = false;
            }
            else
            {
                inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
                inputField.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
                inputField.onFocusSelectAll = true;
            }
            inputField.caretColor = COLOR_ACCENT;
            inputField.selectionColor = new Color(COLOR_ACCENT.r, COLOR_ACCENT.g, COLOR_ACCENT.b, 0.3f);

            // NOTE: do NOT force-uppercase inside onValueChanged — setting inputField.text
            // there fights the XR keyboard's two-way binding (field→UPPER→keyboard→lower→…)
            // and causes an infinite recursion / StackOverflow. The key is uppercased at
            // submit time via NormalizeKey instead.

            // Attach XR Keyboard Display for VR keyboard integration
            AttachXRKeyboardDisplay(inputField);

            return inputField;
        }

        // ─────────────────── Utility Builders ───────────────────

        private string GetKey(TMP_InputField field)
        {
            return field == null ? "" : NormalizeKey(field.text);
        }

        /// <summary>
        /// Turns free-form input ("sboxtestchn10001", "SBOX-TEST-CHN1-0001", spaced, etc.)
        /// into the canonical uppercase XXXX-XXXX-XXXX-XXXX form.
        /// </summary>
        private static string NormalizeKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            string alnum = "";
            foreach (char c in raw.ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) alnum += c;

            string result = "";
            for (int i = 0; i < alnum.Length; i++)
            {
                if (i > 0 && i % 4 == 0) result += "-";
                result += alnum[i];
            }
            return result;
        }

        private void ClearKeyField(TMP_InputField field)
        {
            if (field != null) field.text = "";
        }

        private GameObject CreatePanel(string name, RectTransform parent, Color color,
            bool stretch = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            if (stretch)
            {
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (color.a > 0)
            {
                var img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = (name == "Overlay");
            }

            return go;
        }

        private TextMeshProUGUI CreateTMPText(string name, RectTransform parent,
            string text, float fontSize, FontStyles style, Color color,
            TextAlignmentOptions alignment,
            bool stretch = false, Vector4 padding = default,
            Vector2 anchor = default, Vector2 pivot = default,
            Vector2 size = default, Vector2 pos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            var rt = tmp.rectTransform;

            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(padding.x, padding.w);
                rt.offsetMax = new Vector2(-padding.y, -padding.z);
            }
            else if (size != default)
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;
            }

            return tmp;
        }

        private Button CreatePositionedButton(string name, RectTransform parent,
            string label, Vector2 size, Vector2 pos,
            Color normal, Color hover, UnityEngine.Events.UnityAction onClick,
            bool anchorAtBottom = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            if (anchorAtBottom)
            {
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
            }
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = go.AddComponent<Image>();
            img.color = normal;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var colors = btn.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.pressedColor = new Color(hover.r * 0.8f, hover.g * 0.8f, hover.b * 0.8f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var labelTMP = CreateTMPText("Label", rt,
                label, 14, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(10, 10, 5, 5));
            labelTMP.raycastTarget = false;

            return btn;
        }

        private GameObject CreateLayoutButton(string name, RectTransform parent,
            string label, float height,
            Color normal, Color hover, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var img = go.AddComponent<Image>();
            img.color = normal;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var colors = btn.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.pressedColor = new Color(hover.r * 0.8f, hover.g * 0.8f, hover.b * 0.8f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var labelTMP = CreateTMPText("Label", go.GetComponent<RectTransform>(),
                label, 15, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(15, 15, 5, 5));
            labelTMP.raycastTarget = false;

            return go;
        }

        private void SetRectFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ─────────────────── XR Keyboard Integration ───────────────────

        /// <summary>
        /// Attaches an XRKeyboardDisplay component to the input field's GameObject via reflection.
        /// Requires the XRI Spatial Keyboard sample to be imported in the consuming project.
        /// Degrades gracefully if the sample is not present.
        /// </summary>
        private void AttachXRKeyboardDisplay(TMP_InputField field)
        {
            if (field == null) return;

            // Cache the type lookup
            if (xrKeyboardDisplayType == null)
            {
                xrKeyboardDisplayType = Type.GetType(
                    "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.XRKeyboardDisplay, " +
                    "Unity.XR.Interaction.Toolkit.Samples.SpatialKeyboard");

                if (xrKeyboardDisplayType == null)
                {
                    Debug.LogWarning("[VR Licensing] XRKeyboardDisplay not found. " +
                        "Import the 'Spatial Keyboard' sample from XR Interaction Toolkit " +
                        "in Package Manager to enable VR keyboard support.");
                    return;
                }

                Debug.Log("[VR Licensing] XRKeyboardDisplay type resolved successfully.");
            }

            // If type was previously not found, skip
            if (xrKeyboardDisplayType == null)
                return;

            var go = field.gameObject;
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public;

            // Add the component
            var display = go.AddComponent(xrKeyboardDisplayType);

            // Set inputField via the public property (its setter configures
            // resetOnDeActivation=false and shouldHideSoftKeyboard=true)
            var inputFieldProp = xrKeyboardDisplayType.GetProperty("inputField", flags);
            if (inputFieldProp != null && inputFieldProp.CanWrite)
                inputFieldProp.SetValue(display, field);

            // useSceneKeyboard = false → use GlobalNonNativeKeyboard
            var useSceneKbField = xrKeyboardDisplayType.GetField("m_UseSceneKeyboard", flags);
            if (useSceneKbField != null)
                useSceneKbField.SetValue(display, false);

            // updateOnKeyPress = true
            var updateField = xrKeyboardDisplayType.GetField("m_UpdateOnKeyPress", flags);
            if (updateField != null)
                updateField.SetValue(display, true);

            // monitorInputFieldCharacterLimit = true (respect characterLimit=4)
            var monitorField = xrKeyboardDisplayType.GetField("m_MonitorInputFieldCharacterLimit", flags);
            if (monitorField != null)
                monitorField.SetValue(display, true);

            // hideKeyboardOnDisable = false (avoid closing keyboard when navigating between fields)
            var hideField = xrKeyboardDisplayType.GetField("m_HideKeyboardOnDisable", flags);
            if (hideField != null)
                hideField.SetValue(display, false);

        }

        private IEnumerator ShowSuccessAndHide(string title, string subtitle, float holdSeconds)
        {
            successAnimating = true;

            // The success panel lives inside the overlay — make sure it's visible
            // (in the cached-license path the overlay was never shown).
            overlayPanel.SetActive(true);
            welcomePanel.SetActive(false);
            keyInputPanel.SetActive(false);
            demoExpiredPanel.SetActive(false);
            licenseExpiredPanel.SetActive(false);
            successPanel.SetActive(true);

            if (successTitleText != null) successTitleText.text = title;
            if (successSubtitleText != null) successSubtitleText.text = subtitle;

            // Reset animation state
            successCanvasGroup.alpha = 0f;
            successContentRt.localScale = Vector3.one * 0.5f;

            // ── Animate IN: scale up + fade in (0.45s) ──
            float animDuration = 0.45f;
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                // Ease-out back curve for a satisfying "pop"
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                float scaleT = 1f + 0.05f * Mathf.Sin(t * Mathf.PI); // slight overshoot

                successCanvasGroup.alpha = easeT;
                successContentRt.localScale = Vector3.one * (0.5f + 0.5f * easeT) * scaleT;
                yield return null;
            }
            successCanvasGroup.alpha = 1f;
            successContentRt.localScale = Vector3.one;

            // ── Hold visible ──
            yield return new WaitForSeconds(holdSeconds);

            // ── Animate OUT: fade out (0.4s) ──
            float fadeOutDuration = 0.4f;
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                successCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            successPanel.SetActive(false);
            overlayPanel.SetActive(false);
            successAnimating = false;
        }

        // ─────────────────── DEMO TIMER HUD ───────────────────

        /// <summary>
        /// Builds the demo countdown as its OWN world-space canvas (separate from the
        /// license modal) so it can be head-locked to the bottom-right of the user's view
        /// and stay readable while they move and look around during the demo.
        /// </summary>
        private void BuildDemoTimer()
        {
            var canvasGo = new GameObject("DemoTimerCanvas");
            canvasGo.transform.SetParent(transform);

            demoTimerCanvas = canvasGo.AddComponent<Canvas>();
            demoTimerCanvas.renderMode = RenderMode.WorldSpace;
            demoTimerCanvas.sortingOrder = 101; // above the license canvas

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;

            var crt = canvasGo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(340, 54);
            canvasGo.transform.localScale = Vector3.one * HUD_SCALE;

            // Background bar (fills the canvas)
            demoTimerPanel = CreatePanel("DemoTimerHUD", crt, new Color(0.06f, 0.07f, 0.10f, 0.85f), stretch: true);
            var panelRt = demoTimerPanel.GetComponent<RectTransform>();

            // Accent underline
            var bar = CreatePanel("DemoTimerBar", panelRt, COLOR_GREEN);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.sizeDelta = new Vector2(0, 4);
            barRt.anchoredPosition = Vector2.zero;

            demoTimerText = CreateTMPText("DemoTimerText", panelRt,
                "Demo", 18, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center,
                stretch: true, padding: new Vector4(12, 12, 8, 12));

            canvasGo.SetActive(false);
        }

        /// <summary>Shows the head-locked demo countdown HUD (called when the demo starts).</summary>
        public void ShowDemoTimer()
        {
            if (demoTimerCanvas == null) return;
            demoTimerCanvas.gameObject.SetActive(true);

            // Snap into place immediately so it doesn't flash at the world origin.
            var cam = ResolveHudCam();
            if (cam != null) PositionDemoHud(cam, instant: true);
        }

        /// <summary>Hides the demo countdown HUD.</summary>
        public void HideDemoTimer()
        {
            if (demoTimerCanvas != null) demoTimerCanvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            var cam = ResolveHudCam();

            SyncPassthrough(cam);
            SyncModality();
            SyncViewfinder();

            // Keep the tracking origin honest while the AR session exists (it lives for
            // the whole app run), and emit a periodic breadcrumb of rig/camera heights so
            // any future "player is falling" report can be diagnosed straight from logcat.
            if (--originGuardIn <= 0)
            {
                originGuardIn = 30;
                LicensePassthrough.GuardFloorOrigin();

                if (--diagnosticsIn <= 0)
                {
                    diagnosticsIn = 4; // every ~4 guard ticks ≈ 1.7s at 72 fps
                    if (cam != null)
                    {
                        Transform root = cam.transform.root;
                        Debug.Log($"[VR Licensing] diag: rigY={root.position.y:F2} " +
                                  $"camY={cam.transform.position.y:F2} camLocalY={cam.transform.localPosition.y:F2} " +
                                  $"passthrough={passthroughActive} modal={(overlayPanel != null && overlayPanel.activeSelf)}");
                    }
                }
            }

            // Main license modal: keep it in front of the player while visible so it
            // can't be turned away from and ignored.
            if (cam != null && overlayPanel != null && overlayPanel.activeSelf)
                FollowMainModal(cam);

            // Demo timer HUD (only while the demo runs).
            if (demoTimerCanvas == null || !demoTimerCanvas.gameObject.activeSelf) return;
            if (cam != null) PositionDemoHud(cam, instant: false);

            if (demoManagerRef == null && manager != null)
                demoManagerRef = manager.GetComponent<DemoModeManager>();
            if (demoManagerRef == null || demoTimerText == null) return;

            float remaining = Mathf.Max(0f, demoManagerRef.RemainingDemoSeconds);
            demoTimerText.text = "Demo  ·  " + FormatTime(remaining) + " left";
            demoTimerText.color = remaining <= 60f ? COLOR_ERROR : COLOR_TEXT;
        }

        /// <summary>
        /// Lazy "dead-zone" follow for the main license canvas: it stays put while the user
        /// looks at it (so they can aim at the buttons), but if they turn away past
        /// MODAL_RECENTER_ENTER_ANGLE (or walk too far), it eases back to sit in front of
        /// them again — so the licensing gate can't simply be ignored.
        /// </summary>
        private void FollowMainModal(Camera cam)
        {
            if (canvas == null) return;

            Vector3 camPos = cam.transform.position;
            if (camPos.y < 0.5f) camPos.y = 1.5f; // eye-level fallback (Editor without HMD)

            Vector3 desiredPos = camPos + cam.transform.forward * CANVAS_DISTANCE;
            Quaternion desiredRot = Quaternion.LookRotation(desiredPos - camPos, Vector3.up);

            Vector3 toCanvas = canvas.transform.position - camPos;
            float angle = Vector3.Angle(cam.transform.forward, toCanvas);

            if (angle > MODAL_RECENTER_ENTER_ANGLE || toCanvas.magnitude > MODAL_MAX_DISTANCE)
                recenteringModal = true;

            if (recenteringModal)
            {
                float k = 1f - Mathf.Exp(-MODAL_FOLLOW_SPEED * Time.unscaledDeltaTime);
                canvas.transform.position = Vector3.Lerp(canvas.transform.position, desiredPos, k);
                canvas.transform.rotation = Quaternion.Slerp(canvas.transform.rotation, desiredRot, k);
                if (angle < MODAL_RECENTER_EXIT_ANGLE)
                    recenteringModal = false;
            }
        }

        private Camera ResolveHudCam()
        {
            // Camera.main can change on scene load; re-fetch when the cached one dies.
            if (hudCam == null) hudCam = Camera.main;
            return hudCam;
        }

        /// <summary>
        /// Pins the HUD to the bottom-right of the user's view. Follows head position and
        /// rotation (head-locked) with light smoothing so it stays in the field of view.
        /// </summary>
        private void PositionDemoHud(Camera cam, bool instant)
        {
            Vector3 offset = new Vector3(DEMO_HUD_RIGHT, DEMO_HUD_DOWN, DEMO_HUD_FORWARD);
            Vector3 targetPos = cam.transform.position + cam.transform.rotation * offset;
            Quaternion targetRot = Quaternion.LookRotation(targetPos - cam.transform.position, cam.transform.up);

            var t = demoTimerCanvas.transform;
            if (instant)
            {
                t.SetPositionAndRotation(targetPos, targetRot);
            }
            else
            {
                float k = 1f - Mathf.Exp(-DEMO_HUD_FOLLOW_SPEED * Time.unscaledDeltaTime);
                t.position = Vector3.Lerp(t.position, targetPos, k);
                t.rotation = Quaternion.Slerp(t.rotation, targetRot, k);
            }
        }

        private void OnDestroy()
        {
            if (passthroughActive)
            {
                passthrough.Disable();
                passthroughActive = false;
            }

            // Never leave the game's raycasters disabled behind us.
            foreach (var rc in suppressedRaycasters)
            {
                if (rc != null) rc.enabled = true;
            }
            suppressedRaycasters.Clear();

            if (purchaseQrTexture != null)
            {
                Destroy(purchaseQrTexture);
                purchaseQrTexture = null;
            }
        }

        /// <summary>
        /// Keeps passthrough in lockstep with modal visibility: the real room fades in
        /// whenever the license UI is open (so the user can read a key off their phone,
        /// WhatsApp-linking style) and the game scene comes back the moment it closes.
        /// Driven from Update so every Show/Hide path is covered by a single mechanism.
        /// </summary>
        private void SyncPassthrough(Camera cam)
        {
            bool modalOpen = overlayPanel != null && overlayPanel.activeSelf;
            bool wantBackground = config != null && config.usePassthroughBackground;
            bool scanning = qrScanner != null && qrScanner.IsScanning;
            // Scan mode always shows the real room (that's how the user finds the QR);
            // outside scanning, passthrough is the opt-in background.
            bool want = cam != null && modalOpen && (wantBackground || scanning);

            // The XRI spatial keyboard spawns lazily OUTSIDE the rig hierarchy the first
            // time a field is focused, so it must be pulled onto the render layer while
            // the camera is culling everything else — otherwise the user types blind.
            if (passthroughActive)
            {
                GameObject kb = FindKeyboardRoot();
                if (kb != null) passthrough.BorrowLateHierarchy(kb);
            }

            if (want == passthroughActive) return;

            if (want)
            {
                passthrough.Enable(cam,
                    canvas != null ? canvas.gameObject : null,
                    demoTimerCanvas != null ? demoTimerCanvas.gameObject : null,
                    FindKeyboardRoot());
                // Drop the dark backdrop: the room itself is the background now. The image
                // stays enabled so it keeps blocking clicks on whatever is behind the modal.
                if (overlayImage != null) overlayImage.color = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                passthrough.Disable();
                if (overlayImage != null) overlayImage.color = COLOR_BG_OVERLAY;
            }

            passthroughActive = want;
        }

        // ─────────────────── Gate Modality ───────────────────
        // While the license modal is open, every raycaster that doesn't belong to the
        // license UI or the spatial keyboard is disabled, so the game's own menus can't
        // be clicked "through" the gate (with passthrough hiding them, a user could
        // otherwise press their buttons blind — and did).

        private readonly System.Collections.Generic.List<BaseRaycaster> suppressedRaycasters =
            new System.Collections.Generic.List<BaseRaycaster>();
        private bool modalityActive;
        private int modalityRescanIn;
        private int originGuardIn;
        private int diagnosticsIn;


        private void SyncModality()
        {
            bool modalOpen = overlayPanel != null && overlayPanel.activeSelf;

            if (modalOpen)
            {
                // Rescan periodically: scene loads and late-spawned canvases can bring
                // new raycasters into play while the gate is up.
                if (!modalityActive || --modalityRescanIn <= 0)
                {
                    SuppressForeignRaycasters();
                    modalityRescanIn = 60;
                }
                modalityActive = true;
            }
            else if (modalityActive)
            {
                foreach (var rc in suppressedRaycasters)
                {
                    if (rc != null) rc.enabled = true;
                }
                suppressedRaycasters.Clear();
                modalityActive = false;
            }
        }

        private void SuppressForeignRaycasters()
        {
            GameObject keyboardRoot = FindKeyboardRoot();

            foreach (var rc in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))
            {
                if (!rc.enabled) continue;
                if (rc.GetComponentInParent<LicenseUIBuilder>() != null) continue; // ours
                if (keyboardRoot != null && rc.transform.root.gameObject == keyboardRoot) continue;

                rc.enabled = false;
                suppressedRaycasters.Add(rc);
            }
        }

        private Type globalKeyboardType;
        private System.Reflection.PropertyInfo globalKeyboardInstanceProp;

        /// <summary>
        /// Root of the global XRI spatial keyboard, or null while it hasn't spawned yet
        /// (it is created lazily the first time an input field gains focus). Resolved via
        /// reflection so the package has no hard dependency on the XRI sample.
        /// </summary>
        private GameObject FindKeyboardRoot()
        {
            if (globalKeyboardType == null)
            {
                globalKeyboardType = Type.GetType(
                    "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.GlobalNonNativeKeyboard, " +
                    "Unity.XR.Interaction.Toolkit.Samples.SpatialKeyboard");
                if (globalKeyboardType == null) return null; // sample not imported

                globalKeyboardInstanceProp = globalKeyboardType.GetProperty("instance",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            }

            var instance = globalKeyboardInstanceProp?.GetValue(null) as MonoBehaviour;
            return instance != null ? instance.transform.root.gameObject : null;
        }

        /// <summary>
        /// Human-readable trial length. Demos are commonly configured well under an hour
        /// (10-15 min), so formatting everything as hours would render "0h".
        /// </summary>
        private static string FormatDuration(float seconds)
        {
            if (seconds < 3600f)
                return $"{Mathf.CeilToInt(seconds / 60f)} min";

            float hours = seconds / 3600f;
            return Mathf.Approximately(hours, Mathf.Round(hours))
                ? $"{Mathf.RoundToInt(hours)} h"
                : $"{hours:F1} h";
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        }
    }
}
