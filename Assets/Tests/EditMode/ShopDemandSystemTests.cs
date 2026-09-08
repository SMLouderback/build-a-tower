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

        [Test]
        public void Consume_uses_native_pool_before_spill()
        {
            var demand = DemandFrom(
                RepeatAgents(6, AgentRole.HotelGuest, WealthBand.Premium),
                stars: 0);
            var shop = FoodShop(stars: 3);

            Assert.IsTrue(demand.CanServe(shop, WealthBand.Premium));
            Assert.AreEqual(5, demand.AvailableFor(ShopDemandFamily.Food, WealthBand.Premium));
            Assert.IsTrue(demand.TryConsume(shop, WealthBand.Premium));
            Assert.AreEqual(
                1,
                demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).Served);
            Assert.AreEqual(
                0,
                demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).SpilledOut);

            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Spill_is_one_tier_down_and_capped_at_twenty_five_percent()
        {
            var demand = DemandFrom(
                RepeatAgents(11, AgentRole.HotelGuest, WealthBand.Premium),
                stars: 0);
            var midShop = FoodShop(stars: 2);
            var budgetShop = FoodShop(stars: 0);

            Assert.AreEqual(10, demand.AvailableFor(ShopDemandFamily.Food, WealthBand.Premium));
            Assert.IsTrue(demand.TryConsume(midShop, WealthBand.Premium));
            Assert.IsTrue(demand.TryConsume(midShop, WealthBand.Premium));
            Assert.IsFalse(demand.TryConsume(midShop, WealthBand.Premium));
            Assert.IsFalse(demand.TryConsume(budgetShop, WealthBand.Premium));
            Assert.AreEqual(
                2,
                demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).SpilledOut);
            Assert.AreEqual(
                2,
                demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Mid).SpilledIn);

            Object.DestroyImmediate(midShop);
            Object.DestroyImmediate(budgetShop);
        }

        [Test]
        public void Demand_never_moves_up_or_cross_family()
        {
            var budgetDemand = DemandFrom(
                RepeatAgents(2, AgentRole.OfficeWorker, WealthBand.Basic),
                stars: 0);
            var midFood = FoodShop(stars: 2);
            Assert.IsFalse(budgetDemand.TryConsume(midFood, WealthBand.Basic));

            var premiumDemand = DemandFrom(
                RepeatAgents(1, AgentRole.HotelGuest, WealthBand.Premium),
                stars: 0);
            var premiumRetail = RetailShop(stars: 3);
            Assert.AreEqual(
                1,
                premiumDemand.Snapshot
                    .Pool(ShopDemandFamily.Food, ShopDemandTier.Premium)
                    .Remaining);
            Assert.IsFalse(premiumDemand.TryConsume(premiumRetail, WealthBand.Premium));

            Object.DestroyImmediate(midFood);
            Object.DestroyImmediate(premiumRetail);
        }

        [Test]
        public void Archive_keeps_only_thirty_snapshots_and_next_day_resets()
        {
            var demand = new ShopDemandSystem();
            for (var day = 0; day < 35; day++)
            {
                demand.BeginDay(new List<Agent>(), stars: day, climateMultiplier: 1f);
                demand.Archive();
            }

            Assert.AreEqual(30, demand.History.Count);
            Assert.AreEqual(
                7,
                demand.History[0].Pool(ShopDemandFamily.Food, ShopDemandTier.Budget).Generated);

            demand.BeginDay(new List<Agent>(), stars: 0, climateMultiplier: 1f);

            Assert.AreEqual(
                2,
                demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Budget).Remaining);
            Assert.AreEqual(30, demand.History.Count);
        }

        [Test]
        public void Archive_returns_immutable_copy_and_leaves_current_pools_untouched()
        {
            var demand = DemandFrom(
                RepeatAgents(6, AgentRole.HotelGuest, WealthBand.Premium),
                stars: 0);
            var premiumFood = FoodShop(stars: 3);
            var archived = demand.Archive();

            Assert.AreEqual(
                4,
                demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).Remaining);
            Assert.IsTrue(demand.TryConsume(premiumFood, WealthBand.Premium));
            Assert.AreEqual(
                4,
                archived.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).Remaining);
            Assert.AreEqual(
                4,
                demand.History[0].Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).Remaining);

            Object.DestroyImmediate(premiumFood);
        }

        static ShopDemandSystem DemandFrom(IReadOnlyList<Agent> agents, int stars)
        {
            var demand = new ShopDemandSystem();
            demand.BeginDay(agents, stars, climateMultiplier: 1f);
            return demand;
        }

        static List<Agent> RepeatAgents(int count, AgentRole role, WealthBand wealth)
        {
            var agents = new List<Agent>(count);
            for (var i = 0; i < count; i++)
                agents.Add(AgentOf(role, wealth));
            return agents;
        }

        static RoomTypeSO FoodShop(int stars) => Shop(BuildSubgroup.Food, stars);

        static RoomTypeSO RetailShop(int stars) => Shop(BuildSubgroup.Retail, stars);

        static RoomTypeSO Shop(BuildSubgroup subgroup, int stars)
        {
            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.incomeModel = IncomeModel.TrafficVariable;
            shop.buildFamily = BuildFamily.Shops;
            shop.buildSubgroup = subgroup;
            shop.requiredStars = stars;
            return shop;
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
