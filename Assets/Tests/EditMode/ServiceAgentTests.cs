using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ServiceAgentTests
    {
        [Test]
        public void SyncHomes_spawns_maids_and_handymen_from_StaffedWorkers()
        {
            var grid = ServiceTower(out var hk, out var maint, out _, out _);
            hk.SetStaffedWorkers(2);
            maint.SetStaffedWorkers(1);
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid);

            Assert.AreEqual(2, agents.Agents.Count(a => a.Role == AgentRole.Maid));
            Assert.AreEqual(1, agents.Agents.Count(a => a.Role == AgentRole.Handyman));
            Assert.AreEqual(0, agents.Population, "Service staff must not count toward population.");
        }

        [Test]
        public void SyncHomes_despawns_when_StaffedWorkers_drops()
        {
            var grid = ServiceTower(out var hk, out _, out _, out _);
            hk.SetStaffedWorkers(2);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            Assert.AreEqual(2, agents.Agents.Count(a => a.Role == AgentRole.Maid));

            hk.SetStaffedWorkers(0);
            agents.SyncHomes(grid);

            Assert.AreEqual(0, agents.Agents.Count(a => a.Role == AgentRole.Maid));
        }

        [Test]
        public void Maid_idles_at_housekeeping_when_no_dirty_hotels()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            Assert.IsFalse(hotel.Dirty);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            var home = hk.Origin;
            var clock = new GameClock(1f, 12 * 60);

            PlaceAtRoom(maid, hk);
            maid.Phase = AgentPhase.AtHome;

            for (var i = 0; i < 10; i++)
                agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.AtHome, maid.Phase);
            Assert.IsNull(maid.ServiceTarget);
            Assert.AreEqual(home, maid.Cell);
            Assert.IsFalse(hotel.Dirty);
        }

        [Test]
        public void Maid_returns_home_after_clean_and_stays_when_no_more_dirty()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            hotel.MarkDirty();
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            var clock = new GameClock(1f, 12 * 60);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            PlaceAtRoom(maid, hotel);
            maid.Phase = AgentPhase.Working;
            // Keep ServiceCleanProgress from TryAssign; only ensure dwell finishes this tick.
            maid.ServiceWorkRemaining = Mathf.Max(0.1f, maid.ServiceCleanProgress);

            agents.Tick(maid.ServiceWorkRemaining, clock, grid);
            Assert.IsFalse(hotel.Dirty);
            Assert.IsNull(maid.ServiceTarget);

            // Allow return trip / idle snap.
            for (var i = 0; i < 120; i++)
                agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.AtHome, maid.Phase);
            Assert.AreEqual(hk.Origin, maid.Cell);
            Assert.IsNull(maid.ServiceTarget);
        }

        [Test]
        public void Maid_does_not_pursue_stale_goal_after_failed_job_path()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            Assert.IsFalse(hotel.Dirty);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            var clock = new GameClock(1f, 12 * 60);
            PlaceAtRoom(maid, hk);

            // Simulate a failed assign that used to leave GoalCell pointing at the hotel.
            maid.ServiceTarget = null;
            maid.GoalCell = hotel.Origin;
            maid.PhaseAfterMove = AgentPhase.Working;
            maid.Phase = AgentPhase.Moving;
            maid.Path = new System.Collections.Generic.List<Vector2Int>();
            maid.PathIndex = 0;

            agents.Tick(1f, clock, grid);

            Assert.IsNull(maid.ServiceTarget);
            Assert.IsTrue(
                maid.Phase == AgentPhase.AtHome ||
                (maid.PhaseAfterMove == AgentPhase.AtHome && maid.GoalCell == hk.Origin),
                "Without a valid claim the maid should idle or path home, not chase a stale hotel goal.");
        }

        [Test]
        public void Handyman_idles_at_maintenance_when_all_rooms_at_full_condition()
        {
            var grid = ServiceTower(out _, out var maint, out var hotel, out var office);
            maint.SetStaffedWorkers(1);
            hotel.Condition = 100;
            office.Condition = 100;
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var handyman = agents.Agents.Single(a => a.Role == AgentRole.Handyman);
            var clock = new GameClock(1f, 12 * 60);
            PlaceAtRoom(handyman, maint);
            handyman.Phase = AgentPhase.AtHome;

            for (var i = 0; i < 10; i++)
                agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.AtHome, handyman.Phase);
            Assert.IsNull(handyman.ServiceTarget);
            Assert.AreEqual(maint.Origin, handyman.Cell);
        }

        [Test]
        public void Maid_clears_dirty_after_clean_minutes()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            hotel.MarkDirty();
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            var clock = new GameClock(1f, 12 * 60);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(hotel, maid.ServiceTarget);

            PlaceAtRoom(maid, hotel);
            maid.Phase = AgentPhase.Working;
            maid.ServiceWorkRemaining = Mathf.Max(0.1f, maid.ServiceCleanProgress);

            agents.Tick(maid.ServiceWorkRemaining, clock, grid);

            Assert.IsFalse(hotel.Dirty);
            Assert.IsNull(maid.ServiceTarget);
        }

        [Test]
        public void Maid_ForceCompleteServiceWork_clears_dirty()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            hotel.MarkDirty();
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            PlaceAtRoom(maid, hotel);
            maid.Phase = AgentPhase.Working;

            Assert.IsTrue(agents.ForceCompleteServiceWork(maid));
            Assert.IsFalse(hotel.Dirty);
            Assert.IsNull(maid.ServiceTarget);
        }

        [Test]
        public void Handyman_repairs_plus_10_after_60_minutes()
        {
            var grid = ServiceTower(out _, out var maint, out var hotel, out var office);
            maint.SetStaffedWorkers(1);
            hotel.Condition = 100;
            office.Condition = 55;
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var handyman = agents.Agents.Single(a => a.Role == AgentRole.Handyman);
            var clock = new GameClock(1f, 12 * 60);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(office, handyman.ServiceTarget);

            PlaceAtRoom(handyman, office);
            handyman.Phase = AgentPhase.Working;
            handyman.ServiceWorkRemaining = RoomConditionRules.RepairMinutesPerChunk;

            agents.Tick(RoomConditionRules.RepairMinutesPerChunk, clock, grid);

            Assert.AreEqual(65, office.Condition);
            Assert.IsNull(handyman.ServiceTarget);
        }

        [Test]
        public void Handyman_ignores_broken_and_zero_condition()
        {
            var grid = ServiceTower(out _, out var maint, out var hotel, out var office);
            maint.SetStaffedWorkers(1);
            hotel.Condition = 0;
            office.Condition = 0;
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var handyman = agents.Agents.Single(a => a.Role == AgentRole.Handyman);

            Assert.IsFalse(agents.TryAssignServiceJobs(grid));
            Assert.IsNull(handyman.ServiceTarget);
            Assert.AreEqual(AgentPhase.AtHome, handyman.Phase);
        }

        [Test]
        public void Handyman_ForceComplete_does_not_revive_room_that_broke_mid_job()
        {
            var grid = ServiceTower(out _, out var maint, out _, out var office);
            maint.SetStaffedWorkers(1);
            office.Condition = 1;
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var handyman = agents.Agents.Single(a => a.Role == AgentRole.Handyman);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(office, handyman.ServiceTarget);
            PlaceAtRoom(handyman, office);
            handyman.Phase = AgentPhase.Working;

            office.Condition = 0;
            Assert.IsTrue(office.IsBroken);

            Assert.IsTrue(agents.ForceCompleteServiceWork(handyman));
            Assert.IsTrue(office.IsBroken);
            Assert.AreEqual(0, office.Condition);
            Assert.IsNull(handyman.ServiceTarget);
        }

        [Test]
        public void Maid_claims_oldest_dirty_hotel_by_instance_id()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 0), out _));
            Assert.IsTrue(grid.TryPlace(Housekeeping(), new Vector2Int(0, 1), out var hk));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(3, 1), out var first));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(12, 1), out var second));
            hk.SetStaffedWorkers(1);
            first.MarkDirty();
            second.MarkDirty();
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(first, maid.ServiceTarget);
            Assert.Less(first.InstanceId, second.InstanceId);
        }

        [Test]
        public void Maid_releases_claim_when_stalled_with_empty_path()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            hotel.MarkDirty();
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            var clock = new GameClock(1f, 12 * 60);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(hotel, maid.ServiceTarget);

            // StallInPlace shape: Moving, empty path, claim held (the soft-lock that needed fire/rehire).
            PlaceAtRoom(maid, hk);
            maid.Phase = AgentPhase.Moving;
            maid.Path = new System.Collections.Generic.List<Vector2Int>();
            maid.PathIndex = 0;
            maid.GoalCell = hotel.Origin;
            maid.PhaseAfterMove = AgentPhase.Working;

            agents.Tick(1f, clock, grid);

            Assert.IsNull(maid.ServiceTarget, "Stalled maid must drop Dirty claim so another can clean.");
            Assert.IsTrue(hotel.Dirty);
        }

        [Test]
        public void Two_maids_can_claim_same_event_hall_with_clean_work_pool()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 28, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(14, 0), out _));
            Assert.IsTrue(grid.TryPlace(Housekeeping(), new Vector2Int(0, 1), out var hk));
            Assert.IsTrue(grid.TryPlace(EventHall(), new Vector2Int(4, 1), out var hall));
            hk.SetStaffedWorkers(2);
            hall.QueueCleaning(2, ConferenceSystem.EventPostCleanMinutes);

            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            Assert.AreEqual(2, agents.Agents.Count(a => a.Role == AgentRole.Maid));
            Assert.IsTrue(agents.TryAssignServiceJobs(grid));

            var maids = agents.Agents.Where(a => a.Role == AgentRole.Maid).ToList();
            Assert.AreSame(hall, maids[0].ServiceTarget);
            Assert.AreSame(hall, maids[1].ServiceTarget);
            Assert.AreEqual(AgentSystem.MaidCleanShiftMinutes, maids[0].ServiceCleanProgress);
            Assert.AreEqual(AgentSystem.MaidCleanShiftMinutes, maids[1].ServiceCleanProgress);
        }

        [Test]
        public void Maid_prefers_dirty_hotel_over_event_hall()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 28, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(14, 0), out _));
            Assert.IsTrue(grid.TryPlace(Housekeeping(), new Vector2Int(0, 1), out var hk));
            Assert.IsTrue(grid.TryPlace(EventHall(), new Vector2Int(4, 1), out var hall));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(18, 1), out var hotel));
            hk.SetStaffedWorkers(1);
            hall.QueueCleaning(2, ConferenceSystem.EventPostCleanMinutes);
            hotel.QueueCleanWork(RoomConditionRules.CleanBasicMinutes);

            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(hotel, maid.ServiceTarget);
        }

        [Test]
        public void Handyman_takes_queued_event_hall_repair_job()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 28, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(14, 0), out _));
            Assert.IsTrue(grid.TryPlace(Maintenance(), new Vector2Int(0, 1), out var maint));
            Assert.IsTrue(grid.TryPlace(EventHall(), new Vector2Int(4, 1), out var hall));
            maint.SetStaffedWorkers(1);
            hall.QueueRepairs(1, ConferenceSystem.EventPostRepairMinutes);

            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var handyman = agents.Agents.Single(a => a.Role == AgentRole.Handyman);
            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(hall, handyman.ServiceTarget);
            Assert.AreEqual(ConferenceSystem.EventPostRepairMinutes, handyman.ServiceWorkRemaining);
        }

        [Test]
        public void Maid_failed_job_path_snaps_idle_and_can_claim_other_dirty_hotel()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(20, 0), out _)); // covers floors 0–1
            Assert.IsTrue(grid.TryPlace(Housekeeping(), new Vector2Int(0, 1), out var hk));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(6, 1), out var reachable));
            // Support pad so floor-3 hotel can be built, but no stairs/elevator reach it.
            Assert.IsTrue(grid.TryPlace(Scaffold(), new Vector2Int(6, 2), out _));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(6, 3), out var unreachable));
            hk.SetStaffedWorkers(1);
            reachable.QueueCleanWork(RoomConditionRules.CleanBasicMinutes);
            unreachable.QueueCleanWork(RoomConditionRules.CleanBasicMinutes * 4f);

            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            PlaceAtRoom(maid, hk);
            maid.Phase = AgentPhase.AtHome;
            var clock = new GameClock(1f, 12 * 60);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(reachable, maid.ServiceTarget,
                "After unreachable path fails, maid must claim the reachable dirty hotel.");
            Assert.IsFalse(IsMovementStuck(maid), "Failed path must not leave maid soft-locked Moving.");
            Assert.AreNotEqual(AgentPhase.Moving, maid.Phase);
        }

        static RoomTypeSO Scaffold()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "scaffold";
            so.isScaffolding = true;
            so.allowAboveGround = true;
            so.size = new Vector2Int(9, 1);
            return so;
        }

        [Test]
        public void Maid_releases_claim_after_long_travel_without_arriving()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            hotel.QueueCleanWork(RoomConditionRules.CleanBasicMinutes);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            var clock = new GameClock(1f, 12 * 60);

            Assert.IsTrue(agents.TryAssignServiceJobs(grid));
            Assert.AreSame(hotel, maid.ServiceTarget);

            // Simulate endless transit (stairs queue / congestion) — Moving with a non-empty path.
            PlaceAtRoom(maid, hk);
            maid.Phase = AgentPhase.Moving;
            maid.Path = new System.Collections.Generic.List<Vector2Int>
            {
                hk.Origin,
                new Vector2Int(hk.Origin.x + 1, hk.Origin.y),
                hotel.Origin
            };
            maid.PathIndex = 0;
            maid.GoalCell = hotel.Origin;
            maid.PhaseAfterMove = AgentPhase.Working;
            // Already traveled past the abandon threshold (e.g. overnight stair queue).
            maid.ServiceTravelMinutes = AgentSystem.ServiceTravelAbandonMinutes;

            agents.Tick(1f, clock, grid);

            Assert.IsNull(maid.ServiceTarget,
                "Claim must release after long travel so another maid can clean.");
            Assert.IsTrue(hotel.Dirty);
        }

        [Test]
        public void Idle_maid_claims_dirty_hotel_still_dirty_after_day_boundary()
        {
            var grid = ServiceTower(out var hk, out _, out var hotel, out _);
            hk.SetStaffedWorkers(1);
            hotel.QueueCleanWork(RoomConditionRules.CleanBasicMinutes);
            var agents = CreateAgents(grid);
            agents.SyncHomes(grid);
            var maid = agents.Agents.Single(a => a.Role == AgentRole.Maid);
            PlaceAtRoom(maid, hk);
            maid.Phase = AgentPhase.AtHome;

            // Simulate soft-lock that used to strand maids overnight: Moving, no claim, stale Working goal.
            maid.ServiceTarget = null;
            maid.GoalCell = hotel.Origin;
            maid.PhaseAfterMove = AgentPhase.Working;
            maid.Phase = AgentPhase.Moving;
            maid.Path = new System.Collections.Generic.List<Vector2Int>();
            maid.PathIndex = 0;

            var clock = new GameClock(1f, 23 * 60);
            agents.Tick(1f, clock, grid);
            // Cross midnight.
            clock.AdvanceMinutes(GameClock.MinutesPerDay - clock.MinuteOfDay + 8 * 60);
            for (var i = 0; i < 5; i++)
                agents.Tick(1f, clock, grid);

            Assert.IsTrue(hotel.Dirty);
            Assert.IsTrue(
                maid.ServiceTarget == hotel ||
                maid.Phase == AgentPhase.AtHome ||
                maid.PhaseAfterMove == AgentPhase.AtHome ||
                maid.PhaseAfterMove == AgentPhase.Working,
                "Maid must recover from stale Moving after day rollover and pursue cleaning.");
            // Stronger: eventually claims or is actively cleaning/traveling to the dirty hotel.
            var claimedOrCleaning = false;
            for (var i = 0; i < 30; i++)
            {
                agents.Tick(1f, clock, grid);
                if (maid.ServiceTarget == hotel)
                {
                    claimedOrCleaning = true;
                    break;
                }
            }

            Assert.IsTrue(claimedOrCleaning, "Idle capacity after midnight must re-claim dirty hotels.");
        }

        static bool IsMovementStuck(Agent agent) => AgentSystem.IsMovementStuck(agent);

        static TowerGrid ServiceTower(
            out RoomInstance hk,
            out RoomInstance maint,
            out RoomInstance hotel,
            out RoomInstance office)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(12, 0), out _));
            Assert.IsTrue(grid.TryPlace(Housekeeping(), new Vector2Int(0, 1), out hk));
            Assert.IsTrue(grid.TryPlace(Maintenance(), new Vector2Int(3, 1), out maint));
            Assert.IsTrue(grid.TryPlace(Hotel(), new Vector2Int(6, 1), out hotel));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(15, 1), out office));
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

        static RoomTypeSO Housekeeping()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_housekeeping";
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Maintenance()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_maintenance";
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
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

        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO EventHall()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ConferenceSystem.EventHallId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(12, 2);
            so.eventCapacity = 120;
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }
    }
}
