using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorTests
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

        RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            return so;
        }

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

        [Test]
        public void Place_elevator_1x2_and_reject_stairs_overlap()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var shaft));
            Assert.IsTrue(shaft.Type.isElevatorShaft);
            Assert.IsFalse(grid.CanPlace(Stairs(), new Vector2Int(0, 0)));
        }

        [TestCase(2, 2)]
        [TestCase(1, 31)]
        public void Place_elevator_rejects_invalid_initial_size(int width, int height)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            var elevator = Elevator();
            elevator.size = new Vector2Int(width, height);

            Assert.IsFalse(grid.CanPlace(elevator, new Vector2Int(0, 0)));
            Assert.IsFalse(grid.TryPlace(elevator, new Vector2Int(0, 0), out _));
        }

        [Test]
        public void Extend_elevator_up_to_30_rejects_31()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var shaft));
            Assert.IsTrue(grid.CanExtendElevator(shaft, 0, 29));
            Assert.IsTrue(grid.TryExtendElevator(shaft, 0, 29, out var added));
            Assert.AreEqual(28, added);
            Assert.IsFalse(grid.CanExtendElevator(shaft, 0, 30));
        }

        [Test]
        public void Extend_elevator_rejects_foreign_and_demolished_shafts()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);

            var foreignGrid = new TowerGrid();
            foreignGrid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(foreignGrid.TryPlace(
                Elevator(),
                new Vector2Int(5, 0),
                out var foreignShaft));
            Assert.IsFalse(grid.CanExtendElevator(foreignShaft, 0, 2));
            Assert.IsFalse(grid.TryExtendElevator(foreignShaft, 0, 2, out _));

            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var demolishedShaft));
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(5, 0), out _));
            Assert.IsFalse(grid.CanExtendElevator(demolishedShaft, 0, 2));
            Assert.IsFalse(grid.TryExtendElevator(demolishedShaft, 0, 2, out _));
        }

        [Test]
        public void Elevator_rejects_stairs_cell()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            Assert.IsFalse(grid.CanPlace(Elevator(), new Vector2Int(0, 0)));
        }

        [Test]
        public void Demolish_elevator_restores_room_built_behind_it()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out var lobby);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var shaft));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var office));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var shaftCell));
            Assert.AreSame(shaft, shaftCell);

            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 0), out var lobbyCell));
            Assert.AreSame(lobby, lobbyCell);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var officeCell));
            Assert.AreSame(office, officeCell);
        }
    }
}
