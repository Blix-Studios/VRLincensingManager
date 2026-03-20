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
        // Canvas dimensions in world units (meters)
        private const float CANVAS_WIDTH = 0.8f;
        private const float CANVAS_HEIGHT = 0.5f;
        private const float CANVAS_SCALE = 0.001f; // 1 unit = 1mm, so pixels map nicely
        private const float CANVAS_DISTANCE = 2.0f; // Distance from camera (meters)
        private const int CANVAS_PX_WIDTH = 800;
        private const int CANVAS_PX_HEIGHT = 500;

        // Colors — dark, premium palette
        private static readonly Color COLOR_BG_OVERLAY = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color COLOR_PANEL_BG = new Color(0.10f, 0.10f, 0.14f, 0.95f);
        private static readonly Color COLOR_PANEL_HEADER = new Color(0.13f, 0.13f, 0.18f, 1f);
        private static readonly Color COLOR_ACCENT = new Color(0.29f, 0.56f, 1f, 1f);       // Blue accent
        private static readonly Color COLOR_ACCENT_HOVER = new Color(0.40f, 0.65f, 1f, 1f);
        private static readonly Color COLOR_ERROR = new Color(0.95f, 0.30f, 0.30f, 1f);
        private static readonly Color COLOR_SUCCESS = new Color(0.30f, 0.85f, 0.45f, 1f);
        private static readonly Color COLOR_TEXT = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color COLOR_TEXT_DIM = new Color(0.55f, 0.55f, 0.62f, 1f);
        private static readonly Color COLOR_INPUT_BG = new Color(0.15f, 0.15f, 0.20f, 1f);
        private static readonly Color COLOR_INPUT_BORDER = new Color(0.25f, 0.25f, 0.32f, 1f);

        // ─────────────────────── UI References ───────────────────────
        private Canvas canvas;
        private CanvasScaler canvasScaler;
        private GameObject overlayPanel;

        // License Gate UI
        private GameObject licenseGatePanel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private TMP_InputField[] keyFields;
        private TextMeshProUGUI[] keySeparators;
        private Button activateButton;
        private TextMeshProUGUI activateButtonText;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI demoTimerText;

        // Demo Expired UI
        private GameObject demoExpiredPanel;
        private TextMeshProUGUI expiredTitleText;
        private TMP_InputField[] expiredKeyFields;
        private Button expiredActivateButton;
        private TextMeshProUGUI expiredStatusText;

        // Licensed overlay (brief success feedback)
        private GameObject successPanel;
        private TextMeshProUGUI successText;

        private LicenseConfig config;
        private LicenseManager manager;
        private bool isPositioned;

        // ─────────────────────── Factory ───────────────────────

        /// <summary>
        /// Creates the UI builder, builds all UI elements, and returns the instance.
        /// </summary>
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

        /// <summary>Shows the license key input panel (initial state or manual trigger).</summary>
        public void ShowLicenseGate()
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            licenseGatePanel.SetActive(true);
            demoExpiredPanel.SetActive(false);
            successPanel.SetActive(false);
            statusText.text = "";
            ClearKeyFields(keyFields);
            SetDemoTimerVisible(false);
        }

        /// <summary>Shows demo mode with remaining time and license key option.</summary>
        public void ShowDemoMode(float remainingSeconds)
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            licenseGatePanel.SetActive(true);
            demoExpiredPanel.SetActive(false);
            successPanel.SetActive(false);
            statusText.text = "";

            SetDemoTimerVisible(true);
            UpdateDemoTimer(remainingSeconds);
        }

        /// <summary>Updates the demo mode timer text.</summary>
        public void UpdateDemoTimer(float remainingSeconds)
        {
            if (demoTimerText == null) return;

            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
            demoTimerText.text = $"Modo Demo: {minutes:00}:{seconds:00} restantes";

            // Change color when running low (< 5 min)
            demoTimerText.color = remainingSeconds < 300f ? COLOR_ERROR : COLOR_TEXT_DIM;
        }

        /// <summary>Shows the demo expired panel (blocks app until license entered).</summary>
        public void ShowDemoExpired()
        {
            EnsurePositioned();
            overlayPanel.SetActive(true);
            licenseGatePanel.SetActive(false);
            demoExpiredPanel.SetActive(true);
            successPanel.SetActive(false);
            expiredStatusText.text = "";
            ClearKeyFields(expiredKeyFields);
        }

        /// <summary>Hides all UI (license is valid).</summary>
        public void ShowLicensed()
        {
            // Brief success animation then hide
            StartCoroutine(ShowSuccessAndHide());
        }

        /// <summary>Shows an error message on the currently visible panel.</summary>
        public void ShowError(string message)
        {
            if (licenseGatePanel.activeSelf)
            {
                statusText.text = message;
                statusText.color = COLOR_ERROR;
            }
            else if (demoExpiredPanel.activeSelf)
            {
                expiredStatusText.text = message;
                expiredStatusText.color = COLOR_ERROR;
            }
        }

        /// <summary>Shows a success message on the currently visible panel.</summary>
        public void ShowSuccess(string message = "✓ Licencia activada correctamente")
        {
            if (licenseGatePanel.activeSelf)
            {
                statusText.text = message;
                statusText.color = COLOR_SUCCESS;
            }
            else if (demoExpiredPanel.activeSelf)
            {
                expiredStatusText.text = message;
                expiredStatusText.color = COLOR_SUCCESS;
            }
        }

        /// <summary>Sets the activate button(s) interactable state for loading feedback.</summary>
        public void SetLoading(bool loading)
        {
            if (activateButton != null)
            {
                activateButton.interactable = !loading;
                activateButtonText.text = loading ? "Validando..." : "Activar Licencia";
            }
            if (expiredActivateButton != null)
            {
                expiredActivateButton.interactable = !loading;
            }
        }

        // ─────────────────────── Build Methods ───────────────────────

        private void BuildUI()
        {
            EnsureEventSystem();
            BuildCanvas();
            BuildOverlay();
            BuildLicenseGatePanel();
            BuildDemoExpiredPanel();
            BuildSuccessPanel();

            // Start hidden
            overlayPanel.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("[EventSystem]");
                esGo.transform.SetParent(transform);
                esGo.AddComponent<EventSystem>();

                // Use new Input System's UI module if available, otherwise fall back to legacy
                var inputSystemUIType = Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemUIType != null)
                {
                    esGo.AddComponent(inputSystemUIType);
                    Debug.Log("[VR Licensing] InputSystemUIInputModule agregado (nuevo Input System detectado).");
                }
                else
                {
                    esGo.AddComponent<StandaloneInputModule>();
                    Debug.Log("[VR Licensing] StandaloneInputModule agregado (Input System legacy).");
                }
            }
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("LicenseCanvas");
            canvasGo.transform.SetParent(transform);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30000; // Always on top

            canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 10f;
            canvasScaler.referencePixelsPerUnit = 100f;

            // Add appropriate raycaster based on available packages
            // Use reflection to avoid hard dependency on XR Interaction Toolkit
            bool addedXRRaycaster = false;
            var xrRaycasterType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (xrRaycasterType != null)
            {
                canvasGo.AddComponent(xrRaycasterType);
                addedXRRaycaster = true;
                Debug.Log("[VR Licensing] TrackedDeviceGraphicRaycaster agregado (XR Interaction Toolkit detectado).");
            }

            if (!addedXRRaycaster)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
                Debug.Log("[VR Licensing] GraphicRaycaster estándar agregado (XR Interaction Toolkit no detectado).");
            }

            // Set canvas size in world space
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_PX_WIDTH, CANVAS_PX_HEIGHT);
            canvasGo.transform.localScale = Vector3.one * CANVAS_SCALE;

            // Add LazyFollow for smooth VR head tracking
            AddLazyFollow(canvasGo);
        }

        private void BuildOverlay()
        {
            overlayPanel = CreatePanel("Overlay", canvas.GetComponent<RectTransform>(),
                COLOR_BG_OVERLAY, stretch: true);
        }

        // ─────────────────── LICENSE GATE PANEL ───────────────────

        private void BuildLicenseGatePanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            // Main panel container (centered card)
            licenseGatePanel = CreatePanel("LicenseGatePanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = licenseGatePanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(680, 420);
            panelRt.anchoredPosition = Vector2.zero;

            // Header bar
            var header = CreatePanel("Header", panelRt, COLOR_PANEL_HEADER);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 70);
            headerRt.anchoredPosition = Vector2.zero;

            // Lock icon + Title
            titleText = CreateTMPText("Title", headerRt,
                $"{config.appDisplayName}",
                24, FontStyles.Bold, COLOR_TEXT, TextAlignmentOptions.Center);
            SetRectStretch(titleText.rectTransform, 20, 0, 20, 0);

            // Subtitle
            subtitleText = CreateTMPText("Subtitle", panelRt,
                "Ingresa tu clave de licencia para desbloquear el simulador completo",
                14, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
            var subRt = subtitleText.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.sizeDelta = new Vector2(-60, 30);
            subRt.anchoredPosition = new Vector2(0, -85);

            // Key input area
            var keyContainer = CreatePanel("KeyContainer", panelRt, Color.clear);
            var keyContRt = keyContainer.GetComponent<RectTransform>();
            keyContRt.anchorMin = new Vector2(0.5f, 0.5f);
            keyContRt.anchorMax = new Vector2(0.5f, 0.5f);
            keyContRt.sizeDelta = new Vector2(520, 55);
            keyContRt.anchoredPosition = new Vector2(0, 20);

            var hlg = keyContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            keyFields = new TMP_InputField[4];
            keySeparators = new TextMeshProUGUI[3];

            for (int i = 0; i < 4; i++)
            {
                keyFields[i] = CreateKeyInputField($"KeyField_{i}", keyContainer.GetComponent<RectTransform>(), 95);

                if (i < 3)
                {
                    keySeparators[i] = CreateTMPText($"Sep_{i}", keyContainer.GetComponent<RectTransform>(),
                        "—", 20, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
                    var sepLe = keySeparators[i].gameObject.AddComponent<LayoutElement>();
                    sepLe.preferredWidth = 14;
                }
            }

            SetupKeyFieldAutoAdvance(keyFields);

            // Activate button
            activateButton = CreateButton("ActivateBtn", panelRt, "Activar Licencia",
                OnActivateClicked);
            var btnRt = activateButton.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(280, 45);
            btnRt.anchoredPosition = new Vector2(0, -45);
            activateButtonText = activateButton.GetComponentInChildren<TextMeshProUGUI>();

            // Status text
            statusText = CreateTMPText("StatusText", panelRt,
                "", 13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
            var statusRt = statusText.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.sizeDelta = new Vector2(-40, 30);
            statusRt.anchoredPosition = new Vector2(0, 65);

            // Demo timer text (bottom of panel)
            demoTimerText = CreateTMPText("DemoTimer", panelRt,
                "", 13, FontStyles.Italic, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
            var demoRt = demoTimerText.rectTransform;
            demoRt.anchorMin = new Vector2(0f, 0f);
            demoRt.anchorMax = new Vector2(1f, 0f);
            demoRt.pivot = new Vector2(0.5f, 0f);
            demoRt.sizeDelta = new Vector2(-40, 30);
            demoRt.anchoredPosition = new Vector2(0, 20);
            demoTimerText.gameObject.SetActive(false);
        }

        // ─────────────────── DEMO EXPIRED PANEL ───────────────────

        private void BuildDemoExpiredPanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            demoExpiredPanel = CreatePanel("DemoExpiredPanel", overlayRt, COLOR_PANEL_BG);
            var panelRt = demoExpiredPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(680, 400);
            panelRt.anchoredPosition = Vector2.zero;

            // Header
            var header = CreatePanel("ExpiredHeader", panelRt, COLOR_PANEL_HEADER);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 70);
            headerRt.anchoredPosition = Vector2.zero;

            expiredTitleText = CreateTMPText("ExpiredTitle", headerRt,
                "Demo Expirado",
                24, FontStyles.Bold, COLOR_ERROR, TextAlignmentOptions.Center);
            SetRectStretch(expiredTitleText.rectTransform, 20, 0, 20, 0);

            // Message
            var msgText = CreateTMPText("ExpiredMsg", panelRt,
                $"Tu periodo de prueba de {config.appDisplayName} ha terminado.\nIngresa una clave de licencia para continuar usando el simulador.",
                14, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
            var msgRt = msgText.rectTransform;
            msgRt.anchorMin = new Vector2(0f, 1f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.pivot = new Vector2(0.5f, 1f);
            msgRt.sizeDelta = new Vector2(-60, 50);
            msgRt.anchoredPosition = new Vector2(0, -85);

            // Key input area
            var keyContainer = CreatePanel("ExpKeyContainer", panelRt, Color.clear);
            var keyContRt = keyContainer.GetComponent<RectTransform>();
            keyContRt.anchorMin = new Vector2(0.5f, 0.5f);
            keyContRt.anchorMax = new Vector2(0.5f, 0.5f);
            keyContRt.sizeDelta = new Vector2(520, 55);
            keyContRt.anchoredPosition = new Vector2(0, 5);

            var hlg2 = keyContainer.AddComponent<HorizontalLayoutGroup>();
            hlg2.spacing = 6;
            hlg2.childAlignment = TextAnchor.MiddleCenter;
            hlg2.childForceExpandWidth = false;
            hlg2.childForceExpandHeight = true;
            hlg2.childControlWidth = false;
            hlg2.childControlHeight = true;

            expiredKeyFields = new TMP_InputField[4];

            for (int i = 0; i < 4; i++)
            {
                expiredKeyFields[i] = CreateKeyInputField($"ExpKeyField_{i}",
                    keyContainer.GetComponent<RectTransform>(), 95);

                if (i < 3)
                {
                    var sep = CreateTMPText($"ExpSep_{i}", keyContainer.GetComponent<RectTransform>(),
                        "—", 20, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
                    var sepLe = sep.gameObject.AddComponent<LayoutElement>();
                    sepLe.preferredWidth = 14;
                }
            }

            SetupKeyFieldAutoAdvance(expiredKeyFields);

            // Activate button
            expiredActivateButton = CreateButton("ExpActivateBtn", panelRt, "Activar Licencia",
                OnExpiredActivateClicked);
            var btnRt = expiredActivateButton.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(280, 45);
            btnRt.anchoredPosition = new Vector2(0, -55);

            // Status text
            expiredStatusText = CreateTMPText("ExpStatusText", panelRt,
                "", 13, FontStyles.Normal, COLOR_TEXT_DIM, TextAlignmentOptions.Center);
            var sRt = expiredStatusText.rectTransform;
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 0f);
            sRt.pivot = new Vector2(0.5f, 0f);
            sRt.sizeDelta = new Vector2(-40, 30);
            sRt.anchoredPosition = new Vector2(0, 30);

            demoExpiredPanel.SetActive(false);
        }

        // ─────────────────── SUCCESS PANEL ───────────────────

        private void BuildSuccessPanel()
        {
            var overlayRt = overlayPanel.GetComponent<RectTransform>();

            successPanel = CreatePanel("SuccessPanel", overlayRt, new Color(0.05f, 0.12f, 0.05f, 0.95f));
            var panelRt = successPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(450, 180);
            panelRt.anchoredPosition = Vector2.zero;

            successText = CreateTMPText("SuccessText", panelRt,
                "Licencia Activada!\nDisfruta el simulador!",
                26, FontStyles.Bold, COLOR_SUCCESS, TextAlignmentOptions.Center);
            SetRectStretch(successText.rectTransform, 30, 30, 30, 30);

            successPanel.SetActive(false);
        }

        // ─────────────────── Button Handlers ───────────────────

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

        private void SubmitKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Replace("-", "").Length < 16)
            {
                ShowError("Ingresa la clave completa (XXXX-XXXX-XXXX-XXXX)");
                return;
            }

            SetLoading(true);
            statusText.text = "";
            expiredStatusText.text = "";

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
                    ShowError(error ?? "Error al validar la licencia.");
                }
            });
        }

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

        // ─────────────────── LazyFollow Setup ───────────────────

        private Component lazyFollowInstance;

        /// <summary>
        /// Adds the LazyFollow component from XR Interaction Toolkit via reflection.
        /// Falls back to manual positioning if XRI is not available.
        /// </summary>
        private void AddLazyFollow(GameObject target)
        {
            var lazyFollowType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.BodyUI.LazyFollow, Unity.XR.Interaction.Toolkit");

            if (lazyFollowType != null)
            {
                lazyFollowInstance = target.AddComponent(lazyFollowType);

                // Configure LazyFollow via reflection
                // positionFollowMode = Follow (enum value 1)
                var posModeProp = lazyFollowType.GetProperty("positionFollowMode");
                if (posModeProp != null)
                {
                    var posEnum = posModeProp.PropertyType;
                    posModeProp.SetValue(lazyFollowInstance, Enum.ToObject(posEnum, 1)); // Follow = 1
                }

                // rotationFollowMode = LookAtWithWorldUp (enum value 2)
                var rotModeProp = lazyFollowType.GetProperty("rotationFollowMode");
                if (rotModeProp != null)
                {
                    var rotEnum = rotModeProp.PropertyType;
                    rotModeProp.SetValue(lazyFollowInstance, Enum.ToObject(rotEnum, 2)); // LookAtWithWorldUp = 2
                }

                // speed — how fast it follows
                var speedProp = lazyFollowType.GetProperty("speed");
                if (speedProp != null)
                    speedProp.SetValue(lazyFollowInstance, 5f);

                // targetOffset — place it in front and slightly below eye level
                var offsetProp = lazyFollowType.GetProperty("targetOffset");
                if (offsetProp != null)
                    offsetProp.SetValue(lazyFollowInstance, new Vector3(0f, -0.1f, CANVAS_DISTANCE));

                Debug.Log("[VR Licensing] LazyFollow agregado para seguimiento suave de cabeza.");
            }
            else
            {
                Debug.Log("[VR Licensing] LazyFollow no disponible, usando posicionamiento manual.");
            }
        }

        // ─────────────────── Positioning ───────────────────

        private void EnsurePositioned()
        {
            if (isPositioned) return;
            StartCoroutine(PositionWhenCameraReady());
        }

        private IEnumerator PositionWhenCameraReady()
        {
            // Wait until Camera.main is available
            while (Camera.main == null)
            {
                yield return null;
            }

            var cam = Camera.main;

            // If we have LazyFollow, set its target to the camera
            if (lazyFollowInstance != null)
            {
                var lazyFollowType = lazyFollowInstance.GetType();
                var targetProp = lazyFollowType.GetProperty("target");
                if (targetProp != null)
                    targetProp.SetValue(lazyFollowInstance, cam.transform);
            }
            else
            {
                // Fallback: manual static positioning
                PositionInFrontOfCamera(cam);
            }

            isPositioned = true;
        }

        /// <summary>
        /// Manual fallback positioning when LazyFollow is not available.
        /// </summary>
        private void PositionInFrontOfCamera(Camera cam)
        {
            if (cam == null) return;

            var canvasTransform = canvas.transform;
            Vector3 camPos = cam.transform.position;
            Vector3 camForward = cam.transform.forward;

            canvasTransform.position = camPos + camForward * CANVAS_DISTANCE;
            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - camPos, Vector3.up);
        }

        // ─────────────────── Utility Builders ───────────────────

        private GameObject CreatePanel(string name, RectTransform parent, Color color,
            bool stretch = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (color.a > 0)
            {
                var img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = (name == "Overlay"); // Only overlay blocks raycasts
            }

            return go;
        }

        private TextMeshProUGUI CreateTMPText(string name, RectTransform parent,
            string text, float fontSize, FontStyles style, Color color,
            TextAlignmentOptions alignment)
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

            return tmp;
        }

        private TMP_InputField CreateKeyInputField(string name, RectTransform parent, float width)
        {
            // Container
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 50);

            // Background
            var bgImg = go.AddComponent<Image>();
            bgImg.color = COLOR_INPUT_BG;

            // Add an outline effect for border look
            var outline = go.AddComponent<Outline>();
            outline.effectColor = COLOR_INPUT_BORDER;
            outline.effectDistance = new Vector2(1, -1);

            // Layout element for the HorizontalLayoutGroup
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 50;

            // Text area container
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(10, 5);
            textAreaRt.offsetMax = new Vector2(-10, -5);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = "XXXX";
            placeholder.fontSize = 22;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.color = new Color(0.35f, 0.35f, 0.42f, 0.6f);
            placeholder.alignment = TextAlignmentOptions.Center;
            placeholder.enableWordWrapping = false;
            var phRt = placeholder.rectTransform;
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;

            // Input text
            var inputTextGo = new GameObject("Text");
            inputTextGo.transform.SetParent(textArea.transform, false);
            var inputText = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 22;
            inputText.fontStyle = FontStyles.Bold;
            inputText.color = COLOR_TEXT;
            inputText.alignment = TextAlignmentOptions.Center;
            inputText.enableWordWrapping = false;
            var itRt = inputText.rectTransform;
            itRt.anchorMin = Vector2.zero;
            itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero;
            itRt.offsetMax = Vector2.zero;

            // Caret (invisible child needed by TMP_InputField)
            // TMP_InputField creates its own caret

            // Input field component
            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 4;
            inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
            inputField.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            inputField.caretColor = COLOR_ACCENT;
            inputField.selectionColor = new Color(COLOR_ACCENT.r, COLOR_ACCENT.g, COLOR_ACCENT.b, 0.3f);
            inputField.onFocusSelectAll = true;

            // Force uppercase via onValueChanged
            inputField.onValueChanged.AddListener((value) =>
            {
                string upper = value.ToUpperInvariant();
                if (upper != value)
                {
                    inputField.text = upper;
                }
            });

            return inputField;
        }

        private void SetupKeyFieldAutoAdvance(TMP_InputField[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                int currentIndex = i; // Capture for closure
                fields[i].onValueChanged.AddListener((value) =>
                {
                    if (value.Length >= 4 && currentIndex < fields.Length - 1)
                    {
                        // Auto-advance to next field
                        fields[currentIndex + 1].ActivateInputField();
                        fields[currentIndex + 1].Select();
                    }
                });
            }
        }

        private Button CreateButton(string name, RectTransform parent, string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = COLOR_ACCENT;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            // Hover colors
            var colors = btn.colors;
            colors.normalColor = COLOR_ACCENT;
            colors.highlightedColor = COLOR_ACCENT_HOVER;
            colors.pressedColor = new Color(0.20f, 0.45f, 0.90f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            // Button label
            var labelTMP = CreateTMPText("Label", go.GetComponent<RectTransform>(),
                label, 16, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRectStretch(labelTMP.rectTransform, 10, 5, 10, 5);
            labelTMP.raycastTarget = false;

            return btn;
        }

        private void SetRectStretch(RectTransform rt, float left, float top,
            float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private void ClearKeyFields(TMP_InputField[] fields)
        {
            if (fields == null) return;
            foreach (var field in fields)
            {
                if (field != null) field.text = "";
            }
        }

        private void SetDemoTimerVisible(bool visible)
        {
            if (demoTimerText != null)
                demoTimerText.gameObject.SetActive(visible);
        }

        private IEnumerator ShowSuccessAndHide()
        {
            licenseGatePanel.SetActive(false);
            demoExpiredPanel.SetActive(false);
            successPanel.SetActive(true);

            yield return new WaitForSeconds(2f);

            successPanel.SetActive(false);
            overlayPanel.SetActive(false);
        }
    }
}
