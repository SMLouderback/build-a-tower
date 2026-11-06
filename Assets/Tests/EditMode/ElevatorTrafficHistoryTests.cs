using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorTrafficHistoryTests
    {
        [Test]
        public void RecordBoarding_accumulates_today_then_archives_to_yesterday()
        {
            var shaft = NewShaft();
            shaft.RecordBoarding(10f);
            shaft.RecordBoarding(20f);
            Assert.AreEqual(2, shaft.PassengersToday);
            Assert.AreEqual(15f, shaft.AvgWaitToday, 0.001f);

            shaft.ArchiveDay();
            Assert.AreEqual(0, shaft.PassengersToday);
            Assert.AreEqual(2, shaft.PassengersYesterday);
            Assert.AreEqual(15f, shaft.AvgWaitYesterday, 0.001f);
            Assert.AreEqual(2f, shaft.AveragePassengersLast7Days, 0.001f);
            Assert.AreEqual(15f, shaft.AverageWaitLast7Days, 0.001f);
        }

        [Test]
        public void ElevatorSystem_ArchiveDay_builds_tower_totals()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevRoom));
            Assert.IsTrue(grid.TryExtendElevator(elevRoom, 0, 3, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            Assert.AreEqual(1, elevators.Shafts.Count);
            var shaft = elevators.Shafts[0];
            elevators.RecordBoarding(shaft, 5f);
            elevators.RecordBoarding(shaft, 15f);
            Assert.AreEqual(2, elevators.PassengersToday);
            Assert.AreEqual(10f, elevators.AvgWaitToday, 0.001f);

            elevators.ArchiveDay();
            Assert.AreEqual(0, elevators.PassengersToday);
            Assert.AreEqual(2, elevators.PassengersYesterday);
            Assert.AreEqual(10f, elevators.AvgWaitYesterday, 0.001f);
            Assert.AreEqual(2f, elevators.AveragePassengersLast7Days, 0.001f);
            Assert.AreEqual(10f, elevators.AverageWaitLast7Days, 0.001f);
        }

        [Test]
        public void Weighted_7day_wait_favors_busy_days()
        {
            var shaft = NewShaft();

            // Day 1: 1 passenger, 30m wait
            shaft.RecordBoarding(30f);
            shaft.ArchiveDay();
            // Day 2: 9 passengers, 10m each
            for (var i = 0; i < 9; i++)
                shaft.RecordBoarding(10f);
            shaft.ArchiveDay();

            // Weighted: (30 + 90) / 10 = 12, not (30+10)/2 = 20
            Assert.AreEqual(12f, shaft.AverageWaitLast7Days, 0.001f);
            Assert.AreEqual(5f, shaft.AveragePassengersLast7Days, 0.001f);
        }

        static ElevatorShaftRuntime NewShaft() =>
            new()
            {
                RoomInstanceId = 1,
                Car = new ElevatorCar(),
                UpQueues = new Dictionary<int, Queue<int>>(),
                DownQueues = new Dictionary<int, Queue<int>>()
            };

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            return so;
        }
    }
}
