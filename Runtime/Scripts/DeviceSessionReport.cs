using System.Collections.Generic;
using System.Globalization;

namespace VRLicensing
{
    /// <summary>
    /// Body of <c>report_device_session</c>. The members up to <see cref="eventsJsonArray"/> are
    /// the contract since v1.12; the rest are the v1.12.0-pre.16 additions (spec 11 §4/§6), all
    /// <c>DEFAULT NULL</c> on the server and left out of the JSON when empty. A server that does
    /// not know them answers function-not-found and gets the same report again without them
    /// (<see cref="ToJson"/> with <c>extended = false</c>).
    /// </summary>
    public sealed class DeviceSessionReport
    {
        public const int MaxUpmVersionLength = 20;
        public const int MaxLicenseStateLength = 20;

        public string deviceId;
        public int productId;
        public float demoDeltaSeconds;
        public string deviceModel;
        public string deviceName;
        public string lastLicenseKey;
        /// <summary>Pre-built JSON array of store events, or null.</summary>
        public string eventsJsonArray;

        // v1.12.0-pre.16
        /// <summary>uuid of this report; the server adds nothing for an id it has already seen.</summary>
        public string reportId;
        /// <summary>Host build, e.g. "1.0.0 (31)" (max 40).</summary>
        public string appVersion;
        /// <summary>Version of this package, from package.json (max 20).</summary>
        public string upmVersion;
        /// <summary>demo | demo_expired | licensed | store_subscribed | blocked (see <see cref="LicenseStateCode"/>).</summary>
        public string licenseState;
        /// <summary>Language the license UI is showing, BCP-47 (max 10).</summary>
        public string locale;

        /// <summary>
        /// JSON body. <paramref name="extended"/> false = exactly the pre-pre.16 parameters, for
        /// servers that have not been migrated.
        /// </summary>
        public string ToJson(bool extended)
        {
            var members = new List<string>
            {
                "\"p_device_id\":\"" + JsonText.Escape(deviceId) + "\"",
                "\"p_product_id\":" + productId.ToString(CultureInfo.InvariantCulture),
                "\"p_demo_delta_seconds\":" + (demoDeltaSeconds > 0f ? demoDeltaSeconds : 0f).ToString("0.###", CultureInfo.InvariantCulture),
                "\"p_device_model\":\"" + JsonText.Escape(deviceModel) + "\"",
                "\"p_device_name\":\"" + JsonText.Escape(deviceName) + "\"",
                // "" = "no key now" (the server clears last_license_key); it is always sent.
                "\"p_last_license_key\":\"" + JsonText.Escape(lastLicenseKey) + "\"",
            };
            if (!string.IsNullOrEmpty(eventsJsonArray)) members.Add("\"p_events\":" + eventsJsonArray);

            if (extended)
            {
                Add(members, JsonText.Member("p_report_id", reportId));
                Add(members, JsonText.Member("p_app_version", AppFeedback.NormalizeAppVersion(appVersion)));
                Add(members, JsonText.Member("p_upm_version", Cap(upmVersion, MaxUpmVersionLength)));
                Add(members, JsonText.Member("p_license_state", Cap(licenseState, MaxLicenseStateLength)));
                Add(members, JsonText.Member("p_locale", AppFeedback.NormalizeLocale(locale)));
            }
            return "{" + string.Join(",", members) + "}";
        }

        private static void Add(List<string> members, string member)
        {
            if (member != null) members.Add(member);
        }

        private static string Cap(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string v = value.Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }
    }

    /// <summary>Value of <c>p_license_state</c> (spec 11 §3: CHECK on these five values).</summary>
    public static class LicenseStateCode
    {
        public const string Demo = "demo";
        public const string DemoExpired = "demo_expired";
        public const string Licensed = "licensed";
        public const string StoreSubscribed = "store_subscribed";
        public const string Blocked = "blocked";

        /// <summary>
        /// Maps the client state. An unlocked headset says how it is unlocked whatever else is
        /// true; otherwise an admin block wins. <see cref="LicenseState.Unlicensed"/> (welcome
        /// panel, demo not started or paused) is "demo". <see cref="LicenseState.Expired"/> is
        /// "demo_expired" for a demo AND for a key or subscription that ran out: the server has no
        /// separate value for the latter. <see cref="LicenseState.ClockTampered"/> reports nothing.
        /// </summary>
        public static string For(LicenseState state, bool demoBlocked)
        {
            switch (state)
            {
                case LicenseState.Licensed: return Licensed;
                case LicenseState.SubscribedViaStore: return StoreSubscribed;
                case LicenseState.ClockTampered: return null;
            }
            if (demoBlocked) return Blocked;
            return state == LicenseState.Expired ? DemoExpired : Demo;
        }
    }

    /// <summary>Raw answer of one RPC call, independent of the HTTP stack (tests use a fake transport).</summary>
    public sealed class RpcResponse
    {
        /// <summary>The request reached the server and an HTTP answer came back (whatever its code).</summary>
        public bool transportOk;
        public long httpCode;
        public string body;
        public string error;

        public bool IsSuccess => transportOk && httpCode >= 200 && httpCode < 300;

        /// <summary>
        /// PostgREST answers 404 with code PGRST202 when no function matches the name AND the
        /// named arguments sent — which is what an old server says to a parameter it does not know.
        /// </summary>
        public bool IsFunctionNotFound =>
            transportOk && (httpCode == 404 || (body != null && body.Contains("PGRST202")));

        /// <summary>
        /// True when nobody can tell whether the server applied the call: no answer at all or a
        /// 5xx. (An answer that cannot be read is for the caller to add.)
        /// </summary>
        public bool OutcomeUnknown => !transportOk || httpCode >= 500 || httpCode == 0;
    }
}
