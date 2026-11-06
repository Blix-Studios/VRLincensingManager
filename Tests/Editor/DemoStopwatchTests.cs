using NUnit.Framework;
using UnityEngine;
using VRLicensing;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.16 (spec 11 §6): the demo clock only counts frames that are really played.
    /// Field case: a headset taken off mid-demo reported 118 of 15 minutes, because the clock was
    /// wall time and the first frame after the gap added all of it.
    /// </summary>
    public class DemoStopwatchTests
    {
        private const float Frame = 1f / 72f;

        private static void Play(DemoStopwatch sw, float seconds, float stored, float limit, float frame = Frame)
        {
            int frames = Mathf.RoundToInt(seconds / frame);
            for (int i = 0; i < frames; i++) sw.Advance(frame, true, stored, limit);
        }

        [Test]
        public void NormalFramesAreCounted()
        {
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(Frame, true, 0f, 900f);            // first frame of a segment: dropped
            Assert.AreEqual(0f, sw.SessionSeconds);
            Play(sw, 60f, 0f, 900f);
            Assert.AreEqual(60f, sw.SessionSeconds, 0.05f);
            Assert.AreEqual(100f + sw.SessionSeconds, sw.Total(100f, 900f), 0.001f);
        }

        [Test]
        public void TwentyMinuteFrameGapIsIgnored()
        {
            // Headset off for 20 minutes with the app still "running": frames stop, then one
            // frame arrives whose delta is the whole gap. Before pre.16 that frame ended the demo
            // with 20 extra minutes on the counter.
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(Frame, true, 0f, 900f);
            Play(sw, 30f, 0f, 900f);
            float before = sw.SessionSeconds;

            float counted = sw.Advance(1200f, true, 0f, 900f);

            Assert.AreEqual(0f, counted, "the gap is not demo time");
            Assert.AreEqual(before, sw.SessionSeconds);
            Assert.AreEqual(1, sw.GapsIgnored);
            Assert.Less(sw.Total(0f, 900f), 900f, "the demo has not expired");

            Play(sw, 10f, 0f, 900f);
            Assert.AreEqual(before + 10f, sw.SessionSeconds, 0.05f, "and the clock goes on afterwards");
        }

        [Test]
        public void GapThresholdIsAFewSeconds()
        {
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(Frame, true, 0f, 900f);
            Assert.AreEqual(2.5f, sw.Advance(2.5f, true, 0f, 900f), 1e-4f, "a long hitch (scene load) still counts");
            Assert.AreEqual(0f, sw.Advance(DemoStopwatch.DefaultMaxFrameGapSeconds + 0.1f, true, 0f, 900f));
        }

        [Test]
        public void NothingCountsWhilePausedUnfocusedOrNotWorn()
        {
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(Frame, true, 0f, 900f);
            Play(sw, 5f, 0f, 900f);
            float before = sw.SessionSeconds;

            // 20 minutes of frames that keep coming (headset on a desk, app unfocused) but are not play.
            for (int i = 0; i < 1200; i++) Assert.AreEqual(0f, sw.Advance(1f, false, 0f, 900f));
            Assert.AreEqual(before, sw.SessionSeconds);
            Assert.AreEqual(1200f, sw.IgnoredSeconds, 1f);

            // The frame that resumes spans the pause: dropped. The next one counts.
            Assert.AreEqual(0f, sw.Advance(2f, true, 0f, 900f));
            Assert.AreEqual(Frame, sw.Advance(Frame, true, 0f, 900f), 1e-5f);
        }

        [Test]
        public void StopsAtTheLimitExactly()
        {
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(1f, true, 84.33f, 900f);
            for (int i = 0; i < 2000; i++) sw.Advance(1f, true, 84.33f, 900f); // far more than what is left

            Assert.AreEqual(900f, sw.Total(84.33f, 900f), "total is the limit, not a hair under or over it");
            Assert.AreEqual(0f, sw.Advance(1f, true, 84.33f, 900f), "and nothing is added past it");
        }

        [Test]
        public void StoredTotalAboveTheLimitIsLeftAlone()
        {
            // Rows already over the limit (older builds, or a limit lowered by the admin).
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(1f, true, 7080f, 900f);
            Assert.AreEqual(0f, sw.Advance(1f, true, 7080f, 900f));
            Assert.AreEqual(7080f, sw.Total(7080f, 900f));
        }

        [Test]
        public void RaisedLimitLetsTheClockRunAgain()
        {
            var sw = new DemoStopwatch();
            sw.Restart();
            sw.Advance(1f, true, 0f, 10f);
            for (int i = 0; i < 20; i++) sw.Advance(1f, true, 0f, 10f);
            Assert.AreEqual(10f, sw.Total(0f, 10f));

            Assert.AreEqual(1f, sw.Advance(1f, true, 0f, 7200f), 1e-4f, "server raised the limit to 2 h");
            Assert.AreEqual(11f, sw.Total(0f, 7200f), 1e-3f);
        }

        // ───────────── DemoModeManager driven frame by frame ─────────────

        private GameObject go;
        private DemoModeManager manager;
        private int expiredEvents;

        [SetUp]
        public void SetUp()
        {
            SecureLicenseStorage.ClearAll();
            SecureLicenseStorage.MarkInitialized();
            go = new GameObject("DemoStopwatchTests");
            manager = go.AddComponent<DemoModeManager>();
            expiredEvents = 0;
            manager.OnDemoExpired += () => expiredEvents++;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            SecureLicenseStorage.ClearAll();
        }

        [Test]
        public void Manager_HeadsetOffForTwentyMinutes_DoesNotExpireOrAddTime()
        {
            SecureLicenseStorage.SetDemoUsedSeconds(60f);
            manager.StartDemo(900f);
            manager.Tick(Frame, true);
            for (int i = 0; i < 72 * 30; i++) manager.Tick(Frame, true);           // 30 s of play
            float before = manager.TotalDemoUsedSeconds;
            Assert.AreEqual(90f, before, 0.1f);

            for (int i = 0; i < 1200; i++) manager.Tick(1f, false, "headset not worn"); // off, frames still coming
            manager.Tick(1200f, true);                                             // or: frames stopped, one huge frame
            manager.Tick(1200f, true);

            Assert.AreEqual(before, manager.TotalDemoUsedSeconds, 1e-3f);
            Assert.AreEqual(0, expiredEvents);
            Assert.IsTrue(manager.IsRunning);
            Assert.AreEqual(810f, manager.RemainingDemoSeconds, 0.1f, "the user gets back exactly what was left");
        }

        [Test]
        public void Manager_ExpiresAtTheLimit_AndStoresTheLimit()
        {
            manager.StartDemo(10f);
            for (int i = 0; i < 40 && manager.IsRunning; i++) manager.Tick(1f, true);

            Assert.AreEqual(1, expiredEvents);
            Assert.IsFalse(manager.IsRunning);
            Assert.AreEqual(10f, SecureLicenseStorage.GetDemoUsedSeconds(), "stored total = the limit: the delta stops there");
            Assert.AreEqual(10f, manager.TotalDemoUsedSeconds);
        }

        [Test]
        public void Manager_NothingIsCountedOutsideTheDemo()
        {
            SecureLicenseStorage.SetDemoUsedSeconds(120f);

            for (int i = 0; i < 100; i++) manager.Tick(1f, true);                  // never started (welcome / licensed / expired panel)
            Assert.AreEqual(120f, manager.TotalDemoUsedSeconds);

            manager.StartDemo(900f);
            manager.Tick(1f, true);
            for (int i = 0; i < 5; i++) manager.Tick(1f, true);
            manager.StopDemo();                                                    // key redeemed, plans opened, subscribed…
            for (int i = 0; i < 100; i++) manager.Tick(1f, true);

            Assert.AreEqual(125f, SecureLicenseStorage.GetDemoUsedSeconds(), 1e-3f);
            Assert.AreEqual(125f, manager.TotalDemoUsedSeconds, 1e-3f);
            Assert.AreEqual(0f, DemoReportLedger.Delta(125f, 125f, 900f), "and with the server in sync there is no delta to report");
        }

        [Test]
        public void Manager_RaiseAfterExpiry_ReopensAndCountsAgain()
        {
            manager.StartDemo(10f);
            for (int i = 0; i < 40 && manager.IsRunning; i++) manager.Tick(1f, true);
            Assert.AreEqual(1, expiredEvents);
            Assert.IsTrue(manager.IsDemoExpired);

            manager.SetLimit(7200f);                                               // admin raises the headset to 2 h
            Assert.IsFalse(manager.IsDemoExpired, "a raised limit makes the demo available again");
            Assert.IsTrue(DemoLimit.Reopens(10f, 10f, 7200f));

            manager.StartDemo(7200f);
            Assert.AreEqual(1, expiredEvents, "restart under the new limit does not expire");
            manager.Tick(1f, true);
            for (int i = 0; i < 5; i++) manager.Tick(1f, true);
            Assert.AreEqual(15f, manager.TotalDemoUsedSeconds, 1e-3f);
        }

        [Test]
        public void Manager_UserPresenceProviderIsRespectedByDefaultClock()
        {
            // The provider is what Update consults; here it only has to be settable and read.
            bool worn = false;
            manager.UserPresenceProvider = () => worn;
            Assert.IsFalse(manager.UserPresenceProvider());
            worn = true;
            Assert.IsTrue(manager.UserPresenceProvider());
        }
    }
}
