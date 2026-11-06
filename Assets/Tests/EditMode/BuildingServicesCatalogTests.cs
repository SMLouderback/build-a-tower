using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildingServicesCatalogTests
    {
        [Test]
        public void ServiceMail_resource_matches_catalog()
        {
            AssertServiceRoom(
                "Rooms/ServiceMail",
                "service_mail",
                "Mail Room",
                stars: 1,
                size: new Vector2Int(4, 1),
                buildCost: 38000,
                noiseOutput: 0.15f,
                allowAboveGround: true,
                allowBasement: true);
        }

        [Test]
        public void ServiceRecycling_resource_matches_catalog()
        {
            AssertServiceRoom(
                "Rooms/ServiceRecycling",
                "service_recycling",
                "Recycling Center",
                stars: 2,
                size: new Vector2Int(6, 1),
                buildCost: 72000,
                noiseOutput: 0.40f,
                allowAboveGround: true,
                allowBasement: true);
        }

        [Test]
        public void ServiceLoadingDock_resource_matches_catalog()
        {
            AssertServiceRoom(
                "Rooms/ServiceLoadingDock",
                "service_loading_dock",
                "Loading Dock",
                stars: 2,
                size: new Vector2Int(8, 1),
                buildCost: 95000,
                noiseOutput: 0.55f,
                allowAboveGround: false,
                allowBasement: true);
        }

        static void AssertServiceRoom(
            string resourcePath,
            string id,
            string displayName,
            int stars,
            Vector2Int size,
            int buildCost,
            float noiseOutput,
            bool allowAboveGround,
            bool allowBasement)
        {
            var room = Resources.Load<RoomTypeSO>(resourcePath);
            Assert.IsNotNull(room, $"{resourcePath} should load from Resources");
            Assert.AreEqual(id, room.id);
            Assert.AreEqual(displayName, room.displayName);
            Assert.AreEqual(stars, room.requiredStars);
            Assert.AreEqual(size, room.size);
            Assert.AreEqual(buildCost, room.buildCost);
            Assert.AreEqual(noiseOutput, room.noiseOutput, 0.001f);
            Assert.AreEqual(allowAboveGround, room.allowAboveGround);
            Assert.AreEqual(allowBasement, room.allowBasement);
            Assert.AreEqual(RoomCategory.Service, room.category);
            Assert.AreEqual(BuildFamily.Utility, room.buildFamily);
            Assert.AreEqual(BuildFamily.Utility, room.ResolvedBuildFamily());
            Assert.AreEqual(IncomeModel.None, room.incomeModel);
            Assert.AreEqual(0, room.baseIncome);
        }
    }
}
