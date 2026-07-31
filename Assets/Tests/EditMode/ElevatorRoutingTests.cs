using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorRoutingTests
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

        ElevatorSystem DualShaftsServing0To4(
            out ElevatorShaftRuntime first,
            out ElevatorShaftRuntime second)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var a));
            Assert.IsTrue(grid.TryExtendElevator(a, 0, 4, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var b));
            Assert.IsTrue(grid.TryExtendElevator(b, 0, 4, out _));

            var system = new ElevatorSystem();
            system.SyncFromGrid(grid);
            first = system.Shafts.Single(s => s.RoomInstanceId == a.InstanceId);
            second = system.Shafts.Single(s => s.RoomInstanceId == b.InstanceId);
            return system;
        }

        [Test]
        public void Capacity_is_ten()
        {
            Assert.AreEqual(10, ElevatorCar.Capacity);
        }

        [Test]
        public void Score_penalizes_longer_queues()
        {
            Assert.Greater(
                ElevatorRouting.Score(walkCost: 10, waitEstimate: 5f),
                ElevatorRouting.Score(walkCost: 10, waitEstimate: 1f));
        }

        [Test]
        public void Score_uses_wait_weight_constant()
        {
            Assert.AreEqual(3f, ElevatorRouting.WaitWeight);
            Assert.AreEqual(
                10f + ElevatorRouting.WaitWeight * 2f,
                ElevatorRouting.Score(10, 2f));
        }

        [Test]
        public void IsMeaningfullyBetter_requires_switch_improve_ratio()
        {
            Assert.AreEqual(0.25f, ElevatorRouting.SwitchImproveRatio);
            Assert.IsFalse(ElevatorRouting.IsMeaningfullyBetter(100f, 90f));
            Assert.IsTrue(ElevatorRouting.IsMeaningfullyBetter(100f, 75f));
            Assert.IsFalse(ElevatorRouting.IsMeaningfullyBetter(50f, 50f));
            Assert.IsFalse(ElevatorRouting.IsMeaningfullyBetter(40f, 50f));
        }

        [Test]
        public void GetServingShafts_returns_all_overlapping_non_maintenance()
        {
            var system = DualShaftsServing0To4(out var first, out var second);

            var serving = system.GetServingShafts(0, 4);
            Assert.AreEqual(2, serving.Count);
            CollectionAssert.Contains(serving.Select(s => s.RoomInstanceId).ToList(), first.RoomInstanceId);
            CollectionAssert.Contains(serving.Select(s => s.RoomInstanceId).ToList(), second.RoomInstanceId);

            Assert.IsTrue(system.TrySetMaintenance(first.RoomInstanceId, true));
            serving = system.GetServingShafts(0, 4);
            Assert.AreEqual(1, serving.Count);
            Assert.AreEqual(second.RoomInstanceId, serving[0].RoomInstanceId);
        }

        [Test]
        public void QueueLength_counts_agents_at_floor_and_direction()
        {
            var system = DualShaftsServing0To4(out var shaft, out _);

            Assert.AreEqual(0, system.QueueLength(shaft, 0, ElevatorDirection.Up));
            Assert.IsTrue(system.TryEnqueue(1, shaft.X, 0, ElevatorDirection.Up));
            Assert.IsTrue(system.TryEnqueue(2, shaft.X, 0, ElevatorDirection.Up));
            Assert.IsTrue(system.TryEnqueue(3, shaft.X, 1, ElevatorDirection.Down));

            Assert.AreEqual(2, system.QueueLength(shaft, 0, ElevatorDirection.Up));
            Assert.AreEqual(0, system.QueueLength(shaft, 0, ElevatorDirection.Down));
            Assert.AreEqual(1, system.QueueLength(shaft, 1, ElevatorDirection.Down));
        }

        [Test]
        public void SameWayPassengerCount_counts_destinations_in_direction()
        {
            var system = DualShaftsServing0To4(out var shaft, out _);
            shaft.Car.Floor = 1;
            shaft.Car.PassengerIds.Add(10);
            shaft.Car.PassengerIds.Add(11);
            shaft.Car.PassengerIds.Add(12);
            system.SetPassengerDestination(10, 4);
            system.SetPassengerDestination(11, 3);
            system.SetPassengerDestination(12, 0);

            Assert.AreEqual(2, system.SameWayPassengerCount(shaft, ElevatorDirection.Up));
            Assert.AreEqual(1, system.SameWayPassengerCount(shaft, ElevatorDirection.Down));
        }

        [Test]
        public void EstimateWaitMinutes_grows_with_queue_and_busy_penalty()
        {
            var system = DualShaftsServing0To4(out var shaft, out _);
            shaft.Car.Floor = 0;
            shaft.Car.Direction = ElevatorDirection.None;
            shaft.Car.State = ElevatorCarState.Idle;

            var empty = system.EstimateWaitMinutes(shaft, 0, ElevatorDirection.Up);
            Assert.AreEqual(0f, empty);

            Assert.IsTrue(system.TryEnqueue(1, shaft.X, 0, ElevatorDirection.Up));
            Assert.IsTrue(system.TryEnqueue(2, shaft.X, 0, ElevatorDirection.Up));
            var withQueue = system.EstimateWaitMinutes(shaft, 0, ElevatorDirection.Up);
            var expectedQueue =
                2f / ElevatorCar.Capacity * ElevatorRouting.BoardCycleMinutes;
            Assert.AreEqual(expectedQueue, withQueue, 0.0001f);

            shaft.Car.Floor = 4;
            shaft.Car.Direction = ElevatorDirection.Down;
            var busy = system.EstimateWaitMinutes(shaft, 0, ElevatorDirection.Up);
            Assert.AreEqual(
                expectedQueue + ElevatorRouting.BusyPenaltyMinutes,
                busy,
                0.0001f);
        }

        [Test]
        public void Routing_constants_match_brief()
        {
            Assert.AreEqual(3f, ElevatorRouting.WaitWeight);
            Assert.AreEqual(2f, ElevatorRouting.BoardCycleMinutes);
            Assert.AreEqual(1.5f, ElevatorRouting.BusyPenaltyMinutes);
            Assert.AreEqual(0.25f, ElevatorRouting.SwitchImproveRatio);
            Assert.AreEqual(10f, ElevatorRouting.RescoreIntervalGameMinutes);
            Assert.AreEqual(30f, ElevatorRouting.SwitchCooldownGameMinutes);
        }
    }
}
