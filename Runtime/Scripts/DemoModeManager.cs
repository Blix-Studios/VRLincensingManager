using System;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Manages the demo mode timer. Accumulates playtime across sessions
    /// and fires an event when the demo limit is reached.
    /// </summary>
    public class DemoModeManager : MonoBehaviour
    {
        private float sessionStartTime;
        private float demoLimitSeconds;
        private bool isActive;

        /// <summary>
        /// Fired when the demo time limit is reached.
        /// </summary>
        public event Action OnDemoExpired;

        /// <summary>
        /// Total demo time used across all sessions (in seconds).
        /// </summary>
        public float TotalDemoUsedSeconds => SecureLicenseStorage.GetDemoUsedSeconds() + CurrentSessionDemoSeconds;

        /// <summary>
        /// Demo time used in the current session only.
        /// </summary>
        public float CurrentSessionDemoSeconds => isActive ? (Time.unscaledTime - sessionStartTime) : 0f;

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
        /// <param name="limitSeconds">Maximum demo time in seconds (from LicenseConfig.demoDurationSeconds)</param>
        public void StartDemo(float limitSeconds)
        {
            demoLimitSeconds = limitSeconds;
            sessionStartTime = Time.unscaledTime;
            isActive = true;
            nextDiagAt = Time.unscaledTime + DIAG_INTERVAL;

            Diag($"StartDemo limit={limitSeconds:F0}s stored={SecureLicenseStorage.GetDemoUsedSeconds():F1}s " +
                 $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");

            // Check if already expired from previous sessions
            if (IsDemoExpired)
            {
                isActive = false;
                OnDemoExpired?.Invoke();
            }
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

        // ── Development-build diagnostics (never logged in release builds) ──
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
        /// Resets demo usage (e.g., for admin/debug purposes).
        /// </summary>
        public void ResetDemo()
        {
            SecureLicenseStorage.SetDemoUsedSeconds(0f);
            if (isActive)
            {
                sessionStartTime = Time.unscaledTime;
            }
        }

        private void Update()
        {
            if (!isActive) return;

            if (Debug.isDebugBuild && Time.unscaledTime >= nextDiagAt)
            {
                nextDiagAt = Time.unscaledTime + DIAG_INTERVAL;
                Diag($"tick total={TotalDemoUsedSeconds:F1}s session={CurrentSessionDemoSeconds:F1}s " +
                     $"remaining={RemainingDemoSeconds:F1}s limit={demoLimitSeconds:F0}s " +
                     $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");
            }

            if (IsDemoExpired)
            {
                PersistSessionTime();
                isActive = false;
                Debug.Log("[VR Licensing] Demo time expired.");
                OnDemoExpired?.Invoke();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            Diag($"OnApplicationPause({paused}) active={isActive} total={TotalDemoUsedSeconds:F1}s " +
                 $"unscaledTime={Time.unscaledTime:F1} realtime={Time.realtimeSinceStartup:F1}");
            if (!isActive) return;

            if (paused)
            {
                PersistSessionTime();
            }
            else
            {
                sessionStartTime = Time.unscaledTime;
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            Diag($"OnApplicationFocus({focus}) active={isActive} total={TotalDemoUsedSeconds:F1}s");
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

            float sessionDuration = Time.unscaledTime - sessionStartTime;
            float totalUsed = SecureLicenseStorage.GetDemoUsedSeconds() + sessionDuration;
            SecureLicenseStorage.SetDemoUsedSeconds(totalUsed);

            // Reset so we don't double-count
            sessionStartTime = Time.unscaledTime;
        }
    }
}
