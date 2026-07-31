using System.Collections.Generic;
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
        public void TryBeginCommercialTrip_skips_restaurant_when_remaining_below_price()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithRestaurant();
            PlaceAgentWorkingAtOffice(agent);
            agent.DisposableRemaining = 30;
            agent.DisposableDayIndex = clock.DayIndex;

            Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreEqual(-1, agent.CommercialTripDay);
            Assert.IsNull(agent.VisitTarget);
            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.AreEqual(30, agent.DisposableRemaining);
        }

        [Test]
        public void Visit_complete_reduces_disposable_and_records_shop_earnings()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithShop(open: true);
            PlaceAgentWorkingAtOffice(agent);
            agent.DisposableRemaining = 50;
            agent.DisposableDayIndex = clock.DayIndex;

            Assert.IsTrue(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreSame(shop, agent.VisitTarget);

            agent.Phase = AgentPhase.VisitingShop;
            agent.VisitDwellRemaining = 0.01f;
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(1, shop.VisitsToday);
            Assert.Greater(shop.ShopEarningsToday, 0);
            Assert.Less(agent.DisposableRemaining, 50);
            Assert.AreEqual(50 - agent.DisposableRemaining, shop.ShopEarningsToday);
            Assert.That(shop.ShopEarningsToday, Is.InRange(1, 40));
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

        [Test]
        public void Hotel_evening_window_triggers_at_most_once_per_day()
        {
            var (grid, shop, agents, agent, clock) = SetupHotelWithShop();
            PlaceAgentStayingAtHotel(agent, clock);

            clock.AdvanceMinutes(19 * 60 - clock.MinuteOfDay);
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(clock.DayIndex, agent.CommercialTripDay);
            Assert.AreSame(shop, agent.VisitTarget);
            Assert.AreEqual(AgentPhase.Staying, agent.PhaseAfterVisit);
            Assert.IsTrue(
                agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator,
                "Hotel Staying evening window should begin a commercial trip.");

            agent.Phase = AgentPhase.VisitingShop;
            agent.VisitDwellRemaining = 0.01f;
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(1, shop.VisitsToday);

            agent.Phase = AgentPhase.Staying;
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(1, shop.VisitsToday);
            Assert.AreEqual(clock.DayIndex, agent.CommercialTripDay);
            Assert.IsFalse(
                agent.Phase == AgentPhase.Moving && agent.PhaseAfterMove == AgentPhase.VisitingShop);
        }

        [Test]
        public void Condo_daytime_window_triggers_at_most_once_per_day()
        {
            var (grid, shop, agents, agent, clock) = SetupCondoWithShop();
            PlaceAgentAtHomeInCondo(agent);

            clock.AdvanceMinutes(14 * 60 - clock.MinuteOfDay);
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(clock.DayIndex, agent.CommercialTripDay);
            Assert.AreSame(shop, agent.VisitTarget);
            Assert.AreEqual(AgentPhase.AtHome, agent.PhaseAfterVisit);
            Assert.IsTrue(
                agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator,
                "Condo AtHome daytime window should begin a commercial trip.");

            agent.Phase = AgentPhase.VisitingShop;
            agent.VisitDwellRemaining = 0.01f;
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(1, shop.VisitsToday);

            agent.Phase = AgentPhase.AtHome;
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(1, shop.VisitsToday);
            Assert.AreEqual(clock.DayIndex, agent.CommercialTripDay);
            Assert.IsFalse(
                agent.Phase == AgentPhase.Moving && agent.PhaseAfterMove == AgentPhase.VisitingShop);
        }

        [Test]
        public void Population_excludes_street_visitors()
        {
            var (grid, _, agents, _, clock) = SetupOfficeWithShop(open: true);
            var before = agents.Population;
            Assert.Greater(before, 0);

            Assert.IsTrue(agents.TrySpawnStreetVisitor(grid, clock));
            Assert.AreEqual(
                before,
                agents.Population,
                "StreetVisitor agents must not count toward star Population.");
            Assert.AreEqual(
                1,
                agents.Agents.Count(a => a.Role == AgentRole.StreetVisitor));
        }

        [Test]
        public void AverageStress_excludes_street_visitors()
        {
            var (grid, _, agents, _, clock) = SetupOfficeWithShop(open: true);
            var before = agents.AverageStress;
            Assert.IsTrue(agents.TrySpawnStreetVisitor(grid, clock));

            var street = agents.Agents.First(a => a.Role == AgentRole.StreetVisitor);
            street.Stress = 100f;

            Assert.AreEqual(
                before,
                agents.AverageStress,
                0.001f,
                "StreetVisitor stress must not affect AverageStress.");
        }

        [Test]
        public void Street_visitor_cap_is_eight()
        {
            var (grid, agents, clock) = SetupShopsForStreetTraffic();

            for (var i = 0; i < 20; i++)
                agents.TrySpawnStreetVisitor(grid, clock);

            var streetCount = agents.Agents.Count(a => a.Role == AgentRole.StreetVisitor);
            Assert.AreEqual(AgentSystem.MaxConcurrentStreetVisitors, streetCount);
            Assert.IsFalse(
                agents.TrySpawnStreetVisitor(grid, clock),
                "Spawn must refuse once the concurrent street visitor cap is reached.");
            Assert.AreEqual(
                AgentSystem.MaxConcurrentStreetVisitors,
                agents.Agents.Count(a => a.Role == AgentRole.StreetVisitor));
        }

        [Test]
        public void TryBeginCommercialTrip_releases_slot_when_trip_cannot_start()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithShop(open: true);
            PlaceAgentWorkingAtOffice(agent);

            // Unreachable cell: shop is open from lobby but agent cannot route there.
            agent.Cell = new Vector2Int(5, 5);
            agent.WorldPosition = new Vector2(5.5f, 5.5f);

            Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.AreEqual(-1, agent.CommercialTripDay);
            Assert.IsNull(agent.VisitTarget);
        }

        [Test]
        public void SyncHomes_releases_visitor_slot_when_removing_agent_with_visit_target()
        {
            var (grid, shop, agents, agent, _) = SetupOfficeWithShop(open: true);
            PlaceAgentWorkingAtOffice(agent);

            shop.TryOccupyVisitorSlot();
            agent.VisitTarget = shop;
            agent.CommercialTripDay = 0;
            Assert.AreEqual(1, shop.ConcurrentVisitors);

            var gridWithoutOffice = new TowerGrid();
            Assert.IsTrue(gridWithoutOffice.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(gridWithoutOffice.TryPlace(FastFood(), new Vector2Int(9, 1), out _));

            agents.SyncHomes(gridWithoutOffice);

            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.IsFalse(agents.Agents.Contains(agent));
        }

        [Test]
        public void OnNewDay_resets_concurrent_visitors_for_shops()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(9, 1), out var shop));

            shop.TryOccupyVisitorSlot();
            shop.TryOccupyVisitorSlot();
            Assert.AreEqual(2, shop.ConcurrentVisitors);

            var economy = new EconomySystem(seed: 1);
            var wallet = new FundsWallet(0);
            economy.OnNewDay(grid, new List<Agent>(), wallet);

            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
        }

        [Test]
        public void OnNewDay_pays_shop_earnings_not_visits_times_list_price()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(9, 1), out var shop));

            // Two visits spent 25 + 40; list price is $40 so visits×price would be 80.
            shop.RecordVisit();
            shop.RecordShopSpend(25);
            shop.RecordVisit();
            shop.RecordShopSpend(40);
            Assert.AreEqual(2, shop.VisitsToday);
            Assert.AreEqual(65, shop.ShopEarningsToday);

            var economy = new EconomySystem(seed: 1);
            var wallet = new FundsWallet(0);
            economy.OnNewDay(grid, new List<Agent>(), wallet);

            Assert.AreEqual(65, economy.LastIncome);
            Assert.AreEqual(65, wallet.Balance);
            Assert.AreEqual(65, shop.LifetimeIncome);
            Assert.AreEqual(65, economy.GetLastRoomIncome(shop));
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreNotEqual(80, economy.LastIncome);
        }

        [Test]
        public void SyncHomes_preserves_street_visitors()
        {
            var (grid, agents, clock) = SetupShopsForStreetTraffic();
            Assert.IsTrue(agents.TrySpawnStreetVisitor(grid, clock));
            Assert.AreEqual(1, agents.Agents.Count(a => a.Role == AgentRole.StreetVisitor));

            agents.SyncHomes(grid);

            Assert.AreEqual(
                1,
                agents.Agents.Count(a => a.Role == AgentRole.StreetVisitor),
                "SyncHomes must not remove StreetVisitor agents whose HomeRoom is a shop.");
        }

        static (TowerGrid grid, AgentSystem agents, GameClock clock) SetupShopsForStreetTraffic()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            // Fast Food (4) + Restaurant (6) = 10 slots so the street cap (8) is the binding limit.
            // Price floors at Street band min ($20) so affordability never blocks the spawn-cap test.
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(9, 1), out var fast));
            Assert.IsTrue(grid.TryPlace(Restaurant(), new Vector2Int(10, 1), out var restaurant));
            fast.Type.baseIncome = 20;
            restaurant.Type.baseIncome = 20;
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var clock = new GameClock(1f, 12 * 60);
            return (grid, agents, clock);
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

        static (
            TowerGrid grid,
            RoomInstance shop,
            AgentSystem agents,
            Agent agent,
            GameClock clock) SetupOfficeWithRestaurant()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Restaurant(), new Vector2Int(9, 1), out var shop));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single();
            var clock = new GameClock(1f, 12 * 60);
            return (grid, shop, agents, agent, clock);
        }

        static (
            TowerGrid grid,
            RoomInstance shop,
            AgentSystem agents,
            Agent agent,
            GameClock clock) SetupHotelWithShop()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(9, 1), out var shop));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single();
            var clock = new GameClock(1f, 16 * 60);
            return (grid, shop, agents, agent, clock);
        }

        static (
            TowerGrid grid,
            RoomInstance shop,
            AgentSystem agents,
            Agent agent,
            GameClock clock) SetupCondoWithShop()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(9, 1), out var shop));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single();
            var clock = new GameClock(1f, 12 * 60);
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

        static void PlaceAgentAtHomeInCondo(Agent agent)
        {
            var home = agent.HomeRoom.Origin;
            agent.Cell = home;
            agent.WorldPosition = new Vector2(home.x + 0.5f, home.y + 0.5f);
            agent.Phase = AgentPhase.AtHome;
            agent.HasMovedIn = true;
            agent.Visible = true;
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

        static RoomTypeSO Restaurant()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_restaurant";
            so.category = RoomCategory.Commercial;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.baseIncome = 120;
            so.maxOccupants = 6;
            so.hasActiveHours = true;
            so.activeHoursStart = 11 * 60;
            so.activeHoursEnd = 22 * 60;
            return so;
        }
    }
}
