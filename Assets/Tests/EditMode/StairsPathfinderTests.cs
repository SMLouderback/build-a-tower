using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class StairsPathfinderTests
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

        RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            so.maxOccupants = 2;
            return so;
        }

        RoomTypeSO Stairs()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.category = RoomCategory.Transit;
            so.isStairs = true;
            so.size = new Vector2Int(2, 2);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        RoomTypeSO Pad()
        {
            // 1-wide filler to bridge floors for tests.
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "pad";
            so.category = RoomCategory.Office;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        [Test]
        public void Path_lobby_to_office_via_stairs()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _)); // x 0..8
            // Stairs punch through lobby (0) + office (1).
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var pf = new StairsPathfinder();
            pf.Rebuild(grid);

            Assert.IsTrue(pf.TryFindPath(new Vector2Int(5, 0), new Vector2Int(5, 1), out var path));
            Assert.Greater(path.Count, 1);
        }

        [Test]
        public void TryDemolishAt_removes_stairs_and_restores_underlay()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var office));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out var stairs));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var atStairs));
            Assert.IsTrue(atStairs.Type.isStairs);

            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 0), out var removed, out _, out var restored));
            Assert.AreSame(stairs, removed);
            Assert.IsFalse(grid.Rooms.Contains(stairs));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var afterLobby));
            Assert.IsTrue(afterLobby.Type.isLobby);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var afterOffice));
            Assert.AreSame(office, afterOffice);
            Assert.Contains(office, restored);
        }

        [Test]
        public void Stairs_may_stack_with_one_floor_overlap()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 2), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _)); // floors 0-1 (lobby + floor 1)
            // Same columns one floor up: connecting floor has roles 3/4 under 1/2 - allowed.
            Assert.IsTrue(grid.CanPlace(Stairs(), new Vector2Int(0, 1)));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 1), out _));
        }

        [Test]
        public void Stairs_reject_diagonal_role_1_on_4_overlap()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 2), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _)); // upper-right on floor 1 is role 4 at (1,1)
            // Shift +1 on X so new lower-left (role 1) lands on existing upper-right (role 4).
            Assert.IsFalse(grid.CanPlace(Stairs(), new Vector2Int(1, 1)));
        }

        [Test]
        public void Stairs_may_overlap_lobby_and_rooms()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var office));
            Assert.IsTrue(grid.CanPlace(Stairs(), new Vector2Int(0, 0)));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out var stairs));
            Assert.IsTrue(stairs.Type.isStairs);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var atLobby));
            Assert.AreSame(stairs, atLobby);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var atOffice));
            Assert.AreSame(stairs, atOffice);
            // Underlying office room remains in the tower catalog.
            Assert.That(grid.Rooms, Does.Contain(office));
        }

        [Test]
        public void Demolish_stairs_restores_lobby_and_room_cells()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out var lobby);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var office));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 0), out _, out _, out var restored));
            Assert.That(restored, Does.Contain(lobby));
            Assert.That(restored, Does.Contain(office));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var lobbyCell));
            Assert.AreSame(lobby, lobbyCell);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var officeCell));
            Assert.AreSame(office, officeCell);
        }

        [Test]
        public void Rejects_when_no_stairs_connection()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            var pf = new StairsPathfinder();
            pf.Rebuild(grid);
            Assert.IsFalse(pf.TryFindPath(new Vector2Int(0, 0), new Vector2Int(0, 1), out _));
        }

        [Test]
        public void Floor_span_gate_rejects_delta_over_three()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 2), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 3), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 4), out _));
            var pf = new StairsPathfinder();
            pf.Rebuild(grid);
            Assert.IsFalse(pf.TryFindPath(new Vector2Int(0, 0), new Vector2Int(0, 4), out _));
        }

        [Test]
        public void Stairs_from_lobby_to_basement()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            // 2×2 stairs with origin at B1: covers floors -1 and 0 (lobby).
            Assert.IsTrue(grid.CanPlace(Stairs(), new Vector2Int(0, -1)));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, -1), out var stairs));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, -1), out var atB1));
            Assert.AreSame(stairs, atB1);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var atLobby));
            Assert.AreSame(stairs, atLobby);

            var pf = new StairsPathfinder();
            pf.Rebuild(grid);
            Assert.IsTrue(pf.TryFindPath(new Vector2Int(5, 0), new Vector2Int(0, -1), out var path));
            Assert.Greater(path.Count, 1);
        }

        [Test]
        public void Can_place_stairs_four_by_two()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out var stairs));
            Assert.IsTrue(stairs.Type.isStairs);
            Assert.AreEqual(new Vector2Int(2, 2), stairs.Size);
        }

        [Test]
        public void Can_build_room_behind_existing_stairs()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out var stairs));
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 1)));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var office));
            Assert.That(grid.Rooms, Does.Contain(office));
            // Stairs remain the cell owner / path surface.
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var atStairs));
            Assert.AreSame(stairs, atStairs);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(5, 1), out var atOffice));
            Assert.AreSame(office, atOffice);

            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 0), out _, out _, out var restored));
            Assert.That(restored, Does.Contain(office));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var after));
            Assert.AreSame(office, after);
        }

        [Test]
        public void Cannot_stack_two_rooms_behind_same_stairs_cell()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var bay = ScriptableObject.CreateInstance<RoomTypeSO>();
            bay.id = "bay";
            bay.category = RoomCategory.Office;
            bay.size = new Vector2Int(2, 1);
            bay.allowAboveGround = true;

            Assert.IsTrue(grid.TryPlace(bay, new Vector2Int(0, 1), out _));
            Assert.IsFalse(grid.CanPlace(bay, new Vector2Int(0, 1)));
        }
    }
}
