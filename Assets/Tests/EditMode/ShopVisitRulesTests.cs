using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ShopVisitRulesTests
    {
        [Test]
        public void Fast_food_open_at_noon_closed_at_midnight()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_fast";
            so.category = RoomCategory.Commercial;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.hasActiveHours = true;
            so.activeHoursStart = 11 * 60;
            so.activeHoursEnd = 21 * 60;
            so.baseIncome = 40;
            so.maxOccupants = 4;
            Assert.IsTrue(ShopVisitRules.IsOpen(so, 12 * 60));
            Assert.IsFalse(ShopVisitRules.IsOpen(so, 22 * 60));
        }

        [Test]
        public void IsShop_only_for_traffic_variable()
        {
            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.incomeModel = IncomeModel.TrafficVariable;
            var office = ScriptableObject.CreateInstance<RoomTypeSO>();
            office.incomeModel = IncomeModel.QuarterlyRent;

            Assert.IsTrue(ShopVisitRules.IsShop(shop));
            Assert.IsFalse(ShopVisitRules.IsShop(office));
            Assert.IsFalse(ShopVisitRules.IsShop(null));
        }

        [Test]
        public void SlotCount_uses_max_occupants_minimum_one()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.incomeModel = IncomeModel.TrafficVariable;
            so.maxOccupants = 4;
            Assert.AreEqual(4, ShopVisitRules.SlotCount(so));

            so.maxOccupants = 0;
            Assert.AreEqual(1, ShopVisitRules.SlotCount(so));
            Assert.AreEqual(0, ShopVisitRules.SlotCount(null));
        }

        [Test]
        public void PayPerVisit_returns_base_income()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.baseIncome = 40;
            Assert.AreEqual(40, ShopVisitRules.PayPerVisit(so));
            Assert.AreEqual(0, ShopVisitRules.PayPerVisit(null));
        }

        [Test]
        public void PickDwellMinutes_respects_shop_type_ranges()
        {
            var fast = ScriptableObject.CreateInstance<RoomTypeSO>();
            fast.id = "shop_food_fast";
            fast.incomeModel = IncomeModel.TrafficVariable;

            var restaurant = ScriptableObject.CreateInstance<RoomTypeSO>();
            restaurant.id = "shop_food_restaurant";
            restaurant.incomeModel = IncomeModel.TrafficVariable;

            var retail = ScriptableObject.CreateInstance<RoomTypeSO>();
            retail.id = "shop_retail";
            retail.incomeModel = IncomeModel.TrafficVariable;

            var rng = new System.Random(42);
            for (var i = 0; i < 20; i++)
            {
                var fastDwell = ShopVisitRules.PickDwellMinutes(fast, rng);
                Assert.That(fastDwell, Is.InRange(15, 25));

                var restDwell = ShopVisitRules.PickDwellMinutes(restaurant, rng);
                Assert.That(restDwell, Is.InRange(40, 60));

                var retailDwell = ShopVisitRules.PickDwellMinutes(retail, rng);
                Assert.That(retailDwell, Is.InRange(20, 40));
            }
        }
    }
}
