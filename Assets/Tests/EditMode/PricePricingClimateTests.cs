using NUnit.Framework;

namespace BuildATower.Tests
{
    public class PricePricingClimateTests
    {
        const int StarsTwo = 2;

        [Test]
        public void ComfortMaxTier_at_two_stars_boom_exceeds_normal()
        {
            var normal = PricePricing.ComfortMaxTier(StarsTwo, climateOffset: 0);
            var boom = PricePricing.ComfortMaxTier(StarsTwo, MarketClimate.Boom - MarketClimate.Normal);
            Assert.AreEqual(PricePricing.TierNormal, normal);
            Assert.Greater(boom, normal);
            Assert.AreEqual(PricePricing.TierMax, boom);
        }

        [Test]
        public void ComfortMaxTier_at_two_stars_recession_below_normal()
        {
            var normal = PricePricing.ComfortMaxTier(StarsTwo, climateOffset: 0);
            var recession = PricePricing.ComfortMaxTier(
                StarsTwo,
                MarketClimate.Recession - MarketClimate.Normal);
            Assert.Less(recession, normal);
            Assert.AreEqual(PricePricing.TierLow, recession);
        }

        [Test]
        public void DemandChance_high_tier_improves_under_boom_at_two_stars()
        {
            var underNormal = PricePricing.DemandChance(
                PricePricing.TierHigh,
                StarsTwo,
                climateOffset: 0);
            var underBoom = PricePricing.DemandChance(
                PricePricing.TierHigh,
                StarsTwo,
                MarketClimate.Boom - MarketClimate.Normal);
            Assert.AreEqual(0.4f, underNormal, 0.0001f);
            Assert.AreEqual(1f, underBoom, 0.0001f);
            Assert.Greater(underBoom, underNormal);
        }

        [Test]
        public void MarketHint_includes_climate_name_when_offset_nonzero()
        {
            var hint = PricePricing.MarketHint(
                PricePricing.TierNormal,
                StarsTwo,
                MarketClimate.Strong - MarketClimate.Normal);
            StringAssert.Contains("OK", hint);
            StringAssert.Contains("Strong", hint);
        }

        [Test]
        public void MarketHint_omits_climate_name_when_offset_zero()
        {
            var hint = PricePricing.MarketHint(PricePricing.TierNormal, StarsTwo, climateOffset: 0);
            StringAssert.Contains("OK", hint);
            StringAssert.DoesNotContain("Normal", hint);
            StringAssert.DoesNotContain("economy", hint);
        }
    }
}
