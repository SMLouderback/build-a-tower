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
            security.SetStaffedWorkers(1);
            Assert.IsTrue(shop.TryOccupyVisitorSlot());
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guard = agents.Agents.Single(a => a.Role == AgentRole.Security);
            PlaceAtRoom(guard, security);
            guard.Phase = AgentPhase.AtHome;

            var crime = new CrimeSystem();
            // Idle hotel floor has higher crime, but shop has visitors (busy).
            crime.SetCrime(hotel.Origin.y, 90f);
            crime.SetCrime(shop.Origin.y, 25f);

            var clock = new GameClock(1f, 12 * 60);
            agents.Tick(1f, clock, grid, crime: crime);

            Assert.IsTrue(
                guard.GoalCell?.y == shop.Origin.y || guard.Cell.y == shop.Origin.y,
                "Busy shop/hotel floors should win over higher idle crime.");
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
            // Stacked stairs: floors 0–1 then 1–2 (one-floor overlap).
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 0), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 1), out _));
            // Security abuts stairs on floor 1; hotel/shop abut stairs on floor 2.
            Assert.IsTrue(grid.TryPlace(SecurityPost(), new Vector2Int(10, 1), out security));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 2), out hotel));
            Assert.IsTrue(grid.TryPlace(Shop(), new Vector2Int(14, 2), out shop));
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
    }
}
