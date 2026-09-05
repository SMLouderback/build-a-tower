using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomTypeAssetTests
    {
        [Test]
        public void CreateInstance_requiredStars_defaults_to_zero()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            Assert.AreEqual(0, type.requiredStars);
        }

        [Test]
        public void CreateInstance_eventCapacity_defaults_to_zero()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            Assert.AreEqual(0, type.eventCapacity);
        }

        [Test]
        public void ElevatorNormal_resource_requires_one_star()
        {
            var elevator = Resources.Load<RoomTypeSO>("Rooms/ElevatorNormal");
            Assert.IsNotNull(elevator, "ElevatorNormal should load from Resources/Rooms");
            Assert.AreEqual(1, elevator.requiredStars);
        }

        [Test]
        public void OfficePremium_resource_requires_two_stars()
        {
            var office = Resources.Load<RoomTypeSO>("Rooms/OfficePremium");
            Assert.IsNotNull(office, "OfficePremium should load from Resources/Rooms");
            Assert.AreEqual(2, office.requiredStars);
        }

        [Test]
        public void Ops_service_and_fine_dining_resources_match_catalog_ids()
        {
            AssertRoom("Rooms/Housekeeping", "service_housekeeping", 2, RoomCategory.Service, BuildFamily.Utility);
            AssertRoom("Rooms/Maintenance", "service_maintenance", 2, RoomCategory.Service, BuildFamily.Utility);
            AssertRoom("Rooms/SecurityPost", "service_security", 3, RoomCategory.Service, BuildFamily.Utility);
            AssertRoom("Rooms/ResearchLab", "service_research", 3, RoomCategory.Service, BuildFamily.Utility);
            AssertRoom("Rooms/Conference", "service_conference", 3, RoomCategory.Service, BuildFamily.Utility);
            AssertRoom("Rooms/EventHall", "service_event_hall", 4, RoomCategory.Service, BuildFamily.Utility);
            AssertRoom("Rooms/ShopFineDining", "shop_food_fine", 3, RoomCategory.Commercial, BuildFamily.Shops);
            var fine = Resources.Load<RoomTypeSO>("Rooms/ShopFineDining");
            Assert.AreEqual(BuildSubgroup.Food, fine.ResolvedBuildSubgroup());
            Assert.AreEqual(IncomeModel.TrafficVariable, fine.incomeModel);
            Assert.AreEqual(100, fine.baseIncome);
        }

        [Test]
        public void Conference_resource_is_enlarged_venue()
        {
            var room = Resources.Load<RoomTypeSO>("Rooms/Conference");
            Assert.IsNotNull(room, "Conference should load from Resources/Rooms");
            Assert.AreEqual("service_conference", room.id);
            Assert.AreEqual(3, room.requiredStars);
            Assert.AreEqual(new Vector2Int(5, 1), room.size);
            Assert.AreEqual(40, room.eventCapacity);
            Assert.AreEqual(90000, room.buildCost);
        }

        [Test]
        public void EventHall_resource_matches_venue_catalog()
        {
            var hall = Resources.Load<RoomTypeSO>("Rooms/EventHall");
            Assert.IsNotNull(hall, "EventHall should load from Resources/Rooms");
            Assert.AreEqual("service_event_hall", hall.id);
            Assert.AreEqual("Event Hall", hall.displayName);
            Assert.AreEqual(4, hall.requiredStars);
            Assert.AreEqual(new Vector2Int(9, 2), hall.size);
            Assert.AreEqual(120, hall.eventCapacity);
            Assert.AreEqual(150000, hall.buildCost);
            Assert.AreEqual(RoomCategory.Service, hall.category);
            Assert.AreEqual(BuildFamily.Utility, hall.ResolvedBuildFamily());
        }

        [Test]
        public void Legacy_shop_resources_match_dollhouse_aspect_footprints()
        {
            AssertShop("Rooms/ShopFastFood", "shop_food_fast", 0, 25, 4, new Vector2Int(6, 1), 1f);
            AssertShop("Rooms/ShopRestaurant", "shop_food_restaurant", 0, 50, 6, new Vector2Int(7, 1), 1f);
            AssertShop("Rooms/ShopFineDining", "shop_food_fine", 3, 100, 8, new Vector2Int(5, 1), 1f);
            AssertShop("Rooms/ShopRetail", "shop_retail", 0, 50, 5, new Vector2Int(6, 1), 1f);
        }

        [Test]
        public void New_shop_resources_match_expanded_catalog()
        {
            AssertShop("Rooms/ShopTacoCounter", "shop_food_taco", 0, 28, 4, new Vector2Int(4, 1), 1f);
            AssertShop("Rooms/ShopChickenShack", "shop_food_chicken", 0, 30, 5, new Vector2Int(4, 1), 1f);
            AssertShop("Rooms/ShopMexicanRestaurant", "shop_food_mexican", 2, 60, 6, new Vector2Int(6, 1), 1f);
            AssertShop("Rooms/ShopGagGifts", "shop_retail_gifts", 1, 35, 4, new Vector2Int(4, 1), 2f);
            AssertShop("Rooms/ShopShoeStore", "shop_retail_shoes", 2, 65, 4, new Vector2Int(4, 1), 1f);
            AssertShop("Rooms/ShopDepartmentStore", "shop_retail_department", 3, 55, 12, new Vector2Int(8, 2), 1f);
        }

        static void AssertShop(
            string path,
            string id,
            int stars,
            int payCap,
            int slots,
            Vector2Int size,
            float streetWeight)
        {
            var room = Resources.Load<RoomTypeSO>(path);
            Assert.IsNotNull(room, $"{path} should load from Resources");
            Assert.AreEqual(id, room.id);
            Assert.AreEqual(stars, room.requiredStars);
            Assert.AreEqual(payCap, room.baseIncome);
            Assert.AreEqual(slots, room.maxOccupants);
            Assert.AreEqual(size, room.size);
            Assert.AreEqual(IncomeModel.TrafficVariable, room.incomeModel);
            Assert.AreEqual(streetWeight, room.streetVisitWeight, 0.001f);
            Assert.AreEqual(BuildFamily.Shops, room.ResolvedBuildFamily());
        }

        static void AssertRoom(
            string path,
            string id,
            int stars,
            RoomCategory category,
            BuildFamily family)
        {
            var room = Resources.Load<RoomTypeSO>(path);
            Assert.IsNotNull(room, $"{path} should load from Resources");
            Assert.AreEqual(id, room.id);
            Assert.AreEqual(stars, room.requiredStars);
            Assert.AreEqual(category, room.category);
            Assert.AreEqual(family, room.ResolvedBuildFamily());
        }
    }
}
