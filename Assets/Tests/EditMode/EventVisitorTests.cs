using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class EventVisitorTests
    {
        [Test]
        public void ComputeEventVisitorSpawnPerDay_scales_and_caps()
        {
            Assert.AreEqual(0, AgentSystem.ComputeEventVisitorSpawnPerDay(0));
            Assert.AreEqual(0, AgentSystem.ComputeEventVisitorSpawnPerDay(4));
            Assert.AreEqual(8, AgentSystem.ComputeEventVisitorSpawnPerDay(40));
            Assert.AreEqual(24, AgentSystem.ComputeEventVisitorSpawnPerDay(120));
            Assert.AreEqual(
                AgentSystem.MaxConcurrentEventVisitors,
                AgentSystem.ComputeEventVisitorSpawnPerDay(10_000));
        }

        [Test]
        public void EventVisitor_is_ephemeral_for_SyncHomes()
        {
            var (grid, hall, agents, clock) = SetupHallTower();
            Assert.IsTrue(agents.TrySpawnEventDayVisitor(grid, clock, hall));
            Assert.AreEqual(1, agents.Agents.Count(a => a.Role == AgentRole.EventVisitor));

            agents.SyncHomes(grid);

            Assert.AreEqual(
                1,
                agents.Agents.Count(a => a.Role == AgentRole.EventVisitor),
                "SyncHomes must not remove EventVisitor agents.");
        }

        [Test]
        public void Population_excludes_event_visitors()
        {
            var (grid, hall, agents, clock) = SetupHallTower();
            var before = agents.Population;
            Assert.IsTrue(agents.TrySpawnEventDayVisitor(grid, clock, hall));
            Assert.AreEqual(before, agents.Population);
        }

        [Test]
        public void TrySpawnEventDayVisitor_respects_concurrent_cap()
        {
            var (grid, hall, agents, clock) = SetupHallTower();
            for (var i = 0; i < AgentSystem.MaxConcurrentEventVisitors + 5; i++)
                agents.TrySpawnEventDayVisitor(grid, clock, hall);

            Assert.AreEqual(
                AgentSystem.MaxConcurrentEventVisitors,
                agents.Agents.Count(a => a.Role == AgentRole.EventVisitor));
            Assert.IsFalse(agents.TrySpawnEventDayVisitor(grid, clock, hall));
        }

        [Test]
        public void SyncEventVisitors_spawns_while_live_and_despawns_when_ended()
        {
            var (grid, hall, agents, clock) = SetupHallTower();
            var conference = new ConferenceSystem();
            conference.Active.Phase = MajorEventPhase.Live;
            conference.Active.Name = "TowerCon";
            conference.Active.StartDayIndex = 0;
            conference.Active.EndDayIndex = 2;
            conference.Active.BookedHallInstanceIds.Add(hall.InstanceId);
            conference.BookedHallInstanceIds.Add(hall.InstanceId);

            Assert.AreEqual(120, conference.SumBookedHallCapacity(grid));
            Assert.AreEqual(24, AgentSystem.ComputeEventVisitorSpawnPerDay(120));

            agents.SyncEventVisitors(conference, grid, clock);
            var spawned = agents.Agents.Count(a => a.Role == AgentRole.EventVisitor);
            Assert.Greater(spawned, 0);
            Assert.LessOrEqual(spawned, AgentSystem.MaxConcurrentEventVisitors);

            // Same day: no additional spawn batch.
            agents.SyncEventVisitors(conference, grid, clock);
            Assert.AreEqual(spawned, agents.Agents.Count(a => a.Role == AgentRole.EventVisitor));

            conference.Active.Phase = MajorEventPhase.None;
            conference.BookedHallInstanceIds.Clear();
            conference.Active.BookedHallInstanceIds.Clear();
            agents.SyncEventVisitors(conference, grid, clock);

            Assert.AreEqual(0, agents.Agents.Count(a => a.Role == AgentRole.EventVisitor));
        }

        [Test]
        public void SyncEventVisitors_hotel_fraction_can_claim_bed()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            Assert.IsTrue(grid.TryPlace(EventHall(40), new Vector2Int(0, 1), out var hall));
            Assert.IsTrue(grid.TryPlace(Hotel(2), new Vector2Int(12, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(8, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            Assert.Greater(agents.Agents.Count(a => a.Role == AgentRole.HotelGuest), 0);

            var clock = new GameClock(1f, 12 * 60);
            Assert.IsTrue(agents.TrySpawnEventHotelVisitor(grid, clock));
            Assert.AreEqual(1, agents.Agents.Count(a => a.Role == AgentRole.EventVisitor));
            var visitor = agents.Agents.First(a => a.Role == AgentRole.EventVisitor);
            Assert.AreEqual(RoomCategory.Hotel, visitor.HomeRoom.Type.category);
        }

        static (TowerGrid grid, RoomInstance hall, AgentSystem agents, GameClock clock) SetupHallTower()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            Assert.IsTrue(grid.TryPlace(EventHall(120), new Vector2Int(0, 1), out var hall));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var clock = new GameClock(1f, 12 * 60);
            return (grid, hall, agents, clock);
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
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO EventHall(int eventCapacity)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ConferenceSystem.EventHallId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(12, 2);
            so.allowAboveGround = true;
            so.eventCapacity = eventCapacity;
            so.maxOccupants = 0;
            return so;
        }

        static RoomTypeSO Hotel(int maxOccupants)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel";
            so.category = RoomCategory.Hotel;
            so.size = new Vector2Int(4, 1);
            so.allowAboveGround = true;
            so.maxOccupants = maxOccupants;
            so.baseIncome = 100;
            return so;
        }
    }
}
