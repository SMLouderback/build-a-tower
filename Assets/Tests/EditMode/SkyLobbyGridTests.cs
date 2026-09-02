using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SkyLobbyGridTests
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
            grid.TryPlaceLobby(new SkyLobbyGridTests().Lobby(), 0, 10, TowerGrid.LobbyFloor, out _);

        static void BuildSupportBand(TowerGrid grid, int minX, int maxX, int topFloorInclusive)
        {
            for (var y = 1; y <= topFloorInclusive; y++)
            for (var x = minX; x <= maxX; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));
        }

        [Test]
        public void CanPlaceSkyLobby_rejects_floor_14()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 5, 14);
            Assert.IsFalse(grid.CanPlaceSkyLobby(0, 5, 14));
        }

        [Test]
        public void CanPlaceSkyLobby_accepts_floor_15_when_spaced()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 5, 14);
            Assert.IsTrue(grid.CanPlaceSkyLobby(0, 5, 15));
        }

        [Test]
        public void CanPlaceSkyLobby_rejects_second_within_14_floors()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 5, 44);
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 0, 5, 15, out _));
            Assert.IsFalse(grid.CanPlaceSkyLobby(0, 5, 29));
            Assert.IsTrue(grid.CanPlaceSkyLobby(0, 5, 30));
        }

        [Test]
        public void GetLobbyFloors_includes_ground_and_sky()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 5, 44);
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 0, 5, 30, out _));
            CollectionAssert.AreEquivalent(new[] { 0, 30 }, grid.GetLobbyFloors());
        }

        [Test]
        public void IsTransferLobbyFloor_matches_ground_and_sky()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 5, 44);
            Assert.IsTrue(grid.IsTransferLobbyFloor(0));
            Assert.IsFalse(grid.IsTransferLobbyFloor(30));
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 0, 5, 30, out _));
            Assert.IsTrue(grid.IsTransferLobbyFloor(30));
        }

        [Test]
        public void TryExtendSkyLobby_widens_span()
        {
            var grid = new TowerGrid();
            PlaceGroundLobby(grid);
            BuildSupportBand(grid, 0, 8, 44);
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 2, 5, 30, out var placed));
            Assert.AreEqual(new Vector2Int(2, 30), placed.Origin);
            Assert.IsTrue(grid.TryExtendSkyLobby(SkyLobby(), 30, 1, 7, out var extended, out var added));
            Assert.AreEqual(3, added);
            Assert.AreEqual(7, extended.Size.x);
        }
    }
}
