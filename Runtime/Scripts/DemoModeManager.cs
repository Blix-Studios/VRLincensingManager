using System;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Manages the demo mode timer. Accumulates playtime across sessions
    /// and fires an event when the demo limit is reached.
    /// v1.12.0-pre.16 (spec 11 §6): the time comes from <see cref="DemoStopwatch"/>, fed one
    /// frame at a time, so it only advances while the demo is really being played — not while
    /// the app is paused or unfocused, the headset is off, or frames have stopped — and it
    /// stops at the limit. Until pre.15 it was "now minus session start": a headset taken off
    /// mid-demo kept counting and reported the whole gap (118 of 15 minutes seen in the field).
    /// </summary>
    public class DemoModeManager : MonoBehaviour
    {
        private readonly DemoStopwatch stopwatch = new DemoStopwatch();
        private float demoLimitSeconds;
        private bool isActive;

        private bool appPaused;
        private bool lastCounting = true;
        private string lastHoldReason;

        /// <summary>
        /// Fired when the demo time limit is reached.
        /// </summary>
        public event Action OnDemoExpired;

        /// <summary>
        /// Tells whether the user is wearing the headset. Null = ask the XR head device
        /// (<c>CommonUsages.userPresence</c>; "present" when the runtime does not report it).
        /// Hosts and tests can replace it.
        /// </summary>
        public Func<bool> UserPresenceProvider;

        /// <summary>True between <see cref="StartDemo"/> and <see cref="StopDemo"/> / expiry.</summary>
        public bool IsRunning => isActive;

        /// <summary>
        /// Total demo time used across all sessions (in seconds). Never above the limit unless
        /// the stored total already was (older builds, a lowered limit).
        /// </summary>
        public float TotalDemoUsedSeconds => isActive
            ? stopwatch.Total(SecureLicenseStorage.GetDemoUsedSeconds(), demoLimitSeconds)
            : SecureLicenseStorage.GetDemoUsedSeconds();

        /// <summary>
        /// Demo time used in the current session only.
        /// </summary>
        public float CurrentSessionDemoSeconds => isActive ? stopwatch.SessionSeconds : 0f;

        /// <summary>
        /// Remaining demo time in seconds. Returns 0 if exhausted.
        /// </summary>
        public float RemainingDemoSeconds => Mathf.Max(0f, demoLimitSeconds - TotalDemoUsedSeconds);

        /// <summary>
        /// True if the demo time has been fully used up.
        /// </summary>
        public bool IsDemoExpired => TotalDemoUsedSeconds >= demoLimitSeconds;

        /// <summary>
        /// Starts the demo timer with the configured limit.
        /// </summary>
        /// <param name="limitSeconds">Maximum demo time in seconds — the manager's effective limit
        /// (server value, cached server value or LicenseConfig.demoDurationSeconds as fallback).</param>
        public void StartDemo(float limitSeconds)
        {
            demoLimitSeconds = limitSeconds;
            stopwatch.Restart();
            isActive = true;
            lastCounting = true;
            lastHoldReason = null;
            nextDiagAt = Time.unscaledTime + DIAG_INTERVAL;

            Debug.Log($"[VR Licensing] demo: StartDemo limit={limitSeconds:F0}s stored={SecureLicenseStorage.GetDemoUsedSeconds():F1}s " +
                      $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");

            // Check if already expired from previous sessions
            if (IsDemoExpired)
            {
                isActive = false;
                OnDemoExpired?.Invoke();
            }
        }

        /// <summary>Limit (seconds) the demo currently runs against.</summary>
        public float LimitSeconds => demoLimitSeconds;

        /// <summary>
        /// v1.12.0-pre.13: applies a limit the server changed, without touching the stopwatch.
        /// A higher value extends the remaining time (an exhausted demo stops being expired); a
        /// lower value already exceeded ends the running demo on its next <see cref="Update"/>,
        /// through the normal expiry path.
        /// </summary>
        public void SetLimit(float limitSeconds)
        {
            demoLimitSeconds = limitSeconds;
            Diag($"SetLimit {limitSeconds:F0}s active={isActive} total={TotalDemoUsedSeconds:F1}s");
        }

        /// <summary>
        /// Ends the demo from outside (watchdog in the UI, see LicenseUIBuilder) when the limit
        /// has been crossed but <see cref="Update"/> has not fired the event. Persists the time
        /// and raises <see cref="OnDemoExpired"/> exactly like the normal path. No-op when the
        /// demo is not active.
        /// </summary>
        public void ForceExpire(string reason)
        {
            if (!isActive) return;
            Diag($"ForceExpire ({reason}) total={TotalDemoUsedSeconds:F1}s limit={demoLimitSeconds:F0}s");
            PersistSessionTime();
            isActive = false;
            Debug.LogWarning($"[VR Licensing] Demo time expired (forced by {reason}).");
            OnDemoExpired?.Invoke();
        }

        // ── Diagnostics: the periodic tick is development-build only; state changes of the
        //    stopwatch (held / resumed / frame gap ignored) are logged in every build, because
        //    they are what explains a demo total in the field. ──
        private const float DIAG_INTERVAL = 30f;
        private float nextDiagAt;

        private void Diag(string message)
        {
            if (Debug.isDebugBuild) Debug.Log("[VR Licensing] demo: " + message);
        }

        /// <summary>
        /// Stops the demo timer and persists the accumulated time.
        /// </summary>
        public void StopDemo()
        {
            if (!isActive) return;
            PersistSessionTime();
            isActive = false;
        }

        /// <summary>
        /// Adopts the server's total after an admin reset (v1.12.0-pre.7): the stored value
        /// becomes <paramref name="seconds"/> and, if a demo session is running, its stopwatch
        /// restarts now so the time already counted into the old total is not added again.
        /// </summary>
        public void AdoptServerTotal(float seconds)
        {
            SecureLicenseStorage.SetDemoUsedSeconds(Mathf.Max(0f, seconds));
            if (isActive) stopwatch.Restart();
            Diag($"AdoptServerTotal {seconds:F1}s active={isActive}");
        }

        /// <summary>
        /// Resets demo usage (e.g., for admin/debug purposes).
        /// </summary>
        public void ResetDemo()
        {
            SecureLicenseStorage.SetDemoUsedSeconds(0f);
            if (isActive) stopwatch.Restart();
        }

        private void Update()
        {
            if (!isActive) return;
            Tick(Time.unscaledDeltaTime, IsBeingPlayed(out string holdReason), holdReason);
        }

        /// <summary>
        /// One frame of the demo. Called from <see cref="Update"/>; public so tests can drive the
        /// clock without Play mode. <paramref name="counting"/> false = the frame is not demo time.
        /// </summary>
        public void Tick(float unscaledDeltaTime, bool counting, string holdReason = null)
        {
            if (!isActive) return;

            float stored = SecureLicenseStorage.GetDemoUsedSeconds();
            int gapsBefore = stopwatch.GapsIgnored;
            stopwatch.Advance(unscaledDeltaTime, counting, stored, demoLimitSeconds);

            if (stopwatch.GapsIgnored != gapsBefore)
                Debug.Log($"[VR Licensing] demo: frame gap of {unscaledDeltaTime:F1}s ignored (frames stopped; not demo time). " +
                          $"total={stopwatch.Total(stored, demoLimitSeconds):F1}s unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");

            if (counting != lastCounting || (!counting && holdReason != lastHoldReason))
            {
                Debug.Log(counting
                    ? $"[VR Licensing] demo: clock running again. total={stopwatch.Total(stored, demoLimitSeconds):F1}s ignored={stopwatch.IgnoredSeconds:F1}s " +
                      $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}"
                    : $"[VR Licensing] demo: clock held ({holdReason ?? "not playing"}). total={stopwatch.Total(stored, demoLimitSeconds):F1}s " +
                      $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");
                lastCounting = counting;
                lastHoldReason = counting ? null : holdReason;
            }

            if (Debug.isDebugBuild && Time.unscaledTime >= nextDiagAt)
            {
                nextDiagAt = Time.unscaledTime + DIAG_INTERVAL;
                Diag($"tick total={TotalDemoUsedSeconds:F1}s session={CurrentSessionDemoSeconds:F1}s " +
                     $"remaining={RemainingDemoSeconds:F1}s limit={demoLimitSeconds:F0}s counting={counting} " +
                     $"ignored={stopwatch.IgnoredSeconds:F1}s unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");
            }

            if (IsDemoExpired)
            {
                PersistSessionTime();
                isActive = false;
                Debug.Log("[VR Licensing] Demo time expired.");
                OnDemoExpired?.Invoke();
            }
        }

        /// <summary>
        /// True while this frame is demo time: the app is running, has focus, and the headset is
        /// worn. In the Editor focus is ignored (clicking another window must not stop a test).
        /// </summary>
        private bool IsBeingPlayed(out string holdReason)
        {
            if (appPaused) { holdReason = "app paused"; return false; }
            // Read each frame rather than remembered from OnApplicationFocus: a missed event must
            // never leave the clock held for good (that would be an endless demo).
            if (!Application.isFocused && !Application.isEditor) { holdReason = "app not focused"; return false; }
            if (!IsUserPresent()) { holdReason = "headset not worn"; return false; }
            holdReason = null;
            return true;
        }

        private bool IsUserPresent()
        {
            if (UserPresenceProvider != null) return UserPresenceProvider();
            try
            {
                var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                if (!head.isValid) return true; // no HMD (Editor, simulator): nothing to go by
                return !head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out bool present) || present;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            Diag($"OnApplicationPause({paused}) active={isActive} total={TotalDemoUsedSeconds:F1}s " +
                 $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");
            appPaused = paused;
            if (isActive && paused) PersistSessionTime();
        }

        private void OnApplicationFocus(bool focus)
        {
            Diag($"OnApplicationFocus({focus}) active={isActive} total={TotalDemoUsedSeconds:F1}s " +
                 $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");
            if (isActive && !focus) PersistSessionTime(); // the process can be frozen without a pause
        }

        private void OnDisable()
        {
            Diag($"OnDisable active={isActive} total={TotalDemoUsedSeconds:F1}s");
        }

        private void OnApplicationQuit()
        {
            if (isActive)
            {
                PersistSessionTime();
            }
        }

        private void PersistSessionTime()
        {
            if (!isActive) return;

            if (stopwatch.SessionSeconds > 0f)
                SecureLicenseStorage.SetDemoUsedSeconds(
                    stopwatch.Total(SecureLicenseStorage.GetDemoUsedSeconds(), demoLimitSeconds));

            // Reset so we don't double-count
            stopwatch.Restart();
        }
    }
}
