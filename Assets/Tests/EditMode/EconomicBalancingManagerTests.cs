using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class EconomicBalancingManagerTests
    {
        [Test]
        public void PeriodIncome_Normal_Mid_office_tier_sample_is_golden()
        {
            var so = MidOffice(cellsW: 4, cellsH: 1);
            // 120 * 4 * tier4(1.0) * mid(1.0) * priceNormal(1) * diff(1) * 1 * 1 * 1
            Assert.AreEqual(480, EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Normal,
                climateSpendMult: 1f, pulseMult: 1f, floorFit01: 1f));
        }

        [Test]
        public void PeriodUpkeep_Mid_is_catalog_baseline_of_rent_equivalent()
        {
            var so = MidOffice(4, 1);
            var rent = EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f);
            var upkeep = EconomicBalancingManager.PeriodUpkeep(
                so, GameDifficulty.Normal, pulseMult: 1f);
            Assert.AreEqual(480, rent);
            Assert.AreEqual(120, upkeep);
            Assert.AreEqual(
                VpsfCatalog.UpkeepRatioMid,
                (float)upkeep / rent,
                0.001f);
        }

        [Test]
        public void PeriodUpkeep_Upper_is_about_15_percent_of_rent_equivalent()
        {
            var so = Office(OfficeLuxury.UpperStandardId, LuxuryBand.Upper, 4, 1);
            // tier7 1.35 * classUpper 1.35 → 120*4*1.35*1.35 = 874.8 → 875 income
            var rent = EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f);
            var upkeep = EconomicBalancingManager.PeriodUpkeep(
                so, GameDifficulty.Normal, pulseMult: 1f);
            Assert.AreEqual(875, rent);
            Assert.AreEqual((int)Math.Round(875 * VpsfCatalog.UpkeepRatioUpper), upkeep);
            Assert.AreEqual(VpsfCatalog.UpkeepRatioUpper, (float)upkeep / rent, 0.01f);
        }

        [Test]
        public void PeriodUpkeep_Lower_ratio_constant_is_about_40_percent()
        {
            // Living catalog has no Lower-class ids yet (Base→Mid). Lock the catalog
            // ratio and the balancer formula: upkeep = rentEq * UpkeepRatio(class) * …
            Assert.AreEqual(0.40f, VpsfCatalog.UpkeepRatioLower, 0.001f);
            Assert.AreEqual(0.40f, VpsfCatalog.UpkeepRatio(TenantClass.Lower), 0.001f);

            var so = MidOffice(4, 1);
            var rentEqMid = 120f * 4f * VpsfCatalog.TierMultiplier(4) * VpsfCatalog.ClassMultMid;
            var expectedIfLower = (int)Math.Round(
                rentEqMid / VpsfCatalog.ClassMultMid * VpsfCatalog.ClassMultLower
                * VpsfCatalog.UpkeepRatioLower);
            Assert.AreEqual(
                (int)Math.Round(480f * 0.85f * 0.40f),
                expectedIfLower);
            Assert.AreEqual(163, expectedIfLower);
        }

        [Test]
        public void Difficulty_scales_build_cost_and_income()
        {
            var so = MidOffice(4, 1);

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(so, GameDifficulty.Sandbox));

            var normalCost = EconomicBalancingManager.BuildCost(so, GameDifficulty.Normal);
            var hardCost = EconomicBalancingManager.BuildCost(so, GameDifficulty.Hard);
            Assert.AreEqual(8640, normalCost); // 480 * 18
            Assert.AreEqual(10_800, hardCost); // ceil(8640 * 1.25)

            var normalIncome = EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f);
            var easyIncome = EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Easy, 1f, 1f, 1f);
            var hardIncome = EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Hard, 1f, 1f, 1f);
            Assert.AreEqual(480, normalIncome);
            Assert.AreEqual(600, easyIncome);
            Assert.AreEqual(384, hardIncome);

            var normalUpkeep = EconomicBalancingManager.PeriodUpkeep(so, GameDifficulty.Normal, 1f);
            var hardUpkeep = EconomicBalancingManager.PeriodUpkeep(so, GameDifficulty.Hard, 1f);
            Assert.AreEqual(120, normalUpkeep);
            Assert.AreEqual(96, hardUpkeep); // Round(120 * 0.8) via income mult on rentEq path
        }

        [Test]
        public void PeriodIncome_applies_price_climate_pulse_and_floor_fit()
        {
            var so = MidOffice(4, 1);
            Assert.AreEqual(624, EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierHigh, GameDifficulty.Normal, 1f, 1f, 1f));

            // 480 * 1.1 * 0.9 * 0.8 = 380.16 → 380
            Assert.AreEqual(380, EconomicBalancingManager.PeriodIncome(
                so, PricePricing.TierNormal, GameDifficulty.Normal,
                climateSpendMult: 1.1f, pulseMult: 0.9f, floorFit01: 0.8f));
        }

        [Test]
        public void PeriodUpkeep_scales_with_pulse()
        {
            var so = MidOffice(4, 1);
            Assert.AreEqual(60, EconomicBalancingManager.PeriodUpkeep(
                so, GameDifficulty.Normal, pulseMult: 0.5f));
        }

        [Test]
        public void Unknown_or_null_type_returns_zero()
        {
            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.id = "shop_fast_food";
            shop.category = RoomCategory.Commercial;
            shop.size = new Vector2Int(2, 1);

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(shop, GameDifficulty.Normal));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodIncome(
                shop, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodUpkeep(shop, GameDifficulty.Normal, 1f));

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(null, GameDifficulty.Normal));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodIncome(
                null, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodUpkeep(null, GameDifficulty.Normal, 1f));
        }

        static RoomTypeSO MidOffice(int cellsW, int cellsH) =>
            Office(OfficeLuxury.MidStandardId, LuxuryBand.Mid, cellsW, cellsH);

        static RoomTypeSO Office(string id, LuxuryBand band, int cellsW, int cellsH)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Office;
            so.luxuryBand = band;
            so.size = new Vector2Int(cellsW, cellsH);
            so.incomeModel = IncomeModel.QuarterlyRent;
            return so;
        }
    }
}
