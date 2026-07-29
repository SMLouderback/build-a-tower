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
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 29, 0, out var lobby));
            Assert.IsTrue(grid.HasLobby);
            Assert.AreEqual(0, grid.MinX);
            Assert.AreEqual(29, grid.MaxX);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var at));
            Assert.AreSame(lobby, at);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(29, 0), out _));
        }

        [Test]
        public void Cannot_place_outside_lobby_bounds()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _);
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(15, 1))); // 15..23 exceeds max 20
        }

        [Test]
        public void Cannot_overlap_rooms()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(5, 1)));
        }

        [Test]
        public void Basement_rules_respected()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, -1)));
            Assert.IsTrue(grid.CanPlace(Retail(), new Vector2Int(0, -1)));
        }

        [Test]
        public void Demolish_frees_cells_but_blocks_lobby()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            grid.TryPlace(Office(), new Vector2Int(0, 1), out var office);
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(3, 1), out var removed));
            Assert.AreEqual(office.InstanceId, removed.InstanceId);
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 1)));
            Assert.IsFalse(grid.TryDemolishAt(new Vector2Int(0, 0), out _));
        }

        [Test]
        public void Demolish_under_occupied_floor_leaves_scaffolding()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 2), out _));

            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 1), out _, out var scaffolds));
            Assert.AreEqual(9, scaffolds.Count);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var at));
            Assert.IsTrue(at.Type.isScaffolding);
            // Upper office still supported by scaffolding.
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 2), out _));
            // Floor 3 is supported by the office still on floor 2.
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 3)));
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(10, 1))); // empty + lobby below
        }

        [Test]
        public void Can_rebuild_over_scaffolding_and_clear_unused_studs()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            grid.TryPlace(Office(), new Vector2Int(0, 1), out _);
            grid.TryPlace(Office(), new Vector2Int(0, 2), out _);
            grid.TryDemolishAt(new Vector2Int(0, 1), out _, out _);

            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 1)));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var rebuilt, out var cleared));
            Assert.AreEqual(9, cleared.Count);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var at));
            Assert.AreSame(rebuilt, at);
            Assert.IsFalse(at.Type.isScaffolding);

            // After removing upper office, the rebuilt floor can clear fully (no studs left).
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 2), out _, out var upperScaffolds));
            Assert.AreEqual(0, upperScaffolds.Count);
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 1), out _, out var lowerScaffolds));
            Assert.AreEqual(0, lowerScaffolds.Count);
            Assert.IsFalse(grid.TryGetRoomAt(new Vector2Int(0, 1), out _));
        }

        [Test]
        public void Demolish_only_scaffolds_cells_that_support_something_above()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _)); // x 0..8
            var hotel = ScriptableObject.CreateInstance<RoomTypeSO>();
            hotel.id = "hotel";
            hotel.displayName = "Hotel";
            hotel.category = RoomCategory.Hotel;
            hotel.size = new Vector2Int(4, 1);
            hotel.allowAboveGround = true;
            Assert.IsTrue(grid.TryPlace(hotel, new Vector2Int(0, 2), out _)); // x 0..3

            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 1), out _, out var scaffolds));
            Assert.AreEqual(4, scaffolds.Count);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var stud));
            Assert.IsTrue(stud.Type.isScaffolding);
            Assert.IsFalse(grid.TryGetRoomAt(new Vector2Int(5, 1), out _));
        }

        [Test]
        public void Load_bearing_scaffolding_cannot_be_demolished()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            grid.TryPlace(Office(), new Vector2Int(0, 1), out _);
            grid.TryPlace(Office(), new Vector2Int(0, 2), out _);
            grid.TryDemolishAt(new Vector2Int(0, 1), out _, out _);

            Assert.IsFalse(grid.TryDemolishAt(new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var still));
            Assert.IsTrue(still.Type.isScaffolding);
        }

        [Test]
        public void Lobby_rejects_non_lobby_floor_and_invalid_span()
        {
            var grid = new TowerGrid();
            Assert.IsFalse(grid.CanPlaceLobby(0, 10, 2));
            Assert.IsFalse(grid.CanPlaceLobby(10, 5, 0));
        }

        [Test]
        public void Extend_lobby_widens_bounds_and_occupancy()
        {
            var grid = new TowerGrid();
            var lobbyType = Lobby();
            Assert.IsTrue(grid.TryPlaceLobby(lobbyType, 5, 10, 0, out _));
            Assert.IsTrue(grid.CanExtendLobby(3, 14));
            Assert.IsTrue(grid.TryExtendLobby(lobbyType, 3, 14, out var extended, out var added));
            Assert.AreEqual(6, added);
            Assert.AreEqual(3, grid.MinX);
            Assert.AreEqual(14, grid.MaxX);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(3, 0), out var left));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(14, 0), out var right));
            Assert.AreSame(extended, left);
            Assert.AreSame(extended, right);
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(3, 1)));
        }

        [Test]
        public void Extend_lobby_rejects_shrink_or_no_change()
        {
            var grid = new TowerGrid();
            var lobbyType = Lobby();
            grid.TryPlaceLobby(lobbyType, 5, 10, 0, out _);
            Assert.IsFalse(grid.CanExtendLobby(6, 10));
            Assert.IsFalse(grid.CanExtendLobby(5, 9));
            Assert.IsFalse(grid.CanExtendLobby(5, 10));
        }

        [Test]
        public void Cannot_build_past_empty_floor_below()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            // Floor 2 with nothing on floor 1 in those columns.
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, 2)));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 2)));
        }

        [Test]
        public void Cannot_overhang_past_narrower_floor_below()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            // Hotel is 4 wide at x=0..3 on floor 1.
            var hotel = ScriptableObject.CreateInstance<RoomTypeSO>();
            hotel.id = "hotel";
            hotel.displayName = "Hotel";
            hotel.category = RoomCategory.Hotel;
            hotel.size = new Vector2Int(4, 1);
            hotel.allowAboveGround = true;
            Assert.IsTrue(grid.TryPlace(hotel, new Vector2Int(0, 1), out _));

            // Office 9 wide at x=0 would need support under x=4..8 — missing on floor 1.
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, 2)));
            // Office starting at 10 on floor 1 is fine (lobby below).
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(10, 1)));
        }

        [Test]
        public void Scrollbar_center_range_keeps_camera_view_inside_padded_bounds()
        {
            var range = CutawayCamera.GetScrollableCenterRange(-5f, 40f, 20f);

            Assert.AreEqual(5f, range.x);
            Assert.AreEqual(30f, range.y);
        }
    }
}
