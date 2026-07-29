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
    }
}
