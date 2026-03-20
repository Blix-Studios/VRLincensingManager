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
        private LicenseUIBuilder uiBuilder;

        /// <summary>
        /// Current state of the licensing system.
        /// </summary>
        public LicenseState CurrentState { get; private set; } = LicenseState.Unlicensed;

        /// <summary>
        /// The active license data (null if unlicensed/demo).
        /// </summary>
        public LicenseData ActiveLicense => cachedLicense;

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
        /// </summary>
        private IEnumerator StartLicensingFlow()
        {
            Debug.Log("[VR Licensing] Starting licensing flow...");

            // Step 1: Check for clock tampering
            if (clockGuard.IsClockTampered())
            {
                Debug.LogWarning("[VR Licensing] Clock tampering detected!");
                SetState(LicenseState.ClockTampered);
                OnClockTamperDetected?.Invoke();

                // Try to verify online
                bool isOnline = false;
                yield return supabaseClient.CheckConnectivity(connected => isOnline = connected);

                if (!isOnline)
                {
                    // Can't verify — stay locked
                    Debug.LogError("[VR Licensing] Cannot verify license: clock tampered and no internet.");
                    yield break;
                }

                // Reset clock guard if we can verify online
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
                SecureLicenseStorage.ClearAll();
                cachedLicense = null;
            }

            // Step 3: No valid license — enter demo or unlicensed state
            float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
            if (demoUsed < config.demoDurationSeconds)
            {
                Debug.Log($"[VR Licensing] Entering demo mode. Used: {demoUsed:F0}s / {config.demoDurationSeconds:F0}s");
                SetState(LicenseState.Demo);
                demoManager.StartDemo(config.demoDurationSeconds);
                sessionTracker.StartTracking();
            }
            else
            {
                Debug.Log("[VR Licensing] Demo expired. Awaiting license key.");
                SetState(LicenseState.Expired);
                OnDemoExpired?.Invoke();
            }
        }

        /// <summary>
        /// Call this when the user enters a license key in the UI.
        /// Validates the key online against Supabase.
        /// </summary>
        /// <param name="licenseKey">The key entered by the user.</param>
        /// <param name="onResult">Callback with (success, errorMessage).</param>
        public void SubmitLicenseKey(string licenseKey, Action<bool, string> onResult = null)
        {
            // Normalize: uppercase and trim
            licenseKey = licenseKey.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(licenseKey))
            {
                onResult?.Invoke(false, "Por favor ingresa una clave de licencia.");
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
                OnValidationError?.Invoke(errorMessage ?? "Error desconocido");
                onResult?.Invoke(false, errorMessage ?? "Error al validar la licencia.");
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
        }

        /// <summary>
        /// Periodically checks if the license is still valid.
        /// Also updates the clock guard.
        /// </summary>
        private void Update()
        {
            if (CurrentState == LicenseState.Licensed && cachedLicense != null)
            {
                // Check if license has expired since last frame
                if (!cachedLicense.IsValid)
                {
                    Debug.Log("[VR Licensing] License has expired during session.");
                    sessionTracker.StopTracking();
                    SetState(LicenseState.Expired);
                    OnLicenseExpired?.Invoke();
                    return;
                }

                // Update clock guard periodically (every frame is fine, it's cheap)
                clockGuard.UpdateHighestKnownTime();
            }

            // Update demo timer on the UI
            if (CurrentState == LicenseState.Demo && uiBuilder != null && config != null)
            {
                float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
                float remaining = config.demoDurationSeconds - demoUsed;
                if (remaining > 0)
                {
                    uiBuilder.UpdateDemoTimer(remaining);
                }
            }
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

        private void SetState(LicenseState newState)
        {
            if (CurrentState == newState) return;

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
                    uiBuilder.ShowLicenseGate();
                    break;
                case LicenseState.Demo:
                    float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
                    float remaining = config.demoDurationSeconds - demoUsed;
                    uiBuilder.ShowDemoMode(remaining);
                    break;
                case LicenseState.Licensed:
                    uiBuilder.ShowLicensed();
                    break;
                case LicenseState.Expired:
                    uiBuilder.ShowDemoExpired();
                    break;
                case LicenseState.ClockTampered:
                    uiBuilder.ShowError("Reloj del sistema alterado. Conecta a internet para verificar.");
                    uiBuilder.ShowLicenseGate();
                    break;
            }
        }

        private void HandleDemoExpired()
        {
            SetState(LicenseState.Expired);
            sessionTracker.StopTracking();
            OnDemoExpired?.Invoke();
        }
    }
}
