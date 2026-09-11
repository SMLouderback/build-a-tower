using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ParkingRampPlacementTests
    {
        [Test]
        public void B1_ramp_places_as_lobby_attach()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(2, -1), out var ramp));
            Assert.AreEqual(new Vector2Int(3, 1), ramp.Size);
            Assert.AreEqual(new Vector2Int(2, -1), ramp.Origin);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(2, -1), out var atB1));
            Assert.AreSame(ramp, atB1);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(2, 0), out var atLobby));
            Assert.IsTrue(atLobby.Type.isLobby);
        }

        [Test]
        public void B2_ramp_requires_exact_X_ramp_on_B1()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _));
            // Structural support on B1 without a ramp — ExactRampAbove must still fail.
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(6, -1), out _));

            Assert.IsFalse(grid.CanPlace(Ramp(), new Vector2Int(0, -2)));
            Assert.IsFalse(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out _));

            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.CanPlace(Ramp(), new Vector2Int(0, -2)));
            Assert.IsTrue(grid.TryPlace(Ramp(), new Vector2Int(0, -2), out var b2));
            Assert.AreEqual(new Vector2Int(0, -2), b2.Origin);

            Assert.IsFalse(grid.CanPlace(Ramp(), new Vector2Int(1, -2)));
            Assert.IsFalse(grid.TryPlace(Ramp(), new Vector2Int(1, -2), out _));
        }

        [Test]
        public void Ramp_size_is_3x1()
        {
            Assert.AreEqual(new Vector2Int(3, 1), LoadParkingRampType().size);
        }

        static RoomTypeSO LoadParkingRampType()
        {
            var so = Resources.Load<RoomTypeSO>("Rooms/ParkingRamp");
            Assert.IsNotNull(so, "Expected Resources/Rooms/ParkingRamp");
            return so;
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
            so.displayName = "Parking Ramp";
            so.isParkingRamp = true;
            so.size = new Vector2Int(3, 1);
            so.allowBasement = true;
            so.allowAboveGround = false;
            so.buildFamily = BuildFamily.Transit;
            so.requiredStars = 4;
            return so;
        }
    }
}
