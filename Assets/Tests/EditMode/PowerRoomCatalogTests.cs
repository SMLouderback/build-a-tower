using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class PowerRoomCatalogTests
    {
        [Test]
        public void ServicePower_resource_matches_catalog()
        {
            const string resourcePath = "Rooms/ServicePower";
            var room = Resources.Load<RoomTypeSO>(resourcePath);
            Assert.IsNotNull(room, $"{resourcePath} should load from Resources");
            Assert.AreEqual("service_power", room.id);
            Assert.AreEqual("Power Plant", room.displayName);
            Assert.AreEqual(1, room.requiredStars);
            Assert.AreEqual(new Vector2Int(8, 1), room.size);
            Assert.AreEqual(140000, room.buildCost);
            Assert.AreEqual(0.45f, room.noiseOutput, 0.001f);
            Assert.AreEqual(new Color(0.48f, 0.42f, 0.32f, 1f), room.placeholderColor);
            Assert.IsFalse(room.allowAboveGround);
            Assert.IsTrue(room.allowBasement);
            Assert.IsFalse(room.requiresHousekeeping);
            Assert.AreEqual(IncomeModel.None, room.incomeModel);
            Assert.AreEqual(0, room.baseIncome);
            Assert.AreEqual(0, room.maxOccupants);
            Assert.AreEqual(RoomCategory.Service, room.category);
            Assert.AreEqual(BuildFamily.Utility, room.buildFamily);
            Assert.AreEqual(BuildFamily.Utility, room.ResolvedBuildFamily());
        }
    }
}
