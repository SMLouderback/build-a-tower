using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class FloorValueRulesTests
    {
        [Test]
        public void Street_shop_has_high_fit_on_podium_floors()
        {
            for (var y = 0; y <= 3; y++)
            {
                var fit = FloorValueRules.Fit01(EconomicFamily.Shops, TenantClass.Mid, y, stars: 0);
                Assert.GreaterOrEqual(fit, 0.95f, $"floor {y}");
                Assert.LessOrEqual(fit, FloorValueRules.FitMax);
            }
        }

        [Test]
        public void Street_leisure_has_high_fit_on_podium()
        {
            var fit = FloorValueRules.Fit01(EconomicFamily.Leisure, TenantClass.Mid, floorY: 1, stars: 1);
            Assert.GreaterOrEqual(fit, 0.95f);
        }

        [Test]
        public void Upper_condo_has_higher_fit_on_prestige_than_street()
        {
            var stars = 0;
            var midCap = FloorValueRules.MidCap(stars);
            var prestigeY = midCap + 1;
            var prestige = FloorValueRules.Fit01(EconomicFamily.Condo, TenantClass.Upper, prestigeY, stars);
            var street = FloorValueRules.Fit01(EconomicFamily.Condo, TenantClass.Upper, floorY: 1, stars);
            Assert.Greater(prestige, street);
            Assert.GreaterOrEqual(prestige, 0.95f);
            Assert.LessOrEqual(street, FloorValueRules.WarningThreshold);
            Assert.GreaterOrEqual(street, FloorValueRules.FitMin);
        }

        [Test]
        public void MidCap_scales_up_with_stars()
        {
            Assert.Greater(FloorValueRules.MidCap(3), FloorValueRules.MidCap(0));
            Assert.Greater(FloorValueRules.MidCap(5), FloorValueRules.MidCap(3));
        }

        [Test]
        public void Star_scaled_mid_cap_moves_prestige_boundary()
        {
            // Floor that is prestige at 0★ becomes mid-band once mid-cap rises with stars.
            var floorY = FloorValueRules.MidCap(0) + 1;
            Assert.Greater(floorY, FloorValueRules.MidCap(0));
            Assert.LessOrEqual(floorY, FloorValueRules.MidCap(5));

            var shopAt0 = FloorValueRules.Fit01(EconomicFamily.Shops, TenantClass.Mid, floorY, stars: 0);
            var shopAt5 = FloorValueRules.Fit01(EconomicFamily.Shops, TenantClass.Mid, floorY, stars: 5);
            Assert.Less(shopAt0, shopAt5);
            Assert.LessOrEqual(shopAt0, FloorValueRules.WarningThreshold);
        }

        [Test]
        public void Fit01_clamps_soft_range()
        {
            var badShop = FloorValueRules.Fit01(EconomicFamily.Shops, TenantClass.Mid,
                floorY: FloorValueRules.MidCap(0) + 5, stars: 0);
            Assert.GreaterOrEqual(badShop, FloorValueRules.FitMin);
            Assert.LessOrEqual(badShop, FloorValueRules.FitMax);

            var ideal = FloorValueRules.Fit01(EconomicFamily.Shops, TenantClass.Mid, floorY: 0, stars: 0);
            Assert.AreEqual(FloorValueRules.FitMax, ideal, 0.001f);
        }

        [Test]
        public void TryWarning_when_fit_below_threshold_returns_copy()
        {
            var prestigeY = FloorValueRules.MidCap(0) + 2;
            Assert.IsTrue(FloorValueRules.TryWarning(
                EconomicFamily.Shops, TenantClass.Mid, prestigeY, stars: 0, out var reason));
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason));
            StringAssert.Contains("lower", reason.ToLowerInvariant());
        }

        [Test]
        public void TryWarning_Upper_on_street_mentions_premium_floors()
        {
            Assert.IsTrue(FloorValueRules.TryWarning(
                EconomicFamily.Office, TenantClass.Upper, floorY: 1, stars: 0, out var reason));
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason));
            StringAssert.Contains("above", reason.ToLowerInvariant());
        }

        [Test]
        public void TryWarning_false_when_fit_is_good()
        {
            Assert.IsFalse(FloorValueRules.TryWarning(
                EconomicFamily.Shops, TenantClass.Mid, floorY: 1, stars: 0, out var reason));
            Assert.IsTrue(string.IsNullOrEmpty(reason));
        }

        [Test]
        public void Mid_office_flexible_in_mid_band()
        {
            var midY = 5;
            Assert.LessOrEqual(midY, FloorValueRules.MidCap(0));
            Assert.GreaterOrEqual(midY, 4);
            var fit = FloorValueRules.Fit01(EconomicFamily.Office, TenantClass.Mid, midY, stars: 0);
            Assert.GreaterOrEqual(fit, 0.95f);
        }
    }
}
