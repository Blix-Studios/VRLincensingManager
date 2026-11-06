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

        /// <summary>Slack (seconds) before a local total counts as "above the limit".</summary>
        public const float OverLimitToleranceSeconds = 1f;

        /// <summary>
        /// Local demo total to keep after a <c>get_device_status</c> answer.
        /// <list type="bullet">
        /// <item>Server ahead: the server total (demo used elsewhere / before a wipe).</item>
        /// <item>Local ahead but not above the limit it was counted under: kept. That is demo
        /// time played and not reported yet (a session that ended without expiring, offline
        /// play); it goes out as the next delta. Adopting the server total here would erase it
        /// on every launch.</item>
        /// <item>Local ahead AND above the limit it was counted under: the server total. No
        /// build since pre.16 can count past the limit, so this is an over-count from an older
        /// build that the server has clamped; keeping it would make the next report eat a
        /// raised limit (local 6000, server 900, limit raised to 1800: 900 s must remain).</item>
        /// </list>
        /// </summary>
        /// <param name="localSeconds">Local stored total.</param>
        /// <param name="serverSeconds">Total in the answer.</param>
        /// <param name="limitWhenCountedSeconds">Effective limit BEFORE this answer was adopted.</param>
        public static float LocalAfterStatus(float localSeconds, float serverSeconds, float limitWhenCountedSeconds)
        {
            if (serverSeconds >= localSeconds) return serverSeconds;
            float ceiling = Math.Max(limitWhenCountedSeconds, serverSeconds);
            return localSeconds > ceiling + OverLimitToleranceSeconds ? serverSeconds : localSeconds;
        }

        /// <summary>
        /// Local demo total to keep after a confirmed <c>report_device_session</c>: the server's
        /// total after the report — higher (usage elsewhere) or lower (the server clamped the
        /// delta, or we did) — plus whatever was persisted locally while the request was in
        /// flight. After a report the server is the authority on everything sent.
        /// </summary>
        /// <param name="localNowSeconds">Local stored total when the answer arrived.</param>
        /// <param name="localWhenSentSeconds">Local stored total the report was computed from.</param>
        /// <param name="serverAfterSeconds">Total in the answer.</param>
        public static float LocalAfterReport(float localNowSeconds, float localWhenSentSeconds, float serverAfterSeconds)
        {
            return serverAfterSeconds + Math.Max(0f, localNowSeconds - localWhenSentSeconds);
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
