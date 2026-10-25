using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class PowerSystemTests
    {
        [Test]
        public void StairsLobbyAndPlantCostNothing()
        {
            Assert.AreEqual(0, PowerSystem.Cost(StairsType()));
            Assert.AreEqual(0, PowerSystem.Cost(LobbyType()));
            Assert.AreEqual(0, PowerSystem.Cost(SkyLobbyType()));
            Assert.AreEqual(0, PowerSystem.Cost(PlantType()));
        }

        [Test]
        public void ElevatorCostsTwoAtAnyHeight()
        {
            Assert.AreEqual(2, PowerSystem.Cost(ElevatorType(height: 2)));
            Assert.AreEqual(2, PowerSystem.Cost(ElevatorType(height: 10)));
        }

        [Test]
        public void OtherRoomsCostTheirFloorCount()
        {
            Assert.AreEqual(1, PowerSystem.Cost(RoomType(sizeY: 1)));
            Assert.AreEqual(3, PowerSystem.Cost(AtriumType()));
        }

        [Test]
        public void PlantLadderAndCapacity()
        {
            CollectionAssert.AreEqual(new[] { 50, 80, 120, 170, 230, 300, 380 },
                Enumerable.Range(0, 7).Select(PowerSystem.PlantOutput).ToArray());
            Assert.AreEqual(70, PowerSystem.Capacity(new[] { 0 }, new[] { false }));
            Assert.AreEqual(20, PowerSystem.Capacity(new[] { 0 }, new[] { true }));
        }

        static RoomTypeSO StairsType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.isStairs = true;
            so.size = new Vector2Int(2, 2);
            return so;
        }

        static RoomTypeSO LobbyType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO SkyLobbyType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "sky_lobby";
            so.isSkyLobby = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO PlantType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_power";
            so.size = new Vector2Int(8, 1);
            return so;
        }

        static RoomTypeSO ElevatorType(int height)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator";
            so.isElevatorShaft = true;
            so.size = new Vector2Int(1, height);
            return so;
        }

        static RoomTypeSO RoomType(int sizeY)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_unit";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(3, sizeY);
            return so;
        }

        static RoomTypeSO AtriumType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "leisure_atrium";
            so.isAtrium = true;
            so.size = new Vector2Int(6, 3);
            return so;
        }
    }
}
