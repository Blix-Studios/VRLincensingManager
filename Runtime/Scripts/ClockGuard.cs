using System;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Detects system clock manipulation (rollback) to prevent
    /// users from extending license validity by changing the device date.
    /// </summary>
    public class ClockGuard : MonoBehaviour
    {
        /// <summary>
        /// Checks if the system clock has been rolled back compared to the
        /// highest known time we've ever recorded. If the current time is
        /// earlier than our record, the clock has been tampered with.
        /// </summary>
        /// <returns>True if clock tampering is detected.</returns>
        public bool IsClockTampered()
        {
            long savedHKT = SecureLicenseStorage.GetHighestKnownTime();
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (savedHKT > 0 && currentTime < savedHKT)
            {
                Debug.LogWarning($"[VR Licensing] Clock rollback detected! " +
                    $"Current: {currentTime}, Highest known: {savedHKT}, " +
                    $"Diff: {savedHKT - currentTime}s");
                return true;
            }

            // Update the highest known time
            UpdateHighestKnownTime();
            return false;
        }

        /// <summary>
        /// Updates the highest known time to the current UTC timestamp.
        /// Call this periodically during normal operation.
        /// </summary>
        public void UpdateHighestKnownTime()
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long savedHKT = SecureLicenseStorage.GetHighestKnownTime();

            if (currentTime > savedHKT)
            {
                SecureLicenseStorage.SetHighestKnownTime(currentTime);
            }
        }
    }
}
