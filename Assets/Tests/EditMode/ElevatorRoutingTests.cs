using System.Collections.Generic;
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

        TowerGrid DualShaftTowerForRouting(
            out ElevatorSystem elevators,
            out ElevatorShaftRuntime crowded,
            out ElevatorShaftRuntime empty)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            for (var floor = 1; floor <= 4; floor++)
            {
                var office = Office();
                office.maxOccupants = floor == 4 ? 1 : 0;
                Assert.IsTrue(grid.TryPlace(office, new Vector2Int(0, floor), out _));
            }

            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var a));
            Assert.IsTrue(grid.TryExtendElevator(a, 0, 4, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var b));
            Assert.IsTrue(grid.TryExtendElevator(b, 0, 4, out _));

            elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            crowded = elevators.Shafts.Single(s => s.RoomInstanceId == a.InstanceId);
            empty = elevators.Shafts.Single(s => s.RoomInstanceId == b.InstanceId);
            Assert.AreEqual(0, crowded.X);
            Assert.AreEqual(5, empty.X);
            return grid;
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

        [Test]
        public void TryPlanTrip_picks_empty_shaft_over_crowded_first()
        {
            var grid = DualShaftTowerForRouting(out var elevators, out var crowded, out var empty);
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);

            Assert.AreSame(crowded, elevators.FindServing(0, 4));
            for (var id = 1; id <= ElevatorCar.Capacity; id++)
                Assert.IsTrue(elevators.TryEnqueue(id, crowded.X, 0, ElevatorDirection.Up));

            // Start/goal near shaft B (X=5); FindServing would still return crowded X=0.
            Assert.IsTrue(router.TryPlanTrip(
                new Vector2Int(7, 0),
                new Vector2Int(7, 4),
                out var legs));
            Assert.AreEqual(
                new[] { TransitLegKind.Walk, TransitLegKind.Elevator, TransitLegKind.Walk },
                legs.Select(leg => leg.Kind));
            Assert.AreEqual(empty.X, legs[1].ElevatorX);
            Assert.AreNotEqual(crowded.X, legs[1].ElevatorX);
        }

        [Test]
        public void TryPlanTrip_uses_stairs_when_span_le_3_even_with_elevators()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 2, out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);

            Assert.IsTrue(router.TryPlanTrip(
                new Vector2Int(8, 0),
                new Vector2Int(8, 1),
                out var legs));
            Assert.AreEqual(1, legs.Count);
            Assert.AreEqual(TransitLegKind.Stairs, legs[0].Kind);
            Assert.IsFalse(legs.Any(leg => leg.Kind == TransitLegKind.Elevator));
        }

        [Test]
        public void TryRescoreElevatorWait_switches_when_alternate_much_better()
        {
            var grid = DualShaftTowerForRouting(out var elevators, out var crowded, out var empty);
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var goal = new Vector2Int(7, 4);

            for (var id = 1000; id < 1000 + 20; id++)
                Assert.IsTrue(elevators.TryEnqueue(id, crowded.X, 0, ElevatorDirection.Up));

            PutAgentWaitingOnShaft(agent, elevators, crowded, goal, AgentPhase.Working);

            Assert.IsTrue(agents.TryRescoreElevatorWait(agent, totalMinutes: 100f));
            Assert.AreEqual(
                -1,
                elevators.GetQueueIndex(crowded, 0, ElevatorDirection.Up, agent.Id),
                "Agent must leave the crowded shaft queue after a meaningful switch.");
            Assert.IsNotNull(agent.TripLegs);
            var elevatorLeg = agent.TripLegs.Single(leg => leg.Kind == TransitLegKind.Elevator);
            Assert.AreEqual(empty.X, elevatorLeg.ElevatorX);
            Assert.AreEqual(100f, agent.LastElevatorSwitchTotalMinutes);
        }

        [Test]
        public void TryRescoreElevatorWait_skips_tiny_improvement_and_cooldown()
        {
            var grid = DualShaftTowerForRouting(out var elevators, out var crowded, out var empty);
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var goal = new Vector2Int(7, 4);

            Assert.IsTrue(elevators.TryEnqueue(1000, crowded.X, 0, ElevatorDirection.Up));
            PutAgentWaitingOnShaft(agent, elevators, crowded, goal, AgentPhase.Working);

            Assert.IsFalse(
                agents.TryRescoreElevatorWait(agent, totalMinutes: 100f),
                "One person ahead is not a meaningful improvement over walking to the other shaft.");
            Assert.GreaterOrEqual(
                elevators.GetQueueIndex(crowded, 0, ElevatorDirection.Up, agent.Id),
                0);

            // Make alternate clearly better, but still inside switch cooldown.
            for (var id = 1001; id < 1001 + 20; id++)
                Assert.IsTrue(elevators.TryEnqueue(id, crowded.X, 0, ElevatorDirection.Up));
            agent.NextElevatorRescoreTotalMinutes = 0f;
            agent.LastElevatorSwitchTotalMinutes = 90f;

            Assert.IsFalse(
                agents.TryRescoreElevatorWait(agent, totalMinutes: 100f),
                "Cooldown must block another shaft switch.");
            Assert.GreaterOrEqual(
                elevators.GetQueueIndex(crowded, 0, ElevatorDirection.Up, agent.Id),
                0);
            Assert.AreNotEqual(empty.RoomInstanceId, agent.ElevatorShaftId);
        }

        static void PutAgentWaitingOnShaft(
            Agent agent,
            ElevatorSystem elevators,
            ElevatorShaftRuntime shaft,
            Vector2Int goal,
            AgentPhase after)
        {
            agent.GoalCell = goal;
            agent.PhaseAfterMove = after;
            agent.Cell = new Vector2Int(shaft.X, 0);
            agent.WorldPosition = new Vector2(shaft.X + 0.5f, 0.5f);
            agent.Visible = true;
            agent.ElevatorEntryFloor = 0;
            agent.ElevatorDestFloor = goal.y;
            agent.ElevatorShaftId = shaft.RoomInstanceId;
            agent.ElevatorQueueSide = 1;
            agent.ElevatorWaitMinutes = 0f;
            agent.TripLegs = new List<TransitLeg>
            {
                new TransitLeg
                {
                    Kind = TransitLegKind.Elevator,
                    ElevatorX = shaft.X,
                    EntryFloor = 0,
                    ExitFloor = goal.y
                }
            };
            agent.TripLegIndex = 0;
            Assert.IsTrue(elevators.TryEnqueue(agent.Id, shaft.X, 0, ElevatorDirection.Up));
            elevators.SetPassengerDestination(agent.Id, goal.y);
            agent.Phase = AgentPhase.WaitingAtElevator;
            agent.NextElevatorRescoreTotalMinutes = 0f;
            agent.LastElevatorSwitchTotalMinutes = float.NegativeInfinity;
        }
    }
}
