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
            Assert.AreEqual(
                4,
                demand.AvailableFor(
                    ShopDemandFamily.Food,
                    WealthBand.Premium,
                    ShopDemandTier.Premium));
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

            Assert.AreEqual(
                2,
                demand.AvailableFor(
                    ShopDemandFamily.Food,
                    WealthBand.Premium,
                    ShopDemandTier.Mid));
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
        public void Spill_only_availability_reports_remaining_spill_capacity()
        {
            var demand = DemandFrom(
                RepeatAgents(11, AgentRole.HotelGuest, WealthBand.Premium),
                stars: 0);
            var midShop = FoodShop(stars: 2);

            Assert.AreEqual(
                8,
                demand.AvailableFor(
                    ShopDemandFamily.Food,
                    WealthBand.Premium,
                    ShopDemandTier.Premium));
            Assert.AreEqual(
                2,
                demand.AvailableFor(
                    ShopDemandFamily.Food,
                    WealthBand.Premium,
                    ShopDemandTier.Mid));

            Assert.IsTrue(demand.TryConsume(midShop, WealthBand.Premium));

            Assert.AreEqual(
                1,
                demand.AvailableFor(
                    ShopDemandFamily.Food,
                    WealthBand.Premium,
                    ShopDemandTier.Mid));

            Object.DestroyImmediate(midShop);
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

        [Test]
        public void Stress_formula_combines_matching_and_tower_ratios()
        {
            Assert.AreEqual(
                3.5f,
                ShopDemandBalance.StressForTier(
                    tierUnmetRatio: 0.5f,
                    towerUnmetRatio: 0.25f),
                0.001f);
            Assert.AreEqual(
                0.5f,
                ShopDemandBalance.TowerStress(towerUnmetRatio: 0.25f),
                0.001f);
        }

        [Test]
        public void Matching_tier_gets_more_stress_and_street_is_exempt()
        {
            var basic = AgentOf(AgentRole.OfficeWorker, WealthBand.Basic, stress: 0f);
            var premium = AgentOf(AgentRole.HotelGuest, WealthBand.Premium, stress: 0f);
            var street = AgentOf(AgentRole.StreetVisitor, WealthBand.Street, stress: 0f);
            var demand = DemandFrom(new[] { basic, premium }, stars: 0);
            ConsumeAllPremiumDemand(demand);

            demand.ArchiveAndApplyStress(new[] { basic, premium, street });

            Assert.Greater(basic.Stress, premium.Stress);
            Assert.Greater(premium.Stress, 0f);
            Assert.AreEqual(0f, street.Stress);
        }

        [Test]
        public void Demand_stress_clamps_at_one_hundred()
        {
            var agent = AgentOf(AgentRole.CondoResident, WealthBand.Basic, stress: 99f);
            var demand = DemandFrom(
                RepeatAgents(2, AgentRole.OfficeWorker, WealthBand.Basic),
                stars: 0);

            demand.ArchiveAndApplyStress(new[] { agent });

            Assert.AreEqual(100f, agent.Stress);
        }

        [Test]
        public void Archive_then_BeginDay_keeps_history_and_replaces_current_pools()
        {
            var demand = new ShopDemandSystem();
            demand.BeginDay(
                RepeatAgents(2, AgentRole.OfficeWorker, WealthBand.Basic),
                stars: 0,
                climateMultiplier: 1f);
            var completedGenerated = demand.Snapshot.TotalGenerated;

            demand.ArchiveAndApplyStress(System.Array.Empty<Agent>());
            demand.BeginDay(
                RepeatAgents(2, AgentRole.HotelGuest, WealthBand.Premium),
                stars: 0,
                climateMultiplier: 1f);

            Assert.AreEqual(1, demand.History.Count);
            Assert.AreEqual(completedGenerated, demand.History[0].TotalGenerated);
            Assert.AreNotEqual(completedGenerated, demand.Snapshot.TotalGenerated);
        }

        static ShopDemandSystem DemandFrom(IReadOnlyList<Agent> agents, int stars)
        {
            var demand = new ShopDemandSystem();
            demand.BeginDay(agents, stars, climateMultiplier: 1f);
            return demand;
        }

        static void ConsumeAllPremiumDemand(ShopDemandSystem demand)
        {
            var food = FoodShop(stars: 3);
            var retail = RetailShop(stars: 3);
            while (demand.TryConsume(food, WealthBand.Premium))
            {
            }
            while (demand.TryConsume(retail, WealthBand.Premium))
            {
            }
            Object.DestroyImmediate(food);
            Object.DestroyImmediate(retail);
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

        static Agent AgentOf(AgentRole role, WealthBand wealth, float stress = 0f)
        {
            var agent = new Agent(0, role, null, Vector2Int.zero)
            {
                Wealth = wealth,
                Stress = stress
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
