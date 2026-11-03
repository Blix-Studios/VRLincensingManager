using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.13 (spec 10 §6): the demo length is decided per headset by the server
    /// (<c>device_registry.demo_limit_seconds</c>, default from <c>demo_settings</c>). These are
    /// the pure rules — which limit applies and what a changed limit does to a demo — kept free
    /// of Unity state so they run in edit-mode tests. <see cref="LicenseManager"/> applies them.
    /// </summary>
    public static class DemoLimit
    {
        /// <summary>
        /// Effective limit: the server value received this run when it is positive, otherwise
        /// the last cached server value, otherwise the config fallback (offline first launch).
        /// </summary>
        public static float Resolve(float serverSeconds, float cachedSeconds, float configSeconds)
        {
            if (serverSeconds > 0f) return serverSeconds;
            if (cachedSeconds > 0f) return cachedSeconds;
            return Mathf.Max(0f, configSeconds);
        }

        /// <summary>True when the used time has reached the limit.</summary>
        public static bool IsExpired(float usedSeconds, float limitSeconds) => usedSeconds >= limitSeconds;

        /// <summary>A raised limit makes an exhausted demo available again.</summary>
        public static bool Reopens(float usedSeconds, float oldLimit, float newLimit) =>
            newLimit > oldLimit && IsExpired(usedSeconds, oldLimit) && !IsExpired(usedSeconds, newLimit);

        /// <summary>A lowered limit ends a demo that has already used more than the new value.</summary>
        public static bool Expires(float usedSeconds, float oldLimit, float newLimit) =>
            newLimit < oldLimit && !IsExpired(usedSeconds, oldLimit) && IsExpired(usedSeconds, newLimit);
    }
}
