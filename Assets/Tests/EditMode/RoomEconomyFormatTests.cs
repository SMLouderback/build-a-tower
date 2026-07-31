using BuildATower;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomEconomyFormatTests
    {
        static RoomTypeSO Room(int buildCost, IncomeModel incomeModel, int baseIncome)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.buildCost = buildCost;
            type.incomeModel = incomeModel;
            type.baseIncome = baseIncome;
            return type;
        }

        [Test]
        public void Recurring_room_shows_daily_income()
        {
            var office = Room(40_000, IncomeModel.QuarterlyRent, 3000);

            Assert.AreEqual("Cost: $40,000", RoomEconomyFormat.CostLine(office));
            StringAssert.Contains("$3,000 / day", RoomEconomyFormat.IncomeLine(office));
            Assert.AreEqual("$40k · $3k/d", RoomEconomyFormat.ButtonTag(office));
        }

        [Test]
        public void Upfront_sale_room_shows_one_time_income()
        {
            var condo = Room(80_000, IncomeModel.UpfrontSale, 150_000);

            StringAssert.Contains("$150,000 once", RoomEconomyFormat.IncomeLine(condo));
            Assert.AreEqual("$80k · $150k once", RoomEconomyFormat.ButtonTag(condo));
            Assert.IsNull(RoomEconomyFormat.UpkeepLine(condo));
        }

        [Test]
        public void Elevator_shows_per_floor_cost_and_upkeep()
        {
            var elevator = Room(100_000, IncomeModel.None, 0);
            elevator.isElevatorShaft = true;

            Assert.AreEqual("Cost: $100,000 / floor", RoomEconomyFormat.CostLine(elevator));
            StringAssert.Contains($"${EconomySystem.ElevatorDailyUpkeep:N0} / day", RoomEconomyFormat.UpkeepLine(elevator));
            StringAssert.Contains("/fl", RoomEconomyFormat.ButtonTag(elevator));
        }

        [Test]
        public void Format_shows_per_visit_and_visits_today()
        {
            var shop = Room(100_000, IncomeModel.TrafficVariable, 40);
            shop.id = "shop_food_fast";
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
            var office = Room(40_000, IncomeModel.QuarterlyRent, 3000);
            var instance = new RoomInstance(9, office, Vector2Int.zero, Vector2Int.one);
            instance.PriceTier = PricePricing.TierHigh;

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(lines, "Income: $3,900 / day occupied");
        }

        [Test]
        public void Selected_condo_reports_sale_state()
        {
            var condo = Room(80_000, IncomeModel.UpfrontSale, 150_000);
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
