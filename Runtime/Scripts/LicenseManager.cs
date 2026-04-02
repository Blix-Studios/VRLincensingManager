using System;
using System.Collections;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// The possible states of the licensing system.
    /// </summary>
    public enum LicenseState
    {
        /// <summary>No license and demo not started yet.</summary>
        Unlicensed,
        /// <summary>Running in demo mode (limited time).</summary>
        Demo,
        /// <summary>Valid license active.</summary>
        Licensed,
        /// <summary>License or demo has expired.</summary>
        Expired,
        /// <summary>Clock tampering detected, requires online verification.</summary>
        ClockTampered
    }

    /// <summary>
    /// Main orchestrator for the VR licensing system.
    /// Coordinates all subsystems: validation, storage, clock guard, session tracking, and demo mode.
    /// Attach this to the LicenseGateUI prefab.
    /// </summary>
    public class LicenseManager : MonoBehaviour
    {
        [Header("References (Auto-populated)")]
        [SerializeField] private SupabaseClient supabaseClient;
        [SerializeField] private ClockGuard clockGuard;
        [SerializeField] private SessionTimeTracker sessionTracker;
        [SerializeField] private DemoModeManager demoManager;

        private LicenseConfig config;
        private LicenseData cachedLicense;
        private BrandingData cachedBranding;
        private LicenseUIBuilder uiBuilder;
        private DeviceRegistryData serverDeviceRecord;
        private int sessionCount;
        private float fpsAccumulator;
        private int fpsFrameCount;
        private bool isLicenseExpiry; // true = license expired, false = demo expired

        private const string SESSION_COUNT_KEY = "vrl_session_count";

        /// <summary>
        /// Current state of the licensing system.
        /// </summary>
        public LicenseState CurrentState { get; private set; } = LicenseState.Unlicensed;

        /// <summary>
        /// The active license data (null if unlicensed/demo).
        /// </summary>
        public LicenseData ActiveLicense => cachedLicense;

        /// <summary>
        /// The active branding data configured by the client on the web portal.
        /// Returns null if no branding has been configured.
        /// </summary>
        public BrandingData ActiveBranding => cachedBranding;

        /// <summary>
        /// The configuration used by this manager.
        /// </summary>
        public LicenseConfig Config => config;

        /// <summary>
        /// Sets the code-generated UI builder. Called by LicenseBootstrapper.
        /// </summary>
        public void SetUIBuilder(LicenseUIBuilder builder) => uiBuilder = builder;

        #region Events

        /// <summary>Fired when a license is successfully validated (online or from cache).</summary>
        public event Action<LicenseData> OnLicenseValidated;

        /// <summary>Fired when the license expires.</summary>
        public event Action OnLicenseExpired;

        /// <summary>Fired when the demo time runs out.</summary>
        public event Action OnDemoExpired;

        /// <summary>Fired when clock tampering is detected.</summary>
        public event Action OnClockTamperDetected;

        /// <summary>Fired whenever the license state changes.</summary>
        public event Action<LicenseState> OnStateChanged;

        /// <summary>Fired when an online validation error occurs.</summary>
        public event Action<string> OnValidationError;

        /// <summary>
        /// Fired when branding data is loaded from the server.
        /// Parameter is null if no branding is configured by the client.
        /// </summary>
        public event Action<BrandingData> OnBrandingLoaded;

        #endregion

        /// <summary>
        /// Initialize the licensing system with the project-specific config.
        /// Called by LicenseBootstrapper.
        /// </summary>
        public void Initialize(LicenseConfig licenseConfig)
        {
            config = licenseConfig;

            // Ensure we have all required components
            EnsureComponents();

            // Initialize the Supabase client
            supabaseClient.Initialize(config);

            // Subscribe to demo expiration
            demoManager.OnDemoExpired += HandleDemoExpired;

            // Load session count (encrypted)
            sessionCount = PlayerPrefs.GetInt(SESSION_COUNT_KEY, 0);
            sessionCount++;
            PlayerPrefs.SetInt(SESSION_COUNT_KEY, sessionCount);
            PlayerPrefs.Save();

            // Start the licensing flow
            StartCoroutine(StartLicensingFlow());
        }

        private void OnDestroy()
        {
            if (demoManager != null)
            {
                demoManager.OnDemoExpired -= HandleDemoExpired;
            }
        }

        /// <summary>
        /// Ensures all subsystem components are present on this GameObject.
        /// </summary>
        private void EnsureComponents()
        {
            if (supabaseClient == null)
                supabaseClient = GetComponent<SupabaseClient>() ?? gameObject.AddComponent<SupabaseClient>();
            if (clockGuard == null)
                clockGuard = GetComponent<ClockGuard>() ?? gameObject.AddComponent<ClockGuard>();
            if (sessionTracker == null)
                sessionTracker = GetComponent<SessionTimeTracker>() ?? gameObject.AddComponent<SessionTimeTracker>();
            if (demoManager == null)
                demoManager = GetComponent<DemoModeManager>() ?? gameObject.AddComponent<DemoModeManager>();
        }

        /// <summary>
        /// Main licensing flow executed on initialization.
        /// Now includes integrity checks and server-side device verification.
        /// </summary>
        private IEnumerator StartLicensingFlow()
        {
            Debug.Log("[VR Licensing] Starting licensing flow...");

            // Step 0: Integrity check (detect data wipe / tampering)
            if (!SecureLicenseStorage.IsFirstRun())
            {
                if (!SecureLicenseStorage.VerifyIntegrity())
                {
                    Debug.LogWarning("[VR Licensing] Data integrity violation! Possible tampering detected.");
                    // Don't give a fresh demo — force online verification
                    SetState(LicenseState.ClockTampered, forceUpdate: true);
                    OnClockTamperDetected?.Invoke();

                    bool isOnline = false;
                    yield return supabaseClient.CheckConnectivity(connected => isOnline = connected);

                    if (!isOnline)
                    {
                        Debug.LogError("[VR Licensing] Cannot verify: integrity violation and no internet.");
                        yield break;
                    }

                    // Online — proceed to server-side check which will enforce server demo state
                }
            }
            else
            {
                // First run — mark as initialized
                SecureLicenseStorage.MarkInitialized();
            }

            // Step 1: Check for clock tampering
            if (clockGuard.IsClockTampered())
            {
                Debug.LogWarning("[VR Licensing] Clock tampering detected!");
                SetState(LicenseState.ClockTampered, forceUpdate: true);
                OnClockTamperDetected?.Invoke();

                bool isOnline = false;
                yield return supabaseClient.CheckConnectivity(connected => isOnline = connected);

                if (!isOnline)
                {
                    Debug.LogError("[VR Licensing] Cannot verify license: clock tampered and no internet.");
                    yield break;
                }

                clockGuard.UpdateHighestKnownTime();
            }

            // Step 2: Try to load cached license
            cachedLicense = SecureLicenseStorage.LoadLicenseData();

            if (cachedLicense != null && cachedLicense.IsValid)
            {
                Debug.Log($"[VR Licensing] Cached license valid. Expires: {cachedLicense.expires_at}");
                ActivateLicense(cachedLicense);
                yield break;
            }

            if (cachedLicense != null && !cachedLicense.IsValid)
            {
                Debug.Log("[VR Licensing] Cached license expired.");
                isLicenseExpiry = true;
                SecureLicenseStorage.ClearAll();
                SecureLicenseStorage.MarkInitialized();
                cachedLicense = null;
                SetState(LicenseState.Expired, forceUpdate: true);
                OnLicenseExpired?.Invoke();
                // Send telemetry even on expired license launch
                StartCoroutine(SendStartupTelemetry(null));
                yield break;
            }

            // Step 3: Server-side device check (anti-factory-reset protection)
            bool serverCheckDone = false;
            bool serverDemoBlocked = false;
            float serverDemoUsed = 0f;

            yield return supabaseClient.CheckDeviceDemo(
                config.productId,
                (record) => {
                    serverCheckDone = true;
                    if (record != null)
                    {
                        serverDeviceRecord = record;
                        serverDemoBlocked = record.demo_blocked;
                        serverDemoUsed = record.demo_used_seconds;

                        // If server has more demo time used than local, trust the server
                        float localDemoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
                        if (record.demo_used_seconds > localDemoUsed)
                        {
                            Debug.Log($"[VR Licensing] Server demo time ({record.demo_used_seconds}s) > " +
                                $"local ({localDemoUsed}s). Using server value.");
                            SecureLicenseStorage.SetDemoUsedSeconds(record.demo_used_seconds);
                        }

                        Debug.Log($"[VR Licensing] Server device check: demo_used={record.demo_used_seconds}s, " +
                            $"blocked={record.demo_blocked}, sessions={record.session_count}");
                    }
                },
                (error) => {
                    serverCheckDone = true;
                    Debug.LogWarning($"[VR Licensing] Server device check failed (using local data): {error}");
                }
            );

            // Send startup telemetry (every launch)
            StartCoroutine(SendStartupTelemetry(null));

            // Step 4: Check demo status (combining server + local data)
            if (serverDemoBlocked)
            {
                Debug.Log("[VR Licensing] Device demo has been blocked by admin.");
                isLicenseExpiry = false;
                SetState(LicenseState.Expired, forceUpdate: true);
                OnDemoExpired?.Invoke();
                yield break;
            }

            float demoUsed = Mathf.Max(SecureLicenseStorage.GetDemoUsedSeconds(), serverDemoUsed);
            if (demoUsed >= config.demoDurationSeconds)
            {
                // Demo fully expired — must enter license key
                Debug.Log("[VR Licensing] Demo expired. Awaiting license key.");
                isLicenseExpiry = false;
                SetState(LicenseState.Expired, forceUpdate: true);
                OnDemoExpired?.Invoke();
            }
            else
            {
                // Show welcome panel — user chooses to start demo or enter key
                Debug.Log("[VR Licensing] Showing welcome panel.");
                SetState(LicenseState.Unlicensed, forceUpdate: true);
            }
        }

        public void SubmitLicenseKey(string licenseKey, Action<bool, string> onResult = null)
        {
            licenseKey = licenseKey.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(licenseKey))
            {
                onResult?.Invoke(false, "Please enter a license key.");
                return;
            }

            StartCoroutine(ValidateKeyOnline(licenseKey, onResult));
        }

        private IEnumerator ValidateKeyOnline(string licenseKey, Action<bool, string> onResult)
        {
            bool completed = false;
            LicenseData validLicense = null;
            string errorMessage = null;

            yield return supabaseClient.ValidateLicenseKey(
                licenseKey,
                config.productId,
                (license) =>
                {
                    validLicense = license;
                    completed = true;
                },
                (error) =>
                {
                    errorMessage = error;
                    completed = true;
                }
            );

            if (validLicense != null)
            {
                // License valid — activate it
                SecureLicenseStorage.SaveLicenseData(validLicense);
                demoManager.StopDemo();
                ActivateLicense(validLicense);
                onResult?.Invoke(true, null);
            }
            else
            {
                OnValidationError?.Invoke(errorMessage ?? "Unknown error");
                onResult?.Invoke(false, errorMessage ?? "Error validating the license.");
            }
        }

        /// <summary>
        /// Activates a validated license and starts session tracking.
        /// </summary>
        private void ActivateLicense(LicenseData license)
        {
            cachedLicense = license;
            SetState(LicenseState.Licensed);
            sessionTracker.StartTracking();
            clockGuard.UpdateHighestKnownTime();
            OnLicenseValidated?.Invoke(license);

            Debug.Log($"[VR Licensing] License ACTIVATED — Type: {license.license_type}, " +
                $"Expires: {license.expires_at}, Remaining: {license.TimeRemaining}");

            // Fetch branding data from the web portal (non-blocking)
            StartCoroutine(FetchAndCacheBranding(license.id));

            // Send device telemetry (fire-and-forget, non-blocking)
            StartCoroutine(SendSessionTelemetry(license.id));

            // Register device on server with license key
            float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
            StartCoroutine(supabaseClient.RegisterDevice(
                config.productId, demoUsed, sessionCount,
                license.license_key, null, null));
        }

        /// <summary>
        /// Fetches branding set by the client on the web portal and caches it.
        /// Other scripts can read LicenseManager.ActiveBranding at any time.
        /// </summary>
        private IEnumerator FetchAndCacheBranding(string licenseId)
        {
            yield return supabaseClient.FetchBranding(
                licenseId,
                (branding) =>
                {
                    cachedBranding = branding;
                    OnBrandingLoaded?.Invoke(branding);

                    if (branding != null && branding.HasBranding)
                    {
                        Debug.Log($"[VR Licensing] Client branding: \"{branding.brand_name}\"");
                    }
                    else
                    {
                        Debug.Log("[VR Licensing] No client branding configured — using defaults.");
                    }
                },
                (error) =>
                {
                    Debug.LogWarning($"[VR Licensing] Could not fetch branding: {error}");
                    cachedBranding = null;
                    OnBrandingLoaded?.Invoke(null);
                }
            );
        }

        /// <summary>
        /// Collects device data and sends it to the telemetry table.
        /// </summary>
        private IEnumerator SendSessionTelemetry(string licenseId)
        {
            float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
            float avgFps = fpsFrameCount > 0 ? fpsAccumulator / fpsFrameCount : 0f;

            var payload = TelemetryCollector.Collect(
                licenseId: licenseId,
                sessionDuration: sessionTracker != null ? SecureLicenseStorage.GetSessionUsedSeconds() : 0f,
                demoUsed: demoUsed,
                sessionCount: sessionCount,
                avgFps: avgFps
            );

            yield return supabaseClient.SendTelemetry(payload);
        }

        /// <summary>
        /// Periodically checks if the license is still valid.
        /// Also updates the clock guard.
        /// </summary>
        private void Update()
        {
            // Track FPS for telemetry
            fpsAccumulator += 1f / Time.unscaledDeltaTime;
            fpsFrameCount++;

            if (CurrentState == LicenseState.Licensed && cachedLicense != null)
            {
                // Check if license has expired since last frame
                if (!cachedLicense.IsValid)
                {
                    Debug.Log("[VR Licensing] License has expired during session.");
                    isLicenseExpiry = true;
                    sessionTracker.StopTracking();
                    SetState(LicenseState.Expired);
                    OnLicenseExpired?.Invoke();
                    return;
                }

                // Update clock guard periodically (every frame is fine, it's cheap)
                clockGuard.UpdateHighestKnownTime();
            }

            // Update demo timer in real-time using DemoModeManager
            if (CurrentState == LicenseState.Demo && demoManager != null)
            {
                if (demoManager.IsDemoExpired)
                {
                    // DemoModeManager will fire OnDemoExpired, handled by HandleDemoExpired
                    return;
                }
            }
        }

        /// <summary>
        /// Called by UI when user clicks "Iniciar Demo Gratuita".
        /// Starts the demo timer and hides the UI.
        /// </summary>
        public void StartDemoMode()
        {
            float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
            if (demoUsed >= config.demoDurationSeconds)
            {
                Debug.Log("[VR Licensing] Cannot start demo — already expired.");
                SetState(LicenseState.Expired);
                OnDemoExpired?.Invoke();
                return;
            }

            Debug.Log($"[VR Licensing] Starting demo mode. Used: {demoUsed:F0}s / {config.demoDurationSeconds:F0}s");
            SetState(LicenseState.Demo);
            demoManager.StartDemo(config.demoDurationSeconds);
            sessionTracker.StartTracking();
        }

        /// <summary>
        /// Forces an online re-validation of the current license.
        /// Useful for re-sync after being offline.
        /// </summary>
        public void ForceOnlineRevalidation()
        {
            if (cachedLicense != null && !string.IsNullOrEmpty(cachedLicense.license_key))
            {
                StartCoroutine(ValidateKeyOnline(cachedLicense.license_key, (success, error) =>
                {
                    if (!success)
                    {
                        Debug.LogWarning($"[VR Licensing] Re-validation failed: {error}");
                    }
                }));
            }
        }

        /// <summary>
        /// Clears all license data and resets to unlicensed state.
        /// For admin/debug purposes.
        /// </summary>
        public void ResetLicense()
        {
            sessionTracker.StopTracking();
            demoManager.StopDemo();
            SecureLicenseStorage.ClearAll();
            cachedLicense = null;
            SetState(LicenseState.Unlicensed);
            Debug.Log("[VR Licensing] License reset.");
        }

        private void SetState(LicenseState newState, bool forceUpdate = false)
        {
            if (CurrentState == newState && !forceUpdate) return;

            var previousState = CurrentState;
            CurrentState = newState;
            Debug.Log($"[VR Licensing] State: {previousState} → {newState}");
            OnStateChanged?.Invoke(newState);

            // Drive the UI builder if available
            UpdateUI(newState);
        }

        /// <summary>
        /// Updates the UI builder panels based on the current state.
        /// </summary>
        private void UpdateUI(LicenseState state)
        {
            if (uiBuilder == null) return;

            switch (state)
            {
                case LicenseState.Unlicensed:
                    uiBuilder.ShowWelcome();
                    break;
                case LicenseState.Demo:
                    uiBuilder.HideAll(); // UI hidden, user is in the simulator
                    break;
                case LicenseState.Licensed:
                    uiBuilder.ShowLicensed();
                    break;
                case LicenseState.Expired:
                    if (isLicenseExpiry)
                        uiBuilder.ShowLicenseExpired();
                    else
                        uiBuilder.ShowDemoExpired();
                    break;
                case LicenseState.ClockTampered:
                    uiBuilder.ShowError("System clock has been tampered with. Connect to the internet to verify.");
                    uiBuilder.ShowWelcome();
                    break;
            }
        }

        private void HandleDemoExpired()
        {
            isLicenseExpiry = false;
            SetState(LicenseState.Expired);
            sessionTracker.StopTracking();
            OnDemoExpired?.Invoke();
        }

        /// <summary>
        /// Sends telemetry on every app start (even in demo mode).
        /// If licenseId is null, sends with empty license reference.
        /// </summary>
        private IEnumerator SendStartupTelemetry(string licenseId)
        {
            float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
            float avgFps = fpsFrameCount > 0 ? fpsAccumulator / fpsFrameCount : 0f;

            // Register device on server (upsert)
            yield return supabaseClient.RegisterDevice(
                config.productId, demoUsed, sessionCount,
                cachedLicense?.license_key,
                (record) => { serverDeviceRecord = record; },
                (error) => { Debug.LogWarning($"[VR Licensing] Startup registration failed: {error}"); }
            );
        }
    }
}
