using System.Collections.Generic;
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

        [Test]
        public void PickDwellMinutes_fine_dining_uses_restaurant_range_not_fast_food()
        {
            var fine = ScriptableObject.CreateInstance<RoomTypeSO>();
            fine.id = "shop_food_fine";
            fine.incomeModel = IncomeModel.TrafficVariable;

            var rng = new System.Random(42);
            for (var i = 0; i < 20; i++)
                Assert.That(ShopVisitRules.PickDwellMinutes(fine, rng), Is.InRange(40, 60));
        }

        [Test]
        public void PickDwellMinutes_mexican_uses_mid_sit_down_not_restaurant_range()
        {
            var mexican = ScriptableObject.CreateInstance<RoomTypeSO>();
            mexican.id = "shop_food_mexican";
            mexican.incomeModel = IncomeModel.TrafficVariable;

            var rng = new System.Random(42);
            for (var i = 0; i < 20; i++)
                Assert.That(ShopVisitRules.PickDwellMinutes(mexican, rng), Is.InRange(35, 50));
        }

        [Test]
        public void PickDwellMinutes_new_shop_ids_use_authored_ranges()
        {
            var rng = new System.Random(7);
            AssertDwell("shop_food_taco", 12, 20, rng);
            AssertDwell("shop_food_chicken", 15, 25, rng);
            AssertDwell("shop_retail_gifts", 15, 30, rng);
            AssertDwell("shop_retail_shoes", 25, 40, rng);
            AssertDwell("shop_retail_department", 30, 50, rng);
        }

        [Test]
        public void PickWeightedShop_null_or_empty_returns_null()
        {
            Assert.IsNull(ShopVisitRules.PickWeightedShop(null, new System.Random(1)));
            Assert.IsNull(ShopVisitRules.PickWeightedShop(new List<RoomInstance>(), new System.Random(1)));
        }

        [Test]
        public void PickWeightedShop_single_shop_returns_it()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(LobbySo(), 0, 4, 0, out _);
            var type = ShopSo("shop_retail", 1f);
            grid.TryPlace(type, new Vector2Int(1, 0), out var shop);

            var picked = ShopVisitRules.PickWeightedShop(new List<RoomInstance> { shop }, new System.Random(1));
            Assert.AreSame(shop, picked);
        }

        [Test]
        public void PickWeightedShop_biases_toward_higher_streetVisitWeight()
        {
            var giftsType = ShopSo("shop_retail_gifts", 2f);
            var retailType = ShopSo("shop_retail", 1f);
            var gifts = new RoomInstance(1, giftsType, new Vector2Int(0, 0), giftsType.size);
            var retail = new RoomInstance(2, retailType, new Vector2Int(4, 0), retailType.size);
            var shops = new List<RoomInstance> { gifts, retail };

            var rng = new System.Random(12345);
            var giftsPicks = 0;
            const int trials = 6000;
            for (var i = 0; i < trials; i++)
            {
                var pick = ShopVisitRules.PickWeightedShop(shops, rng);
                if (pick == gifts) giftsPicks++;
            }

            Assert.That(giftsPicks, Is.InRange(3600, 4800));
        }

        static void AssertDwell(string id, int lo, int hi, System.Random rng)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.incomeModel = IncomeModel.TrafficVariable;
            for (var i = 0; i < 15; i++)
                Assert.That(ShopVisitRules.PickDwellMinutes(so, rng), Is.InRange(lo, hi));
        }

        static RoomTypeSO LobbySo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO ShopSo(string id, float streetVisitWeight)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Commercial;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.streetVisitWeight = streetVisitWeight;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            so.maxOccupants = 4;
            return so;
        }
    }
}
