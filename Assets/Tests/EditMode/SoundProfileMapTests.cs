using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SoundProfileMapTests
    {
        [Test]
        public void ForRoom_maps_families_and_specials()
        {
            Assert.AreEqual(SoundProfile.Office, SoundProfileMap.ForRoom(Room("office_base", RoomCategory.Office)));
            Assert.AreEqual(SoundProfile.Hotel, SoundProfileMap.ForRoom(Room("hotel_base", RoomCategory.Hotel)));
            Assert.AreEqual(SoundProfile.Condo, SoundProfileMap.ForRoom(Room("condo_base", RoomCategory.Condo)));
            Assert.AreEqual(SoundProfile.Restaurant, SoundProfileMap.ForRoom(Shop("shop_restaurant", BuildSubgroup.Food)));
            Assert.AreEqual(SoundProfile.Retail, SoundProfileMap.ForRoom(Shop("shop_retail", BuildSubgroup.Retail)));
            Assert.AreEqual(SoundProfile.Conference, SoundProfileMap.ForRoom(Id("service_conference")));
            Assert.AreEqual(SoundProfile.Event, SoundProfileMap.ForRoom(Id("service_event_hall")));
            Assert.AreEqual(SoundProfile.Parking, SoundProfileMap.ForRoom(Parking()));
            Assert.AreEqual(SoundProfile.Elevator, SoundProfileMap.ForRoom(Elevator()));
            Assert.AreEqual(SoundProfile.Stairs, SoundProfileMap.ForRoom(Stairs()));
            Assert.AreEqual(SoundProfile.Utility, SoundProfileMap.ForRoom(Room("security_post", RoomCategory.Service)));
            Assert.AreEqual(SoundProfile.Lobby, SoundProfileMap.ForRoom(Lobby()));
            Assert.AreEqual(SoundProfile.Hotel, SoundProfileMap.ForRoom(Leisure("leisure_gym")));
            Assert.AreEqual(SoundProfile.Hotel, SoundProfileMap.ForRoom(Leisure("leisure_spa")));
            Assert.AreEqual(SoundProfile.Hotel, SoundProfileMap.ForRoom(Leisure("leisure_pool")));
            Assert.AreEqual(SoundProfile.Event, SoundProfileMap.ForRoom(Leisure("leisure_bowling")));
            Assert.AreEqual(SoundProfile.Event, SoundProfileMap.ForRoom(Leisure("leisure_theater")));
        }

        static RoomTypeSO Leisure(string id)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            return so;
        }

        static RoomTypeSO Room(string id, RoomCategory cat)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = cat;
            return so;
        }

        static RoomTypeSO Shop(string id, BuildSubgroup sub)
        {
            var so = Room(id, RoomCategory.Commercial);
            so.buildFamily = BuildFamily.Shops;
            so.buildSubgroup = sub;
            return so;
        }

        static RoomTypeSO Id(string id)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            return so;
        }

        static RoomTypeSO Parking()
        {
            var so = Id(ParkingStalls.ParkingId);
            so.category = RoomCategory.Parking;
            return so;
        }

        static RoomTypeSO Elevator()
        {
            var so = Id("elevator_normal");
            so.isElevatorShaft = true;
            return so;
        }

        static RoomTypeSO Stairs()
        {
            var so = Id("stairs");
            so.isStairs = true;
            return so;
        }

        static RoomTypeSO Lobby()
        {
            var so = Id("lobby");
            so.isLobby = true;
            return so;
        }
    }
}
