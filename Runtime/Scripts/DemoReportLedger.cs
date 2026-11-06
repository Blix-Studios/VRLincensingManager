using System;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.16 (spec 11 §4/§6) - decides what each <c>report_device_session</c> carries so
    /// demo time can never be counted twice or past the limit:
    /// <list type="bullet">
    /// <item>every report has its own <c>p_report_id</c> (uuid); the server ignores a repeated id;</item>
    /// <item>the delta is <c>local - server</c>, never negative and never more than what is left
    /// up to the limit on the server;</item>
    /// <item>a report whose outcome is unknown (timeout, connection lost, 5xx) stays pending: the
    /// caller first asks <c>get_device_status</c> what the server has and reports a new delta
    /// from that; only if that refresh fails too is the SAME report sent again, with the same id
    /// and delta, so the server can drop it if the first attempt did arrive.</item>
    /// </list>
    /// Pure logic; <see cref="LicenseManager"/> owns the requests.
    /// </summary>
    public sealed class DemoReportLedger
    {
        /// <summary>One report: its id and the demo seconds it adds.</summary>
        public sealed class Report
        {
            public string id;
            public float deltaSeconds;
            /// <summary>True when this is the pending report being sent again unchanged.</summary>
            public bool isRetry;
        }

        private Report unconfirmed;

        /// <summary>A report was sent and nobody knows whether the server applied it.</summary>
        public bool HasUnconfirmed => unconfirmed != null;

        /// <summary>
        /// Demo seconds to report: local minus server, clamped to [0, limit - server]. A limit of
        /// 0 or less means "unknown" and only the lower bound applies.
        /// </summary>
        public static float Delta(float localSeconds, float serverSeconds, float limitSeconds)
        {
            float delta = Math.Max(0f, localSeconds - serverSeconds);
            if (limitSeconds > 0f) delta = Math.Min(delta, Math.Max(0f, limitSeconds - serverSeconds));
            return delta;
        }

        /// <summary>
        /// The report to send now. With an unconfirmed report and no fresh server total
        /// (<paramref name="serverTotalRefreshed"/> false) it is that same report again; otherwise
        /// a new one with a new id and a delta computed from the totals given.
        /// </summary>
        public Report Begin(float localSeconds, float serverSeconds, float limitSeconds, bool serverTotalRefreshed)
        {
            if (unconfirmed != null && !serverTotalRefreshed)
            {
                unconfirmed.isRetry = true;
                return unconfirmed;
            }

            unconfirmed = null;
            return new Report
            {
                id = Guid.NewGuid().ToString(),
                deltaSeconds = Delta(localSeconds, serverSeconds, limitSeconds),
            };
        }

        /// <summary>The server answered: the report is settled (applied, or dropped as a duplicate).</summary>
        public void Confirm(Report report)
        {
            if (ReferenceEquals(unconfirmed, report)) unconfirmed = null;
        }

        /// <summary>
        /// The report failed. <paramref name="outcomeUnknown"/> true (timeout, connection error,
        /// 5xx, unreadable answer) keeps it pending; false (the server refused it) forgets it.
        /// </summary>
        public void Fail(Report report, bool outcomeUnknown)
        {
            unconfirmed = outcomeUnknown ? report : null;
        }
    }
}
