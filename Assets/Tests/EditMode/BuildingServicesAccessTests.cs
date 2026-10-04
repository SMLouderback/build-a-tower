using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildingServicesAccessTests
    {
        [Test]
        public void Recycling_on_B1_places_without_ramp()
        {
            var grid = NewGridWithLobby();
            var recycling = Recycling();
            Assert.IsTrue(ParkingStalls.WouldBeVehicleAccessible(grid, recycling, new Vector2Int(0, -1)));
            Assert.IsTrue(grid.CanPlace(recycling, new Vector2Int(0, -1)));
        }

        [Test]
        public void Deep_basement_recycling_needs_ramp_chain()
        {
            var grid = NewGridWithLobby();
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(10, -1), out _));
            var recycling = Recycling();
            Assert.IsFalse(grid.CanPlace(recycling, new Vector2Int(10, -2)));

            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out _));
            Assert.IsTrue(grid.CanPlace(Recycling(), new Vector2Int(3, -2)));
            Assert.IsTrue(grid.TryPlace(Recycling(), new Vector2Int(3, -2), out var nearRamp));
            Assert.IsTrue(ParkingStalls.IsVehicleAccessible(grid, nearRamp));

            Assert.IsFalse(grid.CanPlace(recycling, new Vector2Int(10, -2)));
        }

        [Test]
        public void Loading_dock_bridges_through_accessible_parking()
        {
            var grid = NewGridWithLobby();
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(9, -1), out _));
            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(3, -2), out var bridgeLot));
            Assert.IsTrue(ParkingStalls.IsParkingAccessible(grid, bridgeLot));

            var dock = LoadingDock();
            Assert.IsTrue(ParkingStalls.WouldBeVehicleAccessible(grid, dock, new Vector2Int(9, -2)));
            Assert.IsTrue(grid.CanPlace(dock, new Vector2Int(9, -2)));
        }

        [Test]
        public void Mail_ignores_vehicle_access_gate()
        {
            var grid = NewGridWithLobby();
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(10, -1), out _));
            var mail = Mail();
            Assert.IsFalse(ParkingStalls.RequiresVehicleAccess(mail));
            Assert.IsTrue(grid.CanPlace(mail, new Vector2Int(10, -2)));
        }

        static TowerGrid NewGridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            return grid;
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
            so.isParkingRamp = true;
            so.size = new Vector2Int(3, 1);
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO Recycling()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.RecyclingId;
            so.size = new Vector2Int(6, 1);
            so.allowBasement = true;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO LoadingDock()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.LoadingDockId;
            so.size = new Vector2Int(8, 1);
            so.allowBasement = true;
            so.allowAboveGround = false;
            return so;
        }

        static RoomTypeSO Mail()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.MailId;
            so.size = new Vector2Int(4, 1);
            so.allowBasement = true;
            so.allowAboveGround = true;
            return so;
        }
    }
}
