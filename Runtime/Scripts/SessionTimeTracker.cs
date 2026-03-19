using System;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Tracks real session play time using Time.unscaledTime (monotonic clock).
    /// This counter is immune to Time.timeScale changes and system clock manipulation.
    /// Used to measure actual usage time for license duration enforcement.
    /// </summary>
    public class SessionTimeTracker : MonoBehaviour
    {
        private float sessionStartTime;
        private bool isTracking;

        /// <summary>
        /// Total accumulated usage time in seconds (all sessions combined).
        /// </summary>
        public float TotalUsedSeconds => SecureLicenseStorage.GetSessionUsedSeconds() + CurrentSessionSeconds;

        /// <summary>
        /// Usage time for the current session only.
        /// </summary>
        public float CurrentSessionSeconds => isTracking ? (Time.unscaledTime - sessionStartTime) : 0f;

        /// <summary>
        /// Starts tracking session time.
        /// </summary>
        public void StartTracking()
        {
            if (isTracking) return;
            sessionStartTime = Time.unscaledTime;
            isTracking = true;
        }

        /// <summary>
        /// Stops tracking and persists accumulated time.
        /// </summary>
        public void StopTracking()
        {
            if (!isTracking) return;
            PersistCurrentSession();
            isTracking = false;
        }

        /// <summary>
        /// Checks if accumulated usage exceeds the given maximum duration.
        /// </summary>
        public bool IsTimeExhausted(float maxDurationSeconds)
        {
            return TotalUsedSeconds >= maxDurationSeconds;
        }

        /// <summary>
        /// Resets the accumulated session time (e.g., when a new license is activated).
        /// </summary>
        public void ResetAccumulatedTime()
        {
            SecureLicenseStorage.SetSessionUsedSeconds(0f);
            if (isTracking)
            {
                sessionStartTime = Time.unscaledTime;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && isTracking)
            {
                PersistCurrentSession();
            }
            else if (!paused && isTracking)
            {
                // Resume tracking from now
                sessionStartTime = Time.unscaledTime;
            }
        }

        private void OnApplicationQuit()
        {
            if (isTracking)
            {
                PersistCurrentSession();
            }
        }

        private void PersistCurrentSession()
        {
            if (!isTracking) return;

            float sessionDuration = Time.unscaledTime - sessionStartTime;
            float totalUsed = SecureLicenseStorage.GetSessionUsedSeconds() + sessionDuration;
            SecureLicenseStorage.SetSessionUsedSeconds(totalUsed);

            // Reset session start so we don't double-count
            sessionStartTime = Time.unscaledTime;
        }
    }
}
