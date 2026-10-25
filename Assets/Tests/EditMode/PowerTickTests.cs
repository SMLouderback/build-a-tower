using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class PowerTickTests
    {
        static readonly System.Random GuestRng = new(7);
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

        [Test]
        public void Dark_hotel_is_not_offered_to_new_guests()
        {
            var (sim, grid, _, hotel) = CreateTowerWithElevatorAndHotel();
            sim.Power();
            var agents = sim.Agents;

            Assert.IsTrue(
                agents.TryFindHotelRoomForGuest(grid, WealthBand.Basic, 0, GuestRng, out _, out _));

            hotel.Powered = false;
            Assert.IsFalse(
                agents.TryFindHotelRoomForGuest(grid, WealthBand.Basic, 0, GuestRng, out _, out _));
        }

        [Test]
        public void Powered_to_dark_edge_vacates_hotel_guest_once()
        {
            var (sim, grid, _, hotel) = CreateTowerWithElevatorAndHotel();
            sim.Power();
            sim.Agents.SyncHomes(grid, currentStars: 5, averageCrime: 0f);

            var guestsBefore = sim.Agents.Agents.Count(a => ReferenceEquals(a.HomeRoom, hotel));
            Assert.Greater(guestsBefore, 0, "Hotel should have at least one guest after SyncHomes.");

            hotel.Condition = 0;
            sim.Power();
            Assert.IsFalse(hotel.Powered);

            var guestsAfterFirst = sim.Agents.Agents.Count(a => ReferenceEquals(a.HomeRoom, hotel));
            Assert.AreEqual(0, guestsAfterFirst, "Guests should leave when the hotel goes dark.");
            Assert.IsTrue(hotel.Dirty, "Vacated hotel should be marked dirty for housekeeping.");

            sim.Power();
            Assert.IsFalse(hotel.Powered, "Hotel should stay dark at capacity.");
            var guestsAfterSecond = sim.Agents.Agents.Count(a => ReferenceEquals(a.HomeRoom, hotel));
            Assert.AreEqual(0, guestsAfterSecond, "Staying dark must not vacate again.");
        }

        [Test]
        public void Player_maintenance_persists_while_shaft_is_powered_and_after_power_returns()
        {
            var (sim, grid, elevator, _) = CreateTowerWithElevatorAndHotel();
            sim.Power();
            var shaft = sim.Elevators.FindByRoomId(elevator.InstanceId);
            Assert.IsNotNull(shaft);

            elevator.PlayerMaintenance = true;
            SyncElevatorMaintenance(elevator, shaft);
            Assert.IsTrue(elevator.Powered);
            Assert.IsTrue(shaft.InMaintenance, "Player maintenance stops a powered shaft.");

            elevator.Powered = false;
            SyncElevatorMaintenance(elevator, shaft);
            Assert.IsTrue(
                shaft.InMaintenance,
                "Player maintenance stays active while the shaft is dark.");

            elevator.Powered = true;
            SyncElevatorMaintenance(elevator, shaft);
            Assert.IsTrue(
                shaft.InMaintenance,
                "Player maintenance survives once shaft power returns.");

            elevator.PlayerMaintenance = false;
            SyncElevatorMaintenance(elevator, shaft);
            Assert.IsFalse(shaft.InMaintenance, "Exit maintenance on a powered shaft restores service.");

            elevator.Powered = false;
            elevator.PlayerMaintenance = true;
            SyncElevatorMaintenance(elevator, shaft);
            elevator.PlayerMaintenance = false;
            SyncElevatorMaintenance(elevator, shaft);
            Assert.IsTrue(
                shaft.InMaintenance,
                "Exit maintenance on a dark shaft leaves it stopped.");
        }

        static void SyncElevatorMaintenance(RoomInstance elevator, ElevatorShaftRuntime shaft) =>
            ElevatorShaftRuntime.SyncMaintenanceFromRoom(elevator, shaft);

        static void ShedHotelPower(TowerSimulation sim, TowerGrid grid)
        {
            for (var i = 0; i < 18; i++)
                Assert.IsTrue(grid.TryPlace(FillerType(), FillerCell(i), out _));
            sim.Power();
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

            sim.Router.Rebuild(grid);
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

        [Test]
        public void Base_hotel_fill_ignores_water_multiplier_upper_guest_stress_once_per_day()
        {
            var go = new GameObject("WaterAgentHost");
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;
            var agents = sim.Agents;
            var clock = sim.Clock;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(HotelType(), new Vector2Int(4, 1), out var baseHotel));
            for (var floor = 2; floor <= 4; floor++)
            {
                for (var x = 4; x <= 6; x++)
                    Assert.IsTrue(grid.TryPlace(FillerType(), new Vector2Int(x, floor), out _));
            }
            Assert.IsTrue(
                grid.TryPlace(UpperHotelType(), new Vector2Int(4, 5), out var upperHotel));
            sim.Router.Rebuild(grid);
            sim.Power();
            upperHotel.Powered = true;
            baseHotel.Powered = true;
            agents.SetWaterPressure(0f);

            var lowRoll = new FixedRollRandom(0.05);
            Assert.IsTrue(
                agents.TryFindHotelRoomForGuest(grid, WealthBand.Basic, 0, lowRoll, out var basePick, out _));
            Assert.AreEqual(baseHotel, basePick);

            var highRoll = new FixedRollRandom(0.95);
            Assert.IsFalse(
                agents.TryFindHotelRoomForGuest(
                    grid,
                    WealthBand.Premium,
                    0,
                    highRoll,
                    out _,
                    out _));

            agents.SetWaterPressure(148f);
            Assert.IsTrue(
                agents.TryFindHotelRoomForGuest(
                    grid,
                    WealthBand.Premium,
                    0,
                    new FixedRollRandom(0.05),
                    out var upperPick,
                    out _));
            Assert.AreEqual(upperHotel, upperPick);

            agents.SetWaterPressure(148f);
            agents.SyncHomes(grid, currentStars: 5, averageCrime: 0f);
            var guest = agents.Agents.FirstOrDefault(a =>
                a.Role == AgentRole.HotelGuest && ReferenceEquals(a.HomeRoom, upperHotel));
            Assert.IsNotNull(guest, "Upper hotel should host a guest after SyncHomes.");

            agents.SetWaterPressure(0f);

            var before = guest.Stress;
            agents.Tick(0f, clock, grid);
            Assert.AreEqual(before + 8f, guest.Stress, 0.001f);

            agents.Tick(0f, clock, grid);
            Assert.AreEqual(before + 8f, guest.Stress, 0.001f, "Same day index must not re-apply water stress.");
        }

        static RoomTypeSO UpperHotelType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = HotelLuxury.UpperKingId;
            so.category = RoomCategory.Hotel;
            so.luxuryBand = LuxuryBand.Upper;
            so.size = new Vector2Int(3, 1);
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

        sealed class FixedRollRandom : System.Random
        {
            readonly double _roll;

            public FixedRollRandom(double roll) => _roll = roll;

            public override double NextDouble() => _roll;
        }
    }
}
