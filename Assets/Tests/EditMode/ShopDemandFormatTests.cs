using BuildATower;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ShopDemandFormatTests
    {
        [Test]
        public void Pool_line_shows_native_fulfillment_spill_and_unmet()
        {
            var pool = new ShopDemandPoolSnapshot(
                generated: 10,
                served: 6,
                spilledIn: 1,
                spilledOut: 2,
                remaining: 2);

            Assert.AreEqual(
                "Food / Mid: 8/10 served, spill +1/-2, unmet 2 (80%)",
                ShopDemandFormat.PoolLine(
                    ShopDemandFamily.Food,
                    ShopDemandTier.Mid,
                    pool));
        }

        [TestCase(10, 8, 0, 2, 1, "Underserved")]
        [TestCase(10, 8, 0, 1, 11, "Oversupplied")]
        [TestCase(10, 8, 0, 1, 10, "Balanced")]
        [TestCase(0, 0, 0, 0, 1, "Oversupplied")]
        public void Competition_uses_twenty_percent_threshold_then_shop_count(
            int generated,
            int served,
            int spilledOut,
            int remaining,
            int openShopCount,
            string expected)
        {
            var pool = new ShopDemandPoolSnapshot(
                generated,
                served,
                spilledIn: 0,
                spilledOut,
                remaining);

            Assert.AreEqual(
                expected,
                ShopDemandFormat.Competition(pool, openShopCount));
        }

        [Test]
        public void Open_shop_count_matches_exact_pool_and_excludes_broken_rooms()
        {
            var selected = Shop(BuildSubgroup.Food, stars: 2);
            var matching = Shop(BuildSubgroup.Food, stars: 1);
            var brokenMatching = Shop(BuildSubgroup.Food, stars: 2);
            var otherTier = Shop(BuildSubgroup.Food, stars: 3);
            var otherFamily = Shop(BuildSubgroup.Retail, stars: 2);
            var rooms = new List<RoomInstance>
            {
                Instance(1, matching),
                Instance(2, brokenMatching),
                Instance(3, otherTier),
                Instance(4, otherFamily)
            };
            rooms[1].Condition = 0;

            Assert.AreEqual(
                1,
                ShopDemandFormat.CountOpenShopsInPool(rooms, selected));

            Object.DestroyImmediate(selected);
            Object.DestroyImmediate(matching);
            Object.DestroyImmediate(brokenMatching);
            Object.DestroyImmediate(otherTier);
            Object.DestroyImmediate(otherFamily);
        }

        static RoomTypeSO Shop(BuildSubgroup subgroup, int stars)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.incomeModel = IncomeModel.TrafficVariable;
            type.buildFamily = BuildFamily.Shops;
            type.buildSubgroup = subgroup;
            type.requiredStars = stars;
            return type;
        }

        static RoomInstance Instance(int id, RoomTypeSO type) =>
            new RoomInstance(id, type, Vector2Int.zero, Vector2Int.one);
    }
}
