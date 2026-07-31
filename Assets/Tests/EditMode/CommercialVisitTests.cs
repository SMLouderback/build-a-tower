using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CommercialVisitTests
    {
        [Test]
        public void TryBeginCommercialTrip_starts_when_open_reachable_shop_exists()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithShop(open: true);
            PlaceAgentWorkingAtOffice(agent);

            Assert.IsTrue(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreEqual(clock.DayIndex, agent.CommercialTripDay);
            Assert.AreSame(shop, agent.VisitTarget);
            Assert.AreEqual(AgentPhase.Working, agent.PhaseAfterVisit);
            Assert.AreEqual(1, shop.ConcurrentVisitors);
            Assert.That(agent.VisitDwellRemaining, Is.InRange(15f, 25f));
            Assert.IsTrue(
                agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator,
                "Trip should begin toward the shop.");
        }

        [Test]
        public void TryBeginCommercialTrip_rejects_second_trip_same_day()
        {
            var (grid, _, agents, agent, clock) = SetupOfficeWithShop(open: true);
            PlaceAgentWorkingAtOffice(agent);

            Assert.IsTrue(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
        }

        [Test]
        public void TryBeginCommercialTrip_skips_closed_shop()
        {
            var (grid, _, agents, agent, clock) = SetupOfficeWithShop(open: false);
            PlaceAgentWorkingAtOffice(agent);

            Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreEqual(-1, agent.CommercialTripDay);
        }

        [Test]
        public void Office_lunch_trip_records_visit_after_dwell()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithShop(open: true);

            // Commute in.
            clock.AdvanceMinutes(agent.ArrivalMinute - clock.MinuteOfDay);
            for (var i = 0; i < 400 && agent.Phase != AgentPhase.Working; i++)
                agents.Tick(1f, clock, grid);
            Assert.AreEqual(AgentPhase.Working, agent.Phase);

            // Enter lunch window (~noon).
            var lunch = 12 * 60;
            if (clock.MinuteOfDay < lunch)
                clock.AdvanceMinutes(lunch - clock.MinuteOfDay);

            for (var i = 0; i < 2000 && shop.VisitsToday < 1; i++)
                agents.Tick(1f, clock, grid);

            Assert.GreaterOrEqual(shop.VisitsToday, 1, "Completed lunch dwell should RecordVisit.");
            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.IsNull(agent.VisitTarget);
            Assert.AreEqual(
                AgentPhase.Working,
                agent.Phase,
                "Agent should return to work after the visit.");
        }

        [Test]
        public void Office_lunch_window_triggers_at_most_once_per_day()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithShop(open: true);
            PlaceAgentWorkingAtOffice(agent);

            clock.AdvanceMinutes(12 * 60 - clock.MinuteOfDay);
            Assert.IsTrue(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));

            // Force-complete dwell path without relying on movement.
            agent.Phase = AgentPhase.VisitingShop;
            agent.VisitDwellRemaining = 0.01f;
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(1, shop.VisitsToday);

            // Still lunch; schedule must not start another commercial trip.
            agent.Phase = AgentPhase.Working;
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(1, shop.VisitsToday);
            Assert.AreEqual(clock.DayIndex, agent.CommercialTripDay);
            Assert.IsFalse(
                agent.Phase == AgentPhase.Moving && agent.PhaseAfterMove == AgentPhase.VisitingShop);
        }

        static (
            TowerGrid grid,
            RoomInstance shop,
            AgentSystem agents,
            Agent agent,
            GameClock clock) SetupOfficeWithShop(bool open)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(9, 1), out var shop));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single();
            var clock = new GameClock(1f, open ? 12 * 60 : 22 * 60);
            return (grid, shop, agents, agent, clock);
        }

        static void PlaceAgentWorkingAtOffice(Agent agent)
        {
            var home = agent.HomeRoom.Origin;
            agent.Cell = home;
            agent.WorldPosition = new Vector2(home.x + 0.5f, home.y + 0.5f);
            agent.Phase = AgentPhase.Working;
            agent.Visible = true;
            agent.CheckedOutToday = true;
            agent.WorkedMinutes = 60;
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

        static RoomTypeSO FastFood()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_fast";
            so.category = RoomCategory.Commercial;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.baseIncome = 40;
            so.maxOccupants = 4;
            so.hasActiveHours = true;
            so.activeHoursStart = 11 * 60;
            so.activeHoursEnd = 21 * 60;
            return so;
        }
    }
}
