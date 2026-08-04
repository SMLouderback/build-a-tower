using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HotelLuxuryTests
    {
        [Test]
        public void RoomTypeSO_luxury_defaults()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            Assert.AreEqual(LuxuryBand.None, so.luxuryBand);
            Assert.AreEqual(0f, so.cleanMinutes);
        }

        [Test]
        public void AcceptsGuest_basic_only_base()
        {
            Assert.IsTrue(HotelLuxury.AcceptsGuest(LuxuryBand.Base, WealthBand.Basic));
            Assert.IsFalse(HotelLuxury.AcceptsGuest(LuxuryBand.Mid, WealthBand.Basic));
        }

        [Test]
        public void AcceptsGuest_premium_upper_king_or_suite()
        {
            Assert.IsTrue(HotelLuxury.AcceptsGuest(LuxuryBand.Upper, WealthBand.Premium, "hotel_upper_suite"));
            Assert.IsTrue(HotelLuxury.AcceptsGuest(LuxuryBand.Upper, WealthBand.Premium, "hotel_upper_king"));
            Assert.IsFalse(HotelLuxury.AcceptsGuest(LuxuryBand.Upper, WealthBand.Premium, "hotel_upper_standard"));
        }

        [Test]
        public void LuxuryClimateBias_upper_recession_is_minus_two()
        {
            Assert.AreEqual(-2, HotelLuxury.LuxuryClimateBias(LuxuryBand.Upper, MarketClimate.Recession));
        }

        [Test]
        public void CheckInFillMultiplier_upper_recession_low()
        {
            Assert.AreEqual(0.2f, HotelLuxury.CheckInFillMultiplier(LuxuryBand.Upper, MarketClimate.Recession), 0.0001f);
        }

        [Test]
        public void RollGuestBand_high_crime_never_premium()
        {
            var rng = new System.Random(1);
            for (var i = 0; i < 80; i++)
            {
                var band = HotelLuxury.RollGuestBand(stars: 5, averageCrime: 50f, climateStep: MarketClimate.Boom, rng);
                Assert.AreNotEqual(WealthBand.Premium, band);
            }
        }

        [Test]
        public void DemandChanceFloor_base_recession_mild_overprice_is_0_85()
        {
            Assert.AreEqual(0.85f, HotelLuxury.DemandChanceFloor(LuxuryBand.Base, MarketClimate.Recession, overpriceSteps: 0), 0.0001f);
            Assert.AreEqual(0.85f, HotelLuxury.DemandChanceFloor(LuxuryBand.Base, MarketClimate.Recession, overpriceSteps: 1), 0.0001f);
        }

        [Test]
        public void DemandChanceFloor_zero_when_not_base_recession_or_severe_overprice()
        {
            Assert.AreEqual(0f, HotelLuxury.DemandChanceFloor(LuxuryBand.Base, MarketClimate.Recession, overpriceSteps: 2), 0.0001f);
            Assert.AreEqual(0f, HotelLuxury.DemandChanceFloor(LuxuryBand.Base, MarketClimate.Normal, overpriceSteps: 1), 0.0001f);
            Assert.AreEqual(0f, HotelLuxury.DemandChanceFloor(LuxuryBand.Upper, MarketClimate.Recession, overpriceSteps: 1), 0.0001f);
        }

        [Test]
        public void TryFindHotelRoomForGuest_basic_picks_base_not_upper()
        {
            var grid = TwoHotelTower(out var baseHotel, out var upperHotel);
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsTrue(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Basic, MarketClimate.Normal, rng, out var room, out _));
            Assert.AreSame(baseHotel, room);
            Assert.AreNotSame(upperHotel, room);
        }

        [Test]
        public void TryFindHotelRoomForGuest_upper_rejects_base_only_match()
        {
            var grid = TwoHotelTower(out _, out var upperHotel);
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsTrue(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Upper, MarketClimate.Normal, rng, out var room, out _));
            Assert.AreSame(upperHotel, room);
        }

        [Test]
        public void TryFindHotelRoomForGuest_basic_fails_when_only_upper()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(UpperHotel(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsFalse(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Basic, MarketClimate.Normal, rng, out _, out _));
        }

        [Test]
        public void SyncHomes_stores_wealth_on_hotel_guest()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(BaseHotel(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid, currentStars: 0, averageCrime: 0f);

            Assert.GreaterOrEqual(agents.Agents.Count, 1);
            Assert.AreEqual(AgentRole.HotelGuest, agents.Agents[0].Role);
            Assert.AreEqual(WealthBand.Basic, agents.Agents[0].Wealth);
        }

        static TowerGrid TwoHotelTower(out RoomInstance baseHotel, out RoomInstance upperHotel)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(BaseHotel(), new Vector2Int(0, 1), out baseHotel));
            Assert.IsTrue(grid.TryPlace(UpperHotel(), new Vector2Int(0, 2), out upperHotel));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 2), out _));
            return grid;
        }

        static AgentSystem CreateAgents(TowerGrid grid)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            return new AgentSystem(router);
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

        static RoomTypeSO BaseHotel()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel_base";
            so.category = RoomCategory.Hotel;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO UpperHotel()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel_upper_standard";
            so.category = RoomCategory.Hotel;
            so.luxuryBand = LuxuryBand.Upper;
            so.size = new Vector2Int(5, 1);
            so.maxOccupants = 4;
            so.allowAboveGround = true;
            return so;
        }
    }
}
