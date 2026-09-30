using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AmenitySystemTests
    {
        [Test]
        public void Spa_relief_stronger_than_bowling()
        {
            Assert.Greater(AmenitySystem.ReliefForId("leisure_spa"), AmenitySystem.ReliefForId("leisure_bowling"));
        }

        [Test]
        public void In_range_spa_relieves_once_per_day()
        {
            var grid = BuildGridWithCondoAndSpa(out var condo, out _);
            var agent = new Agent(1, AgentRole.CondoResident, condo, condo.Origin)
            {
                Stress = 40f,
                HasMovedIn = true
            };

            Assert.IsTrue(AmenitySystem.TryApplyDailyRelief(agent, grid, dayIndex: 1));
            Assert.AreEqual(35f, agent.Stress, 0.001f);
            Assert.IsFalse(AmenitySystem.TryApplyDailyRelief(agent, grid, dayIndex: 1));
            Assert.AreEqual(35f, agent.Stress, 0.001f);

            Assert.IsTrue(AmenitySystem.TryApplyDailyRelief(agent, grid, dayIndex: 2));
            Assert.AreEqual(30f, agent.Stress, 0.001f);
        }

        [Test]
        public void Broken_or_out_of_range_amenity_ignored()
        {
            var grid = BuildGridWithCondoAndSpa(out var condo, out var spa);
            spa.Condition = 0;
            var agent = new Agent(1, AgentRole.CondoResident, condo, condo.Origin)
            {
                Stress = 40f,
                HasMovedIn = true
            };
            Assert.IsFalse(AmenitySystem.TryApplyDailyRelief(agent, grid, dayIndex: 1));
            Assert.AreEqual(40f, agent.Stress, 0.001f);

            Object.DestroyImmediate(spa.Type);
            var farSpaType = LeisureSo("leisure_spa", new Vector2Int(6, 1));
            Assert.IsTrue(grid.TryPlace(farSpaType, new Vector2Int(20, 1), out _));
            Assert.IsFalse(AmenitySystem.TryApplyDailyRelief(agent, grid, dayIndex: 2));
            Assert.AreEqual(40f, agent.Stress, 0.001f);

            Object.DestroyImmediate(farSpaType);
        }

        [Test]
        public void HotelDemandBonus_requires_spa_or_gym_in_range()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            var hotelType = ScriptableObject.CreateInstance<RoomTypeSO>();
            hotelType.id = "hotel";
            hotelType.category = RoomCategory.Hotel;
            hotelType.size = new Vector2Int(6, 1);
            hotelType.allowAboveGround = true;
            hotelType.maxOccupants = 4;
            Assert.IsTrue(grid.TryPlace(hotelType, new Vector2Int(0, 1), out var hotel));

            Assert.AreEqual(0f, AmenitySystem.HotelDemandBonus(grid, hotel));

            var gymType = LeisureSo("leisure_gym", new Vector2Int(6, 1));
            Assert.IsTrue(grid.TryPlace(gymType, new Vector2Int(8, 1), out _));
            Assert.AreEqual(0.08f, AmenitySystem.HotelDemandBonus(grid, hotel));

            Object.DestroyImmediate(hotelType);
            Object.DestroyImmediate(gymType);
        }

        [Test]
        public void Atrium_relief_uses_wider_range()
        {
            var condoType = ScriptableObject.CreateInstance<RoomTypeSO>();
            condoType.id = "condo";
            condoType.category = RoomCategory.Condo;
            condoType.size = new Vector2Int(6, 1);
            condoType.allowAboveGround = true;
            var atriumType = LeisureSo("leisure_atrium", new Vector2Int(6, 1), isAtrium: true);

            var inRangeGrid = new TowerGrid();
            inRangeGrid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            Assert.IsTrue(inRangeGrid.TryPlace(atriumType, new Vector2Int(0, 1), out _));
            Assert.IsTrue(inRangeGrid.TryPlace(condoType, new Vector2Int(AmenitySystem.AtriumMaxHorizontalCells, 1), out var condoInRange));
            Assert.AreEqual(5f, AmenitySystem.MaxReliefInRange(inRangeGrid, condoInRange));

            var farHorizGrid = new TowerGrid();
            farHorizGrid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            Assert.IsTrue(farHorizGrid.TryPlace(atriumType, new Vector2Int(0, 1), out _));
            Assert.IsTrue(farHorizGrid.TryPlace(condoType, new Vector2Int(AmenitySystem.AtriumMaxHorizontalCells + 1, 1), out var farCondo));
            Assert.AreEqual(0f, AmenitySystem.MaxReliefInRange(farHorizGrid, farCondo));

            // Exactly 6 floors above the condo: in range.
            var vertInRangeGrid = BuildGridWithAtriumAt(
                atriumType, condoType, AmenitySystem.AtriumMaxFloorDelta + 1, out var lowCondo);
            Assert.AreEqual(5f, AmenitySystem.MaxReliefInRange(vertInRangeGrid, lowCondo));

            // 7 floors above the condo: out of range.
            var vertFarGrid = BuildGridWithAtriumAt(
                atriumType, condoType, AmenitySystem.AtriumMaxFloorDelta + 2, out var farLowCondo);
            Assert.AreEqual(0f, AmenitySystem.MaxReliefInRange(vertFarGrid, farLowCondo));

            Object.DestroyImmediate(condoType);
            Object.DestroyImmediate(atriumType);
        }

        /// <summary>Condo at floor 1, scaffold column, atrium at <paramref name="atriumFloor"/> (x 0..5).</summary>
        static TowerGrid BuildGridWithAtriumAt(
            RoomTypeSO atriumType, RoomTypeSO condoType, int atriumFloor, out RoomInstance condo)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(condoType, new Vector2Int(0, 1), out condo), "condo");
            for (var y = 2; y < atriumFloor; y++)
            for (var x = 0; x <= 5; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _), $"scaffold {x},{y}");
            Assert.IsTrue(grid.TryPlace(atriumType, new Vector2Int(0, atriumFloor), out _), "atrium");
            return grid;
        }

        [TestCase("leisure_casino", 3f)]
        [TestCase("leisure_nightclub", 2f)]
        [TestCase("leisure_chapel", 4f)]
        [TestCase("leisure_atrium", 5f)]
        public void ReliefForId_new_leisure_rooms(string id, float expected)
        {
            Assert.AreEqual(expected, AmenitySystem.ReliefForId(id));
        }

        [Test]
        public void Other_leisure_still_uses_tight_range()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            var condoType = ScriptableObject.CreateInstance<RoomTypeSO>();
            condoType.id = "condo";
            condoType.category = RoomCategory.Condo;
            condoType.size = new Vector2Int(6, 1);
            condoType.allowAboveGround = true;
            Assert.IsTrue(grid.TryPlace(condoType, new Vector2Int(0, 1), out var condo));

            var spaType = LeisureSo("leisure_spa", new Vector2Int(6, 1));
            Assert.IsTrue(grid.TryPlace(spaType, new Vector2Int(13, 1), out _));
            Assert.AreEqual(0f, AmenitySystem.MaxReliefInRange(grid, condo));

            Object.DestroyImmediate(condoType);
            Object.DestroyImmediate(spaType);
        }

        [Test]
        public void MaxReliefInRange_picks_strongest_amenity()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            var condoType = ScriptableObject.CreateInstance<RoomTypeSO>();
            condoType.id = "condo";
            condoType.category = RoomCategory.Condo;
            condoType.size = new Vector2Int(6, 1);
            condoType.allowAboveGround = true;
            Assert.IsTrue(grid.TryPlace(condoType, new Vector2Int(0, 1), out var condo));

            Assert.IsTrue(grid.TryPlace(LeisureSo("leisure_spa", new Vector2Int(6, 1)), new Vector2Int(6, 1), out _));
            Assert.IsTrue(grid.TryPlace(LeisureSo("leisure_bowling", new Vector2Int(10, 1)), new Vector2Int(12, 1), out _));

            Assert.AreEqual(5f, AmenitySystem.MaxReliefInRange(grid, condo));

            Object.DestroyImmediate(condoType);
        }

        static TowerGrid BuildGridWithCondoAndSpa(out RoomInstance condo, out RoomInstance spa)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _);
            var condoType = ScriptableObject.CreateInstance<RoomTypeSO>();
            condoType.id = "condo";
            condoType.category = RoomCategory.Condo;
            condoType.size = new Vector2Int(6, 1);
            condoType.allowAboveGround = true;
            Assert.IsTrue(grid.TryPlace(condoType, new Vector2Int(0, 1), out condo));

            var spaType = LeisureSo("leisure_spa", new Vector2Int(6, 1));
            Assert.IsTrue(grid.TryPlace(spaType, new Vector2Int(8, 1), out spa));

            Object.DestroyImmediate(condoType);
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

        static RoomTypeSO LeisureSo(string id, Vector2Int size, bool isAtrium = false)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Commercial;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.buildFamily = BuildFamily.Leisure;
            so.size = size;
            so.allowAboveGround = true;
            so.isAtrium = isAtrium;
            return so;
        }
    }
}
