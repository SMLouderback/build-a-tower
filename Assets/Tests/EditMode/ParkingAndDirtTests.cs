using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ParkingAndDirtTests
    {
        [Test]
        public void DirtBand_ShouldRestore_for_vacated_elevator_basement_cell()
        {
            // Elevator resize clears structure tiles on vacated B1 cells; those must restore dirt.
            var cell = new Vector2Int(5, -1);
            Assert.IsTrue(DirtBand.Contains(cell));
            Assert.IsTrue(DirtBand.ShouldRestore(cell, null));
        }

        [Test]
        public void DirtBand_ShouldRestore_false_when_room_occupies_cell()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _));
            var parking = Parking();
            Assert.IsTrue(grid.TryPlace(parking, new Vector2Int(0, -1), out _));

            Assert.IsFalse(DirtBand.ShouldRestore(new Vector2Int(0, -1), grid));
            Assert.IsTrue(DirtBand.ShouldRestore(new Vector2Int(10, -1), grid));
        }

        [Test]
        public void ParkingStalls_total_and_claim_release()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 12, 0, out _));
            Assert.IsTrue(grid.TryPlace(Valet(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(4, -1), out _));

            Assert.AreEqual(6, ParkingStalls.TotalStalls(grid));
            Assert.IsTrue(ParkingStalls.HasOperationalValet(grid));

            var agents = new List<Agent>();
            var a = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
            Assert.IsTrue(ParkingStalls.TryClaim(a, grid, agents));
            agents.Add(a);
            Assert.AreEqual(5, ParkingStalls.FreeStalls(grid, agents));

            ParkingStalls.Release(a);
            Assert.AreEqual(6, ParkingStalls.FreeStalls(grid, agents));
        }

        [Test]
        public void ParkingStalls_claim_fails_without_valet()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 12, 0, out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(0, -1), out _));

            var a = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
            Assert.IsFalse(ParkingStalls.TryClaim(a, grid, new List<Agent>()));
        }

        [Test]
        public void ParkingRamp_B1_accessible_without_ramp_B2_needs_chain()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Valet(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(4, -1), out _));
            // B2 needs B1 support in the same columns; place beside the ramp columns.
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(10, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(10, -2), out var deep));

            Assert.IsTrue(ParkingStalls.IsParkingFloorAccessible(grid, -1));
            Assert.IsFalse(ParkingStalls.IsParkingFloorAccessible(grid, -2));
            Assert.IsFalse(ParkingStalls.IsParkingAccessible(grid, deep));
            Assert.AreEqual(12, ParkingStalls.TotalStalls(grid)); // only two B1 lots

            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out _));
            // Ramp at x0–2 does not touch parking at x10–15 — still inaccessible.
            Assert.IsTrue(ParkingStalls.IsParkingFloorAccessible(grid, -2));
            Assert.IsFalse(ParkingStalls.IsParkingAccessible(grid, deep));
            Assert.AreEqual(12, ParkingStalls.TotalStalls(grid));
        }

        [Test]
        public void ParkingRamp_adjacent_parking_chain_counts_toward_lobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 30, 0, out _));
            // Support row on B1 for deep lots.
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(9, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(16, -1), out _));

            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out _)); // x0–2, floors -2/-1
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -2), out var nearRamp)); // touches ramp
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(9, -2), out var mid)); // touches nearRamp
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(16, -2), out var gap)); // gap at x15

            Assert.IsTrue(ParkingStalls.IsParkingAccessible(grid, nearRamp));
            Assert.IsTrue(ParkingStalls.IsParkingAccessible(grid, mid));
            Assert.IsFalse(ParkingStalls.IsParkingAccessible(grid, gap));
            // B1: 3×6=18, B2 connected: 2×6=12 → 30
            Assert.AreEqual(30, ParkingStalls.TotalStalls(grid));
        }

        [Test]
        public void ParkingRamp_stack_unlocks_deeper_floor()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            // Support columns for deep parking beside ramp stack (x0–2).
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -2), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -3), out _));

            Assert.AreEqual(6, ParkingStalls.TotalStalls(grid)); // B1 only

            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out _)); // -2..-1, touches x3 parking
            Assert.AreEqual(12, ParkingStalls.TotalStalls(grid)); // B1+B2

            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -3), out _)); // -3..-2, touches B3 parking
            Assert.AreEqual(18, ParkingStalls.TotalStalls(grid)); // B1+B2+B3
        }

        [Test]
        public void ParkingRamp_can_land_on_lobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.CanPlace(Ramp(), new Vector2Int(2, -1)));
            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(2, -1), out var ramp));
            Assert.IsTrue(ramp.Type.isParkingRamp);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(2, 0), out var atLobby));
            Assert.AreSame(ramp, atLobby);
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

        static RoomTypeSO Valet()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.ValetId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO Parking()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.ParkingId;
            so.category = RoomCategory.Parking;
            so.size = new Vector2Int(6, 1);
            so.allowBasement = true;
            so.maxOccupants = 6;
            return so;
        }

        static RoomTypeSO Ramp()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.RampId;
            so.displayName = "Parking Ramp";
            so.isParkingRamp = true;
            so.size = new Vector2Int(3, 2);
            so.allowBasement = true;
            so.allowAboveGround = false;
            so.buildFamily = BuildFamily.Transit;
            so.requiredStars = 4;
            return so;
        }
    }
}
