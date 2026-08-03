using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentSystemTests
    {
        [Test]
        public void Inaccessible_condo_stays_vacant_and_does_not_pay()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 1), out var condo));
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            var notifiedRooms = new List<RoomInstance>();
            var wallet = new FundsWallet(0);
            var economy = new EconomySystem();

            agents.SyncHomes(grid, room =>
            {
                notifiedRooms.Add(room);
                economy.TrySellCondo(room, wallet);
            });

            Assert.AreEqual(0, agents.Agents.Count);
            Assert.AreEqual(0, agents.Population);
            Assert.IsFalse(condo.CondoSold);
            Assert.AreEqual(0, wallet.Balance);
            Assert.IsEmpty(notifiedRooms);
        }

        [Test]
        public void Reachable_condo_pays_only_after_buyer_arrives()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 1), out var condo));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            var notifiedRooms = new List<RoomInstance>();
            var wallet = new FundsWallet(0);
            var economy = new EconomySystem();

            agents.SyncHomes(grid, room =>
            {
                notifiedRooms.Add(room);
                economy.TrySellCondo(room, wallet);
            });

            var buyer = agents.Agents.Single();
            Assert.AreEqual(AgentPhase.Outside, buyer.Phase);
            Assert.AreEqual(0, agents.Population);
            Assert.IsFalse(condo.CondoSold);
            Assert.AreEqual(0, wallet.Balance);
            Assert.IsEmpty(notifiedRooms);

            var clock = new GameClock(1f, 12 * 60);
            for (var i = 0; i < 20 && buyer.Phase != AgentPhase.AtHome; i++)
                agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.AtHome, buyer.Phase);
            Assert.AreEqual(1, agents.Population);
            Assert.IsTrue(condo.CondoSold);
            Assert.AreEqual(condo.Type.baseIncome, wallet.Balance);
            CollectionAssert.AreEqual(new[] { condo }, notifiedRooms);
        }

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

            agents.Tick(11f, clock, grid);

            Assert.Greater(agent.Stress, 0f);
            Assert.Greater(agent.ElevatorWaitMinutes, 10f);
        }

        [Test]
        public void Walking_distance_scales_with_game_minutes_not_real_time()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Office(true), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));

            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents[0];
            var clock = new GameClock(1f, agent.ArrivalMinute);

            agents.Tick(0.01f, clock, grid);
            Assert.AreEqual(AgentPhase.Moving, agent.Phase);
            var start = agent.WorldPosition;

            agents.Tick(1f, clock, grid);
            var movedInOneGameMinute = Vector2.Distance(start, agent.WorldPosition);

            Assert.AreEqual(
                AgentSystem.MoveCellsPerSecond,
                movedInOneGameMinute,
                0.05f,
                "At 1 game minute, agents should move MoveCellsPerSecond cells.");
        }

        static RoomTypeSO Stairs()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "stairs";
            room.isStairs = true;
            room.allowAboveGround = true;
            room.allowBasement = true;
            room.size = new Vector2Int(2, 2);
            return room;
        }

        [Test]
        public void IsMovementStuck_true_when_moving_with_goal_and_empty_path()
        {
            var agent = new Agent(1, AgentRole.OfficeWorker, null, new Vector2Int(5, 0));
            agent.Phase = AgentPhase.Moving;
            agent.GoalCell = new Vector2Int(5, 4);
            agent.Path = new List<Vector2Int>();
            Assert.IsTrue(AgentSystem.IsMovementStuck(agent));

            agent.Path = new List<Vector2Int> { new Vector2Int(5, 0), new Vector2Int(5, 1) };
            agent.PathIndex = 0;
            Assert.IsFalse(AgentSystem.IsMovementStuck(agent));

            agent.PathIndex = 2;
            Assert.IsTrue(AgentSystem.IsMovementStuck(agent));
        }

        [Test]
        public void Path_stuck_replan_recovers_when_route_appears()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Office(true), new Vector2Int(0, 1), out _));
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            var clock = new GameClock();

            agents.SyncHomes(grid);
            Assert.Greater(agents.Agents.Count, 0);
            var agent = agents.Agents[0];
            agent.Cell = new Vector2Int(5, 0);
            agent.WorldPosition = new Vector2(5.5f, 0.5f);
            agent.Phase = AgentPhase.Moving;
            agent.GoalCell = new Vector2Int(4, 1);
            agent.PhaseAfterMove = AgentPhase.Working;
            agent.Path = new List<Vector2Int>();
            agent.PathIndex = 0;
            agent.TripLegs = new List<TransitLeg>();
            agent.PathStuckMinutes = AgentSystem.PathStuckReplanIntervalMinutes;

            // Still no vertical route → replan keeps stall.
            agents.Tick(1f, clock, grid);
            Assert.IsTrue(AgentSystem.IsMovementStuck(agent));

            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(8, 0), out _));
            router.Rebuild(grid);
            agent.Phase = AgentPhase.Moving;
            agent.GoalCell = new Vector2Int(4, 1);
            agent.Path = new List<Vector2Int>();
            agent.PathIndex = 0;
            agent.PathStuckMinutes = AgentSystem.PathStuckReplanIntervalMinutes;
            agents.Tick(1f, clock, grid);

            Assert.IsFalse(AgentSystem.IsMovementStuck(agent));
            Assert.IsTrue(agent.Path != null && agent.Path.Count > 0);
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

        static RoomTypeSO Condo()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "condo";
            room.category = RoomCategory.Condo;
            room.size = Vector2Int.one;
            room.maxOccupants = 1;
            room.allowAboveGround = true;
            room.incomeModel = IncomeModel.UpfrontSale;
            room.baseIncome = 150_000;
            return room;
        }
    }
}
