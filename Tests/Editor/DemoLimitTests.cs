using NUnit.Framework;
using UnityEngine;
using VRLicensing;

namespace VRLicensing.Tests
{
    /// <summary>
    /// Server-decided demo length (spec 10 §6, v1.12.0-pre.13): which limit applies, what a
    /// raised or lowered limit does, and the sealed cache in SecureLicenseStorage.
    /// </summary>
    public class DemoLimitTests
    {
        [Test]
        public void ServerLimitWins()
        {
            Assert.AreEqual(7200f, DemoLimit.Resolve(7200f, 900f, 900f), "server over cache and config");
            Assert.AreEqual(600f, DemoLimit.Resolve(600f, 7200f, 900f), "a lower server value still wins");
        }

        [Test]
        public void CacheUsedOffline()
        {
            Assert.AreEqual(7200f, DemoLimit.Resolve(0f, 7200f, 900f), "no server answer this run → cached value");
            Assert.AreEqual(7200f, DemoLimit.Resolve(-1f, 7200f, 900f), "negative server value is ignored");
        }

        [Test]
        public void ConfigUsedWithNoCache()
        {
            Assert.AreEqual(900f, DemoLimit.Resolve(0f, 0f, 900f));
            Assert.AreEqual(0f, DemoLimit.Resolve(0f, 0f, -5f), "config never goes negative");
        }

        [Test]
        public void RaiseUnexpires()
        {
            // 15-minute demo fully used, admin raises the headset to 2 h.
            Assert.IsTrue(DemoLimit.IsExpired(900f, 900f));
            Assert.IsTrue(DemoLimit.Reopens(900f, 900f, 7200f));
            Assert.IsFalse(DemoLimit.IsExpired(900f, 7200f));
            Assert.IsFalse(DemoLimit.Reopens(100f, 900f, 7200f), "a demo that was not exhausted is extended, not reopened");
            Assert.IsFalse(DemoLimit.Reopens(900f, 900f, 900f), "same limit changes nothing");
        }

        [Test]
        public void LowerExpires()
        {
            // 25 minutes used of a 2 h demo, admin sets the headset back to 15 min.
            Assert.IsTrue(DemoLimit.Expires(1500f, 7200f, 900f));
            Assert.IsTrue(DemoLimit.IsExpired(1500f, 900f));
            Assert.IsFalse(DemoLimit.Expires(600f, 7200f, 900f), "under the new limit the demo goes on");
            Assert.IsFalse(DemoLimit.Expires(1500f, 900f, 7200f), "raising never expires");
        }

        [Test]
        public void DemoModeManager_SetLimit_RaiseReopensAndLowerExpires()
        {
            var go = new GameObject("DemoLimitTests");
            try
            {
                SecureLicenseStorage.ClearAll();
                SecureLicenseStorage.MarkInitialized();
                SecureLicenseStorage.SetDemoUsedSeconds(1000f);

                var dm = go.AddComponent<DemoModeManager>();
                int expiredEvents = 0;
                dm.OnDemoExpired += () => expiredEvents++;

                dm.StartDemo(900f);                       // already over the 15-minute limit
                Assert.AreEqual(1, expiredEvents, "StartDemo on an exhausted demo fires the expiry");
                Assert.IsTrue(dm.IsDemoExpired);

                dm.SetLimit(7200f);                       // server raises the headset to 2 h
                Assert.AreEqual(7200f, dm.LimitSeconds);
                Assert.IsFalse(dm.IsDemoExpired, "a raised limit makes the demo available again");
                Assert.AreEqual(6200f, dm.RemainingDemoSeconds, 0.5f);

                dm.StartDemo(7200f);
                Assert.AreEqual(1, expiredEvents, "restart under the new limit does not expire");

                dm.SetLimit(900f);                        // server lowers it again
                Assert.IsTrue(dm.IsDemoExpired, "a lowered limit already exceeded expires at the next check");
                Assert.AreEqual(0f, dm.RemainingDemoSeconds);
            }
            finally
            {
                Object.DestroyImmediate(go);
                SecureLicenseStorage.ClearAll();
            }
        }

        [Test]
        public void Storage_CachesServerLimitInsideTheSeal()
        {
            SecureLicenseStorage.ClearAll();
            try
            {
                SecureLicenseStorage.MarkInitialized();
                Assert.AreEqual(0f, SecureLicenseStorage.GetDemoLimitSeconds(), "nothing cached yet");

                SecureLicenseStorage.SetDemoLimitSeconds(7200f);
                Assert.AreEqual(7200f, SecureLicenseStorage.GetDemoLimitSeconds());
                Assert.IsTrue(SecureLicenseStorage.VerifyIntegrity(), "the cached limit is part of the seal");

                PlayerPrefs.SetString("vrl_demo_limit", "tampered");
                Assert.IsFalse(SecureLicenseStorage.VerifyIntegrity(), "editing the cached limit breaks the seal");
                Assert.AreEqual(0f, SecureLicenseStorage.GetDemoLimitSeconds(), "an unreadable cache reads as none");
            }
            finally
            {
                SecureLicenseStorage.ClearAll();
            }
        }
    }
}
