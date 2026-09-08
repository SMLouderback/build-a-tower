using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ShopDemandSystemTests
    {
        [TestCase(BuildSubgroup.Food, ShopDemandFamily.Food)]
        [TestCase(BuildSubgroup.Retail, ShopDemandFamily.Retail)]
        public void Family_maps_from_shop_subgroup(
            BuildSubgroup subgroup,
            ShopDemandFamily expected)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.buildFamily = BuildFamily.Shops;
            type.buildSubgroup = subgroup;

            Assert.AreEqual(expected, ShopDemandBalance.FamilyFor(type));

            Object.DestroyImmediate(type);
        }

        [TestCase(0, ShopDemandTier.Budget)]
        [TestCase(1, ShopDemandTier.Mid)]
        [TestCase(2, ShopDemandTier.Mid)]
        [TestCase(3, ShopDemandTier.Premium)]
        [TestCase(5, ShopDemandTier.Premium)]
        public void Shop_tier_maps_from_required_stars(
            int stars,
            ShopDemandTier expected)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.requiredStars = stars;

            Assert.AreEqual(expected, ShopDemandBalance.TierForShop(type));

            Object.DestroyImmediate(type);
        }

        [TestCase(WealthBand.Street, ShopDemandTier.Budget)]
        [TestCase(WealthBand.Basic, ShopDemandTier.Budget)]
        [TestCase(WealthBand.Mid, ShopDemandTier.Mid)]
        [TestCase(WealthBand.Upper, ShopDemandTier.Premium)]
        [TestCase(WealthBand.Premium, ShopDemandTier.Premium)]
        public void Demand_tier_maps_from_wealth(
            WealthBand wealth,
            ShopDemandTier expected) =>
            Assert.AreEqual(expected, ShopDemandBalance.TierForWealth(wealth));

        [Test]
        public void BeginDay_generates_deterministic_population_and_street_demand()
        {
            var agents = new List<Agent>
            {
                AgentOf(AgentRole.OfficeWorker, WealthBand.Basic),
                AgentOf(AgentRole.OfficeWorker, WealthBand.Basic),
                AgentOf(AgentRole.CondoResident, WealthBand.Mid),
                AgentOf(AgentRole.HotelGuest, WealthBand.Premium),
            };
            var demand = new ShopDemandSystem();

            demand.BeginDay(agents, stars: 2, climateMultiplier: 1f);

            AssertPool(demand.Snapshot, ShopDemandFamily.Food, ShopDemandTier.Budget, generated: 5);
            AssertPool(demand.Snapshot, ShopDemandFamily.Retail, ShopDemandTier.Budget, generated: 2);
            AssertPool(demand.Snapshot, ShopDemandFamily.Food, ShopDemandTier.Mid, generated: 1);
            AssertPool(demand.Snapshot, ShopDemandFamily.Retail, ShopDemandTier.Mid, generated: 0);
            AssertPool(demand.Snapshot, ShopDemandFamily.Food, ShopDemandTier.Premium, generated: 1);
            AssertPool(demand.Snapshot, ShopDemandFamily.Retail, ShopDemandTier.Premium, generated: 0);
        }

        static Agent AgentOf(AgentRole role, WealthBand wealth)
        {
            var agent = new Agent(0, role, null, Vector2Int.zero)
            {
                Wealth = wealth
            };
            return agent;
        }

        static void AssertPool(
            ShopDemandSnapshot snapshot,
            ShopDemandFamily family,
            ShopDemandTier tier,
            int generated)
        {
            var pool = snapshot.Pool(family, tier);
            Assert.AreEqual(generated, pool.Generated);
            Assert.AreEqual(generated, pool.Remaining);
            Assert.AreEqual(0, pool.Served);
            Assert.AreEqual(0, pool.SpilledIn);
            Assert.AreEqual(0, pool.SpilledOut);
        }
    }
}
