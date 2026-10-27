using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class CleanlinessHudTests
    {
        [Test]
        public void FormatClean_rounds_stored_percent()
        {
            Assert.AreEqual("Clean 72%", PowerHudFormat.FormatClean(72.4f));
            Assert.AreEqual("Clean 140%", PowerHudFormat.FormatClean(140f));
        }

        [Test]
        public void FormatTreatment_shows_built_or_roman_level_and_output()
        {
            Assert.AreEqual(
                "Treatment Built · 12",
                PowerHudFormat.FormatTreatment(0, CleanlinessSystem.TreatmentSupply(0)));
            Assert.AreEqual(
                "Treatment II · 32",
                PowerHudFormat.FormatTreatment(2, CleanlinessSystem.TreatmentSupply(2)));
        }

        [Test]
        public void FormatUpgrade_treatment_level_two_matches_locked_fee_and_days()
        {
            const int nextLevel = 2;
            var days = CleanlinessSystem.TreatmentUpgradeMinutes(nextLevel) / 1440;
            var fee = CleanlinessSystem.TreatmentUpgradeFee(nextLevel);
            Assert.AreEqual(
                "Upgrade to II · 3 days · $70,000",
                PowerHudFormat.FormatUpgrade(nextLevel, days, fee));
        }
    }
}
