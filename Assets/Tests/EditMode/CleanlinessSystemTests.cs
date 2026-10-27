using System.Linq;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class CleanlinessSystemTests
    {
        [Test]
        public void Target_is_100_when_nobody_is_home()
        {
            Assert.AreEqual(100f, CleanlinessSystem.Target(0, 0));
            Assert.AreEqual(100f, CleanlinessSystem.Target(12, 12));
            Assert.AreEqual(50f, CleanlinessSystem.Target(12, 24));
            Assert.AreEqual(0f, CleanlinessSystem.Target(0, 8));
            Assert.AreEqual(400f, CleanlinessSystem.Target(48, 12));
        }

        [Test]
        public void Step_closes_a_tenth_of_the_gap()
        {
            Assert.AreEqual(90f, CleanlinessSystem.Step(100f, 0f));
            Assert.AreEqual(81f, CleanlinessSystem.Step(90f, 0f));
            Assert.AreEqual(72.9f, CleanlinessSystem.Step(81f, 0f), 0.001f);
        }

        [Test]
        public void Effects_use_the_locked_slopes()
        {
            Assert.AreEqual(1f, CleanlinessSystem.Dirt(0f));
            Assert.AreEqual(0.5f, CleanlinessSystem.Dirt(50f));
            Assert.AreEqual(0f, CleanlinessSystem.Dirt(100f));
            Assert.AreEqual(0f, CleanlinessSystem.Dirt(140f));
            Assert.AreEqual(0.85f, CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 0f));
            Assert.AreEqual(0.65f, CleanlinessSystem.FillMultiplier(LuxuryBand.Mid, 0f));
            Assert.AreEqual(0.35f, CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 0f));
            Assert.AreEqual(0.85f, CleanlinessSystem.FillMultiplier(LuxuryBand.None, 0f));
            Assert.AreEqual(0.925f, CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 50f), 0.001f);
            Assert.AreEqual(1f, CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 140f));
            Assert.AreEqual(0.08f, CleanlinessSystem.IllnessPerMinute(0f));
            Assert.AreEqual(0.04f, CleanlinessSystem.IllnessPerMinute(50f));
            Assert.AreEqual(0f, CleanlinessSystem.IllnessPerMinute(100f));
            Assert.AreEqual(0.60f, CleanlinessSystem.RestaurantWeightFactor(0f));
            Assert.AreEqual(0.80f, CleanlinessSystem.RestaurantWeightFactor(50f));
            Assert.AreEqual(1f, CleanlinessSystem.RestaurantWeightFactor(140f));
        }

        [Test]
        public void Treatment_table_matches_the_pump_clock_shape()
        {
            CollectionAssert.AreEqual(new[] { 12, 20, 32, 48 },
                new[] { 0, 1, 2, 3 }.Select(CleanlinessSystem.TreatmentSupply).ToArray());
            Assert.AreEqual(30000, CleanlinessSystem.TreatmentUpgradeFee(1));
            Assert.AreEqual(70000, CleanlinessSystem.TreatmentUpgradeFee(2));
            Assert.AreEqual(140000, CleanlinessSystem.TreatmentUpgradeFee(3));
            Assert.AreEqual(0, CleanlinessSystem.TreatmentUpgradeFee(4));
            Assert.AreEqual(1440, CleanlinessSystem.TreatmentUpgradeMinutes(1));
            Assert.AreEqual(4320, CleanlinessSystem.TreatmentUpgradeMinutes(2));
            Assert.AreEqual(10080, CleanlinessSystem.TreatmentUpgradeMinutes(3));
        }
    }
}
