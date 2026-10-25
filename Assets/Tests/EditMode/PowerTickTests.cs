using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class PowerTickTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyAll<TowerSimulation>();
            DestroyAll<BuildController>();
        }

        [Test]
        public void Power_list_sheds_empty_hotel_before_elevator_and_plant_upgrade_and_break()
        {
            var (sim, grid, elevator, hotel) = CreateTowerWithElevatorAndHotel();
            sim.Power();

            Assert.IsTrue(elevator.Powered, "Elevator should be powered at free capacity.");
            Assert.IsTrue(hotel.Powered, "Empty hotel should be powered when load fits.");

            for (var i = 0; i < 18; i++)
                Assert.IsTrue(
                    grid.TryPlace(FillerType(), FillerCell(i), out _),
                    $"Office {i} should place.");

            sim.Power();

            Assert.IsTrue(elevator.Powered, "Elevator should stay powered when shedding.");
            Assert.IsFalse(hotel.Powered, "Empty hotel should lose power before the elevator.");

            for (var x = 0; x < 8; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, -1), out _), "Basement scaffold.");
            Assert.IsTrue(
                grid.TryPlace(PlantType(), new Vector2Int(0, -1), out var plant),
                "Plant should place in basement.");
            for (var i = 0; i < 50; i++)
                Assert.IsTrue(
                    grid.TryPlace(FillerType(), FillerCell(18 + i), out _),
                    $"Extra office {i} should place.");

            sim.Power();
            Assert.IsFalse(hotel.Powered, "Hotel should stay dark at built plant capacity.");

            plant.UpgradeRunning = true;
            plant.UpgradeMinutesRemaining = PowerSystem.PlantUpgradeMinutes(1);
            sim.TickPowerClocks(PowerSystem.PlantUpgradeMinutes(1));

            Assert.AreEqual(1, plant.UtilityLevel);
            Assert.IsTrue(hotel.Powered, "Hotel should regain power after plant upgrade to level I.");
            Assert.AreEqual(
                PowerSystem.FreeCapacity + PowerSystem.PlantOutput(1),
                PowerSystem.Capacity(new[] { plant.UtilityLevel }, new[] { false }));

            plant.Condition = 0;
            sim.Power();
            Assert.IsFalse(hotel.Powered, "Broken plant should drop output and leave the hotel dark.");
            Assert.AreEqual(
                PowerSystem.FreeCapacity,
                PowerSystem.Capacity(new[] { plant.UtilityLevel }, new[] { true }));
        }

        static (TowerSimulation sim, TowerGrid grid, RoomInstance elevator, RoomInstance hotel)
            CreateTowerWithElevatorAndHotel()
        {
            var go = new GameObject("PowerTickHost");
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryPlace(HotelType(), new Vector2Int(4, 1), out var hotel));

            return (sim, grid, elevator, hotel);
        }

        static RoomTypeSO LobbyType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO ElevatorType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator";
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.size = new Vector2Int(1, 2);
            return so;
        }

        static RoomTypeSO HotelType()
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

        static Vector2Int FillerCell(int index) => new Vector2Int(7, 1 + index);

        static RoomTypeSO FillerType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_filler";
            so.category = RoomCategory.Office;
            so.luxuryBand = LuxuryBand.Base;
            so.size = Vector2Int.one;
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO PlantType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_power";
            so.category = RoomCategory.Service;
            so.buildFamily = BuildFamily.Utility;
            so.allowBasement = true;
            so.allowAboveGround = false;
            so.size = new Vector2Int(8, 1);
            return so;
        }

        static void DestroyAll<T>() where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    Object.DestroyImmediate(found[i].gameObject);
            }
        }
    }
}
