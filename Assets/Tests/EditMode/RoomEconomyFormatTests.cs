using BuildATower;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomEconomyFormatTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        static RoomTypeSO MidOffice()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = OfficeLuxury.MidStandardId;
            type.category = RoomCategory.Office;
            type.luxuryBand = LuxuryBand.Mid;
            type.size = new Vector2Int(4, 1);
            type.incomeModel = IncomeModel.QuarterlyRent;
            return type;
        }

        static RoomTypeSO CondoBase()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = CondoLuxury.BaseId;
            type.category = RoomCategory.Condo;
            type.luxuryBand = LuxuryBand.Base;
            type.size = Vector2Int.one;
            type.incomeModel = IncomeModel.UpfrontSale;
            return type;
        }

        static RoomTypeSO FastFood()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "shop_food_fast";
            type.category = RoomCategory.Commercial;
            type.buildFamily = BuildFamily.Shops;
            type.buildSubgroup = BuildSubgroup.Food;
            type.size = Vector2Int.one;
            type.incomeModel = IncomeModel.TrafficVariable;
            return type;
        }

        [Test]
        public void Recurring_room_shows_daily_income()
        {
            var office = MidOffice();
            // rentEq 480; build 8640; income 480
            Assert.AreEqual("Cost: $8,640", RoomEconomyFormat.CostLine(office));
            StringAssert.Contains("$480 / day", RoomEconomyFormat.IncomeLine(office));
            Assert.AreEqual("$8.6k · $480/d", RoomEconomyFormat.ButtonTag(office));
        }

        [Test]
        public void Upfront_sale_room_shows_one_time_income()
        {
            var condo = CondoBase();
            // rentEq 144; build 2592; sale 144
            StringAssert.Contains("$144 once", RoomEconomyFormat.IncomeLine(condo));
            Assert.AreEqual("$2.6k · $144 once", RoomEconomyFormat.ButtonTag(condo));
            StringAssert.Contains("Upkeep:", RoomEconomyFormat.UpkeepLine(condo));
        }

        [Test]
        public void Elevator_shows_per_floor_cost_and_upkeep()
        {
            var elevator = ScriptableObject.CreateInstance<RoomTypeSO>();
            elevator.id = "elevator_normal";
            elevator.isElevatorShaft = true;
            elevator.category = RoomCategory.Transit;
            elevator.size = new Vector2Int(1, 2);

            var unit = BuildEconomy.UnitBuildCost(elevator);
            Assert.AreEqual($"Cost: ${unit:N0} / floor", RoomEconomyFormat.CostLine(elevator));
            StringAssert.Contains($"${EconomySystem.ElevatorDailyUpkeep:N0} / day", RoomEconomyFormat.UpkeepLine(elevator));
            StringAssert.Contains("/fl", RoomEconomyFormat.ButtonTag(elevator));
        }

        [Test]
        public void Format_shows_per_visit_and_visits_today()
        {
            var shop = FastFood();
            var instance = new RoomInstance(7, shop, Vector2Int.zero, Vector2Int.one);
            instance.RecordVisit();
            instance.RecordShopSpend(25);
            instance.RecordVisit();
            instance.RecordShopSpend(40);

            StringAssert.Contains("/ visit", RoomEconomyFormat.IncomeLine(shop));
            StringAssert.Contains("spent dollars at midnight", RoomEconomyFormat.IncomeLine(shop));

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(lines, "Visits today: 2");
            CollectionAssert.Contains(lines, "Earnings today: $65");
            StringAssert.Contains("/visit", RoomEconomyFormat.ButtonTag(shop));
        }

        [Test]
        public void Selected_unit_shows_tier_scaled_income()
        {
            var office = MidOffice();
            var instance = new RoomInstance(9, office, Vector2Int.zero, office.size);
            instance.PriceTier = PricePricing.TierHigh;

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(lines, "Income: $468 / day occupied"); // 624 * street Mid fit 0.75
        }

        [Test]
        public void Selected_shop_lines_show_pool_upkeep_and_negative_net()
        {
            var shop = FastFood();
            shop.requiredStars = 0;
            var instance = new RoomInstance(10, shop, Vector2Int.zero, Vector2Int.one);
            instance.ArchiveShopDay(creditedRevenue: 10, upkeep: 19);
            var demand = new ShopDemandSystem();
            demand.BeginDay(new List<Agent>(), stars: 0, climateMultiplier: 1f);

            var lines = RoomEconomyFormat.SelectedUnitLines(
                instance,
                null,
                null,
                demand,
                openShopCountInPool: 1);

            CollectionAssert.Contains(lines, "Demand: Food / Budget");
            CollectionAssert.Contains(lines, "Visits yesterday: 0");
            CollectionAssert.Contains(lines, "Yesterday revenue: $10");
            CollectionAssert.Contains(lines, "Daily upkeep: $19");
            CollectionAssert.Contains(lines, "Yesterday net: -$9");
            CollectionAssert.Contains(lines, "Competition: Underserved");

            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Newly_built_shop_shows_known_daily_upkeep_before_first_rollover()
        {
            var shop = FastFood();
            shop.buildSubgroup = BuildSubgroup.Retail;
            var instance = new RoomInstance(11, shop, Vector2Int.zero, Vector2Int.one);

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, null);

            CollectionAssert.Contains(lines, "Daily upkeep: $19");
            CollectionAssert.Contains(lines, "Yesterday net: $0");

            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Selected_condo_reports_sale_state()
        {
            var condo = CondoBase();
            var instance = new RoomInstance(8, condo, Vector2Int.zero, Vector2Int.one);

            var beforeSale = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(beforeSale, "Status: For sale — no payout yet");

            var buyer = new Agent(1, AgentRole.CondoResident, instance, Vector2Int.zero);
            var duringMove = RoomEconomyFormat.SelectedUnitLines(
                instance,
                new List<Agent> { buyer },
                new EconomySystem());
            CollectionAssert.Contains(duringMove, "Status: Buyer moving in — no payout yet");

            instance.CondoSold = true;
            var afterSale = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(afterSale, "Status: Sold");
        }

        [TestCase(500, "$500")]
        [TestCase(3000, "$3k")]
        [TestCase(4500, "$4.5k")]
        [TestCase(150_000, "$150k")]
        [TestCase(2_000_000, "$2M")]
        public void Abbreviate_shortens_dollar_amounts(int dollars, string expected)
        {
            Assert.AreEqual(expected, RoomEconomyFormat.Abbreviate(dollars));
        }
    }
}
