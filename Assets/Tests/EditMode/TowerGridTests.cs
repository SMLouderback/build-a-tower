using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TowerGridTests
    {
        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.displayName = "Lobby";
            so.category = RoomCategory.Structure;
            so.size = new Vector2Int(1, 1);
            so.buildCost = 1000;
            so.isLobby = true;
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.displayName = "Office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.buildCost = 40000;
            so.allowAboveGround = true;
            so.allowBasement = false;
            return so;
        }

        RoomTypeSO Retail()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "retail";
            so.displayName = "Retail";
            so.category = RoomCategory.Commercial;
            so.size = new Vector2Int(16, 1);
            so.buildCost = 100000;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        [Test]
        public void Cannot_place_office_before_lobby()
        {
            var grid = new TowerGrid();
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, 1)));
        }

        [Test]
        public void Place_lobby_sets_bounds_and_occupancy()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 29, 1, out var lobby));
            Assert.IsTrue(grid.HasLobby);
            Assert.AreEqual(0, grid.MinX);
            Assert.AreEqual(29, grid.MaxX);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var at));
            Assert.AreSame(lobby, at);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(29, 1), out _));
        }

        [Test]
        public void Cannot_place_outside_lobby_bounds()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 1, out _);
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(15, 2))); // 15..23 exceeds max 20
        }

        [Test]
        public void Cannot_overlap_rooms()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 1, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 2), out _));
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(5, 2)));
        }

        [Test]
        public void Basement_rules_respected()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 1, out _);
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, -1)));
            Assert.IsTrue(grid.CanPlace(Retail(), new Vector2Int(0, -1)));
        }

        [Test]
        public void Demolish_frees_cells_but_blocks_lobby()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 1, out _);
            grid.TryPlace(Office(), new Vector2Int(0, 2), out var office);
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(3, 2), out var removed));
            Assert.AreEqual(office.InstanceId, removed.InstanceId);
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 2)));
            Assert.IsFalse(grid.TryDemolishAt(new Vector2Int(0, 1), out _));
        }

        [Test]
        public void Lobby_rejects_non_floor_1_and_invalid_span()
        {
            var grid = new TowerGrid();
            Assert.IsFalse(grid.CanPlaceLobby(0, 10, 2));
            Assert.IsFalse(grid.CanPlaceLobby(10, 5, 1));
        }
    }
}
