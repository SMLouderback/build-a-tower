using NUnit.Framework;

namespace BuildATower.Tests
{
    public class PricePricingTests
    {
        [TestCase(0, 0.7f)]
        [TestCase(1, 1f)]
        [TestCase(2, 1.3f)]
        [TestCase(3, 1.6f)]
        public void PayoutMultiplier_matches_tier_table(int tier, float expected)
        {
            Assert.AreEqual(expected, PricePricing.PayoutMultiplier(tier), 0.0001f);
        }

        [Test]
        public void ScaledIncome_rounds_nearest()
        {
            Assert.AreEqual(2100, PricePricing.ScaledIncome(3000, PricePricing.TierLow));
            Assert.AreEqual(4800, PricePricing.ScaledIncome(3000, PricePricing.TierMax));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        public void ComfortMaxTier_follows_five_star_band(int stars, int expectedTier)
        {
            Assert.AreEqual(expectedTier, PricePricing.ComfortMaxTier(stars));
        }

        [Test]
        public void DemandChance_full_when_at_or_under_comfort()
        {
            Assert.AreEqual(1f, PricePricing.DemandChance(PricePricing.TierNormal, stars: 1));
        }

        [Test]
        public void DemandChance_drops_when_overpriced()
        {
            Assert.AreEqual(0.4f, PricePricing.DemandChance(PricePricing.TierHigh, stars: 1), 0.0001f);
            Assert.AreEqual(0.1f, PricePricing.DemandChance(PricePricing.TierMax, stars: 0), 0.0001f);
        }

        [Test]
        public void MarketHint_reports_ok_or_overpriced()
        {
            StringAssert.Contains("OK", PricePricing.MarketHint(PricePricing.TierNormal, 1));
            StringAssert.Contains("Overpriced", PricePricing.MarketHint(PricePricing.TierMax, 1));
        }
    }
}
