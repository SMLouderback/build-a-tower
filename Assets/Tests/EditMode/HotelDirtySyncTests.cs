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
        public void Hotel_first_of_two_guests_checkout_does_not_dirty_while_other_stays()
        {
            var grid = LivingTower(Hotel(maxOccupants: 2), out var hotel);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            Assert.AreEqual(2, agents.Agents.Count);

            var clock = new GameClock(1f, 10 * 60);
            clock.AdvanceMinutes(GameClock.MinutesPerDay);
            var first = agents.Agents[0];
            var second = agents.Agents[1];
            PlaceAgentStayingOvernight(first, clock);
            PlaceAgentStayingOvernight(second, clock);

            // Only first guest reaches the checkout branch this tick.
            second.CheckInDay = clock.DayIndex; // still "today" — not due to checkout

            clock.AdvanceMinutes(1);
            agents.Tick(1f, clock, grid);

            Assert.IsTrue(first.CheckedOutToday);
            Assert.IsFalse(second.CheckedOutToday);
            Assert.IsFalse(hotel.Dirty, "Room must stay clean while another guest is still staying.");
        }

        [Test]
        public void Hotel_last_guest_checkout_marks_room_dirty()
        {
            var grid = LivingTower(Hotel(maxOccupants: 2), out var hotel);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var clock = new GameClock(1f, 10 * 60);
            clock.AdvanceMinutes(GameClock.MinutesPerDay);
            foreach (var guest in agents.Agents)
                PlaceAgentStayingOvernight(guest, clock);

            clock.AdvanceMinutes(1);
            agents.Tick(1f, clock, grid);

            Assert.IsTrue(hotel.Dirty);
            Assert.IsTrue(agents.Agents.All(a => a.CheckedOutToday));
        }

        [Test]
        public void SyncHomes_assigns_distinct_home_slots()
        {
            var grid = LivingTower(Hotel(maxOccupants: 2), out var hotel);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);

            Assert.AreEqual(2, agents.Agents.Count);
            Assert.AreEqual(0, agents.Agents[0].HomeSlot);
            Assert.AreEqual(1, agents.Agents[1].HomeSlot);
            Assert.AreNotEqual(agents.Agents[0].Cell, agents.Agents[1].Cell);
            Assert.AreEqual(hotel.Origin.x, agents.Agents[0].Cell.x);
            Assert.AreEqual(hotel.Origin.x + 1, agents.Agents[1].Cell.x);
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
            Assert.AreEqual(0, agents.Population, "Vacant Outside hotel beds must not inflate population.");
        }

        [Test]
        public void Population_counts_hotel_guest_only_while_in_tower()
        {
            var grid = LivingTower(Hotel(), out _);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var guest = agents.Agents[0];

            Assert.AreEqual(AgentPhase.Outside, guest.Phase);
            Assert.AreEqual(0, agents.Population);

            guest.Phase = AgentPhase.AtHome;
            guest.Visible = true;
            Assert.AreEqual(1, agents.Population);

            guest.Phase = AgentPhase.Outside;
            Assert.AreEqual(0, agents.Population);
        }

        [Test]
        public void Dirty_hotel_blocks_check_in()
        {
            var (grid, hotel, agents, agent, clock) = SetupHotel();
            hotel.MarkDirty();
            agent.Phase = AgentPhase.Outside;
            agent.CheckInDay = -1;
            agent.CheckedOutToday = true;
            agent.CheckInMinute = AgentSystem.HotelCheckInMinute;
            clock.AdvanceMinutes(16 * 60 - clock.MinuteOfDay);

            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.Outside, agent.Phase);
            Assert.AreEqual(-1, agent.CheckInDay);
        }

        [Test]
        public void Hotel_guest_does_not_check_in_before_personal_time()
        {
            var (grid, _, agents, agent, clock) = SetupHotelAtMinute(16 * 60);
            agent.Phase = AgentPhase.Outside;
            agent.Visible = false;
            agent.CheckInDay = -1;
            agent.CheckedOutToday = false;
            agent.CheckInMinute = 18 * 60; // 6pm

            agents.Tick(1f, clock, grid);
            Assert.AreEqual(AgentPhase.Outside, agent.Phase);
            Assert.AreEqual(-1, agent.CheckInDay);

            clock.AdvanceMinutes(18 * 60 - clock.MinuteOfDay);
            agents.Tick(1f, clock, grid);
            Assert.AreNotEqual(-1, agent.CheckInDay);
        }

        [Test]
        public void RollHotelCheckInMinute_stays_in_4pm_to_7pm_window()
        {
            var rng = new System.Random(0);
            for (var i = 0; i < 200; i++)
            {
                var m = AgentSystem.RollHotelCheckInMinute(rng);
                Assert.GreaterOrEqual(m, AgentSystem.HotelCheckInMinute);
                Assert.LessOrEqual(m, AgentSystem.HotelCheckInLatestMinute);
            }
        }

        [Test]
        public void IsHotelCheckInDue_respects_earliest_and_personal_time()
        {
            Assert.IsFalse(AgentSystem.IsHotelCheckInDue(15 * 60 + 59, 16 * 60));
            Assert.IsTrue(AgentSystem.IsHotelCheckInDue(16 * 60, 16 * 60));
            Assert.IsFalse(AgentSystem.IsHotelCheckInDue(17 * 60, 18 * 60));
            Assert.IsTrue(AgentSystem.IsHotelCheckInDue(18 * 60, 18 * 60));
            Assert.IsTrue(AgentSystem.IsHotelCheckInDue(19 * 60, 18 * 60));
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
            GameClock clock) SetupHotel() =>
            SetupHotelAtMinute(10 * 60);

        static (
            TowerGrid grid,
            RoomInstance hotel,
            AgentSystem agents,
            Agent agent,
            GameClock clock) SetupHotelAtMinute(int minuteOfDay)
        {
            var grid = LivingTower(Hotel(), out var hotel);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single();
            var clock = new GameClock(1f, minuteOfDay);
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
            // Due at earliest so existing morning tests (≈10:00) still trigger checkout.
            agent.CheckoutMinute = AgentSystem.HotelCheckoutEarliestMinute;
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
            agent.CheckoutMinute = AgentSystem.HotelCheckoutLatestMinute;
        }

        [Test]
        public void Hotel_guest_does_not_checkout_before_6am()
        {
            var (grid, hotel, agents, agent, clock) = SetupHotelAtMinute(5 * 60);
            PlaceAgentStayingOvernight(agent, clock);
            agent.CheckoutMinute = AgentSystem.HotelCheckoutEarliestMinute;

            clock.AdvanceMinutes(1);
            agents.Tick(1f, clock, grid);

            Assert.IsFalse(agent.CheckedOutToday);
            Assert.IsFalse(hotel.Dirty);
            Assert.AreEqual(AgentPhase.Staying, agent.Phase);
        }

        [Test]
        public void Hotel_guest_checkouts_at_personal_checkout_minute()
        {
            var (grid, hotel, agents, agent, clock) = SetupHotelAtMinute(9 * 60);
            PlaceAgentStayingOvernight(agent, clock);
            agent.CheckoutMinute = 10 * 60;

            clock.AdvanceMinutes(1); // 9:01 — not yet
            agents.Tick(1f, clock, grid);
            Assert.IsFalse(agent.CheckedOutToday);

            clock.AdvanceMinutes(10 * 60 - clock.MinuteOfDay);
            agents.Tick(1f, clock, grid);
            Assert.IsTrue(agent.CheckedOutToday);
            Assert.IsTrue(hotel.Dirty);
        }

        [Test]
        public void RollHotelCheckoutMinute_stays_in_6am_to_11am_window()
        {
            var rng = new System.Random(0);
            for (var i = 0; i < 200; i++)
            {
                var m = AgentSystem.RollHotelCheckoutMinute(rng);
                Assert.GreaterOrEqual(m, AgentSystem.HotelCheckoutEarliestMinute);
                Assert.LessOrEqual(m, AgentSystem.HotelCheckoutLatestMinute);
            }
        }

        [Test]
        public void IsHotelCheckoutDue_respects_earliest_and_personal_time()
        {
            Assert.IsFalse(AgentSystem.IsHotelCheckoutDue(5 * 60 + 59, 6 * 60));
            Assert.IsTrue(AgentSystem.IsHotelCheckoutDue(6 * 60, 6 * 60));
            Assert.IsFalse(AgentSystem.IsHotelCheckoutDue(9 * 60, 10 * 60));
            Assert.IsTrue(AgentSystem.IsHotelCheckoutDue(10 * 60, 10 * 60));
            Assert.IsTrue(AgentSystem.IsHotelCheckoutDue(11 * 60, 10 * 60));
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

        static RoomTypeSO Hotel(int maxOccupants = 1)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel";
            so.category = RoomCategory.Hotel;
            so.size = new Vector2Int(9, 1);
            so.maxOccupants = maxOccupants;
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
