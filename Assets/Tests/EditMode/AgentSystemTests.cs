using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentSystemTests
    {
        [Test]
        public void Agent_rides_elevator_and_alights_at_destination()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);

            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);
            Assert.AreEqual(4, agent.ElevatorDestFloor);
            Assert.AreEqual(1, agent.TripLegIndex);

            elevators.Tick(0.1f);
            agents.Tick(0f, clock, grid);
            Assert.AreEqual(AgentPhase.Riding, agent.Phase);

            elevators.Tick(8.9f);
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.Working, agent.Phase);
            Assert.AreEqual(4, agent.Cell.y);
            Assert.IsFalse(elevators.Shafts[0].Car.PassengerIds.Contains(agent.Id));
        }

        [Test]
        public void Maintenance_toggle_does_not_strand_waiting_agents()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);

            agents.Tick(1f, clock, grid);
            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);
            var shaftId = elevators.Shafts[0].RoomInstanceId;

            // Into maintenance and straight back out.
            Assert.IsTrue(elevators.TrySetMaintenance(shaftId, true));
            agents.OnElevatorServiceChanged(shaftId);
            Assert.IsTrue(elevators.TrySetMaintenance(shaftId, false));
            agents.OnElevatorServiceChanged(shaftId);

            // The queued call must still be honoured and the ride must complete.
            for (var i = 0; i < 200; i++)
            {
                elevators.Tick(0.25f);
                agents.Tick(0.25f, clock, grid);
                if (agent.Phase == AgentPhase.Working) break;
            }

            Assert.AreEqual(
                AgentPhase.Working,
                agent.Phase,
                "Agent must not remain stuck in the elevator queue after a maintenance toggle.");
            Assert.AreEqual(4, agent.Cell.y);
        }

        [Test]
        public void Orphaned_queue_entry_is_cleaned_up_and_agent_replans()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);

            agents.Tick(1f, clock, grid);
            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);

            // Simulate losing the queue slot (what previously stranded agents).
            Assert.IsTrue(elevators.RemoveFromQueues(agent.Id));
            var shaft = elevators.Shafts[0];
            Assert.AreEqual(
                -1,
                elevators.GetQueueIndex(shaft, 0, ElevatorDirection.Up, agent.Id));

            agents.Tick(0.25f, clock, grid);

            // The watchdog must recover the trip rather than leave a dead wait.
            Assert.GreaterOrEqual(
                elevators.GetQueueIndex(shaft, 0, ElevatorDirection.Up, agent.Id),
                0,
                "The watchdog should re-plan and re-queue an agent that lost its slot.");
        }

        [Test]
        public void Waiting_agents_stand_beside_the_shaft_not_inside_it()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);

            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);
            var shaft = elevators.Shafts[0];
            var shaftCentre = shaft.X + 0.5f;
            Assert.Greater(
                Mathf.Abs(agent.WorldPosition.x - shaftCentre),
                0.5f,
                "A queued agent must render outside the shaft cell.");
            Assert.AreEqual(agent.ElevatorEntryFloor + 0.5f, agent.WorldPosition.y, 0.001f);
        }

        [Test]
        public void Queue_lane_positions_are_ordered_and_compact_after_boarding()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(4, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 4, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            var shaft = elevators.Shafts[0];

            Assert.IsTrue(elevators.TryEnqueue(101, shaft.X, 0, ElevatorDirection.Up));
            Assert.IsTrue(elevators.TryEnqueue(102, shaft.X, 0, ElevatorDirection.Up));
            Assert.AreEqual(0, elevators.GetQueueIndex(shaft, 0, ElevatorDirection.Up, 101));
            Assert.AreEqual(1, elevators.GetQueueIndex(shaft, 0, ElevatorDirection.Up, 102));
            Assert.AreEqual(-1, elevators.GetQueueIndex(shaft, 0, ElevatorDirection.Up, 999));

            shaft.UpQueues[0].Dequeue();
            Assert.AreEqual(0, elevators.GetQueueIndex(shaft, 0, ElevatorDirection.Up, 102));
        }

        [Test]
        public void Waiting_over_ten_game_minutes_increases_stress()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);

            clock.Tick(11f);
            agents.Tick(1f, clock, grid);

            Assert.Greater(agent.Stress, 0f);
            Assert.Greater(agent.ElevatorWaitMinutes, 10f);
        }

        static TowerGrid CreateFourFloorOfficeTower()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _));
            for (var floor = 1; floor <= 4; floor++)
                Assert.IsTrue(grid.TryPlace(Office(floor == 4), new Vector2Int(0, floor), out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 4, out _));
            return grid;
        }

        static RoomTypeSO Lobby()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "lobby";
            room.isLobby = true;
            room.allowAboveGround = true;
            room.size = Vector2Int.one;
            return room;
        }

        static RoomTypeSO Office(bool occupied)
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "office";
            room.category = RoomCategory.Office;
            room.size = new Vector2Int(9, 1);
            room.maxOccupants = occupied ? 1 : 0;
            room.allowAboveGround = true;
            return room;
        }

        static RoomTypeSO Elevator()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "elevator";
            room.category = RoomCategory.Transit;
            room.size = new Vector2Int(1, 2);
            room.isElevatorShaft = true;
            room.allowAboveGround = true;
            room.allowBasement = true;
            return room;
        }
    }
}
