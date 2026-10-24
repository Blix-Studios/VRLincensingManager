using System;
using System.Collections.Generic;
using NUnit.Framework;
using VRLicensing;

namespace VRLicensing.Tests
{
    public class StoreEventQueueTests
    {
        private string stored;
        private int saves;

        private StoreEventQueue NewQueue()
        {
            return new StoreEventQueue(() => stored, s => { stored = s; saves++; });
        }

        [SetUp]
        public void SetUp()
        {
            stored = null;
            saves = 0;
        }

        [Test]
        public void Enqueue_WritesNameSkuTermAndTimeOnly()
        {
            var q = NewQueue();
            q.Enqueue(StoreEventQueue.CheckoutOpened, "sub_access", "MONTHLY", new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc));

            string json = q.PeekJson();
            Assert.AreEqual("[{\"name\":\"checkout_opened\",\"sku\":\"sub_access\",\"term\":\"MONTHLY\",\"at\":\"2026-10-23T12:00:00.0000000Z\"}]", json);
            StringAssert.DoesNotContain("user", json);
            StringAssert.DoesNotContain("device", json);
        }

        [Test]
        public void PlansOpened_HasEmptyTerm()
        {
            var q = NewQueue();
            q.Enqueue(StoreEventQueue.PlansOpened, "sub_access", null, DateTime.UtcNow);
            StringAssert.Contains("\"name\":\"plans_opened\"", q.PeekJson());
            StringAssert.Contains("\"term\":\"\"", q.PeekJson());
        }

        [Test]
        public void IsImmediate_PlansOpenedAndCheckoutsOnly()
        {
            Assert.IsTrue(StoreEventQueue.IsImmediate(StoreEventQueue.PlansOpened));
            Assert.IsTrue(StoreEventQueue.IsImmediate(StoreEventQueue.CheckoutOpened));
            Assert.IsTrue(StoreEventQueue.IsImmediate(StoreEventQueue.CheckoutCompleted));
            Assert.IsTrue(StoreEventQueue.IsImmediate(StoreEventQueue.CheckoutCancelled));
            Assert.IsFalse(StoreEventQueue.IsImmediate(StoreEventQueue.SubscriptionVerified));
            Assert.IsFalse(StoreEventQueue.IsImmediate(null));
        }

        [Test]
        public void Enqueue_PersistsOneObjectPerLine()
        {
            var q = NewQueue();
            q.Enqueue(StoreEventQueue.PlansOpened, "sub_access", null, DateTime.UtcNow);
            q.Enqueue(StoreEventQueue.CheckoutOpened, "sub_access", "ANNUAL", DateTime.UtcNow);

            Assert.AreEqual(2, saves);
            Assert.AreEqual(2, stored.Split('\n').Length);

            var reloaded = NewQueue();
            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual(q.PeekJson(), reloaded.PeekJson());
        }

        [Test]
        public void Drain_ReturnsArrayClearsAndPersistsEmpty()
        {
            var q = NewQueue();
            q.Enqueue(StoreEventQueue.CheckoutCompleted, "sub_access", "SEMIANNUAL", DateTime.UtcNow);

            string drained = q.Drain();
            StringAssert.StartsWith("[{", drained);
            Assert.AreEqual(0, q.Count);
            Assert.AreEqual("", stored);
            Assert.IsNull(q.Drain());
            Assert.AreEqual(0, NewQueue().Count);
        }

        [Test]
        public void Restore_PutsDrainedEventsBackInOrderAndPersists()
        {
            var q = NewQueue();
            q.Enqueue(StoreEventQueue.CheckoutOpened, "sub_access", "MONTHLY", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            q.Enqueue(StoreEventQueue.CheckoutCancelled, "sub_access", "MONTHLY", new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc));
            string drained = q.Drain();

            q.Enqueue(StoreEventQueue.PlansOpened, "sub_access", null, new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
            q.Restore(drained);

            Assert.AreEqual(3, q.Count);
            var parts = StoreEventQueue.SplitArray(q.PeekJson());
            StringAssert.Contains("checkout_opened", parts[0]);
            StringAssert.Contains("checkout_cancelled", parts[1]);
            StringAssert.Contains("plans_opened", parts[2]);
            Assert.AreEqual(3, NewQueue().Count);
        }

        [Test]
        public void Cap_KeepsTheNewestFifty()
        {
            var q = NewQueue();
            for (int i = 0; i < StoreEventQueue.MaxEvents + 5; i++)
                q.Enqueue(StoreEventQueue.PlansOpened, "sub_access", null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i));

            Assert.AreEqual(StoreEventQueue.MaxEvents, q.Count);
            var parts = StoreEventQueue.SplitArray(q.PeekJson());
            StringAssert.Contains("2026-01-01T00:00:05", parts[0]);   // the five oldest were dropped
            StringAssert.Contains("2026-01-01T00:00:54", parts[parts.Count - 1]);
        }

        [Test]
        public void Escape_KeepsJsonValid()
        {
            var q = NewQueue();
            q.Enqueue("weird\"name", "sku\\x", "term\n", DateTime.UtcNow);
            string json = q.PeekJson();
            StringAssert.Contains("\"name\":\"weird\\\"name\"", json);
            StringAssert.Contains("\"sku\":\"sku\\\\x\"", json);
            StringAssert.Contains("\"term\":\"term\\n\"", json);
            Assert.AreEqual(1, StoreEventQueue.SplitArray(json).Count);
        }

        [Test]
        public void Load_IgnoresGarbageLines()
        {
            stored = "not json\n{\"name\":\"plans_opened\",\"sku\":\"s\",\"term\":\"\",\"at\":\"x\"}\n\n   \n";
            Assert.AreEqual(1, NewQueue().Count);
        }
    }
}
