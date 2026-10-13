using UnityEditor;
using UnityEngine;
using System.IO;

namespace VRLicensing.Editor
{
    /// <summary>
    /// Editor window for initial VR Licensing setup.
    /// Accessible via Tools > Blix Studios > Licensing Setup.
    /// Allows creating the LicenseConfig ScriptableObject in Resources
    /// and editing its core fields (Product ID, App Display Name).
    /// </summary>
    public class LicensingSetupWindow : EditorWindow
    {
        private const string ResourcesFolderPath = "Assets/Resources";
        private const string ConfigAssetPath = "Assets/Resources/LicenseConfig.asset";
        private const string ConfigResourceName = "LicenseConfig";

        private LicenseConfig _config;
        private SerializedObject _serializedConfig;
        private Vector2 _scrollPosition;

        // Cached serialized properties
        private SerializedProperty _productIdProp;
        private SerializedProperty _appDisplayNameProp;
        private SerializedProperty _demoDurationProp;
        private SerializedProperty _maxOfflineHoursProp;
        private SerializedProperty _rsaPublicKeyProp;
        private SerializedProperty _storePlatformProp;
        private SerializedProperty _storeSkuProp;
        private SerializedProperty _storeTermsProp;
        private SerializedProperty _enforceEntitlementProp;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _statusBoxSuccess;
        private GUIStyle _statusBoxWarning;
        private bool _stylesInitialized;

        [MenuItem("Tools/Blix Studios/Licensing Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<LicensingSetupWindow>("VR Licensing Setup");
            window.minSize = new Vector2(420, 480);
            window.Show();
        }

        private void OnEnable()
        {
            TryLoadExistingConfig();
        }

        private void OnFocus()
        {
            TryLoadExistingConfig();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 8, 8)
            };

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 10, 4)
            };

            _statusBoxSuccess = new GUIStyle("HelpBox")
            {
                richText = true,
                fontSize = 11,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _statusBoxWarning = new GUIStyle("HelpBox")
            {
                richText = true,
                fontSize = 11,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _stylesInitialized = false; // Rebuild every repaint to handle skin changes
        }

        private void OnGUI()
        {
            InitStyles();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // ── Header ──────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🎮 VR Licensing Setup", _headerStyle);
            EditorGUILayout.LabelField("Blix Studios — Initial Configuration", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(4);

            DrawSeparator();

            // ── Section 1: LicenseConfig Asset ──────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. License Configuration Asset", _sectionStyle);
            EditorGUILayout.Space(2);

            if (_config != null)
            {
                EditorGUILayout.HelpBox(
                    "✅  LicenseConfig found at:\n" + ConfigAssetPath,
                    MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select in Project", GUILayout.Height(24)))
                {
                    Selection.activeObject = _config;
                    EditorGUIUtility.PingObject(_config);
                }
                if (GUILayout.Button("Delete & Recreate", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Delete LicenseConfig?",
                        "This will delete the existing LicenseConfig and create a new one with default values.\n\nAre you sure?",
                        "Delete & Recreate", "Cancel"))
                    {
                        AssetDatabase.DeleteAsset(ConfigAssetPath);
                        CreateLicenseConfig();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "⚠  No LicenseConfig found in Assets/Resources.\n" +
                    "Click the button below to generate one.",
                    MessageType.Warning);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("🔧  Generate LicenseConfig", GUILayout.Height(32)))
                {
                    CreateLicenseConfig();
                }
            }

            DrawSeparator();

            // ── Section 2: Core Settings ────────────────────────────
            if (_config != null && _serializedConfig != null)
            {
                _serializedConfig.Update();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("2. Product Settings", _sectionStyle);
                EditorGUILayout.Space(2);

                EditorGUILayout.PropertyField(_productIdProp,
                    new GUIContent("Product ID",
                        "The numeric product/simulator ID from the Supabase products table."));

                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(_appDisplayNameProp,
                    new GUIContent("App Display Name",
                        "The visible simulator name displayed in the licensing UI panels."));

                DrawSeparator();

                // ── Section 3: Advanced Settings ────────────────────
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("3. Advanced Settings", _sectionStyle);
                EditorGUILayout.Space(2);

                EditorGUILayout.PropertyField(_demoDurationProp,
                    new GUIContent("Demo Duration (seconds)",
                        "Maximum demo time in seconds. Default: 3600 (1 hour)."));

                EditorGUILayout.Space(2);

                EditorGUILayout.PropertyField(_maxOfflineHoursProp,
                    new GUIContent("Max Offline Hours",
                        "Maximum hours offline before requiring server reconnection. Default: 72."));

                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField("RSA Public Key (PEM)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_rsaPublicKeyProp, GUIContent.none);

                DrawSeparator();

                // ── Section 4: Store (v1.12) ────────────────────────
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("4. Store (Meta Horizon Store / Steam)", _sectionStyle);
                EditorGUILayout.Space(2);

                EditorGUILayout.PropertyField(_storePlatformProp,
                    new GUIContent("Store Platform",
                        "None for company APKs. MetaHorizon for Meta Horizon Store builds (entitlement check + in-app subscriptions)."));
                EditorGUILayout.PropertyField(_storeSkuProp,
                    new GUIContent("Subscription SKU", "One SKU per tier in the Meta dashboard; default sub_access."));
                EditorGUILayout.PropertyField(_storeTermsProp,
                    new GUIContent("Terms (Meta OfferTerm)", "MONTHLY, SEMIANNUAL, ANNUAL… The panel shows ANNUAL first."), true);
                EditorGUILayout.PropertyField(_enforceEntitlementProp,
                    new GUIContent("Enforce Entitlement",
                        "ON only in published store builds. OFF during development (sideloaded APKs fail the check)."));

                bool metaInstalled = MetaPlatformDefineSync.IsMetaPlatformInstalled();
                var platform = (StorePlatform)_storePlatformProp.enumValueIndex;
                if (platform == StorePlatform.MetaHorizon && !metaInstalled)
                {
                    EditorGUILayout.HelpBox(
                        "Meta XR Platform SDK (com.meta.xr.sdk.platform) is NOT installed. The build will behave as " +
                        "storePlatform = None. Install the package (scoped registry https://npm.developer.oculus.com, " +
                        "scope com.meta.xr) and set the App ID in Meta > Platform > Edit Settings.",
                        MessageType.Warning);
                }
                else if (platform == StorePlatform.MetaHorizon)
                {
                    EditorGUILayout.HelpBox(
                        "Meta XR Platform SDK installed — VRL_META_PLATFORM is active. Remember: App ID in " +
                        "Meta > Platform > Edit Settings, the three SKUs created in the dashboard, and " +
                        "showPurchaseCta OFF.",
                        MessageType.Info);
                }
                else if (platform == StorePlatform.Steam)
                {
                    EditorGUILayout.HelpBox("Steam is reserved for phase 2; the build behaves as None.", MessageType.Info);
                }

                DrawSeparator();

                // ── Status Summary ──────────────────────────────────
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Status Summary", _sectionStyle);
                EditorGUILayout.Space(2);

                DrawStatusSummary();

                _serializedConfig.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════
        //  Private Helpers
        // ═══════════════════════════════════════════════════════════

        private void TryLoadExistingConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<LicenseConfig>(ConfigAssetPath);

            if (_config == null)
            {
                // Also try Resources.Load in case it's at a different sub-path
                _config = Resources.Load<LicenseConfig>(ConfigResourceName);
            }

            if (_config != null)
            {
                _serializedConfig = new SerializedObject(_config);
                CacheSerializedProperties();
            }
            else
            {
                _serializedConfig = null;
            }
        }

        private void CacheSerializedProperties()
        {
            if (_serializedConfig == null) return;

            _productIdProp = _serializedConfig.FindProperty("productId");
            _appDisplayNameProp = _serializedConfig.FindProperty("appDisplayName");
            _demoDurationProp = _serializedConfig.FindProperty("demoDurationSeconds");
            _maxOfflineHoursProp = _serializedConfig.FindProperty("maxOfflineHours");
            _rsaPublicKeyProp = _serializedConfig.FindProperty("rsaPublicKeyPem");
            _storePlatformProp = _serializedConfig.FindProperty("storePlatform");
            _storeSkuProp = _serializedConfig.FindProperty("storeSubscriptionSku");
            _storeTermsProp = _serializedConfig.FindProperty("storeTerms");
            _enforceEntitlementProp = _serializedConfig.FindProperty("enforceEntitlement");
        }

        private void CreateLicenseConfig()
        {
            // Ensure the Resources folder exists
            if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                // Handle nested paths: ensure Assets exists, then create Resources
                string parentFolder = Path.GetDirectoryName(ResourcesFolderPath).Replace("\\", "/");
                string newFolderName = Path.GetFileName(ResourcesFolderPath);

                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    // Shouldn't happen for "Assets", but safety check
                    Directory.CreateDirectory(
                        Path.Combine(Application.dataPath,
                            ResourcesFolderPath.Replace("Assets/", "")));
                    AssetDatabase.Refresh();
                }
                else
                {
                    AssetDatabase.CreateFolder(parentFolder, newFolderName);
                }
            }

            var config = CreateInstance<LicenseConfig>();
            config.appDisplayName = "VR Simulator";
            config.demoDurationSeconds = 3600f;
            config.maxOfflineHours = 72f;

            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _config = config;
            _serializedConfig = new SerializedObject(_config);
            CacheSerializedProperties();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = _config;
            EditorGUIUtility.PingObject(_config);

            Debug.Log($"[VR Licensing] ✅ LicenseConfig created at: {ConfigAssetPath}");
        }

        private void DrawStatusSummary()
        {
            bool hasProductId = _config.productId > 0;
            bool hasDisplayName = !string.IsNullOrWhiteSpace(_config.appDisplayName);
            bool hasPublicKey = !string.IsNullOrWhiteSpace(_config.rsaPublicKeyPem);

            string productStatus = hasProductId
                ? $"✅  Product ID: <b>{_config.productId}</b>"
                : "⚠  Product ID not set";

            string nameStatus = hasDisplayName
                ? $"✅  App Name: <b>{_config.appDisplayName}</b>"
                : "⚠  App Display Name not set";

            string keyStatus = hasPublicKey
                ? "✅  RSA Public Key configured"
                : "ℹ  RSA Public Key not set (optional for online-only mode)";

            EditorGUILayout.HelpBox(
                $"{productStatus}\n{nameStatus}\n{keyStatus}",
                hasProductId && hasDisplayName ? MessageType.Info : MessageType.Warning);

            if (hasProductId && hasDisplayName)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "🚀  Your licensing configuration is ready.\n" +
                    "The system will auto-initialize at runtime via LicenseBootstrapper.",
                    MessageType.None);
            }
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(4);
        }
    }
}
