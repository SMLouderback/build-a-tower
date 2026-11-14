using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CleanlinessRoomTests
    {
        [Test]
        public void ServiceTreatment_resource_matches_catalog()
        {
            const string resourcePath = "Rooms/ServiceTreatment";
            var room = Resources.Load<RoomTypeSO>(resourcePath);
            Assert.IsNotNull(room, $"{resourcePath} should load from Resources");
            Assert.AreEqual("service_treatment", room.id);
            Assert.AreEqual("Water Treatment", room.displayName);
            Assert.AreEqual(1, room.requiredStars);
            Assert.AreEqual(new Vector2Int(6, 1), room.size);
            Assert.AreEqual(90000, room.buildCost);
            Assert.AreEqual(0.40f, room.noiseOutput, 0.001f);
            Assert.AreEqual(new Color(0.32f, 0.46f, 0.40f, 1f), room.placeholderColor);
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
