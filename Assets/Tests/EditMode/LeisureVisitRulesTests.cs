using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class LeisureVisitRulesTests
    {
        [Test]
        public void IsShop_excludes_leisure_traffic_venues()
        {
            var gym = ScriptableObject.CreateInstance<RoomTypeSO>();
            gym.id = "leisure_gym";
            gym.incomeModel = IncomeModel.TrafficVariable;
            gym.buildFamily = BuildFamily.Leisure;
            Assert.IsTrue(ShopVisitRules.IsTrafficVenue(gym));
            Assert.IsFalse(ShopVisitRules.IsShop(gym));

            Object.DestroyImmediate(gym);
        }

        [Test]
        public void IsShop_still_true_for_food_shop()
        {
            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.id = "shop_food_fast";
            shop.incomeModel = IncomeModel.TrafficVariable;
            shop.buildFamily = BuildFamily.Shops;
            shop.buildSubgroup = BuildSubgroup.Food;
            Assert.IsTrue(ShopVisitRules.IsShop(shop));

            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Leisure_dwell_ranges_match_spec()
        {
            var rng = new System.Random(1);
            var theater = ScriptableObject.CreateInstance<RoomTypeSO>();
            theater.id = "leisure_theater";
            for (var i = 0; i < 20; i++)
            {
                var d = ShopVisitRules.PickDwellMinutes(theater, rng);
                Assert.GreaterOrEqual(d, 90);
                Assert.LessOrEqual(d, 130);
            }

            Object.DestroyImmediate(theater);
        }

        [Test]
        public void Leisure_gym_spa_pool_bowling_dwell_ranges_match_spec()
        {
            var rng = new System.Random(2);
            AssertLeisureDwell("leisure_gym", 30, 50, rng);
            AssertLeisureDwell("leisure_spa", 45, 75, rng);
            AssertLeisureDwell("leisure_pool", 40, 70, rng);
            AssertLeisureDwell("leisure_bowling", 50, 80, rng);
        }

        static void AssertLeisureDwell(string id, int lo, int hi, System.Random rng)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.incomeModel = IncomeModel.TrafficVariable;
            for (var i = 0; i < 15; i++)
                Assert.That(ShopVisitRules.PickDwellMinutes(so, rng), Is.InRange(lo, hi));
            Object.DestroyImmediate(so);
        }
    }
}
