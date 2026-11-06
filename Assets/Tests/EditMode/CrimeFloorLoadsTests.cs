using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CrimeFloorLoadsTests
    {
        [Test]
        public void VisitorCrimeWeight_matches_leisure_ids()
        {
            Assert.AreEqual(1.75f, CrimeFloorLoads.VisitorCrimeWeight(LeisureSo("leisure_casino", new Vector2Int(10, 1))), 0.001f);
            Assert.AreEqual(1.5f, CrimeFloorLoads.VisitorCrimeWeight(LeisureSo("leisure_nightclub", new Vector2Int(8, 1))), 0.001f);
            Assert.AreEqual(0.5f, CrimeFloorLoads.VisitorCrimeWeight(LeisureSo("leisure_chapel", new Vector2Int(6, 1))), 0.001f);
            Assert.AreEqual(1f, CrimeFloorLoads.VisitorCrimeWeight(LeisureSo("leisure_gym", new Vector2Int(6, 1))), 0.001f);
            Assert.AreEqual(1f, CrimeFloorLoads.VisitorCrimeWeight(null), 0.001f);
        }

        [Test]
        public void Casino_busy_load_is_scaled()
        {
            var grid = BuildGridWithBusyVenue("leisure_casino", new Vector2Int(10, 1), visitorCount: 4, out _);
            var loads = CrimeFloorLoads.ShopLoadByFloor(grid);
            Assert.IsTrue(loads.TryGetValue(1, out var load));
            Assert.AreEqual(7f, load, 0.001f);
        }

        [Test]
        public void Nightclub_busy_load_is_scaled()
        {
            var grid = BuildGridWithBusyVenue("leisure_nightclub", new Vector2Int(8, 1), visitorCount: 4, out _);
            var loads = CrimeFloorLoads.ShopLoadByFloor(grid);
            Assert.IsTrue(loads.TryGetValue(1, out var load));
            Assert.AreEqual(6f, load, 0.001f);
        }

        [Test]
        public void Chapel_busy_load_is_scaled()
        {
            var grid = BuildGridWithBusyVenue("leisure_chapel", new Vector2Int(6, 1), visitorCount: 4, out _);
            var loads = CrimeFloorLoads.ShopLoadByFloor(grid);
            Assert.IsTrue(loads.TryGetValue(1, out var load));
            Assert.AreEqual(2f, load, 0.001f);
        }

        static TowerGrid BuildGridWithBusyVenue(string leisureId, Vector2Int size, int visitorCount, out RoomInstance venue)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _));
            var type = LeisureSo(leisureId, size);
            Assert.IsTrue(grid.TryPlace(type, new Vector2Int(0, 1), out venue));
            for (var i = 0; i < visitorCount; i++)
                Assert.IsTrue(venue.TryOccupyVisitorSlot());
            return grid;
        }

        static RoomTypeSO LobbySo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO LeisureSo(string id, Vector2Int size)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Commercial;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.buildFamily = BuildFamily.Leisure;
            so.size = size;
            so.allowAboveGround = true;
            so.maxOccupants = 12;
            return so;
        }
    }
}
