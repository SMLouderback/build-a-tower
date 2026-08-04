using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ParkingAndDirtTests
    {
        [Test]
        public void DirtBand_ShouldRestore_only_for_empty_basement_in_band()
        {
            Assert.IsTrue(DirtBand.ShouldRestore(new Vector2Int(0, -1), null));
            Assert.IsFalse(DirtBand.ShouldRestore(new Vector2Int(0, 0), null));
            Assert.IsFalse(DirtBand.ShouldRestore(new Vector2Int(0, 1), null));
            Assert.IsFalse(DirtBand.ShouldRestore(new Vector2Int(DirtBand.MinX - 1, -1), null));
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
    }
}
