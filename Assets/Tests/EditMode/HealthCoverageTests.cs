using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HealthCoverageTests
    {
        [Test]
        public void Trash_covers_within_20_cells_and_3_floors_inclusive()
        {
            var trash = Room(1, TrashType(), new Vector2Int(0, 5), Vector2Int.one);
            var rooms = new List<RoomInstance> { trash };

            Assert.IsTrue(HealthCoverage.TrashCovers(5, 20, rooms));
            Assert.IsTrue(HealthCoverage.TrashCovers(8, 0, rooms));
            Assert.IsFalse(HealthCoverage.TrashCovers(5, 21, rooms));
            Assert.IsFalse(HealthCoverage.TrashCovers(9, 0, rooms));
        }

        [Test]
        public void Broken_trash_covers_nothing_dirty_trash_still_covers()
        {
            var workingDirty = Room(1, TrashType(), new Vector2Int(0, 0), Vector2Int.one);
            workingDirty.MarkDirty();
            var broken = Room(2, TrashType(), new Vector2Int(0, 0), Vector2Int.one);
            broken.Condition = 0;

            Assert.IsTrue(HealthCoverage.TrashCovers(0, 0, new List<RoomInstance> { workingDirty }));
            Assert.IsFalse(HealthCoverage.TrashCovers(0, 0, new List<RoomInstance> { broken }));
        }

        [Test]
        public void Public_restroom_assigns_at_most_three_nearest_need_rooms()
        {
            var restroom = Room(10, PublicRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1));
            var needs = new List<RoomInstance>
            {
                Room(1, ShopType(), new Vector2Int(1, 0), Vector2Int.one),
                Room(2, ShopType(), new Vector2Int(2, 0), Vector2Int.one),
                Room(3, ShopType(), new Vector2Int(3, 0), Vector2Int.one),
                Room(4, ShopType(), new Vector2Int(4, 0), Vector2Int.one),
            };

            var rooms = new List<RoomInstance> { restroom };
            rooms.AddRange(needs);
            var assignments = HealthCoverage.AssignPublicRestrooms(rooms);

            Assert.AreEqual(3, assignments.Count);
            Assert.AreEqual(new[] { 1, 2, 3 }, assignments.Select(a => a.NeedInstanceId).ToArray());
            Assert.IsTrue(assignments.All(a => a.FacilityInstanceId == 10));
        }

        [Test]
        public void Public_street_bonus_only_for_shops_and_restaurants()
        {
            var restroom = Room(10, PublicRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1));
            var shop = Room(1, ShopType(), new Vector2Int(1, 0), Vector2Int.one);
            var food = Room(2, FoodShopType(), new Vector2Int(2, 0), Vector2Int.one);
            var lobby = Room(3, LobbyType(), new Vector2Int(3, 0), Vector2Int.one);
            var leisure = Room(4, LeisureType(), new Vector2Int(4, 0), Vector2Int.one);

            var byNeed = HealthCoverage.AssignPublicRestrooms(Combine(restroom, shop, food, lobby, leisure))
                .ToDictionary(a => a.NeedInstanceId);

            Assert.AreEqual(3, byNeed.Count);
            Assert.IsTrue(byNeed[1].StreetBonus);
            Assert.IsTrue(byNeed[2].StreetBonus);
            Assert.IsFalse(byNeed[3].StreetBonus);

            var leisureOnly = HealthCoverage.AssignPublicRestrooms(Combine(
                Room(10, PublicRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1)),
                Room(1, LeisureType(), new Vector2Int(1, 0), Vector2Int.one))).Single();
            Assert.IsFalse(leisureOnly.StreetBonus);
        }

        [Test]
        public void Public_dirty_or_broken_restroom_covers_nothing()
        {
            var dirty = Room(10, PublicRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1));
            dirty.MarkDirty();
            var broken = Room(11, PublicRestroomType(), new Vector2Int(5, 0), new Vector2Int(3, 1));
            broken.Condition = 0;
            var need = Room(1, ShopType(), new Vector2Int(1, 0), Vector2Int.one);

            Assert.AreEqual(0, HealthCoverage.AssignPublicRestrooms(Combine(dirty, need)).Count);
            Assert.AreEqual(0, HealthCoverage.AssignPublicRestrooms(Combine(broken, need)).Count);
        }

        [Test]
        public void Public_tie_breaks_to_lower_restroom_instance_id()
        {
            var farRestroom = Room(20, PublicRestroomType(), new Vector2Int(10, 0), new Vector2Int(3, 1));
            var nearRestroom = Room(10, PublicRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1));
            var need = Room(1, ShopType(), new Vector2Int(5, 0), Vector2Int.one);

            var assignment = HealthCoverage.AssignPublicRestrooms(Combine(farRestroom, nearRestroom, need)).Single();

            Assert.AreEqual(10, assignment.FacilityInstanceId);
        }

        [Test]
        public void Office_restroom_ignores_upper_and_caps_at_four()
        {
            var restroom = Room(100, OfficeRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1));
            var offices = new List<RoomInstance>
            {
                Room(1, OfficeType(LuxuryBand.Base), new Vector2Int(1, 0), Vector2Int.one),
                Room(2, OfficeType(LuxuryBand.Mid), new Vector2Int(2, 0), Vector2Int.one),
                Room(3, OfficeType(LuxuryBand.Upper), new Vector2Int(3, 0), Vector2Int.one),
                Room(4, OfficeType(LuxuryBand.Base), new Vector2Int(4, 0), Vector2Int.one),
                Room(5, OfficeType(LuxuryBand.Base), new Vector2Int(5, 0), Vector2Int.one),
                Room(6, OfficeType(LuxuryBand.Base), new Vector2Int(6, 0), Vector2Int.one),
                Room(7, OfficeType(LuxuryBand.Base), new Vector2Int(7, 0), Vector2Int.one),
            };

            var rooms = new List<RoomInstance> { restroom };
            rooms.AddRange(offices);
            var assignments = HealthCoverage.AssignOfficeRestrooms(rooms);

            Assert.AreEqual(4, assignments.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 4, 5 }, assignments.Select(a => a.NeedInstanceId));
            Assert.IsTrue(assignments.All(a => !a.StreetBonus));
            Assert.IsFalse(assignments.Any(a => a.NeedInstanceId == 3));
        }

        [Test]
        public void Office_dirty_or_broken_restroom_covers_nothing()
        {
            var dirty = Room(10, OfficeRestroomType(), new Vector2Int(0, 0), new Vector2Int(3, 1));
            dirty.MarkDirty();
            var need = Room(1, OfficeType(LuxuryBand.Base), new Vector2Int(1, 0), Vector2Int.one);

            Assert.AreEqual(0, HealthCoverage.AssignOfficeRestrooms(Combine(dirty, need)).Count);
        }

        static List<RoomInstance> Combine(params RoomInstance[] rooms) => new(rooms);

        static RoomInstance Room(int id, RoomTypeSO type, Vector2Int origin, Vector2Int size) =>
            new(id, type, origin, size);

        static RoomTypeSO TrashType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_trash";
            so.category = RoomCategory.Service;
            return so;
        }

        static RoomTypeSO PublicRestroomType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_public_restroom";
            so.category = RoomCategory.Service;
            return so;
        }

        static RoomTypeSO OfficeRestroomType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_office_restroom";
            so.category = RoomCategory.Service;
            return so;
        }

        static RoomTypeSO ShopType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_retail";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Shops;
            return so;
        }

        static RoomTypeSO FoodShopType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_cafe";
            so.category = RoomCategory.Commercial;
            so.buildSubgroup = BuildSubgroup.Food;
            return so;
        }

        static RoomTypeSO LobbyType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby_main";
            so.category = RoomCategory.Transit;
            so.isLobby = true;
            return so;
        }

        static RoomTypeSO LeisureType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "leisure_pool";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            return so;
        }

        static RoomTypeSO OfficeType(LuxuryBand band)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_unit";
            so.category = RoomCategory.Office;
            so.luxuryBand = band;
            return so;
        }
    }
}
