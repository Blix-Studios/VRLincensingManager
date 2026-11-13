using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.17 - which build this is, for <c>p_app_version</c> (device reports, feedback).
    /// Until now every project had to set <see cref="LicenseManager.AppVersion"/> itself or the
    /// server only saw <c>Application.version</c> without the build number; the package now
    /// builds "&lt;Application.version&gt; (&lt;versionCode&gt;)" on its own. A value set by
    /// the host still wins.
    /// </summary>
    public static class LicenseBuildInfo
    {
        /// <summary>Server-side cap of <c>p_app_version</c>.</summary>
        public const int MaxLength = 40;

        private static string cachedDefault;

        /// <summary>"1.0.0 (31)"; just "1.0.0" when there is no build number (code &lt;= 0). Capped at 40.</summary>
        public static string Compose(string version, int versionCode)
        {
            string v = string.IsNullOrWhiteSpace(version) ? "" : version.Trim();
            string text = versionCode > 0 ? (v.Length > 0 ? v + " (" + versionCode + ")" : "(" + versionCode + ")") : v;
            return text.Length > MaxLength ? text.Substring(0, MaxLength) : text;
        }

        /// <summary>What is reported: the host's value when it set one, otherwise <see cref="DefaultAppVersion"/>.</summary>
        public static string Effective(string hostValue)
        {
            return string.IsNullOrWhiteSpace(hostValue) ? DefaultAppVersion : hostValue;
        }

        /// <summary>
        /// "&lt;Application.version&gt; (&lt;versionCode of the installed APK&gt;)". Outside
        /// Android (Editor, desktop) there is no version code and it is just Application.version.
        /// Read once per run.
        /// </summary>
        public static string DefaultAppVersion
        {
            get
            {
                if (cachedDefault == null) cachedDefault = Compose(Application.version, InstalledVersionCode());
                return cachedDefault;
            }
        }

        /// <summary>versionCode of the installed package (Android PackageManager); 0 elsewhere or on failure.</summary>
        public static int InstalledVersionCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var info = manager.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0))
                {
                    return info.Get<int>("versionCode");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VR Licensing] versionCode of the installed APK unavailable: " + e.Message);
                return 0;
            }
#else
            return 0;
#endif
        }
    }
}
