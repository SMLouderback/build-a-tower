using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HotelDirtySyncTests
    {
        [Test]
        public void Hotel_checkout_marks_room_dirty()
        {
            var (grid, hotel, agents, agent, clock) = SetupHotel();
            PlaceAgentStayingOvernight(agent, clock);

            clock.AdvanceMinutes(1);
            agents.Tick(1f, clock, grid);

            Assert.IsTrue(agent.CheckedOutToday);
            Assert.IsTrue(hotel.Dirty);
        }

        [Test]
        public void SyncHomes_skips_dirty_hotel()
        {
            var grid = LivingTower(Hotel(), out var hotel);
            hotel.MarkDirty();
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            Assert.AreEqual(0, agents.Agents.Count);
        }

        [Test]
        public void SyncHomes_skips_broken_hotel()
        {
            var grid = LivingTower(Hotel(), out var hotel);
            hotel.Condition = 0;
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            Assert.AreEqual(0, agents.Agents.Count);
            Assert.IsTrue(hotel.IsBroken);
        }

        [Test]
        public void SyncHomes_skips_broken_office()
        {
            var grid = LivingTower(Office(), out var office);
            office.Condition = 0;
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            Assert.AreEqual(0, agents.Agents.Count);
        }

        [Test]
        public void SyncHomes_skips_broken_condo()
        {
            var grid = LivingTower(Condo(), out var condo);
            condo.Condition = 0;
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            Assert.AreEqual(0, agents.Agents.Count);
        }

        [Test]
        public void SyncHomes_fills_clean_hotel()
        {
            var grid = LivingTower(Hotel(), out _);
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            Assert.AreEqual(1, agents.Agents.Count);
            Assert.AreEqual(AgentRole.HotelGuest, agents.Agents[0].Role);
        }

        [Test]
        public void Dirty_hotel_blocks_check_in()
        {
            var (grid, hotel, agents, agent, clock) = SetupHotel();
            hotel.MarkDirty();
            agent.Phase = AgentPhase.Outside;
            agent.CheckInDay = -1;
            agent.CheckedOutToday = true;
            clock.AdvanceMinutes(16 * 60 - clock.MinuteOfDay);

            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.Outside, agent.Phase);
            Assert.AreEqual(-1, agent.CheckInDay);
        }

        [Test]
        public void Low_home_condition_adds_stress_once_per_day()
        {
            var (grid, hotel, agents, agent, clock) = SetupHotel();
            hotel.Condition = RoomConditionRules.StressBelow - 1;
            PlaceAgentStayingAtHotel(agent, clock);
            var before = agent.Stress;

            agents.Tick(0f, clock, grid);

            Assert.AreEqual(
                before + AgentSystem.LowConditionStressPerDay,
                agent.Stress,
                0.001f);

            agents.Tick(0f, clock, grid);
            Assert.AreEqual(
                before + AgentSystem.LowConditionStressPerDay,
                agent.Stress,
                0.001f,
                "Low-condition stress should apply once per day.");
        }

        static (
            TowerGrid grid,
            RoomInstance hotel,
            AgentSystem agents,
            Agent agent,
            GameClock clock) SetupHotel()
        {
            var grid = LivingTower(Hotel(), out var hotel);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single();
            var clock = new GameClock(1f, 10 * 60);
            clock.AdvanceMinutes(GameClock.MinutesPerDay);
            return (grid, hotel, agents, agent, clock);
        }

        static TowerGrid LivingTower(RoomTypeSO living, out RoomInstance room)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(living, new Vector2Int(0, 1), out room));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            return grid;
        }

        static AgentSystem CreateAgents(TowerGrid grid)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            return new AgentSystem(router);
        }

        static void PlaceAgentStayingOvernight(Agent agent, GameClock clock)
        {
            PlaceAgentStayingAtHotel(agent, clock);
            agent.CheckInDay = clock.DayIndex - 1;
            agent.CheckedOutToday = false;
        }

        static void PlaceAgentStayingAtHotel(Agent agent, GameClock clock)
        {
            var home = agent.HomeRoom.Origin;
            agent.Cell = home;
            agent.WorldPosition = new Vector2(home.x + 0.5f, home.y + 0.5f);
            agent.Phase = AgentPhase.Staying;
            agent.Visible = true;
            agent.CheckInDay = clock.DayIndex;
            agent.CheckedOutToday = false;
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

        static RoomTypeSO Hotel()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel";
            so.category = RoomCategory.Hotel;
            so.size = new Vector2Int(9, 1);
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Condo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "condo";
            so.category = RoomCategory.Condo;
            so.size = Vector2Int.one;
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = 150_000;
            return so;
        }
    }
}
