using System.Linq;
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

        [Test]
        public void SyncFromGrid_creates_runtime_for_each_elevator_shaft()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(3, 0), out var first));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(8, -1), out var second));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);

            Assert.AreEqual(2, system.Shafts.Count);
            var firstRuntime = system.Shafts.Single(s => s.RoomInstanceId == first.InstanceId);
            Assert.AreEqual(3, firstRuntime.X);
            Assert.AreEqual(0, firstRuntime.MinFloor);
            Assert.AreEqual(1, firstRuntime.MaxFloor);
            Assert.AreEqual(0, firstRuntime.Car.Floor);
            Assert.IsTrue(firstRuntime.UpQueues.ContainsKey(0));
            Assert.IsTrue(firstRuntime.DownQueues.ContainsKey(1));

            var secondRuntime = system.Shafts.Single(s => s.RoomInstanceId == second.InstanceId);
            Assert.AreEqual(-1, secondRuntime.MinFloor);
            Assert.AreEqual(0, secondRuntime.MaxFloor);
            Assert.AreEqual(0, secondRuntime.Car.Floor);
        }

        [Test]
        public void FindServing_requires_both_floors_and_optional_matching_x()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(3, 0), out var shortShaft));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(8, 0), out var tallShaft));
            Assert.IsTrue(grid.TryExtendElevator(tallShaft, 0, 4, out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);

            Assert.AreEqual(tallShaft.InstanceId, system.FindServing(0, 4).RoomInstanceId);
            Assert.AreEqual(tallShaft.InstanceId, system.FindServing(8, 0, 4).RoomInstanceId);
            Assert.IsNull(system.FindServing(3, 0, 4));
            Assert.IsNull(system.FindServing(-1, 4));
        }

        [Test]
        public void TryEnqueue_adds_agent_only_to_matching_floor_direction_and_shaft()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(3, 0), out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);
            var shaft = system.Shafts[0];

            Assert.IsTrue(system.TryEnqueue(99, 3, 1, ElevatorDirection.Down));
            Assert.AreEqual(99, shaft.DownQueues[1].Dequeue());
            Assert.IsFalse(system.TryEnqueue(99, 3, 2, ElevatorDirection.Down));
            Assert.IsFalse(system.TryEnqueue(99, 4, 1, ElevatorDirection.Down));
            Assert.IsFalse(system.TryEnqueue(99, 3, 1, ElevatorDirection.None));
        }

        [Test]
        public void Elevator_car_moves_toward_queued_floor()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 4, out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);
            var shaft = system.Shafts[0];
            Assert.AreEqual(0, shaft.Car.Floor);
            Assert.IsTrue(system.TryEnqueue(99, shaft.X, 4, ElevatorDirection.Down));

            for (var i = 0; i < 20; i++)
                system.Tick(1f);

            Assert.AreEqual(4, shaft.Car.Floor);
        }

        [Test]
        public void Elevator_boards_only_up_to_capacity_and_alights_at_destination()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 2, out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);
            var shaft = system.Shafts[0];
            for (var id = 1; id <= ElevatorCar.Capacity + 1; id++)
            {
                system.SetPassengerDestination(id, 2);
                Assert.IsTrue(system.TryEnqueue(id, shaft.X, 0, ElevatorDirection.Up));
            }

            system.Tick(ElevatorCar.DoorDwellMinutes);

            Assert.AreEqual(ElevatorCar.Capacity, shaft.Car.PassengerIds.Count);
            Assert.AreEqual(1, shaft.UpQueues[0].Count);

            system.Tick(2 * ElevatorCar.MinutesPerFloor);

            Assert.AreEqual(2, shaft.Car.Floor);
            Assert.AreEqual(ElevatorCarState.DoorsOpen, shaft.Car.State);
            Assert.IsEmpty(shaft.Car.PassengerIds);
        }

        [Test]
        public void Router_uses_stairs_when_span_le_3()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);

            Assert.IsTrue(router.TryPlanTrip(
                new Vector2Int(5, 0),
                new Vector2Int(5, 1),
                out var legs));
            Assert.AreEqual(1, legs.Count);
            Assert.AreEqual(TransitLegKind.Stairs, legs[0].Kind);
            Assert.Greater(legs[0].Cells.Count, 1);
        }

        [Test]
        public void Router_needs_elevator_when_span_gt_3()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            for (var floor = 1; floor <= 4; floor++)
                Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, floor), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            Assert.IsFalse(router.TryPlanTrip(
                new Vector2Int(5, 0),
                new Vector2Int(5, 4),
                out _));

            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 4, out _));
            router.Rebuild(grid);

            Assert.IsTrue(router.TryPlanTrip(
                new Vector2Int(5, 0),
                new Vector2Int(5, 4),
                out var legs));
            Assert.AreEqual(
                new[] { TransitLegKind.Walk, TransitLegKind.Elevator, TransitLegKind.Walk },
                legs.Select(leg => leg.Kind));
            Assert.AreEqual(0, legs[1].ElevatorX);
            Assert.AreEqual(0, legs[1].EntryFloor);
            Assert.AreEqual(4, legs[1].ExitFloor);
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 4) },
                legs[1].Cells);
        }
    }
}
