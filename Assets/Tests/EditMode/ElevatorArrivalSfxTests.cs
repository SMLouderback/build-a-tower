using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorArrivalSfxTests
    {
        [TearDown]
        public void TearDown()
        {
            ElevatorSystem.CarArrivedFromMoving = null;
        }

        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
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
        public void ShouldPlayElevatorArrivalSfx_only_from_Moving()
        {
            Assert.IsTrue(TowerAudio.ShouldPlayElevatorArrivalSfx(ElevatorCarState.Moving));
            Assert.IsFalse(TowerAudio.ShouldPlayElevatorArrivalSfx(ElevatorCarState.Idle));
            Assert.IsFalse(TowerAudio.ShouldPlayElevatorArrivalSfx(ElevatorCarState.DoorsOpen));
        }

        [Test]
        public void ShouldEmitElevatorArrivalSfx_selected_always_when_from_Moving()
        {
            Assert.IsTrue(TowerAudio.ShouldEmitElevatorArrivalSfx(
                ElevatorCarState.Moving, shaftSelected: true, random01: 0.99f));
            Assert.IsFalse(TowerAudio.ShouldEmitElevatorArrivalSfx(
                ElevatorCarState.Idle, shaftSelected: true, random01: 0f));
        }

        [Test]
        public void ShouldEmitElevatorArrivalSfx_random_only_below_chance()
        {
            Assert.IsTrue(TowerAudio.ShouldEmitElevatorArrivalSfx(
                ElevatorCarState.Moving, shaftSelected: false, random01: 0f));
            Assert.IsFalse(TowerAudio.ShouldEmitElevatorArrivalSfx(
                ElevatorCarState.Moving,
                shaftSelected: false,
                random01: TowerAudio.ElevatorArrivalChance));
        }

        [Test]
        public void CanPlayElevatorArrivalSfx_blocks_within_cooldown()
        {
            Assert.IsFalse(TowerAudio.CanPlayElevatorArrivalSfx(1.05f, 1.00f, 5f));
            Assert.IsFalse(TowerAudio.CanPlayElevatorArrivalSfx(5.99f, 1.00f, 5f));
        }

        [Test]
        public void CanPlayElevatorArrivalSfx_allows_after_cooldown()
        {
            Assert.IsTrue(TowerAudio.CanPlayElevatorArrivalSfx(6.00f, 1.00f, 5f));
            Assert.IsTrue(TowerAudio.CanPlayElevatorArrivalSfx(10.00f, 1.00f, 5f));
        }

        [Test]
        public void Moving_to_DoorsOpen_raises_CarArrivedFromMoving_once()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 2, out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);
            var shaft = system.Shafts[0];
            system.SetPassengerDestination(1, 1);
            Assert.IsTrue(system.TryEnqueue(1, shaft.X, 0, ElevatorDirection.Up));

            system.Tick(ElevatorCar.DoorDwellMinutes);
            Assert.AreEqual(ElevatorCarState.Idle, shaft.Car.State);

            var arrivals = 0;
            ElevatorShaftRuntime arrived = null;
            ElevatorSystem.CarArrivedFromMoving = s =>
            {
                arrivals++;
                arrived = s;
            };

            system.Tick(ElevatorCar.MinutesPerFloor);
            Assert.AreEqual(1, shaft.Car.Floor);
            Assert.AreEqual(ElevatorCarState.DoorsOpen, shaft.Car.State);
            Assert.AreEqual(1, arrivals);
            Assert.AreSame(shaft, arrived);
        }

        [Test]
        public void Idle_same_floor_DoorsOpen_does_not_raise_arrival()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 2, out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);
            var shaft = system.Shafts[0];
            Assert.AreEqual(0, shaft.Car.Floor);
            Assert.IsTrue(system.TryEnqueue(1, shaft.X, 0, ElevatorDirection.Up));

            var arrivals = 0;
            ElevatorSystem.CarArrivedFromMoving = _ => arrivals++;

            system.Tick(0.01f);
            Assert.AreEqual(ElevatorCarState.DoorsOpen, shaft.Car.State);
            Assert.AreEqual(0, arrivals);
        }
    }
}
