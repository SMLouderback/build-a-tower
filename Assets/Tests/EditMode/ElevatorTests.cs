using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorTests
    {
        RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator_normal";
            so.displayName = "Elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.buildCost = 20000;
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        [Test]
        public void Elevator_type_flags_shaft()
        {
            var e = Elevator();
            Assert.IsTrue(e.isElevatorShaft);
            Assert.AreEqual(new Vector2Int(1, 2), e.size);
        }
    }
}
