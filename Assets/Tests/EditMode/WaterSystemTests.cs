using System.Linq;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class WaterSystemTests
    {
        const float Delta = 0.0001f;

        [Test]
        public void Height_counts_floors_from_lobby_inclusive()
        {
            Assert.AreEqual(15, WaterSystem.Height(0, 14));
            Assert.AreEqual(1, WaterSystem.Height(0, 0));
        }

        [Test]
        public void Demand_scales_with_height_and_upper_rooms()
        {
            Assert.AreEqual(54, WaterSystem.Demand(15, 4, 2));
            Assert.AreEqual(80, WaterSystem.Demand(40, 0, 0));
            Assert.AreEqual(120, WaterSystem.Demand(40, 10, 0));
        }

        [Test]
        public void PressurePercent_is_supply_over_demand_times_one_hundred()
        {
            Assert.AreEqual(80f / 54f * 100f, WaterSystem.PressurePercent(80, 54), Delta);
            Assert.AreEqual(80f / 120f * 100f, WaterSystem.PressurePercent(80, 120), Delta);
        }

        [Test]
        public void Pump_supply_ladder_and_upgrade_tables()
        {
            CollectionAssert.AreEqual(new[] { 80, 120, 180, 260 },
                Enumerable.Range(0, 4).Select(WaterSystem.PumpSupply).ToArray());
            Assert.AreEqual(30000, WaterSystem.PumpUpgradeFee(1));
            Assert.AreEqual(70000, WaterSystem.PumpUpgradeFee(2));
            Assert.AreEqual(140000, WaterSystem.PumpUpgradeFee(3));
            Assert.AreEqual(1440, WaterSystem.PumpUpgradeMinutes(1));
            Assert.AreEqual(4320, WaterSystem.PumpUpgradeMinutes(2));
            Assert.AreEqual(10080, WaterSystem.PumpUpgradeMinutes(3));
            Assert.AreEqual(0, WaterSystem.PumpUpgradeFee(4));
            Assert.AreEqual(0, WaterSystem.PumpUpgradeMinutes(0));
        }

        [Test]
        public void Upper_fill_multiplier_tracks_pressure()
        {
            Assert.AreEqual(0.25f, WaterSystem.UpperFillMultiplier(0f), Delta);
            Assert.AreEqual(0.625f, WaterSystem.UpperFillMultiplier(50f), Delta);
            Assert.AreEqual(1f, WaterSystem.UpperFillMultiplier(100f), Delta);
            Assert.AreEqual(1f, WaterSystem.UpperFillMultiplier(140f), Delta);
        }

        [Test]
        public void Upper_stress_gain_falls_to_zero_at_full_pressure()
        {
            Assert.AreEqual(8, WaterSystem.UpperStressGain(0f));
            Assert.AreEqual(4, WaterSystem.UpperStressGain(50f));
            Assert.AreEqual(0, WaterSystem.UpperStressGain(100f));
            Assert.AreEqual(0, WaterSystem.UpperStressGain(140f));
        }
    }
}
