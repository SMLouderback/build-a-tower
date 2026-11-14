using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class PowerHudTests
    {
        [Test]
        public void FormatPower_renders_use_and_capacity()
        {
            Assert.AreEqual("Power 48/70", PowerHudFormat.FormatPower(48, 70));
            Assert.AreEqual("Power 80/70", PowerHudFormat.FormatPower(80, 70));
        }

        [Test]
        public void FormatWater_rounds_pressure_percent()
        {
            Assert.AreEqual("Water 72%", PowerHudFormat.FormatWater(72.4f));
            Assert.AreEqual("Water 148%", PowerHudFormat.FormatWater(148f));
        }

        [Test]
        public void FormatRoomCost_prefixes_power_draw()
        {
            Assert.AreEqual("Power 2", PowerHudFormat.FormatRoomCost(2));
            Assert.AreEqual("Power 1", PowerHudFormat.FormatRoomCost(1));
        }

        [Test]
        public void FormatPlant_shows_built_or_roman_level_and_output()
        {
            Assert.AreEqual("Power plant Built · 50", PowerHudFormat.FormatPlant(0, 50));
            Assert.AreEqual("Power plant II · 120", PowerHudFormat.FormatPlant(2, 120));
        }

        [Test]
        public void FormatPump_shows_built_or_roman_level_and_supply()
        {
            Assert.AreEqual("Pump Built · 80", PowerHudFormat.FormatPump(0, 80));
            Assert.AreEqual("Pump I · 120", PowerHudFormat.FormatPump(1, 120));
        }

        [Test]
        public void FormatUpgrade_shows_target_level_days_and_fee()
        {
            Assert.AreEqual(
                "Upgrade to II · 3 days · $90,000",
                PowerHudFormat.FormatUpgrade(2, 3, 90000));
        }
    }
}
