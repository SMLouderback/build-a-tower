using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class FloorLabelsTests
    {
        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO SkyLobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "sky_lobby";
            so.isSkyLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static void PlaceGroundLobby(TowerGrid grid) =>
            grid.TryPlaceLobby(new FloorLabelsTests().Lobby(), 0, 10, TowerGrid.LobbyFloor, out _);

        static void BuildSupportBand(TowerGrid grid, int minX, int maxX, int topFloorInclusive)
        {
            for (var y = 1; y <= topFloorInclusive; y++)
            for (var x = minX; x <= maxX; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));
        }

        [Test]
        public void Format_lobby_basement_and_above()
        {
            var grid = new TowerGrid();
            Assert.AreEqual("L", FloorLabels.Format(0, grid));
            Assert.AreEqual("B1", FloorLabels.Format(-1, grid));
            Assert.AreEqual("B2", FloorLabels.Format(-2, grid));
            Assert.AreEqual("3", FloorLabels.Format(3, grid));
        }

        [Test]
        public void Format_sky_lobby_uses_SL_prefix()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 5, 14);
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 0, 5, 15, out _));
            Assert.AreEqual("SL15", FloorLabels.Format(15, grid));
        }

        [Test]
        public void IsActive_follows_Serves()
        {
            var shaft = new ElevatorShaftRuntime
            {
                Kind = ElevatorShaftKind.Express,
                MinFloor = 0,
                MaxFloor = 10,
                StopFloors = new HashSet<int> { 0, 15 }
            };
            Assert.IsTrue(FloorLabels.IsActive(shaft, 0));
            Assert.IsFalse(FloorLabels.IsActive(shaft, 5));
            Assert.IsFalse(FloorLabels.IsActive(null, 0));
        }
    }
}
