using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRLicensing;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.16 (spec 11 §4/§6): what a session report carries (report id, build, package
    /// version, licence state, language), how an old server is handled (one retry without the
    /// new parameters) and how the delta is kept from being sent twice or past the limit.
    /// </summary>
    public class DeviceSessionReportTests
    {
        private const string OkBody =
            "{\"ok\":true,\"demo_used_seconds\":120,\"demo_limit_seconds\":900,\"demo_blocked\":false,\"session_count\":3}";
        private const string NotFoundBody =
            "{\"code\":\"PGRST202\",\"details\":\"Searched for the function public.report_device_session with parameters …\"," +
            "\"hint\":null,\"message\":\"Could not find the function public.report_device_session(…) in the schema cache\"}";

        private static readonly string[] NewMembers =
            { "p_report_id", "p_app_version", "p_upm_version", "p_license_state", "p_locale" };

        private static DeviceSessionReport Sample()
        {
            return new DeviceSessionReport
            {
                deviceId = "dev-1", productId = 1, demoDeltaSeconds = 42.5f,
                deviceModel = "Oculus Quest 3", deviceName = "Quest", lastLicenseKey = null,
                eventsJsonArray = "[{\"name\":\"plans_opened\"}]",
                reportId = "3f2c1c9e-0b7a-4c57-9d0e-0d6f8f4f8a11",
                appVersion = "1.0.0 (31)", upmVersion = "1.12.0-pre.16",
                licenseState = LicenseStateCode.Demo, locale = "nl",
            };
        }

        // ───────────── body ─────────────

        [Test]
        public void ExtendedBody_CarriesTheNewFields()
        {
            string json = Sample().ToJson(extended: true);
            StringAssert.Contains("\"p_report_id\":\"3f2c1c9e-0b7a-4c57-9d0e-0d6f8f4f8a11\"", json);
            StringAssert.Contains("\"p_app_version\":\"1.0.0 (31)\"", json);
            StringAssert.Contains("\"p_upm_version\":\"1.12.0-pre.16\"", json);
            StringAssert.Contains("\"p_license_state\":\"demo\"", json);
            StringAssert.Contains("\"p_locale\":\"nl\"", json);
            StringAssert.Contains("\"p_demo_delta_seconds\":42.5", json);
            StringAssert.Contains("\"p_last_license_key\":\"\"", json, "\"\" still means: no key now");
            StringAssert.Contains("\"p_events\":[{\"name\":\"plans_opened\"}]", json);
        }

        [Test]
        public void PlainBody_IsExactlyTheOldContract()
        {
            string json = Sample().ToJson(extended: false);
            foreach (string member in NewMembers) StringAssert.DoesNotContain(member, json);
            Assert.AreEqual(
                "{\"p_device_id\":\"dev-1\",\"p_product_id\":1,\"p_demo_delta_seconds\":42.5," +
                "\"p_device_model\":\"Oculus Quest 3\",\"p_device_name\":\"Quest\",\"p_last_license_key\":\"\"," +
                "\"p_events\":[{\"name\":\"plans_opened\"}]}", json);
        }

        [Test]
        public void EmptyOptionalFieldsAreOmitted_AndLongOnesCapped()
        {
            var r = Sample();
            r.appVersion = null; r.locale = "  "; r.licenseState = null; r.eventsJsonArray = null;
            r.upmVersion = "1.12.0-pre.16-a-very-long-suffix";
            r.demoDeltaSeconds = -5f;
            string json = r.ToJson(true);
            StringAssert.DoesNotContain("p_app_version", json);
            StringAssert.DoesNotContain("p_locale", json);
            StringAssert.DoesNotContain("p_license_state", json);
            StringAssert.DoesNotContain("p_events", json);
            StringAssert.Contains("\"p_upm_version\":\"1.12.0-pre.16-a-very\"", json);
            StringAssert.Contains("\"p_demo_delta_seconds\":0", json);
        }

        [Test]
        public void LicenseStateMapping()
        {
            Assert.AreEqual("demo", LicenseStateCode.For(LicenseState.Demo, false, false));
            Assert.AreEqual("demo", LicenseStateCode.For(LicenseState.Unlicensed, false, false), "welcome panel: demo not started / paused");
            Assert.AreEqual("demo_expired", LicenseStateCode.For(LicenseState.Expired, false, false), "the demo ran out");
            Assert.AreEqual("license_expired", LicenseStateCode.For(LicenseState.Expired, false, true), "a key or a subscription ran out");
            Assert.AreEqual("license_expired", LicenseStateCode.For(LicenseState.Expired, true, true), "more specific than the demo block");
            Assert.AreEqual("licensed", LicenseStateCode.For(LicenseState.Licensed, false, false));
            Assert.AreEqual("store_subscribed", LicenseStateCode.For(LicenseState.SubscribedViaStore, false, false));
            Assert.AreEqual("blocked", LicenseStateCode.For(LicenseState.Unlicensed, true, false));
            Assert.AreEqual("blocked", LicenseStateCode.For(LicenseState.Expired, true, false));
            Assert.AreEqual("licensed", LicenseStateCode.For(LicenseState.Licensed, true, false), "an unlocked headset says how it is unlocked");
            Assert.AreEqual("demo", LicenseStateCode.For(LicenseState.Demo, false, true), "a stale expiry flag does not leak into other states");
            Assert.IsNull(LicenseStateCode.For(LicenseState.ClockTampered, false, false));

            // The six values the server's CHECK accepts (spec 11 §3), each within the cap.
            foreach (string code in new[] { "demo", "demo_expired", "licensed", "license_expired", "store_subscribed", "blocked" })
                Assert.LessOrEqual(code.Length, DeviceSessionReport.MaxLicenseStateLength);
        }

        [Test]
        public void PackageVersionConstantMatchesPackageJson()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LicenseManager).Assembly);
            Assert.IsNotNull(info, "the package must be resolvable from its runtime assembly");
            Assert.AreEqual(info.version, PackageVersion.Value,
                "PackageVersion.cs is stale: let Editor/PackageVersionGenerator regenerate it after bumping package.json");
            Assert.LessOrEqual(PackageVersion.Value.Length, DeviceSessionReport.MaxUpmVersionLength);
        }

        // ───────────── transport: new server, old server, no answer ─────────────

        private GameObject go;
        private SupabaseClient client;
        private readonly List<string> bodies = new List<string>();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DeviceSessionReportTests");
            client = go.AddComponent<SupabaseClient>();
            bodies.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        private void UseServer(Func<string, RpcResponse> answer)
        {
            client.Transport = (function, body, done) => Fake(function, body, done, answer);
        }

        private IEnumerator Fake(string function, string body, Action<RpcResponse> done, Func<string, RpcResponse> answer)
        {
            Assert.AreEqual("report_device_session", function);
            bodies.Add(body);
            done(answer(body));
            yield break;
        }

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext())
                if (routine.Current is IEnumerator inner) Run(inner);
        }

        private static RpcResponse Http(long code, string body) =>
            new RpcResponse { transportOk = true, httpCode = code, body = body };

        [Test]
        public void NewServer_OneCallWithTheFields()
        {
            UseServer(_ => Http(200, OkBody));
            DeviceStatus status = null;
            Run(client.ReportDeviceSession(Sample(), s => status = s, (e, u) => Assert.Fail(e)));

            Assert.AreEqual(1, bodies.Count);
            foreach (string member in NewMembers) StringAssert.Contains(member, bodies[0]);
            Assert.AreEqual(120f, status.demo_used_seconds);
            Assert.IsTrue(client.ExtendedReportSupported);
        }

        [Test]
        public void OldServer_FunctionNotFound_RetriesOnceWithoutTheFields()
        {
            // An old server knows report_device_session, but not with these named arguments.
            UseServer(body => body.Contains("p_report_id") ? Http(404, NotFoundBody) : Http(200, OkBody));
            DeviceStatus status = null;
            Run(client.ReportDeviceSession(Sample(), s => status = s, (e, u) => Assert.Fail(e)));

            Assert.AreEqual(2, bodies.Count, "exactly one retry");
            foreach (string member in NewMembers) StringAssert.DoesNotContain(member, bodies[1]);
            StringAssert.Contains("\"p_demo_delta_seconds\":42.5", bodies[1], "same report, same delta");
            Assert.IsNotNull(status);
            Assert.IsFalse(client.ExtendedReportSupported);

            // The rest of the run does not ask again.
            Run(client.ReportDeviceSession(Sample(), s => { }, (e, u) => Assert.Fail(e)));
            Assert.AreEqual(3, bodies.Count);
            foreach (string member in NewMembers) StringAssert.DoesNotContain(member, bodies[2]);
        }

        [Test]
        public void FunctionReallyMissing_FailsAfterTheSingleRetry_AsRefused()
        {
            UseServer(_ => Http(404, NotFoundBody));
            string error = null; bool unknown = true;
            Run(client.ReportDeviceSession(Sample(), s => Assert.Fail("no success expected"), (e, u) => { error = e; unknown = u; }));

            Assert.AreEqual(2, bodies.Count);
            Assert.IsNotNull(error);
            Assert.IsFalse(unknown, "the server answered: nothing was applied");
        }

        [Test]
        public void NoAnswer_IsAnUnknownOutcome_AndIsNotRetriedHere()
        {
            UseServer(_ => new RpcResponse { transportOk = false, httpCode = 0, error = "Request timeout" });
            bool unknown = false; string error = null;
            Run(client.ReportDeviceSession(Sample(), s => Assert.Fail("no success expected"), (e, u) => { error = e; unknown = u; }));

            Assert.AreEqual(1, bodies.Count);
            Assert.IsTrue(unknown);
            StringAssert.Contains("Request timeout", error);
            Assert.IsTrue(client.ExtendedReportSupported, "a timeout says nothing about the server's version");
        }

        [Test]
        public void ServerError_IsUnknown_RefusalIsNot()
        {
            UseServer(_ => Http(503, "upstream timeout"));
            bool unknown = false;
            Run(client.ReportDeviceSession(Sample(), s => Assert.Fail(), (e, u) => unknown = u));
            Assert.IsTrue(unknown, "5xx: the function may have run");

            UseServer(_ => Http(200, "{\"ok\":false,\"error\":\"Missing device identifier.\"}"));
            unknown = true;
            Run(client.ReportDeviceSession(Sample(), s => Assert.Fail(), (e, u) => unknown = u));
            Assert.IsFalse(unknown);
        }

        [Test]
        public void LegacyOverload_StillWorks()
        {
            UseServer(_ => Http(200, OkBody));
            DeviceStatus status = null;
            Run(client.ReportDeviceSession(1, 12f, "", s => status = s, e => Assert.Fail(e), null));
            Assert.AreEqual(1, bodies.Count);
            StringAssert.Contains("\"p_demo_delta_seconds\":12", bodies[0]);
            StringAssert.DoesNotContain("p_report_id", bodies[0], "no id was given, none is sent");
            Assert.IsNotNull(status);
        }

        // ───────────── ledger: ids, delta clamp, unconfirmed reports ─────────────

        [Test]
        public void Delta_IsLocalMinusServer_NeverNegative_NeverPastTheLimit()
        {
            Assert.AreEqual(60f, DemoReportLedger.Delta(100f, 40f, 900f));
            Assert.AreEqual(0f, DemoReportLedger.Delta(40f, 100f, 900f), "server ahead: nothing to add");
            Assert.AreEqual(900f, DemoReportLedger.Delta(7080f, 0f, 900f), "118 min counted locally by an old build: only the limit is reported");
            Assert.AreEqual(20f, DemoReportLedger.Delta(950f, 880f, 900f));
            Assert.AreEqual(0f, DemoReportLedger.Delta(1000f, 950f, 900f), "server already at or over the limit");
            Assert.AreEqual(7080f, DemoReportLedger.Delta(7080f, 0f, 0f), "limit unknown: only the lower bound");
        }

        [Test]
        public void ServerClamp_LocalAdoptsTheLowerServerTotal_SoARaisedLimitIsNotEaten()
        {
            // Headset over-counted by an old build: local 6000 s. The server has clamped the row
            // to its limit (900 s) and the admin then raised the limit to 1800 s.
            const float local = 6000f, server = 900f, oldLimit = 900f, newLimit = 1800f;

            // get_device_status at launch: the local total is far above the limit it was counted under.
            float afterStatus = DemoReportLedger.LocalAfterStatus(local, server, oldLimit);
            Assert.AreEqual(900f, afterStatus);
            Assert.AreEqual(900f, newLimit - afterStatus, "remaining 900 s");
            Assert.AreEqual(0f, DemoReportLedger.Delta(afterStatus, server, newLimit), "and the next report adds nothing");

            // Same headset when only a report answer arrives (no status first): the delta is
            // clamped to what is left (here against the old limit: nothing) and the answer wins.
            float delta = DemoReportLedger.Delta(local, server, oldLimit);
            Assert.AreEqual(0f, delta);
            float afterReport = DemoReportLedger.LocalAfterReport(local, local, server + delta);
            Assert.AreEqual(900f, afterReport);
            Assert.AreEqual(900f, newLimit - afterReport, "remaining 900 s");

            // What happened before this rule: the local total stayed at 6000 and the next report
            // filled the raised limit at once.
            Assert.AreEqual(900f, DemoReportLedger.Delta(local, server, newLimit), "the raise would have been eaten");
        }

        [Test]
        public void StatusAnswer_KeepsUnreportedDemoTime()
        {
            // A demo session that ended without expiring is only reported at the next launch:
            // local 300 s, server 0 s. Adopting the server total here would give the time back
            // on every launch.
            Assert.AreEqual(300f, DemoReportLedger.LocalAfterStatus(300f, 0f, 900f));
            Assert.AreEqual(300f, DemoReportLedger.Delta(300f, 0f, 900f), "it goes out as the next delta");

            Assert.AreEqual(900f, DemoReportLedger.LocalAfterStatus(900f, 600f, 900f), "exactly at the limit is still real play");
            Assert.AreEqual(900.5f, DemoReportLedger.LocalAfterStatus(900.5f, 600f, 900f), "a frame over the limit (old builds) too");
            Assert.AreEqual(500f, DemoReportLedger.LocalAfterStatus(120f, 500f, 900f), "server ahead: trust the server");
            Assert.AreEqual(1140f, DemoReportLedger.LocalAfterStatus(1140f, 1140f, 900f), "row not clamped yet: nothing to do");
            Assert.AreEqual(900f, DemoReportLedger.LocalAfterStatus(1140f, 900f, 900f), "19 of 15 min, clamped by the server");
        }

        [Test]
        public void ReportAnswer_IsTheAuthority_PlusWhatWasPersistedMeanwhile()
        {
            Assert.AreEqual(600f, DemoReportLedger.LocalAfterReport(600f, 600f, 600f), "delta accepted in full");
            Assert.AreEqual(1500f, DemoReportLedger.LocalAfterReport(600f, 600f, 1500f), "usage from elsewhere");
            Assert.AreEqual(900f, DemoReportLedger.LocalAfterReport(3000f, 3000f, 900f), "server clamp");
            Assert.AreEqual(930f, DemoReportLedger.LocalAfterReport(3030f, 3000f, 900f), "30 s persisted while the request was in flight are kept");
        }

        [Test]
        public void EveryReportHasItsOwnId()
        {
            var ledger = new DemoReportLedger();
            var a = ledger.Begin(100f, 40f, 900f, false);
            ledger.Confirm(a);
            var b = ledger.Begin(130f, 100f, 900f, false);

            Assert.IsTrue(Guid.TryParse(a.id, out _));
            Assert.IsTrue(Guid.TryParse(b.id, out _));
            Assert.AreNotEqual(a.id, b.id);
            Assert.AreEqual(60f, a.deltaSeconds);
            Assert.AreEqual(30f, b.deltaSeconds);
            Assert.IsFalse(a.isRetry);
            Assert.IsFalse(ledger.HasUnconfirmed);
        }

        [Test]
        public void Timeout_ThenRefresh_ReportsANewDeltaFromTheServerTotal()
        {
            // The expiry report (900 s) times out but the server did apply it; the expired panel
            // then flushes plans_opened. Before pre.16 that second report carried the 900 s again.
            var ledger = new DemoReportLedger();
            var first = ledger.Begin(900f, 0f, 900f, false);
            ledger.Fail(first, outcomeUnknown: true);
            Assert.IsTrue(ledger.HasUnconfirmed);

            // get_device_status says the server already has 900 s.
            var second = ledger.Begin(900f, 900f, 900f, serverTotalRefreshed: true);

            Assert.AreNotEqual(first.id, second.id);
            Assert.AreEqual(0f, second.deltaSeconds, "nothing is counted twice");
            Assert.IsFalse(second.isRetry);
        }

        [Test]
        public void Timeout_AndNoRefresh_SendsTheSameReportAgain()
        {
            var ledger = new DemoReportLedger();
            var first = ledger.Begin(300f, 100f, 900f, false);
            ledger.Fail(first, outcomeUnknown: true);

            // Still offline: the local total moved on, but the retry is the same report.
            var retry = ledger.Begin(360f, 100f, 900f, serverTotalRefreshed: false);

            Assert.AreEqual(first.id, retry.id, "same id: the server drops it if the first attempt arrived");
            Assert.AreEqual(200f, retry.deltaSeconds, "and the same delta");
            Assert.IsTrue(retry.isRetry);

            ledger.Confirm(retry);
            Assert.IsFalse(ledger.HasUnconfirmed);
            var next = ledger.Begin(360f, 300f, 900f, false);
            Assert.AreNotEqual(first.id, next.id);
            Assert.AreEqual(60f, next.deltaSeconds);
        }

        [Test]
        public void Refusal_IsNotKeptPending()
        {
            var ledger = new DemoReportLedger();
            var first = ledger.Begin(300f, 100f, 900f, false);
            ledger.Fail(first, outcomeUnknown: false);
            Assert.IsFalse(ledger.HasUnconfirmed);
            Assert.AreNotEqual(first.id, ledger.Begin(300f, 100f, 900f, false).id);
        }

        [Test]
        public void RpcResponse_Classification()
        {
            Assert.IsTrue(Http(404, NotFoundBody).IsFunctionNotFound);
            Assert.IsTrue(Http(404, "").IsFunctionNotFound);
            Assert.IsFalse(Http(200, OkBody).IsFunctionNotFound);
            Assert.IsFalse(Http(401, "{\"message\":\"Invalid API key\"}").IsFunctionNotFound);
            Assert.IsFalse(new RpcResponse { transportOk = false }.IsFunctionNotFound);

            Assert.IsTrue(new RpcResponse { transportOk = false }.OutcomeUnknown);
            Assert.IsTrue(Http(502, "").OutcomeUnknown);
            Assert.IsFalse(Http(400, "").OutcomeUnknown);
            Assert.IsFalse(Http(200, OkBody).OutcomeUnknown);
        }
    }
}
