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
        private const float CANVAS_SCALE = 0.001f;
        private const float CANVAS_DISTANCE = 2.0f;
        private const int CANVAS_PX_WIDTH = 800;
        private const int CANVAS_PX_HEIGHT = 500;

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
        private TMP_InputField[] keyFields;
        private Button activateButton;
        private TextMeshProUGUI activateButtonText;
        private TextMeshProUGUI statusText;

        // Demo Expired Panel
        private GameObject demoExpiredPanel;
        private TMP_InputField[] expiredKeyFields;
        private Button expiredActivateButton;
        private TextMeshProUGUI expiredActivateButtonText;
        private TextMeshProUGUI expiredStatusText;

        // License Expired Panel
        private GameObject licenseExpiredPanel;
        private TMP_InputField[] licExpiredKeyFields;
        private Button licExpiredActivateButton;
        private TextMeshProUGUI licExpiredActivateButtonText;
        private TextMeshProUGUI licExpiredStatusText;

        // Success Panel
        private GameObject successPanel;

        private LicenseConfig config;
        private LicenseManager manager;
        private bool isPositioned;
        private Component lazyFollowInstance;

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

            float hours = config.demoDurationSeconds / 3600f;
            float used = 0f;
            if (manager != null)
            {
                var dm = manager.GetComponent<DemoModeManager>();
                if (dm != null) used = dm.TotalDemoUsedSeconds;
            }
            float remainingH = Mathf.Max(0, (config.demoDurationSeconds - used) / 3600f);
            welcomeInfoText.text = $"Free demo: {remainingH:F1}h remaining of {hours:F0}h";
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
            ClearKeyFields(keyFields);
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
            ClearKeyFields(expiredKeyFields);
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
            ClearKeyFields(licExpiredKeyFields);
        }

        /// <summary>Hides all UI (license valid or demo running).</summary>
        public void HideAll()
        {
            overlayPanel.SetActive(false);
        }

        /// <summary>Shows success feedback then hides everything.</summary>
        public void ShowLicensed()
        {
            StartCoroutine(ShowSuccessAndHide());
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

            // Start hidden
            overlayPanel.SetActive(false);
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("LicenseCanvas");
            canvasGo.transform.SetParent(transform);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30000;

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

            // Button 3: Scan QR Passthrough (secondary)
            CreateLayoutButton("ScanQRBtn", btnContRt,
                "Scan QR (Passthrough)", 45,
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER,
                OnScanQRClicked);

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
            keyFields = BuildKeyFieldRow("KeyFields", panelRt, new Vector2(0, 15));

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

            demoExpiredPanel = CreatePanel("DemoExpiredPanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = demoExpiredPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(650, 400);
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
                $"Your trial period for {config.appDisplayName} has ended.\nEnter a license key to continue.",
                13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(550, 45), pos: new Vector2(0, -72));

            // Key fields
            expiredKeyFields = BuildKeyFieldRow("ExpKeyFields", panelRt, new Vector2(0, 10));

            // Activate button
            expiredActivateButton = CreatePositionedButton("ExpActivateBtn", panelRt,
                "Activate License", new Vector2(260, 42), new Vector2(0, -45),
                COLOR_ACCENT, COLOR_ACCENT_HOVER, OnExpiredActivateClicked);
            expiredActivateButtonText = expiredActivateButton.GetComponentInChildren<TextMeshProUGUI>();

            // Status
            expiredStatusText = CreateTMPText("ExpStatusText", panelRt,
                "", 13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
                size: new Vector2(500, 25), pos: new Vector2(0, 55));

            // Scan QR button
            CreatePositionedButton("ExpScanQR", panelRt,
                "Scan QR (Passthrough)", new Vector2(260, 35), new Vector2(0, 18),
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER, OnScanQRClicked,
                anchorAtBottom: true);

            demoExpiredPanel.SetActive(false);
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
            licExpiredKeyFields = BuildKeyFieldRow("LicExpKeyFields", panelRt, new Vector2(0, 10));

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

            // Scan QR button
            CreatePositionedButton("LicExpScanQR", panelRt,
                "Scan QR (Passthrough)", new Vector2(260, 35), new Vector2(0, 18),
                COLOR_SECONDARY, COLOR_SECONDARY_HOVER, OnScanQRClicked,
                anchorAtBottom: true);

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
            CreateTMPText("SuccessTitle", successContentRt,
                "\u00a1Key redeemed successfully!",
                26, FontStyles.Bold, COLOR_SUCCESS, TextAlignmentOptions.Center,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                size: new Vector2(480, 40), pos: new Vector2(0, -135));

            // ── Subtitle text ──
            CreateTMPText("SuccessSubtitle", successContentRt,
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
            TogglePassthrough();
        }

        private void OnBackClicked()
        {
            ShowWelcome();
        }

        private void OnActivateClicked()
        {
            string key = GetKeyFromFields(keyFields);
            SubmitKey(key);
        }

        private void OnExpiredActivateClicked()
        {
            string key = GetKeyFromFields(expiredKeyFields);
            SubmitKey(key);
        }

        private void OnLicExpiredActivateClicked()
        {
            string key = GetKeyFromFields(licExpiredKeyFields);
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

        // ─────────────────── Passthrough ───────────────────

        /// <summary>
        /// Toggles Meta Quest passthrough via reflection (no hard dependency).
        /// </summary>
        private void TogglePassthrough()
        {
            // Try OVRManager (Meta XR SDK)
            var ovrManagerType = Type.GetType("OVRManager, Oculus.VR");
            if (ovrManagerType != null)
            {
                var instanceProp = ovrManagerType.GetProperty("instance",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (instanceProp != null)
                {
                    var instance = instanceProp.GetValue(null);
                    if (instance != null)
                    {
                        var passthroughField = ovrManagerType.GetField("isInsightPassthroughEnabled");
                        if (passthroughField != null)
                        {
                            bool current = (bool)passthroughField.GetValue(instance);
                            passthroughField.SetValue(instance, !current);
                            Debug.Log($"[VR Licensing] Passthrough toggled to: {!current}");
                            return;
                        }
                    }
                }
            }

            // Fallback: log that passthrough is not available
            Debug.LogWarning("[VR Licensing] Passthrough no disponible. " +
                "Necesitas Meta XR SDK (OVRManager) para activar passthrough.");
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
                    new Vector3(0f, -0.1f, CANVAS_DISTANCE), flags);

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

                // Initial position
                var ct = canvas.transform;
                ct.position = cam.transform.position + cam.transform.forward * CANVAS_DISTANCE + cam.transform.up * -0.1f;
                ct.rotation = Quaternion.LookRotation(ct.position - cam.transform.position, Vector3.up);
            }
            else
            {
                // Manual positioning fallback
                var ct = canvas.transform;
                ct.position = cam.transform.position + cam.transform.forward * CANVAS_DISTANCE;
                ct.rotation = Quaternion.LookRotation(ct.position - cam.transform.position, Vector3.up);
            }
        }

        // ─────────────────── Key Field Builders ───────────────────

        private TMP_InputField[] BuildKeyFieldRow(string name, RectTransform parent, Vector2 position)
        {
            var container = CreatePanel(name, parent, Color.clear);
            var contRt = container.GetComponent<RectTransform>();
            contRt.anchorMin = new Vector2(0.5f, 0.5f);
            contRt.anchorMax = new Vector2(0.5f, 0.5f);
            contRt.sizeDelta = new Vector2(500, 50);
            contRt.anchoredPosition = position;

            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var fields = new TMP_InputField[4];

            for (int i = 0; i < 4; i++)
            {
                fields[i] = CreateKeyInputField($"{name}_F{i}", contRt, 100);

                if (i < 3)
                {
                    var sep = CreateTMPText($"{name}_S{i}", contRt,
                        "-", 10, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
                    var sepLe = sep.gameObject.AddComponent<LayoutElement>();
                    sepLe.preferredWidth = 12;
                    sepLe.flexibleWidth = 0;
                }
            }

            SetupKeyFieldAutoAdvance(fields);
            return fields;
        }

        private TMP_InputField CreateKeyInputField(string name, RectTransform parent, float width)
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
            ph.text = "XXXX";
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
            inputField.characterLimit = 4;
            inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
            inputField.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            inputField.caretColor = COLOR_ACCENT;
            inputField.selectionColor = new Color(COLOR_ACCENT.r, COLOR_ACCENT.g, COLOR_ACCENT.b, 0.3f);
            inputField.onFocusSelectAll = true;

            inputField.onValueChanged.AddListener((value) =>
            {
                string upper = value.ToUpperInvariant();
                if (upper != value) inputField.text = upper;
            });

            return inputField;
        }

        private void SetupKeyFieldAutoAdvance(TMP_InputField[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                int idx = i;
                fields[i].onValueChanged.AddListener((value) =>
                {
                    if (value.Length >= 4 && idx < fields.Length - 1)
                    {
                        fields[idx + 1].ActivateInputField();
                        fields[idx + 1].Select();
                    }
                });
            }
        }

        // ─────────────────── Utility Builders ───────────────────

        private string GetKeyFromFields(TMP_InputField[] fields)
        {
            string result = "";
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) result += "-";
                result += fields[i].text.Trim().ToUpperInvariant();
            }
            return result;
        }

        private void ClearKeyFields(TMP_InputField[] fields)
        {
            if (fields == null) return;
            foreach (var f in fields)
                if (f != null) f.text = "";
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

        private void CreateLayoutButton(string name, RectTransform parent,
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
        }

        private void SetRectFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private IEnumerator ShowSuccessAndHide()
        {
            welcomePanel.SetActive(false);
            keyInputPanel.SetActive(false);
            demoExpiredPanel.SetActive(false);
            successPanel.SetActive(true);

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

            // ── Hold visible for 2.5 seconds ──
            yield return new WaitForSeconds(2.5f);

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
        }
    }
}
