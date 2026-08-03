using System;
using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CondoCommuteTests
    {
        [Test]
        public void SyncHomes_reserves_desks_for_moved_in_condo_residents()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Office(5), new Vector2Int(0, 1), out var officeA));
            Assert.IsTrue(grid.TryPlace(Office(5), new Vector2Int(0, 2), out var officeB));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 3), out _));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 4), out _));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 5), out _));
            Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 6), out _));
            for (var floor = 0; floor <= 5; floor++)
                Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, floor), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);

            agents.SyncHomes(grid, currentStars: 5);

            var officeWorkersBefore = agents.Agents.Count(a => a.Role == AgentRole.OfficeWorker);
            Assert.AreEqual(10, officeWorkersBefore, "Offices should fill fully before condo move-in.");

            var condos = agents.Agents.Where(a => a.Role == AgentRole.CondoResident).ToList();
            Assert.AreEqual(4, condos.Count, "Expected four condo buyers with demand guaranteed.");

            var clock = new GameClock(1f, 12 * 60);
            for (var i = 0; i < 200 && condos.Any(c => !c.HasMovedIn); i++)
                agents.Tick(1f, clock, grid);

            foreach (var condo in condos)
            {
                if (!condo.HasMovedIn)
                {
                    condo.HasMovedIn = true;
                    condo.Phase = AgentPhase.AtHome;
                }
            }

            Assert.AreEqual(4, agents.Agents.Count(a => a.Role == AgentRole.CondoResident && a.HasMovedIn));

            agents.SyncHomes(grid, currentStars: 5);

            var officeWorkers = agents.Agents.Where(a => a.Role == AgentRole.OfficeWorker).ToList();
            Assert.AreEqual(8, officeWorkers.Count, "10 desks − 2 reserved → 8 OfficeWorkers.");

            var emptySeats = 0;
            foreach (var office in new[] { officeA, officeB })
            {
                var homeCount = officeWorkers.Count(a => ReferenceEquals(a.HomeRoom, office));
                emptySeats += office.Type.maxOccupants - homeCount;
            }

            Assert.AreEqual(2, emptySeats, "Reserved empty seats across offices should total inTowerWanted.");
        }

        [Test]
        public void AssignCondoJobs_in_tower_within_wanted_and_office_capacity()
        {
            var (grid, agents, offices) = BuildMovedInReservedTower(condoCount: 4);
            var inTowerWanted = CondoEmployment.InTowerWanted(10, 4);
            Assert.AreEqual(2, inTowerWanted);

            var clock = new GameClock(1f, 0);
            agents.Tick(1f, clock, grid);

            var movedIn = agents.Agents
                .Where(a => a.Role == AgentRole.CondoResident && a.HasMovedIn)
                .OrderBy(a => a.Id)
                .ToList();
            Assert.AreEqual(4, movedIn.Count);

            var inTower = movedIn.Where(a => a.JobKind == CondoJobKind.InTower).ToList();
            Assert.AreEqual(inTowerWanted, inTower.Count);
            Assert.LessOrEqual(inTower.Count, inTowerWanted);

            var claimsByOffice = new Dictionary<RoomInstance, int>();
            foreach (var condo in inTower)
            {
                Assert.IsNotNull(condo.WorkplaceRoom);
                Assert.IsTrue(offices.Any(o => ReferenceEquals(o, condo.WorkplaceRoom)),
                    "In-tower workplace must be an office.");
                Assert.AreEqual(0, condo.CommuteOneWayMinutes);
                Assert.AreEqual(8 * 60, condo.WorkMinutes);
                Assert.AreEqual(CondoOutsidePhase.None, condo.OutsideWorkPhase);
                Assert.AreEqual(0f, condo.OutsideDwellRemaining);
                Assert.GreaterOrEqual(condo.LeaveHomeMinute, 6 * 60);
                Assert.Less(condo.LeaveHomeMinute, 9 * 60);

                claimsByOffice.TryGetValue(condo.WorkplaceRoom, out var n);
                claimsByOffice[condo.WorkplaceRoom] = n + 1;
            }

            foreach (var office in offices)
            {
                var homeWorkers = agents.Agents.Count(a =>
                    a.Role == AgentRole.OfficeWorker && ReferenceEquals(a.HomeRoom, office));
                claimsByOffice.TryGetValue(office, out var condoClaims);
                Assert.LessOrEqual(homeWorkers + condoClaims, office.Type.maxOccupants,
                    "Condo claims must not exceed spare home seats.");
            }
        }

        [Test]
        public void AssignCondoJobs_outside_commute_minutes_in_range()
        {
            var (grid, agents, _) = BuildMovedInReservedTower(condoCount: 4);
            var clock = new GameClock(1f, 0);
            agents.Tick(1f, clock, grid);

            var outside = agents.Agents
                .Where(a => a.Role == AgentRole.CondoResident && a.JobKind == CondoJobKind.Outside)
                .ToList();
            Assert.Greater(outside.Count, 0);
            foreach (var condo in outside)
            {
                Assert.IsNull(condo.WorkplaceRoom);
                Assert.GreaterOrEqual(condo.CommuteOneWayMinutes, CondoEmployment.CommuteMinMinutes);
                Assert.LessOrEqual(condo.CommuteOneWayMinutes, CondoEmployment.CommuteMaxMinutes);
                Assert.AreEqual(8 * 60, condo.WorkMinutes);
                Assert.AreEqual(CondoOutsidePhase.None, condo.OutsideWorkPhase);
            }
        }

        [Test]
        public void AssignCondoJobs_outside_at_least_half_of_residents()
        {
            var (grid, agents, _) = BuildMovedInReservedTower(condoCount: 4);
            var clock = new GameClock(1f, 0);
            agents.Tick(1f, clock, grid);

            var residents = agents.Agents.Count(a => a.Role == AgentRole.CondoResident && a.HasMovedIn);
            var outside = agents.Agents.Count(a =>
                a.Role == AgentRole.CondoResident && a.JobKind == CondoJobKind.Outside);
            var minOutside = (int)Math.Ceiling(residents / 2.0);
            Assert.GreaterOrEqual(outside, minOutside);
        }

        [Test]
        public void AssignCondoJobs_reruns_after_SyncHomes_invalidates_day_mark()
        {
            var (grid, agents, _) = BuildMovedInReservedTower(condoCount: 4);
            var clock = new GameClock(1f, 0);
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(2, agents.Agents.Count(a => a.JobKind == CondoJobKind.InTower));

            // Force all Outside (simulate assign before desks reserved), then SyncHomes must allow re-assign.
            foreach (var a in agents.Agents.Where(a => a.Role == AgentRole.CondoResident && a.HasMovedIn))
            {
                a.JobKind = CondoJobKind.Outside;
                a.WorkplaceRoom = null;
                a.CommuteOneWayMinutes = 30;
            }

            agents.SyncHomes(grid, currentStars: 5);
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(2, agents.Agents.Count(a => a.JobKind == CondoJobKind.InTower));
        }

        [Test]
        public void UpdateCondo_outside_round_trip_returns_home_after_short_workday()
        {
            var (grid, agents, _) = BuildMovedInReservedTower(condoCount: 4);
            var clock = new GameClock(1f, 0);
            agents.Tick(1f, clock, grid);

            var condo = agents.Agents.First(a =>
                a.Role == AgentRole.CondoResident && a.JobKind == CondoJobKind.Outside);
            condo.Cell = new Vector2Int(condo.HomeRoom.Origin.x, condo.HomeRoom.Origin.y);
            condo.WorldPosition = new Vector2(condo.Cell.x + 0.5f, condo.Cell.y + 0.5f);
            condo.LeaveHomeMinute = 6 * 60;
            condo.CommuteOneWayMinutes = 1;
            condo.WorkMinutes = 1;
            condo.CheckedOutToday = false;
            condo.OutsideWorkPhase = CondoOutsidePhase.None;
            condo.OutsideDwellRemaining = 0f;

            clock.AdvanceMinutes(6 * 60);

            var reachedOutside = false;
            for (var i = 0; i < 800; i++)
            {
                agents.Tick(1f, clock, grid);
                if (condo.Phase == AgentPhase.Outside)
                    reachedOutside = true;
                if (reachedOutside &&
                    condo.Phase == AgentPhase.AtHome &&
                    condo.OutsideWorkPhase == CondoOutsidePhase.None)
                    break;
            }

            Assert.IsTrue(reachedOutside, "Outside condo should leave the tower for work.");
            Assert.AreEqual(AgentPhase.AtHome, condo.Phase);
            Assert.AreEqual(CondoOutsidePhase.None, condo.OutsideWorkPhase);
            Assert.IsTrue(condo.CheckedOutToday, "Workday start flag should remain until midnight reset.");
        }

        [Test]
        public void UpdateCondo_in_tower_begins_trip_toward_workplace()
        {
            var (grid, agents, _) = BuildMovedInReservedTower(condoCount: 4);
            var clock = new GameClock(1f, 0);
            agents.Tick(1f, clock, grid);

            var condo = agents.Agents.First(a =>
                a.Role == AgentRole.CondoResident && a.JobKind == CondoJobKind.InTower);
            Assert.IsNotNull(condo.WorkplaceRoom);
            condo.Cell = new Vector2Int(condo.HomeRoom.Origin.x, condo.HomeRoom.Origin.y);
            condo.WorldPosition = new Vector2(condo.Cell.x + 0.5f, condo.Cell.y + 0.5f);
            condo.LeaveHomeMinute = 6 * 60;
            condo.CheckedOutToday = false;
            condo.WorkedMinutes = 0;

            clock.AdvanceMinutes(6 * 60);
            agents.Tick(1f, clock, grid);

            var workplaceCell = new Vector2Int(
                condo.WorkplaceRoom.Origin.x,
                condo.WorkplaceRoom.Origin.y);
            var headedToWork =
                condo.Phase == AgentPhase.Working ||
                (condo.Phase == AgentPhase.Moving &&
                 condo.GoalCell.HasValue &&
                 condo.GoalCell.Value == workplaceCell) ||
                (condo.Phase is AgentPhase.WaitingAtElevator or AgentPhase.Riding &&
                 condo.PhaseAfterMove == AgentPhase.Working);

            Assert.IsTrue(
                headedToWork,
                $"Expected trip toward workplace; phase={condo.Phase}, goal={condo.GoalCell}, after={condo.PhaseAfterMove}");
            Assert.IsTrue(condo.CheckedOutToday);
        }

        static (TowerGrid grid, AgentSystem agents, RoomInstance[] offices) BuildMovedInReservedTower(
            int condoCount)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Office(5), new Vector2Int(0, 1), out var officeA));
            Assert.IsTrue(grid.TryPlace(Office(5), new Vector2Int(0, 2), out var officeB));
            for (var i = 0; i < condoCount; i++)
                Assert.IsTrue(grid.TryPlace(Condo(), new Vector2Int(0, 3 + i), out _));
            for (var floor = 0; floor <= 2 + condoCount; floor++)
                Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, floor), out _));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router);

            agents.SyncHomes(grid, currentStars: 5);

            var condos = agents.Agents.Where(a => a.Role == AgentRole.CondoResident).ToList();
            Assert.AreEqual(condoCount, condos.Count);
            foreach (var condo in condos)
            {
                condo.HasMovedIn = true;
                condo.Phase = AgentPhase.AtHome;
                condo.Visible = true;
            }

            agents.SyncHomes(grid, currentStars: 5);
            return (grid, agents, new[] { officeA, officeB });
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

        static RoomTypeSO Office(int maxOccupants)
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "office";
            room.category = RoomCategory.Office;
            room.size = new Vector2Int(9, 1);
            room.maxOccupants = maxOccupants;
            room.allowAboveGround = true;
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
