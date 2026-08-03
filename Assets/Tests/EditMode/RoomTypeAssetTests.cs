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
            Assert.AreEqual(200, fine.baseIncome);
        }

        [Test]
        public void Conference_resource_is_enlarged_venue()
        {
            var room = Resources.Load<RoomTypeSO>("Rooms/Conference");
            Assert.IsNotNull(room, "Conference should load from Resources/Rooms");
            Assert.AreEqual("service_conference", room.id);
            Assert.AreEqual(3, room.requiredStars);
            Assert.AreEqual(new Vector2Int(8, 1), room.size);
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
            Assert.AreEqual(new Vector2Int(12, 2), hall.size);
            Assert.AreEqual(120, hall.eventCapacity);
            Assert.AreEqual(150000, hall.buildCost);
            Assert.AreEqual(RoomCategory.Service, hall.category);
            Assert.AreEqual(BuildFamily.Utility, hall.ResolvedBuildFamily());
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
