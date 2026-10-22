using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

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
        ClockTampered,
        /// <summary>
        /// Unlocked by a store subscription verified by the server (v1.12). Equivalent to
        /// <see cref="Licensed"/> for gating; no client branding is applied.
        /// </summary>
        SubscribedViaStore
    }

    /// <summary>What the store currently offers / already granted to the signed-in user.</summary>
    public sealed class StoreOffersResult
    {
        /// <summary>The subscription product with its billing plans (term + localized price), or null.</summary>
        public StoreProductInfo product;
        /// <summary>Configured terms in display order (ANNUAL first).</summary>
        public List<string> terms = new List<string>();
        /// <summary>Active subscription the store already reports for this account, if any.</summary>
        public StorePurchaseInfo existingSubscription;
        public string error;
    }

    /// <summary>
    /// Main orchestrator for the VR licensing system.
    /// Coordinates all subsystems: validation, storage, clock guard, session tracking, demo mode
    /// and, in store builds (v1.12), the store platform (entitlement check + subscriptions).
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
        private Texture2D cachedBrandingLogo;
        private Sprite cachedBrandingSprite;
        private LicenseUIBuilder uiBuilder;
        private float serverReportedDemo; // demo seconds the server already knows about (for additive delta)
        private bool activatingFromCache; // true = re-activating a stored license at startup (vs fresh redemption)
        private int sessionCount;
        private float fpsAccumulator;
        private int fpsFrameCount;
        private bool isLicenseExpiry; // true = license expired, false = demo expired
        private bool isStoreExpiry;   // true = the store subscription cache ran out (v1.12)
        private string licenseInvalidReason; // "expired", "revoked", "suspended"… for the expired panel
        private string lastValidationCode;   // last code from validate_and_bind_license (ok / not_found / revoked / error…)

        // ── Store platform (v1.12) ──
        private IStorePlatformProvider storeProvider;
        private StorePlatform effectiveStorePlatform = StorePlatform.None;
        private string storeInitError;
        private string storeEntitlementError;
        private bool storeInitCompleted;
        private StoreEntitlementData cachedStoreEntitlement;
        private bool activatingStoreFromCache;
        private string storeUserId;                 // Meta user id once the server verified it (phase B)
        private StoreProductInfo storeProduct;          // per-session product cache (SKU + billing plans)
        private readonly List<string> storeEvents = new List<string>(); // pending p_events (phase B)
        private string pendingNotice;               // shown once on the next panel (e.g. offline notice)
        private bool entitlementBlocked;

        private const string SESSION_COUNT_KEY = "vrl_session_count";
        private const float STORE_INIT_TIMEOUT = 10f; // VRC.Quest.Security.1: entitlement within 10 s
        private const float ENTITLEMENT_QUIT_DELAY = 10f;

        // Platform SDK callbacks only arrive while the app runs (Callback.RunCallbacks in Update),
        // so time spent paused behind a system dialog (permissions, "Restore app", Guardian…) must
        // not count against the entitlement deadline — a 15 s dialog at boot used to end in
        // "entitlement check failed → quit". Active time = realtime minus paused time.
        private float pausedAccum;
        private float pausedAt = -1f;
        private float ActiveTime => Time.realtimeSinceStartup - pausedAccum;

        private void OnApplicationPause(bool paused)
        {
            if (paused) { if (pausedAt < 0f) pausedAt = Time.realtimeSinceStartup; }
            else if (pausedAt >= 0f) { pausedAccum += Time.realtimeSinceStartup - pausedAt; pausedAt = -1f; }
        }

        /// <summary>
        /// Current state of the licensing system.
        /// </summary>
        public LicenseState CurrentState { get; private set; } = LicenseState.Unlicensed;

        /// <summary>True in both unlocked states: <see cref="LicenseState.Licensed"/> and <see cref="LicenseState.SubscribedViaStore"/>.</summary>
        public bool IsUnlocked => CurrentState == LicenseState.Licensed || CurrentState == LicenseState.SubscribedViaStore;

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
        /// Promotion reported by the server for this product (null when none is active).
        /// Refreshed on every device status / session report.
        /// </summary>
        public PromoData ActivePromo { get; private set; }

        /// <summary>
        /// The downloaded branding logo as a Texture2D.
        /// Available after OnBrandingLogoReady fires. Null if no logo configured.
        /// </summary>
        public Texture2D BrandingLogoTexture => cachedBrandingLogo;

        /// <summary>
        /// The downloaded branding logo as a Sprite (created from BrandingLogoTexture).
        /// Available after OnBrandingLogoReady fires. Null if no logo configured.
        /// </summary>
        public Sprite BrandingLogoSprite => cachedBrandingSprite;

        /// <summary>
        /// The configuration used by this manager.
        /// </summary>
        public LicenseConfig Config => config;

        /// <summary>
        /// The store platform this build actually behaves as: the configured one when its SDK
        /// is present, otherwise <see cref="StorePlatform.None"/>.
        /// </summary>
        public StorePlatform EffectiveStorePlatform => effectiveStorePlatform;

        /// <summary>True when subscription plans can be offered (storePlatform resolved to MetaHorizon).</summary>
        public bool StoreOffersEnabled => effectiveStorePlatform == StorePlatform.MetaHorizon;

        /// <summary>Store provider in use, or null when <see cref="EffectiveStorePlatform"/> is None.</summary>
        public IStorePlatformProvider StoreProvider => storeProvider;

        /// <summary>Result of the store app-ownership check: true/false once done, null while unknown.</summary>
        public bool? StoreEntitled => storeProvider != null ? storeProvider.IsEntitled : null;

        /// <summary>The server-verified store subscription unlocking the app (null otherwise).</summary>
        public StoreEntitlementData ActiveStoreEntitlement => cachedStoreEntitlement;

        /// <summary>Privacy device code of this headset (XXXXX-XXXXX), see <see cref="VRLicensing.DeviceCode"/>.</summary>
        public string DeviceCode => VRLicensing.DeviceCode.Current;

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

        /// <summary>
        /// Fired when the branding logo image has been downloaded and is ready as Texture2D/Sprite.
        /// Parameters: (Texture2D logoTexture, Sprite logoSprite). Both null if no logo configured.
        /// </summary>
        public event Action<Texture2D, Sprite> OnBrandingLogoReady;

        /// <summary>Fired once the store app-ownership (entitlement) check finished: (entitled, error).</summary>
        public event Action<bool, string> OnStoreEntitlementChecked;

        /// <summary>Fired when a server-verified store subscription unlocks the app (fresh or from cache).</summary>
        public event Action<StoreEntitlementData> OnStoreSubscriptionActivated;

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

            // Resolve the store platform (v1.12). Without the store SDK the build behaves as None.
            ResolveStoreProvider();

            // Development builds only: the raw id the RPCs send and the privacy code derived from
            // it, so the server-side hash (over device_registry.device_unique_id) can be checked
            // character by character against a real headset. Never logged in release builds.
            if (Debug.isDebugBuild)
                Debug.Log($"[VR Licensing] dev: device id '{VRLicensing.DeviceCode.RawDeviceId}' → device code {VRLicensing.DeviceCode.Current}");

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

        private void ResolveStoreProvider()
        {
            effectiveStorePlatform = StorePlatformRegistry.Effective(config.storePlatform);
            if (config.storePlatform != StorePlatform.None && effectiveStorePlatform == StorePlatform.None)
                Debug.LogWarning("[VR Licensing] " + StorePlatformRegistry.UnavailableReason(config.storePlatform));

            storeProvider = effectiveStorePlatform != StorePlatform.None
                ? StorePlatformRegistry.Create(effectiveStorePlatform)
                : null;

            if (storeProvider != null)
                Debug.Log($"[VR Licensing] Store platform: {effectiveStorePlatform} " +
                          $"(subscription SKU {config.storeSubscriptionSku}; terms " +
                          $"{string.Join(", ", StoreSubscription.OrderedTerms(config.storeTerms))}" +
                          $"{(StoreVerification.StubVerifier != null ? "; EDITOR STUB verification" : "")})");
        }

        /// <summary>
        /// Main licensing flow executed on initialization.
        /// Now includes integrity checks and server-side device verification.
        /// </summary>
        private IEnumerator StartLicensingFlow()
        {
            Debug.Log("[VR Licensing] Starting licensing flow...");

            // Assume the server already knows the local demo total; GetDeviceStatus (demo
            // path only) refines this to the real server value so the reported delta is
            // correct. In licensed paths this keeps the startup report a no-op touch.
            serverReportedDemo = SecureLicenseStorage.GetDemoUsedSeconds();

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

            // Step 1b (v1.12): store platform init + app-ownership check, within 10 s.
            // Never blocks builds that are not store builds (storeProvider == null).
            yield return InitializeStorePlatform();

            if (entitlementBlocked)
            {
                // Only with LicenseConfig.enforceEntitlement = true (published store builds).
                SetState(LicenseState.Unlicensed, forceUpdate: true);
                uiBuilder?.ShowEntitlementBlocked(
                    "This app is not available for the signed-in Meta account.\n" +
                    "Please install it from the Meta Horizon Store with the account that owns it.");
                StartCoroutine(QuitAfter(ENTITLEMENT_QUIT_DELAY));
                yield break;
            }

            // Step 2: Try to load cached license
            cachedLicense = SecureLicenseStorage.LoadLicenseData();

            if (cachedLicense != null && cachedLicense.IsValid)
            {
                Debug.Log($"[VR Licensing] Cached license valid. Expires: {cachedLicense.expires_at}");
                activatingFromCache = true; // already-licensed device — show the "all good" notice
                ActivateLicense(cachedLicense);
                yield break;
            }

            // Step 2b (v1.12): cached store subscription, then (phase B) online verification.
            bool storeUnlocked = false;
            yield return TryUnlockFromStore(result => storeUnlocked = result);
            if (storeUnlocked) yield break;

            if (cachedLicense != null && !cachedLicense.IsValid)
            {
                // Any status other than "active" (expired, revoked, suspended, unknown…) lands here.
                licenseInvalidReason = cachedLicense.InvalidReason;
                Debug.Log($"[VR Licensing] Cached license not valid ({licenseInvalidReason}).");
                isLicenseExpiry = true;
                isStoreExpiry = false;
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
            bool serverDemoBlocked = false;
            float serverDemoUsed = 0f;

            yield return supabaseClient.GetDeviceStatus(
                config.productId,
                storeUserId, // null until the server verified a store account (phase B)
                (status) => {
                    if (status != null && status.found)
                    {
                        serverDemoBlocked = status.demo_blocked;
                        serverDemoUsed = status.demo_used_seconds;
                        serverReportedDemo = status.demo_used_seconds;
                        ApplyServerDemoReset(status);
                        ActivePromo = (status.promo != null && status.promo.IsValid) ? status.promo : null;

                        // If server has more demo time used than local, trust the server
                        float localDemoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
                        if (status.demo_used_seconds > localDemoUsed)
                        {
                            Debug.Log($"[VR Licensing] Server demo time ({status.demo_used_seconds}s) > " +
                                $"local ({localDemoUsed}s). Using server value.");
                            SecureLicenseStorage.SetDemoUsedSeconds(status.demo_used_seconds);
                        }

                        Debug.Log($"[VR Licensing] Server device check: demo_used={status.demo_used_seconds}s, " +
                            $"blocked={status.demo_blocked}, sessions={status.session_count}");
                    }
                },
                (error) => {
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
                isStoreExpiry = false;
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
                isStoreExpiry = false;
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

        #region Store platform (v1.12)

        /// <summary>
        /// Platform SDK init + app-ownership (entitlement) check with one shared 10 s budget.
        /// Failures are logged and, unless <see cref="LicenseConfig.enforceEntitlement"/> is on,
        /// the app simply continues in demo / license-key mode (development default).
        /// </summary>
        private IEnumerator InitializeStorePlatform()
        {
            if (storeProvider == null) yield break;

            float deadline = ActiveTime + STORE_INIT_TIMEOUT;

            bool initDone = false, initOk = false;
            string initError = null;
            Coroutine initCo = StartCoroutine(storeProvider.Initialize((ok, err) => { initDone = true; initOk = ok; initError = err; }));
            while (!initDone && ActiveTime < deadline) yield return null;

            if (!initDone)
            {
                StopCoroutine(initCo);
                storeInitError = $"platform init timed out after {STORE_INIT_TIMEOUT:F0} s";
            }
            else if (!initOk)
            {
                storeInitError = initError ?? "platform init failed";
            }

            if (storeInitError != null)
            {
                Debug.LogWarning($"[VR Licensing] Store platform ({effectiveStorePlatform}) unavailable: {storeInitError}. " +
                                 "Continuing without store features (no subscriptions offered this session).");
                storeInitCompleted = true;
                storeEntitlementError = storeInitError;
                FinishEntitlement(false, storeInitError);
                yield break;
            }

            bool entDone = false, entOk = false;
            string entError = null;
            Coroutine entCo = StartCoroutine(storeProvider.CheckEntitlement((ok, err) => { entDone = true; entOk = ok; entError = err; }));
            while (!entDone && ActiveTime < deadline) yield return null;

            if (!entDone)
            {
                StopCoroutine(entCo);
                entError = $"entitlement check timed out after {STORE_INIT_TIMEOUT:F0} s";
            }

            storeInitCompleted = true;
            if (entOk)
            {
                Debug.Log($"[VR Licensing] Store entitlement check passed ({Time.realtimeSinceStartup:F1} s since start, {pausedAccum:F1} s of it paused).");
                FinishEntitlement(true, null);
            }
            else
            {
                storeEntitlementError = entError ?? "not entitled";
                Debug.LogWarning($"[VR Licensing] Store entitlement check FAILED: {storeEntitlementError}. " +
                                 (config.enforceEntitlement
                                     ? "enforceEntitlement is ON — locking the app."
                                     : "enforceEntitlement is OFF — continuing in demo / license-key mode " +
                                       "(expected for sideloaded APKs on accounts that are not org members or test users)."));
                FinishEntitlement(false, storeEntitlementError);
            }
        }

        private void FinishEntitlement(bool entitled, string error)
        {
            entitlementBlocked = !entitled && config.enforceEntitlement;
            OnStoreEntitlementChecked?.Invoke(entitled, error);
        }

        private IEnumerator QuitAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Debug.LogError("[VR Licensing] Entitlement check failed and enforceEntitlement is ON — quitting.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Spec §1.4, run on EVERY launch with a store platform, before the demo can start (a
        /// user may have subscribed from the store page before installing, and a subscriber must
        /// never see the countdown or the paywall):
        ///   cached store entitlement still valid → unlock;
        ///   otherwise ask the store whether the account holds the subscription and, when it does
        ///   (or a stale cache existed), ask the server to verify it. The client never unlocks on
        ///   the store's word alone. No network with a stale cache → locked with the offline notice.
        /// </summary>
        private IEnumerator TryUnlockFromStore(Action<bool> onDone)
        {
            if (storeProvider == null || !storeProvider.IsInitialized)
            {
                onDone(false);
                yield break;
            }

            StoreEntitlementData cached = SecureLicenseStorage.LoadStoreEntitlement();
            bool hadStaleCache = false;

            if (cached != null)
            {
                if (cached.IsCacheValid && cached.product_id == config.productId)
                {
                    Debug.Log($"[VR Licensing] Cached store subscription valid ({cached.sku} {cached.term}, {cached.status}, " +
                              $"cache until {cached.cache_until}).");
                    activatingStoreFromCache = true;
                    ActivateStoreSubscription(cached);
                    onDone(true);
                    yield break;
                }

                Debug.Log("[VR Licensing] Cached store subscription needs re-verification " +
                          $"(cache until {cached.cache_until}, status {cached.status}).");
                hadStaleCache = true;
                SecureLicenseStorage.ClearStoreEntitlement();
            }

            // Does the signed-in store account hold the subscription (bought in-app or from the
            // store page)? Only a store call; cheap and allowed on every launch.
            StorePurchaseInfo storeSays = null;
            string storeError = null;
            yield return storeProvider.GetActiveSubscription(config.storeSubscriptionSku,
                (purchase, err) => { storeSays = purchase; storeError = err; });
            if (storeError != null)
                Debug.LogWarning($"[VR Licensing] Could not read the account's purchases at boot: {storeError}");

            if (storeSays == null && !hadStaleCache)
            {
                onDone(false); // nothing to verify: demo / license-key flow as usual
                yield break;
            }

            if (storeSays != null)
                Debug.Log($"[VR Licensing] Store reports an active subscription ({storeSays.sku} {storeSays.term}) — verifying with the server.");

            bool ok = false;
            string verifyError = null;
            yield return VerifyStoreEntitlementOnline((success, error) => { ok = success; verifyError = error; }, silent: true);
            if (ok)
            {
                onDone(true);
                yield break;
            }

            Debug.LogWarning($"[VR Licensing] Store subscription could not be verified with the server: {verifyError}");
            pendingNotice = "An internet connection is needed to verify your subscription.";
            onDone(false);
        }

        private void ActivateStoreSubscription(StoreEntitlementData data)
        {
            cachedStoreEntitlement = data;
            if (!string.IsNullOrEmpty(data.store_user_id)) storeUserId = data.store_user_id;
            demoManager.StopDemo();
            plansPausedDemo = false;
            SetState(LicenseState.SubscribedViaStore);
            sessionTracker.StartTracking();
            clockGuard.UpdateHighestKnownTime();
            OnStoreSubscriptionActivated?.Invoke(data);

            Debug.Log($"[VR Licensing] Store subscription ACTIVE — {data.sku} {data.term} ({data.status}), " +
                      $"expires {data.expires_at}, offline cache until {data.cache_until}");

            // No branding for individual subscribers. Report the session (and pending events).
            StartCoroutine(ReportDemoUsage(null));
        }

        /// <summary>
        /// What to show on the subscription column: the viewer's existing subscription (if the
        /// store already reports one) and the subscription product with its billing plans (term +
        /// store-localized price). The product is cached per session.
        /// </summary>
        public IEnumerator RefreshStoreOffers(Action<StoreOffersResult> onDone)
        {
            var result = new StoreOffersResult();

            if (storeProvider == null || !storeProvider.IsInitialized)
            {
                result.error = storeInitError ?? "store platform not initialized";
                onDone?.Invoke(result);
                yield break;
            }

            string sku = (config.storeSubscriptionSku ?? "").Trim();
            if (sku.Length == 0)
            {
                result.error = "no subscription SKU configured in LicenseConfig.storeSubscriptionSku";
                onDone?.Invoke(result);
                yield break;
            }

            // Meta requirement: check the viewer's current subscription BEFORE offering plans.
            yield return storeProvider.GetActiveSubscription(sku, (purchase, err) =>
            {
                result.existingSubscription = purchase;
                if (err != null) Debug.LogWarning($"[VR Licensing] Could not read store purchases: {err}");
            });

            if (storeProduct == null)
            {
                StoreProductInfo fetched = null;
                string fetchError = null;
                yield return storeProvider.GetSubscriptionProduct(sku, (product, err) => { fetched = product; fetchError = err; });
                if (fetchError != null) result.error = fetchError;
                else storeProduct = fetched;
            }

            result.product = storeProduct;
            result.terms = StoreSubscription.OrderedTerms(config.storeTerms);
            onDone?.Invoke(result);
        }

        /// <summary>
        /// Opens the store checkout for one term of the subscription SKU, then asks the server
        /// to verify the subscription and unlocks. Result: (success, message). A dismissed
        /// checkout is reported as (false, "Checkout cancelled.").
        /// </summary>
        public void SubscribeViaStore(string term, Action<bool, string> onResult)
        {
            if (storeProvider == null || !storeProvider.IsInitialized)
            {
                onResult?.Invoke(false, "The store is not available right now.");
                return;
            }
            StartCoroutine(SubscribeViaStoreRoutine(term, onResult));
        }

        private IEnumerator SubscribeViaStoreRoutine(string term, Action<bool, string> onResult)
        {
            term = StoreSubscription.NormalizeTerm(term);
            string checkoutSku = StoreSubscription.CheckoutSku(config.storeSubscriptionSku, term);
            QueueStoreEvent("checkout_opened", term);

            StoreCheckoutResult checkout = null;
            yield return storeProvider.LaunchCheckout(checkoutSku, r => checkout = r);

            if (checkout == null || checkout.outcome == StoreCheckoutOutcome.Cancelled)
            {
                QueueStoreEvent("checkout_cancelled", term);
                Debug.Log($"[VR Licensing] Store checkout cancelled ({checkoutSku}).");
                onResult?.Invoke(false, "Checkout cancelled.");
                yield break;
            }

            if (checkout.outcome == StoreCheckoutOutcome.Failed)
            {
                QueueStoreEvent("checkout_cancelled", term);
                Debug.LogWarning($"[VR Licensing] Store checkout failed ({checkoutSku}): {checkout.error}");
                onResult?.Invoke(false, "Checkout failed: " + checkout.error);
                yield break;
            }

            QueueStoreEvent("checkout_completed", term);
            Debug.Log($"[VR Licensing] Store checkout completed ({checkoutSku}, purchase {checkout.purchase?.purchaseId}).");

            yield return VerifyStoreEntitlementOnline(onResult, silent: false);
        }

        /// <summary>
        /// Re-verifies an existing store subscription with the server (used when the store
        /// already reports one for this account, or after the offline cache ran out).
        /// </summary>
        public void VerifyStoreSubscription(Action<bool, string> onResult)
        {
            StartCoroutine(VerifyStoreEntitlementOnline(onResult, silent: false));
        }

        /// <summary>
        /// Server-authoritative verification (spec §1.1): store user id + one-time proof →
        /// verify-store-entitlement → encrypted cache → <see cref="LicenseState.SubscribedViaStore"/>.
        /// In the editor, <see cref="StoreVerification.StubVerifier"/> may stand in for the server.
        /// </summary>
        private IEnumerator VerifyStoreEntitlementOnline(Action<bool, string> onResult, bool silent)
        {
            if (storeProvider == null || !storeProvider.IsInitialized)
            {
                onResult?.Invoke(false, "The store is not available right now.");
                yield break;
            }

            string userId = null, nonce = null, identityError = null;
            yield return storeProvider.GetUserIdentity((u, n, e) => { userId = u; nonce = n; identityError = e; });

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(nonce))
            {
                if (!silent) Debug.LogWarning($"[VR Licensing] Could not identify the store account: {identityError}");
                onResult?.Invoke(false, "Could not identify your Meta account. Check your connection and try again.");
                yield break;
            }

            StoreVerifyResult verify = null;
            string verifyError = null;
            if (StoreVerification.StubVerifier != null)
            {
                verify = StoreVerification.StubVerifier(userId, nonce, config.productId);
                if (verify == null) verifyError = "editor stub: server down";
                yield return null;
            }
            else
            {
                yield return supabaseClient.VerifyStoreEntitlement(
                    userId, nonce, config.productId, VRLicensing.DeviceCode.RawDeviceId, storeProvider.PlatformId,
                    r => verify = r, e => verifyError = e);
            }

            if (verify == null)
            {
                if (!silent) Debug.LogWarning($"[VR Licensing] Store verification failed: {verifyError}");
                onResult?.Invoke(false, "Could not verify the subscription. Check your connection and try again.");
                yield break;
            }

            storeUserId = userId;

            if (!verify.entitled)
            {
                SecureLicenseStorage.ClearStoreEntitlement();
                Debug.Log($"[VR Licensing] Server reports no active subscription for this account (status: {verify.status}).");
                onResult?.Invoke(false, "No active subscription was found for this Meta account.");
                yield break;
            }

            var data = StoreEntitlementData.FromServer(verify, storeProvider.PlatformId, userId,
                config.productId, config.maxOfflineHours);
            SecureLicenseStorage.SaveStoreEntitlement(data);
            QueueStoreEvent("subscription_verified", verify.term);

            activatingStoreFromCache = false;
            ActivateStoreSubscription(data);
            onResult?.Invoke(true, null);
        }

        /// <summary>
        /// Queues a checkout analytics event for the next report_device_session (p_events).
        /// Data rules (spec §1.4): name, SKU, term and time only — never the Meta user id.
        /// </summary>
        private void QueueStoreEvent(string name, string term)
        {
            string at = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            storeEvents.Add("{\"name\":\"" + name + "\",\"sku\":\"" + (config.storeSubscriptionSku ?? "") +
                            "\",\"term\":\"" + (term ?? "") + "\",\"at\":\"" + at + "\"}");
            if (storeEvents.Count > 50) storeEvents.RemoveAt(0);
        }

        /// <summary>Returns the pending events as a JSON array (and clears them), or null.</summary>
        private string DrainStoreEvents()
        {
            if (storeEvents.Count == 0) return null;
            string json = "[" + string.Join(",", storeEvents) + "]";
            storeEvents.Clear();
            return json;
        }

        #endregion

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
            LicenseData validLicense = null;
            string errorMessage = null;

            // Validation AND device binding happen atomically server-side via RPC.
            // The server rejects keys bound to another headset (code: bound_other_device).
            lastValidationCode = null;
            yield return supabaseClient.ValidateAndBindLicense(
                licenseKey,
                config.productId,
                SystemInfo.deviceUniqueIdentifier,
                (license) => { validLicense = license; lastValidationCode = "ok"; },
                (code, error) => { errorMessage = error; lastValidationCode = code ?? "error"; }
            );

            if (validLicense == null)
            {
                OnValidationError?.Invoke(errorMessage ?? "Unknown error");
                onResult?.Invoke(false, errorMessage ?? "Error validating the license.");
                yield break;
            }

            // Defensive: the server should only return active licenses, but a record whose
            // status is anything else (revoked, suspended, a status added later…) must never
            // unlock. Treat it exactly like a rejection, with the status as the reason.
            if (!validLicense.IsValid)
            {
                string reason = validLicense.InvalidReason;
                lastValidationCode = reason;
                string msg = $"This license is not active ({reason}). Ask your organisation for a new key.";
                Debug.LogWarning($"[VR Licensing] Server returned a non-active license ({reason}) — rejected.");
                OnValidationError?.Invoke(msg);
                onResult?.Invoke(false, msg);
                yield break;
            }

            // License valid and bound to this device — activate
            SecureLicenseStorage.SaveLicenseData(validLicense);
            demoManager.StopDemo();
            activatingFromCache = false; // fresh key redemption — show the "redeemed" notice
            ActivateLicense(validLicense);
            onResult?.Invoke(true, null);
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
            StartCoroutine(FetchAndCacheBranding(license));

            // Send device telemetry (fire-and-forget, non-blocking)
            StartCoroutine(SendSessionTelemetry(license));

            // Report session to server (delta since last report; refreshes last_license_key)
            StartCoroutine(ReportDemoUsage(license.license_key));
        }

        /// <summary>
        /// Fetches branding set by the client on the web portal and caches it.
        /// v1.12: through the get_license_branding RPC; falls back to the direct table read
        /// while the function is not deployed. Other scripts can read
        /// LicenseManager.ActiveBranding at any time.
        /// </summary>
        private IEnumerator FetchAndCacheBranding(LicenseData license)
        {
            bool functionMissing = false;
            bool handled = false;

            yield return supabaseClient.FetchBrandingRpc(
                license.license_key, config.productId, SystemInfo.deviceUniqueIdentifier,
                (branding) => { handled = true; ApplyBranding(branding); },
                (missing, error) =>
                {
                    functionMissing = missing;
                    if (!missing)
                    {
                        handled = true;
                        Debug.LogWarning($"[VR Licensing] Could not fetch branding: {error}");
                        ApplyBranding(null);
                    }
                });

            if (handled) yield break;

            if (functionMissing)
                Debug.Log("[VR Licensing] get_license_branding not deployed yet — falling back to the direct read.");

            yield return supabaseClient.FetchBranding(
                license.id,
                ApplyBranding,
                (error) =>
                {
                    Debug.LogWarning($"[VR Licensing] Could not fetch branding: {error}");
                    ApplyBranding(null);
                }
            );
        }

        private void ApplyBranding(BrandingData branding)
        {
            cachedBranding = branding;
            OnBrandingLoaded?.Invoke(branding);

            if (branding != null && branding.HasBranding)
            {
                Debug.Log($"[VR Licensing] Client branding: \"{branding.brand_name}\"");

                // Download logo image if URL is provided
                if (!string.IsNullOrEmpty(branding.logo_url))
                    StartCoroutine(DownloadBrandingLogo(branding.logo_url));
                else
                    OnBrandingLogoReady?.Invoke(null, null);
            }
            else
            {
                Debug.Log("[VR Licensing] No client branding configured — using defaults.");
                OnBrandingLogoReady?.Invoke(null, null);
            }
        }

        /// <summary>
        /// Downloads the branding logo from the URL and caches it as Texture2D and Sprite.
        /// Fires OnBrandingLogoReady when done.
        /// </summary>
        private IEnumerator DownloadBrandingLogo(string logoUrl)
        {
            Debug.Log($"[VR Licensing] Downloading branding logo from: {logoUrl}");

            using (var request = UnityWebRequestTexture.GetTexture(logoUrl))
            {
                request.timeout = 15;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VR Licensing] Failed to download branding logo: {request.error}");
                    cachedBrandingLogo = null;
                    cachedBrandingSprite = null;
                    OnBrandingLogoReady?.Invoke(null, null);
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                texture.name = "BrandingLogo";

                // Create a Sprite from the Texture2D
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                sprite.name = "BrandingLogoSprite";

                cachedBrandingLogo = texture;
                cachedBrandingSprite = sprite;

                Debug.Log($"[VR Licensing] Branding logo downloaded: {texture.width}x{texture.height}px");

                OnBrandingLogoReady?.Invoke(texture, sprite);
            }
        }

        /// <summary>
        /// Collects device data and sends it to the telemetry table.
        /// v1.12: through the report_telemetry RPC, with a fallback to the direct insert while
        /// the function is not deployed. Store-subscribed sessions (no key) do not call this;
        /// they are covered by report_device_session.
        /// </summary>
        private IEnumerator SendSessionTelemetry(LicenseData license)
        {
            float demoUsed = SecureLicenseStorage.GetDemoUsedSeconds();
            float avgFps = fpsFrameCount > 0 ? fpsAccumulator / fpsFrameCount : 0f;

            var payload = TelemetryCollector.Collect(
                licenseId: license.id,
                sessionDuration: sessionTracker != null ? SecureLicenseStorage.GetSessionUsedSeconds() : 0f,
                demoUsed: demoUsed,
                sessionCount: sessionCount,
                avgFps: avgFps
            );

            bool fallback = false;
            yield return supabaseClient.SendTelemetryRpc(
                license.license_key, config.productId, SystemInfo.deviceUniqueIdentifier, payload,
                () => fallback = true);

            if (fallback)
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
                    licenseInvalidReason = cachedLicense.InvalidReason;
                    Debug.Log($"[VR Licensing] License stopped being valid during the session ({licenseInvalidReason}).");
                    isLicenseExpiry = true;
                    isStoreExpiry = false;
                    sessionTracker.StopTracking();
                    SetState(LicenseState.Expired);
                    OnLicenseExpired?.Invoke();
                    return;
                }

                // Update clock guard periodically (every frame is fine, it's cheap)
                clockGuard.UpdateHighestKnownTime();
            }

            if (CurrentState == LicenseState.SubscribedViaStore && cachedStoreEntitlement != null)
            {
                // The offline cache ran out (term ended, or maxOfflineHours without the server).
                if (!cachedStoreEntitlement.IsCacheValid)
                {
                    Debug.Log("[VR Licensing] Store subscription cache expired during session — re-verification needed.");
                    isLicenseExpiry = true;
                    isStoreExpiry = true;
                    sessionTracker.StopTracking();
                    cachedStoreEntitlement = null;
                    SecureLicenseStorage.ClearStoreEntitlement();
                    SetState(LicenseState.Expired);
                    OnLicenseExpired?.Invoke();
                    return;
                }

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
                isLicenseExpiry = false;
                isStoreExpiry = false;
                SetState(LicenseState.Expired);
                OnDemoExpired?.Invoke();
                return;
            }

            Debug.Log($"[VR Licensing] Starting demo mode. Used: {demoUsed:F0}s / {config.demoDurationSeconds:F0}s");
            SetState(LicenseState.Demo);
            demoManager.StartDemo(config.demoDurationSeconds);
            sessionTracker.StartTracking();
        }

        // ─────────────── Plans before / during the demo (v1.12.0-pre.8) ───────────────

        private bool plansPausedDemo;

        /// <summary>
        /// Opens the subscription plans panel (store builds only). Allowed while
        /// <see cref="LicenseState.Unlicensed"/> (welcome panel) and <see cref="LicenseState.Demo"/>
        /// (e.g. from the game's pause menu). During the demo the stopwatch is paused until
        /// <see cref="ClosePlans"/>; a purchase ends the demo through the normal
        /// <see cref="LicenseState.SubscribedViaStore"/> path. No-op in any other state or
        /// without a store platform, so projects can wire a button unconditionally.
        /// </summary>
        public void ShowPlans()
        {
            if (!StoreOffersEnabled || uiBuilder == null)
            {
                Debug.Log("[VR Licensing] ShowPlans ignored — no store platform or UI.");
                return;
            }
            if (CurrentState != LicenseState.Unlicensed && CurrentState != LicenseState.Demo)
            {
                Debug.Log($"[VR Licensing] ShowPlans ignored in state {CurrentState}.");
                return;
            }

            bool fromDemo = CurrentState == LicenseState.Demo;
            if (fromDemo && demoManager != null && !plansPausedDemo)
            {
                demoManager.StopDemo(); // persists the time used so far and stops the stopwatch
                plansPausedDemo = true;
                Debug.Log("[VR Licensing] Plans opened during the demo — demo paused.");
            }
            uiBuilder.ShowPlans(fromDemo);
        }

        /// <summary>
        /// Closes the plans panel without buying: back to the welcome panel, or back into the
        /// demo with the stopwatch running again (the remaining time is exactly what it was).
        /// </summary>
        public void ClosePlans()
        {
            if (uiBuilder == null) return;
            uiBuilder.HidePlans();

            if (plansPausedDemo)
            {
                plansPausedDemo = false;
                if (CurrentState == LicenseState.Demo && demoManager != null)
                {
                    Debug.Log("[VR Licensing] Plans closed — demo resumed.");
                    SetState(LicenseState.Demo, forceUpdate: true);     // modal hidden, HUD back
                    demoManager.StartDemo(config.demoDurationSeconds); // re-checks the limit
                }
                return;
            }

            if (CurrentState == LicenseState.Unlicensed) uiBuilder.ShowWelcome();
        }

        /// <summary>
        /// Watchdog entry point (called by the UI, which lives on another GameObject): the demo
        /// limit has been crossed but the demo is still in <see cref="LicenseState.Demo"/>. Ends
        /// it through <see cref="DemoModeManager.ForceExpire"/>, which raises OnDemoExpired and
        /// therefore the normal expired-panel path.
        /// </summary>
        public void ForceDemoExpiry(string reason)
        {
            if (CurrentState != LicenseState.Demo || demoManager == null) return;
            Debug.LogWarning($"[VR Licensing] Demo limit crossed without expiry event — forcing ({reason}).");
            demoManager.ForceExpire(reason);
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
                    if (success) return;

                    // "error" = could not reach / parse the server: keep the cached license.
                    // Anything else is the server's verdict (revoked, expired, bound elsewhere,
                    // not found…): the cached license must stop unlocking the app right away.
                    if (lastValidationCode == null || lastValidationCode == "error")
                    {
                        Debug.LogWarning($"[VR Licensing] Re-validation could not complete: {error}");
                        return;
                    }

                    Debug.LogWarning($"[VR Licensing] Server rejected the cached license ({lastValidationCode}): {error}");
                    RevokeCachedLicense(lastValidationCode, error);
                }));
            }
            else if (CurrentState == LicenseState.SubscribedViaStore)
            {
                VerifyStoreSubscription((success, error) =>
                {
                    if (!success) Debug.LogWarning($"[VR Licensing] Store re-verification failed: {error}");
                });
            }
        }

        /// <summary>
        /// The server said the cached license is no longer good (revoked, expired, re-bound…):
        /// drop it and show the license-expired panel with the server's message.
        /// </summary>
        private void RevokeCachedLicense(string code, string serverMessage)
        {
            sessionTracker.StopTracking();
            SecureLicenseStorage.ClearAll();
            SecureLicenseStorage.MarkInitialized();
            cachedLicense = null;
            cachedBranding = null;
            isLicenseExpiry = true;
            isStoreExpiry = false;
            licenseInvalidReason = code;
            pendingNotice = string.IsNullOrEmpty(serverMessage) ? null : serverMessage;
            SetState(LicenseState.Expired, forceUpdate: true);
            OnLicenseExpired?.Invoke();
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
            cachedStoreEntitlement = null;
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

            // The demo countdown HUD is only visible while in Demo state.
            if (state != LicenseState.Demo) uiBuilder.HideDemoTimer();

            switch (state)
            {
                case LicenseState.Unlicensed:
                    uiBuilder.ShowWelcome();
                    break;
                case LicenseState.Demo:
                    uiBuilder.HideAll();       // modal hidden, user is in the simulator
                    uiBuilder.ShowDemoTimer(); // ...but keep the countdown on screen
                    break;
                case LicenseState.Licensed:
                    uiBuilder.ShowLicensed(activatingFromCache);
                    break;
                case LicenseState.SubscribedViaStore:
                    uiBuilder.ShowSubscribed(activatingStoreFromCache);
                    break;
                case LicenseState.Expired:
                    if (isLicenseExpiry)
                        uiBuilder.ShowLicenseExpired(isStoreExpiry, licenseInvalidReason);
                    else
                        uiBuilder.ShowDemoExpired();
                    break;
                case LicenseState.ClockTampered:
                    uiBuilder.ShowError("System clock has been tampered with. Connect to the internet to verify.");
                    uiBuilder.ShowWelcome();
                    break;
            }

            // One-shot notice (e.g. "internet needed to verify your subscription") on the panel
            // the user is now looking at.
            if (pendingNotice != null && (state == LicenseState.Unlicensed || state == LicenseState.Expired))
            {
                uiBuilder.ShowNotice(pendingNotice);
                pendingNotice = null;
            }
        }

        private void HandleDemoExpired()
        {
            isLicenseExpiry = false;
            isStoreExpiry = false;
            SetState(LicenseState.Expired);
            sessionTracker.StopTracking();
            // Push the demo time consumed this session to the server immediately.
            StartCoroutine(ReportDemoUsage(cachedLicense?.license_key));
            OnDemoExpired?.Invoke();
        }

        /// <summary>
        /// Sends telemetry on every app start (even in demo mode).
        /// If licenseId is null, sends with empty license reference.
        /// </summary>
        private IEnumerator SendStartupTelemetry(string licenseId)
        {
            // Report the startup session to the server (delta since last report).
            // This also upserts the device record (last_seen, model, etc.).
            yield return ReportDemoUsage(cachedLicense?.license_key);
        }

        /// <summary>
        /// v1.12.0-pre.7 - admin demo reset. The server only ever ADDS usage (contract section 2),
        /// so a reset from /admin/devices is signalled by a newer <c>demo_reset_at</c>: the first
        /// time a given date is seen, the server's (lower) total is adopted instead of being
        /// treated as "server behind local" and re-pushed as a delta. The date is remembered
        /// (encrypted, sealed) so the same reset is never applied twice. Servers without the
        /// field change nothing. Returns true when the local total was lowered to the server's.
        /// </summary>
        private bool ApplyServerDemoReset(DeviceStatus status)
        {
            if (status == null || string.IsNullOrEmpty(status.demo_reset_at)) return false;
            string applied = SecureLicenseStorage.GetDemoResetMarker();
            if (applied == status.demo_reset_at) return false;

            float local = SecureLicenseStorage.GetDemoUsedSeconds();
            if (demoManager != null && demoManager.CurrentSessionDemoSeconds > 0f)
                local += demoManager.CurrentSessionDemoSeconds;

            bool lowered = status.demo_used_seconds < local;
            if (lowered)
            {
                Debug.Log($"[VR Licensing] Admin demo reset ({status.demo_reset_at}) applied: " +
                          $"{local:F0}s -> {status.demo_used_seconds:F0}s.");
                if (demoManager != null) demoManager.AdoptServerTotal(status.demo_used_seconds);
                else SecureLicenseStorage.SetDemoUsedSeconds(status.demo_used_seconds);
            }
            SecureLicenseStorage.SetDemoResetMarker(status.demo_reset_at);
            return lowered;
        }

        /// <summary>
        /// Reports demo usage to the server as a positive delta (server ADDS it and
        /// never lowers the counter). Keeps <see cref="serverReportedDemo"/> in sync so
        /// repeated calls only send newly-consumed time, and adopts the server total if
        /// it is ahead (e.g. usage from another session / anti-wipe).
        /// v1.12: also flushes pending store analytics events (p_events, phase B).
        /// </summary>
        private IEnumerator ReportDemoUsage(string lastLicenseKey)
        {
            float localDemo = SecureLicenseStorage.GetDemoUsedSeconds();
            float delta = Mathf.Max(0f, localDemo - serverReportedDemo);

            yield return supabaseClient.ReportDeviceSession(
                config.productId, delta, lastLicenseKey,
                (status) =>
                {
                    if (status != null)
                    {
                        serverReportedDemo = status.demo_used_seconds;
                        ActivePromo = (status.promo != null && status.promo.IsValid) ? status.promo : null;
                        if (ApplyServerDemoReset(status)) localDemo = status.demo_used_seconds;
                        if (status.demo_used_seconds > localDemo)
                            SecureLicenseStorage.SetDemoUsedSeconds(status.demo_used_seconds);
                    }
                },
                (error) => { Debug.LogWarning($"[VR Licensing] Session report failed: {error}"); },
                DrainStoreEvents()
            );
        }
    }
}
