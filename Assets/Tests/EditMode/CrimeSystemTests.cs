using System;
using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CrimeSystemTests
    {
        [Test]
        public void Shop_load_raises_crime_on_that_floor()
        {
            var crime = new CrimeSystem();
            var shop = new Dictionary<int, float> { [3] = 2f };
            var hotel = new Dictionary<int, float>();
            crime.Tick(10f, shop, hotel, totalStaffedSecurityWorkers: 0,
                patrolFloors: Array.Empty<int>(), criminalFloors: Array.Empty<int>());
            Assert.Greater(crime.GetCrime(3), 0f);
            Assert.AreEqual(0f, crime.GetCrime(2));
        }

        [Test]
        public void Staffed_baseline_decays_all_floors_with_crime()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(1, 50f);
            crime.SetCrime(5, 50f);
            crime.Tick(10f,
                new Dictionary<int, float>(),
                new Dictionary<int, float>(),
                totalStaffedSecurityWorkers: 2,
                Array.Empty<int>(), Array.Empty<int>());
            Assert.Less(crime.GetCrime(1), 50f);
            Assert.Less(crime.GetCrime(5), 50f);
        }

        [Test]
        public void Patrol_decays_local_floor_faster_than_baseline_alone()
        {
            var withPatrol = new CrimeSystem();
            var baselineOnly = new CrimeSystem();
            withPatrol.SetCrime(4, 80f);
            baselineOnly.SetCrime(4, 80f);
            var empty = new Dictionary<int, float>();
            withPatrol.Tick(5f, empty, empty, 1, new[] { 4 }, Array.Empty<int>());
            baselineOnly.Tick(5f, empty, empty, 1, Array.Empty<int>(), Array.Empty<int>());
            Assert.Less(withPatrol.GetCrime(4), baselineOnly.GetCrime(4));
        }

        [Test]
        public void Criminal_raises_floor_crime_and_capture_drops()
        {
            var crime = new CrimeSystem();
            crime.Tick(4f, new Dictionary<int, float>(), new Dictionary<int, float>(), 0,
                Array.Empty<int>(), new[] { 2 });
            Assert.Greater(crime.GetCrime(2), 0f);
            var before = crime.GetCrime(2);
            crime.ApplyCaptureDrop(2);
            Assert.AreEqual(Mathf.Max(0f, before - CrimeSystem.CaptureCrimeDrop), crime.GetCrime(2));
        }

        [Test]
        public void Crime_clamps_to_0_100()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(0, 200f);
            Assert.AreEqual(100f, crime.GetCrime(0));
            crime.SetCrime(0, -5f);
            Assert.AreEqual(0f, crime.GetCrime(0));
        }

        [Test]
        public void DisplayCrime_lags_behind_raw_average_spikes()
        {
            var crime = new CrimeSystem();
            var shop = new Dictionary<int, float> { [1] = 4f };
            var empty = new Dictionary<int, float>();
            crime.Tick(2f, shop, empty, 0, Array.Empty<int>(), Array.Empty<int>());
            Assert.Greater(crime.AverageCrime, 0f);
            Assert.Less(crime.DisplayCrime, crime.AverageCrime);

            for (var i = 0; i < 40; i++)
                crime.Tick(5f, empty, empty, 0, Array.Empty<int>(), Array.Empty<int>());
            Assert.AreEqual(crime.AverageCrime, crime.DisplayCrime, 1.5f);
        }

        [Test]
        public void ShopLoadByFloor_counts_concurrent_visitors_on_shop_floor()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(FastFood(), new Vector2Int(1, 2), out var shop));

            Assert.IsTrue(shop.TryOccupyVisitorSlot());
            Assert.IsTrue(shop.TryOccupyVisitorSlot());

            var loads = CrimeFloorLoads.ShopLoadByFloor(grid);
            Assert.IsTrue(loads.TryGetValue(2, out var load));
            Assert.AreEqual(2f, load);
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
            return so;
        }
    }
}
