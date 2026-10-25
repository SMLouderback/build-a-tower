using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class WaterSystemTests
    {
        const float Delta = 0.0001f;

        [TearDown]
        public void TearDownWaterTower()
        {
            var sims = Object.FindObjectsByType<TowerSimulation>(FindObjectsSortMode.None);
            for (var i = 0; i < sims.Length; i++)
            {
                if (sims[i] != null)
                    Object.DestroyImmediate(sims[i].gameObject);
            }
        }

        [Test]
        public void Height_counts_floors_from_lobby_inclusive()
        {
            Assert.AreEqual(15, WaterSystem.Height(0, 14));
            Assert.AreEqual(1, WaterSystem.Height(0, 0));
        }

        [Test]
        public void Demand_scales_with_height_and_upper_rooms()
        {
            Assert.AreEqual(54, WaterSystem.Demand(15, 4, 2));
            Assert.AreEqual(80, WaterSystem.Demand(40, 0, 0));
            Assert.AreEqual(120, WaterSystem.Demand(40, 10, 0));
        }

        [Test]
        public void PressurePercent_is_supply_over_demand_times_one_hundred()
        {
            Assert.AreEqual(80f / 54f * 100f, WaterSystem.PressurePercent(80, 54), Delta);
            Assert.AreEqual(80f / 120f * 100f, WaterSystem.PressurePercent(80, 120), Delta);
        }

        [Test]
        public void Pump_supply_ladder_and_upgrade_tables()
        {
            CollectionAssert.AreEqual(new[] { 80, 120, 180, 260 },
                Enumerable.Range(0, 4).Select(WaterSystem.PumpSupply).ToArray());
            Assert.AreEqual(30000, WaterSystem.PumpUpgradeFee(1));
            Assert.AreEqual(70000, WaterSystem.PumpUpgradeFee(2));
            Assert.AreEqual(140000, WaterSystem.PumpUpgradeFee(3));
            Assert.AreEqual(1440, WaterSystem.PumpUpgradeMinutes(1));
            Assert.AreEqual(4320, WaterSystem.PumpUpgradeMinutes(2));
            Assert.AreEqual(10080, WaterSystem.PumpUpgradeMinutes(3));
            Assert.AreEqual(0, WaterSystem.PumpUpgradeFee(4));
            Assert.AreEqual(0, WaterSystem.PumpUpgradeMinutes(0));
        }

        [Test]
        public void Upper_fill_multiplier_tracks_pressure()
        {
            Assert.AreEqual(0.25f, WaterSystem.UpperFillMultiplier(0f), Delta);
            Assert.AreEqual(0.625f, WaterSystem.UpperFillMultiplier(50f), Delta);
            Assert.AreEqual(1f, WaterSystem.UpperFillMultiplier(100f), Delta);
            Assert.AreEqual(1f, WaterSystem.UpperFillMultiplier(140f), Delta);
        }

        [Test]
        public void Upper_stress_gain_falls_to_zero_at_full_pressure()
        {
            Assert.AreEqual(8, WaterSystem.UpperStressGain(0f));
            Assert.AreEqual(4, WaterSystem.UpperStressGain(50f));
            Assert.AreEqual(0, WaterSystem.UpperStressGain(100f));
            Assert.AreEqual(0, WaterSystem.UpperStressGain(140f));
        }

        [Test]
        public void Tower_water_pressure_tracks_pump_power_and_upper_fill_multiplier()
        {
            var (sim, pump) = CreateFifteenFloorUpperTower();
            sim.Power();
            pump.Powered = false;
            sim.ComputeWater();

            Assert.AreEqual(0, sim.Water.Supply);
            Assert.AreEqual(54, sim.Water.Demand);
            Assert.AreEqual(0f, sim.Water.PressurePercent, Delta);
            Assert.AreEqual(
                0.25f,
                WaterSystem.UpperFillMultiplier(sim.Water.PressurePercent),
                Delta);

            pump.Powered = true;
            sim.ComputeWater();

            Assert.AreEqual(80, sim.Water.Supply);
            Assert.AreEqual(80f / 54f * 100f, sim.Water.PressurePercent, 0.5f);
            Assert.AreEqual(
                1f,
                WaterSystem.UpperFillMultiplier(sim.Water.PressurePercent),
                Delta);
        }

        static (TowerSimulation sim, RoomInstance pump) CreateFifteenFloorUpperTower()
        {
            var go = new GameObject("WaterTowerHost");
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));

            for (var floor = 1; floor <= 9; floor++)
            {
                Assert.IsTrue(grid.TryPlace(FillerType(), new Vector2Int(7, floor), out _));
                for (var x = 9; x <= 13; x++)
                    Assert.IsTrue(grid.TryPlace(FillerType(), new Vector2Int(x, floor), out _));
                for (var x = 15; x <= 19; x++)
                    Assert.IsTrue(grid.TryPlace(FillerType(), new Vector2Int(x, floor), out _));
            }

            for (var i = 0; i < 4; i++)
                Assert.IsTrue(
                    grid.TryPlace(UpperHotelType(), new Vector2Int(9, 10 + i), out _),
                    $"Upper hotel {i}");
            for (var floor = 10; floor <= 13; floor++)
            {
                for (var x = 15; x <= 19; x++)
                    Assert.IsTrue(grid.TryPlace(FillerType(), new Vector2Int(x, floor), out _));
            }

            for (var i = 0; i < 2; i++)
                Assert.IsTrue(
                    grid.TryPlace(UpperCondoType(), new Vector2Int(9 + i * 6, 14), out _),
                    $"Upper condo {i}");

            Assert.IsTrue(
                grid.TryPlaceScaffold(new Vector2Int(0, -1), out _));
            Assert.IsTrue(
                grid.TryPlace(PumpType(), new Vector2Int(0, -1), out var pump));

            sim.Router.Rebuild(grid);
            return (sim, pump);
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

        static RoomTypeSO UpperHotelType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = HotelLuxury.UpperKingId;
            so.category = RoomCategory.Hotel;
            so.luxuryBand = LuxuryBand.Upper;
            so.size = new Vector2Int(5, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO UpperCondoType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = CondoLuxury.UpperStandardId;
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Upper;
            so.size = new Vector2Int(5, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO PumpType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_pump";
            so.category = RoomCategory.Service;
            so.buildFamily = BuildFamily.Utility;
            so.allowBasement = true;
            so.allowAboveGround = false;
            so.size = new Vector2Int(6, 1);
            return so;
        }
    }
}
