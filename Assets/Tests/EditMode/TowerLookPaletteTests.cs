using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TowerLookPaletteTests
    {
        [Test]
        public void ForRoom_office_is_cooler_than_lobby()
        {
            var lobby = ScriptableObject.CreateInstance<RoomTypeSO>();
            lobby.isLobby = true;
            lobby.category = RoomCategory.Structure;

            var office = ScriptableObject.CreateInstance<RoomTypeSO>();
            office.category = RoomCategory.Office;
            office.luxuryBand = LuxuryBand.Base;

            var lobbyColor = TowerLookPalette.ForRoom(lobby);
            var officeColor = TowerLookPalette.ForRoom(office);

            Assert.Less(officeColor.r, lobbyColor.r);
            Assert.Greater(officeColor.b, lobbyColor.b);
        }

        [Test]
        public void ForRoom_hotel_is_purple_ish()
        {
            var hotel = ScriptableObject.CreateInstance<RoomTypeSO>();
            hotel.category = RoomCategory.Hotel;
            hotel.luxuryBand = LuxuryBand.Mid;

            var c = TowerLookPalette.ForRoom(hotel);
            Assert.Greater(c.b, c.g);
            Assert.Greater(c.r, 0.4f);
        }

        [Test]
        public void ForRoom_parking_is_dark()
        {
            var parking = ScriptableObject.CreateInstance<RoomTypeSO>();
            parking.id = ParkingStalls.ParkingId;
            parking.category = RoomCategory.Parking;

            var c = TowerLookPalette.ForRoom(parking);
            Assert.Less(c.grayscale, 0.4f);
        }
    }
}
