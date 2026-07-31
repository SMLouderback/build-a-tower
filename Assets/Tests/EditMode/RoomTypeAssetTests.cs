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
            AssertRoom("Rooms/ShopFineDining", "shop_food_fine", 3, RoomCategory.Commercial, BuildFamily.Shops);
            var fine = Resources.Load<RoomTypeSO>("Rooms/ShopFineDining");
            Assert.AreEqual(BuildSubgroup.Food, fine.ResolvedBuildSubgroup());
            Assert.AreEqual(IncomeModel.TrafficVariable, fine.incomeModel);
            Assert.AreEqual(200, fine.baseIncome);
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
