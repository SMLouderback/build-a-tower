using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class VisitHistoryRingTests
    {
        [Test]
        public void Empty_ring_yesterday_and_average_are_zero()
        {
            var ring = new VisitHistoryRing();
            Assert.AreEqual(0, ring.Yesterday);
            Assert.AreEqual(0f, ring.Average());
            Assert.AreEqual(0, ring.RecordedDays);
        }

        [Test]
        public void Single_push_is_yesterday_and_average()
        {
            var ring = new VisitHistoryRing();
            ring.Push(12);
            Assert.AreEqual(12, ring.Yesterday);
            Assert.AreEqual(12f, ring.Average());
            Assert.AreEqual(1, ring.RecordedDays);
        }

        [Test]
        public void Average_uses_only_recorded_days()
        {
            var ring = new VisitHistoryRing();
            ring.Push(10);
            ring.Push(20);
            Assert.AreEqual(20, ring.Yesterday);
            Assert.AreEqual(15f, ring.Average());
            Assert.AreEqual(2, ring.RecordedDays);
        }

        [Test]
        public void Eighth_push_drops_oldest()
        {
            var ring = new VisitHistoryRing();
            for (var i = 1; i <= 7; i++)
                ring.Push(i * 10);

            Assert.AreEqual(70, ring.Yesterday);
            Assert.AreEqual(40f, ring.Average()); // (10+20+...+70)/7 = 40

            ring.Push(100);
            Assert.AreEqual(100, ring.Yesterday);
            Assert.AreEqual(7, ring.RecordedDays);
            // Dropped 10: (20+30+40+50+60+70+100)/7 = 370/7
            Assert.AreEqual(370f / 7f, ring.Average(), 0.001f);
        }

        [Test]
        public void Negative_visits_clamp_to_zero()
        {
            var ring = new VisitHistoryRing();
            ring.Push(-3);
            Assert.AreEqual(0, ring.Yesterday);
        }
    }
}
