using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class HealthSystemTests
    {
        [Test]
        public void Guest_floor_without_trash_rises()
        {
            var health = new HealthSystem();
            health.Tick(1f,
                new Dictionary<int, int> { [3] = 2 },
                new Dictionary<int, bool> { [3] = false },
                new Dictionary<int, int>(),
                new List<int>(),
                towerFloorCount: 1);
            Assert.AreEqual(0.08f, health.GetIllness(3), 0.0001f);
        }

        [Test]
        public void Guest_floor_with_trash_does_not_rise()
        {
            var health = new HealthSystem();
            health.Tick(1f,
                new Dictionary<int, int> { [3] = 4 },
                new Dictionary<int, bool> { [3] = true },
                new Dictionary<int, int>(),
                new List<int>(),
                towerFloorCount: 1);
            Assert.AreEqual(0f, health.GetIllness(3), 0.0001f);
        }

        [Test]
        public void Clinic_on_its_floor_beats_eight_floors_away_and_stacks()
        {
            var near = new HealthSystem();
            near.SetIllness(0, 50f);
            near.SetIllness(8, 50f);
            near.Tick(1f, new Dictionary<int, int>(), new Dictionary<int, bool>(),
                new Dictionary<int, int>(), new List<int> { 0 }, towerFloorCount: 9);
            var farDrop = 50f - near.GetIllness(8);
            var nearDrop = 50f - near.GetIllness(0);
            Assert.Greater(nearDrop, farDrop);

            var two = new HealthSystem();
            two.SetIllness(0, 50f);
            two.Tick(1f, new Dictionary<int, int>(), new Dictionary<int, bool>(),
                new Dictionary<int, int>(), new List<int> { 0, 1 }, towerFloorCount: 2);
            Assert.Less(two.GetIllness(0), near.GetIllness(0));
        }

        [Test]
        public void Fill_is_full_below_threshold_and_085_at_threshold()
        {
            Assert.AreEqual(1f, HealthSystem.FillMultiplier(0.09f, HealthSystem.HotelFillThreshold), 0.0001f);
            Assert.AreEqual(0.85f, HealthSystem.FillMultiplier(0.10f, HealthSystem.HotelFillThreshold), 0.0001f);
            Assert.AreEqual(0f, HealthSystem.FillMultiplier(0.25f, HealthSystem.HotelFillThreshold), 0.0001f);
            Assert.AreEqual(0.85f, HealthSystem.FillMultiplier(0.15f, HealthSystem.OfficeFillThreshold), 0.0001f);
            Assert.AreEqual(0.85f, HealthSystem.FillMultiplier(0.20f, HealthSystem.CondoFillThreshold), 0.0001f);
        }

        [Test]
        public void Quarantine_latches_until_22_percent()
        {
            Assert.IsFalse(HealthSystem.EvaluateQuarantine(false, 0.24f));
            Assert.IsTrue(HealthSystem.EvaluateQuarantine(false, 0.25f));
            Assert.IsTrue(HealthSystem.EvaluateQuarantine(true, 0.23f));
            Assert.IsFalse(HealthSystem.EvaluateQuarantine(true, 0.22f));
        }
    }
}
