using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SecurityPatrolTests
    {
        [Test]
        public void SyncHomes_spawns_security_from_StaffedWorkers()
        {
            var grid = SecurityTower(out var security, out _, out _);
            security.SetStaffedWorkers(1);
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            Assert.AreSame(security, guard.HomeRoom);
            Assert.AreEqual(security.Origin, guard.Cell);
            Assert.AreEqual(0, agents.Population, "Security must not count toward population.");
        }

        [Test]
        public void SyncHomes_despawns_security_when_StaffedWorkers_drops()
        {
            var grid = SecurityTower(out var security, out _, out _);
            security.SetStaffedWorkers(2);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            Assert.AreEqual(2, agents.Agents.Count(a => a.Role == AgentRole.Security));

            security.SetStaffedWorkers(0);
            agents.SyncHomes(grid);

            Assert.AreEqual(0, agents.Agents.Count(a => a.Role == AgentRole.Security));
        }

        [Test]
        public void Security_paths_toward_highest_crime_floor()
        {
            var grid = SecurityTower(out var security, out var hotel, out _);
            security.SetStaffedWorkers(1);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, security);
            guard.Phase = AgentPhase.AtHome;

            var crime = new CrimeSystem();
            crime.SetCrime(security.Origin.y, 5f);
            crime.SetCrime(hotel.Origin.y, 40f);

            var clock = new GameClock(1f, 12 * 60);
            agents.Tick(1f, clock, grid, crime: crime);

            Assert.IsTrue(
                guard.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding ||
                (guard.Phase == AgentPhase.Working && guard.Cell.y == hotel.Origin.y),
                "Security should begin a patrol toward the high-crime floor.");
            Assert.IsTrue(
                guard.GoalCell?.y == hotel.Origin.y || guard.Cell.y == hotel.Origin.y,
                "Patrol goal should be the high-crime hotel floor.");
        }

        [Test]
        public void Security_prefers_busy_shop_floor_over_higher_idle_crime()
        {
            var grid = SecurityTower(out var security, out var hotel, out var shop);
            Assert.AreNotEqual(
                hotel.Origin.y,
                shop.Origin.y,
                "Hotel and shop must be on different floors so busy preference is proven.");
            security.SetStaffedWorkers(1);
            Assert.IsTrue(shop.TryOccupyVisitorSlot());
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, security);
            guard.Phase = AgentPhase.AtHome;

            var crime = new CrimeSystem();
            // Idle hotel floor has higher crime, but shop floor is busy (visitors).
            crime.SetCrime(hotel.Origin.y, 90f);
            crime.SetCrime(shop.Origin.y, 25f);

            var clock = new GameClock(1f, 12 * 60);
            agents.Tick(1f, clock, grid, crime: crime);

            Assert.IsTrue(
                guard.GoalCell?.y == shop.Origin.y || guard.Cell.y == shop.Origin.y,
                "Busy shop floor should win over higher idle crime on another floor.");
        }

        [Test]
        public void Security_does_not_replan_patrol_while_waiting_or_riding_elevator()
        {
            var grid = ElevatorSecurityTower(
                out var security,
                out var hotel,
                out var decoy,
                out var elevators,
                out var agents);
            Assert.AreNotEqual(hotel.Origin.y, decoy.Origin.y);
            security.SetStaffedWorkers(1);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, security);
            guard.Phase = AgentPhase.AtHome;

            var crime = new CrimeSystem();
            crime.SetCrime(hotel.Origin.y, 80f);
            crime.SetCrime(decoy.Origin.y, 10f);
            var clock = new GameClock(1f, 12 * 60);

            for (var i = 0; i < 40 && guard.Phase != AgentPhase.WaitingAtElevator; i++)
                agents.Tick(1f, clock, grid, crime: crime);

            Assert.AreEqual(AgentPhase.WaitingAtElevator, guard.Phase);
            Assert.IsTrue(guard.GoalCell.HasValue);
            Assert.AreEqual(hotel.Origin.y, guard.GoalCell.Value.y);
            var waitingGoal = guard.GoalCell;

            // Tempt a replan toward the decoy floor while queued.
            crime.SetCrime(hotel.Origin.y, 5f);
            crime.SetCrime(decoy.Origin.y, 95f);
            for (var i = 0; i < 5; i++)
                agents.Tick(1f, clock, grid, crime: crime);

            Assert.AreEqual(AgentPhase.WaitingAtElevator, guard.Phase);
            Assert.AreEqual(waitingGoal, guard.GoalCell, "WaitingAtElevator must keep stable GoalCell.");

            // Board and confirm Riding also keeps the original patrol goal.
            for (var i = 0; i < 40 && guard.Phase != AgentPhase.Riding; i++)
            {
                elevators.Tick(0.25f);
                agents.Tick(0f, clock, grid, crime: crime);
            }

            Assert.AreEqual(AgentPhase.Riding, guard.Phase);
            var ridingGoal = guard.GoalCell;
            Assert.AreEqual(waitingGoal, ridingGoal);

            crime.SetCrime(hotel.Origin.y, 1f);
            crime.SetCrime(decoy.Origin.y, 99f);
            for (var i = 0; i < 5; i++)
                agents.Tick(0f, clock, grid, crime: crime);

            Assert.AreEqual(AgentPhase.Riding, guard.Phase);
            Assert.AreEqual(ridingGoal, guard.GoalCell, "Riding must keep stable GoalCell.");
        }

        [Test]
        public void Security_dwells_then_replans_after_PatrolDwellMinutes()
        {
            var grid = SecurityTower(out var security, out var hotel, out var shop);
            security.SetStaffedWorkers(1);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, hotel);
            guard.Phase = AgentPhase.AtHome;

            var crime = new CrimeSystem();
            crime.SetCrime(hotel.Origin.y, 40f);
            var clock = new GameClock(1f, 12 * 60);

            agents.Tick(0f, clock, grid, crime: crime);
            Assert.AreEqual(AgentPhase.Working, guard.Phase);
            Assert.AreEqual(
                AgentSystem.PatrolDwellMinutes,
                guard.ServiceWorkRemaining,
                0.001f,
                "Arriving on the patrol floor should start a full dwell.");

            agents.Tick(AgentSystem.PatrolDwellMinutes * 0.5f, clock, grid, crime: crime);
            Assert.AreEqual(AgentPhase.Working, guard.Phase);
            Assert.Greater(guard.ServiceWorkRemaining, 0f);

            // After dwell, prefer the other crime floor (replan) rather than idle forever.
            crime.SetCrime(hotel.Origin.y, 5f);
            crime.SetCrime(shop.Origin.y, 70f);
            agents.Tick(AgentSystem.PatrolDwellMinutes, clock, grid, crime: crime);

            Assert.IsTrue(
                guard.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding ||
                (guard.Phase == AgentPhase.Working && guard.Cell.y == shop.Origin.y),
                "After dwell completes, security should replan toward a new patrol floor.");
            Assert.IsTrue(
                guard.GoalCell?.y == shop.Origin.y || guard.Cell.y == shop.Origin.y,
                "Replan after dwell should target the new highest-crime floor.");
        }

        [Test]
        public void Security_idles_home_when_crime_is_zero()
        {
            var grid = SecurityTower(out var security, out _, out _);
            security.SetStaffedWorkers(1);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, security);
            guard.Phase = AgentPhase.AtHome;

            var crime = new CrimeSystem();
            var clock = new GameClock(1f, 12 * 60);
            for (var i = 0; i < 5; i++)
                agents.Tick(1f, clock, grid, crime: crime);

            Assert.AreEqual(AgentPhase.AtHome, guard.Phase);
            Assert.AreEqual(security.Origin, guard.Cell);
        }

        [Test]
        public void CollectFloorsForRole_lists_security_floors()
        {
            var grid = SecurityTower(out var security, out _, out _);
            security.SetStaffedWorkers(1);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, security);

            var floors = new System.Collections.Generic.List<int>();
            agents.CollectFloorsForRole(AgentRole.Security, floors);

            Assert.AreEqual(1, floors.Count);
            Assert.AreEqual(security.Origin.y, floors[0]);
        }

        static TowerGrid SecurityTower(
            out RoomInstance security,
            out RoomInstance hotel,
            out RoomInstance shop)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            // Stacked stairs: floors 0–1, 1–2, 2–3 (one-floor overlap).
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 0), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 2), out _));
            // Left of stairs: stacked hotels — floor 2 is the idle high-crime target.
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 1), out _));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 2), out hotel));
            // Right of stairs: security on 1; shop stacked so busy floor is 3 (≠ hotel).
            Assert.IsTrue(grid.TryPlace(SecurityPost(), new Vector2Int(14, 1), out security));
            Assert.IsTrue(grid.TryPlace(Shop(), new Vector2Int(16, 1), out _));
            Assert.IsTrue(grid.TryPlace(Shop(), new Vector2Int(16, 2), out _));
            Assert.IsTrue(grid.TryPlace(Shop(), new Vector2Int(16, 3), out shop));
            return grid;
        }

        static TowerGrid ElevatorSecurityTower(
            out RoomInstance security,
            out RoomInstance hotel,
            out RoomInstance decoy,
            out ElevatorSystem elevators,
            out AgentSystem agents)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(12, 0), out var shaft));
            Assert.IsTrue(grid.TryExtendElevator(shaft, 0, 3, out _));
            // Stacked hotel column for support; top floor is the initial patrol target.
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 1), out _));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 2), out _));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 3), out hotel));
            Assert.IsTrue(grid.TryPlace(SecurityPost(), new Vector2Int(13, 1), out security));
            Assert.IsTrue(grid.TryPlace(Shop(), new Vector2Int(15, 1), out _));
            Assert.IsTrue(grid.TryPlace(Shop(), new Vector2Int(15, 2), out decoy));

            elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            agents = new AgentSystem(router);
            return grid;
        }

        static AgentSystem CreateAgents(TowerGrid grid)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            return new AgentSystem(router);
        }

        static void PlaceAtRoom(Agent agent, RoomInstance room)
        {
            var cell = room.Origin;
            agent.Cell = cell;
            agent.WorldPosition = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            agent.Visible = true;
            agent.Path?.Clear();
            agent.TripLegs?.Clear();
            agent.GoalCell = null;
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Stairs()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.isStairs = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            so.size = new Vector2Int(2, 2);
            return so;
        }

        static RoomTypeSO SecurityPost()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_security";
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(2, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Hotel()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel";
            so.category = RoomCategory.Hotel;
            so.size = new Vector2Int(9, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Shop()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_fast";
            so.category = RoomCategory.Commercial;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 4;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.TrafficVariable;
            return so;
        }

        static RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }
    }
}
