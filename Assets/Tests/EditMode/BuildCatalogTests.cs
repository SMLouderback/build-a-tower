using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildCatalogTests
    {
        static RoomTypeSO Make(
            string id,
            string displayName,
            RoomCategory category,
            bool stairs = false,
            bool elevator = false,
            BuildFamily family = BuildFamily.None,
            BuildSubgroup subgroup = BuildSubgroup.None)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.displayName = displayName;
            so.category = category;
            so.isStairs = stairs;
            so.isElevatorShaft = elevator;
            so.buildFamily = family;
            so.buildSubgroup = subgroup;
            return so;
        }

        [Test]
        public void Group_nests_office_hotel_condo_transit_and_shops()
        {
            var rooms = new List<RoomTypeSO>
            {
                Make("office", "Office", RoomCategory.Office),
                Make("office_prem", "Premium Office", RoomCategory.Office),
                Make("hotel", "Hotel", RoomCategory.Hotel),
                Make("condo", "Condo", RoomCategory.Condo),
                Make("retail", "Retail Store", RoomCategory.Commercial),
                Make("food_fast", "Fast Food", RoomCategory.Commercial),
                Make("stairs", "Stairs", RoomCategory.Transit, stairs: true),
                Make("elevator", "Elevator", RoomCategory.Transit, elevator: true)
            };

            var groups = BuildCatalog.Group(rooms);

            Assert.AreEqual(5, groups.Count);
            Assert.AreEqual(BuildFamily.Office, groups[0].Family);
            Assert.AreEqual(2, groups[0].Rooms.Count);
            Assert.AreEqual(BuildFamily.Hotel, groups[1].Family);
            Assert.AreEqual(BuildFamily.Condo, groups[2].Family);
            Assert.AreEqual(BuildFamily.Shops, groups[3].Family);
            Assert.AreEqual(2, groups[3].Subgroups.Count);
            Assert.AreEqual(BuildSubgroup.Food, groups[3].Subgroups[0].Subgroup);
            Assert.AreEqual(BuildSubgroup.Retail, groups[3].Subgroups[1].Subgroup);
            Assert.AreEqual(BuildFamily.Transit, groups[4].Family);
            Assert.AreEqual(2, groups[4].Rooms.Count);
        }

        [Test]
        public void Group_omits_empty_utility_family()
        {
            var rooms = new List<RoomTypeSO>
            {
                Make("office", "Office", RoomCategory.Office)
            };

            var groups = BuildCatalog.Group(rooms);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(BuildFamily.Office, groups[0].Family);
        }

        [Test]
        public void ResolvedBuildSubgroup_infers_food_from_id()
        {
            var food = Make("shop_food_fast", "Counter", RoomCategory.Commercial);
            Assert.AreEqual(BuildSubgroup.Food, food.ResolvedBuildSubgroup());
        }

        [Test]
        public void Group_nests_three_shops_under_food_and_retail()
        {
            var rooms = new List<RoomTypeSO>
            {
                Make("shop_food_fast", "Fast Food", RoomCategory.Commercial),
                Make("shop_food_restaurant", "Restaurant", RoomCategory.Commercial),
                Make("shop_retail", "Retail", RoomCategory.Commercial)
            };

            var groups = BuildCatalog.Group(rooms);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(BuildFamily.Shops, groups[0].Family);
            Assert.AreEqual(2, groups[0].Subgroups.Count);
            Assert.AreEqual(BuildSubgroup.Food, groups[0].Subgroups[0].Subgroup);
            Assert.AreEqual(2, groups[0].Subgroups[0].Rooms.Count);
            Assert.AreEqual("shop_food_fast", groups[0].Subgroups[0].Rooms[0].id);
            Assert.AreEqual("shop_food_restaurant", groups[0].Subgroups[0].Rooms[1].id);
            Assert.AreEqual(BuildSubgroup.Retail, groups[0].Subgroups[1].Subgroup);
            Assert.AreEqual(1, groups[0].Subgroups[1].Rooms.Count);
            Assert.AreEqual("shop_retail", groups[0].Subgroups[1].Rooms[0].id);
        }
    }
}
