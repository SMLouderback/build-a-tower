using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CleanlinessTickTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var build in Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                Object.DestroyImmediate(build.gameObject);

            GameSession.ResetForTests();
        }

        [Test]
        public void Hourly_drift_steps_twice_on_150_minutes_and_carries_remainder()
        {
            var sim = CreateTowerWithTwoSoldCondos();
            sim.RestoreCleanliness(null);
            Assert.AreEqual(100f, sim.Cleanliness);

            sim.TickCleanliness(150f);
            Assert.AreEqual(81f, sim.Cleanliness, 0.001f);

            sim.TickCleanliness(29f);
            Assert.AreEqual(81f, sim.Cleanliness, 0.001f);

            sim.TickCleanliness(1f);
            Assert.AreEqual(CleanlinessSystem.Step(81f, 0f), sim.Cleanliness, 0.001f);
        }

        [Test]
        public void Powered_treatment_adds_supply_toward_target_on_hour_step()
        {
            var (sim, treatment) = CreateTowerWithTreatmentAndSewage(12);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 50f });
            sim.Power();
            Assert.IsTrue(treatment.Powered);
            Assert.IsFalse(treatment.IsBroken);

            sim.TickCleanliness(60f);

            var target = CleanlinessSystem.Target(CleanlinessSystem.TreatmentSupply(0), 12);
            Assert.AreEqual(100f, target, 0.001f);
            Assert.AreEqual(CleanlinessSystem.Step(50f, target), sim.Cleanliness, 0.001f);
        }

        [Test]
        public void Broken_or_unpowered_treatment_adds_no_supply()
        {
            var (sim, treatment) = CreateTowerWithTreatmentAndSewage(12);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 40f });
            sim.Power();
            Assert.IsTrue(treatment.Powered);

            treatment.Condition = 0;
            sim.TickCleanliness(60f);
            var targetNoSupply = CleanlinessSystem.Target(0, 12);
            Assert.AreEqual(CleanlinessSystem.Step(40f, targetNoSupply), sim.Cleanliness, 0.001f);

            treatment.Condition = 100;
            treatment.Powered = false;
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 40f });
            sim.TickCleanliness(60f);
            Assert.AreEqual(CleanlinessSystem.Step(40f, targetNoSupply), sim.Cleanliness, 0.001f);
        }

        [Test]
        public void Dirty_water_at_stored_zero_raises_sewage_floor_illness_by_0_08_per_minute()
        {
            var sim = CreateTowerWithSoldCondoOnFloor(1);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 0f });
            Assert.AreEqual(0f, sim.Health.GetIllness(1));

            sim.TickHealth(1f);

            Assert.AreEqual(0.08f, sim.Health.GetIllness(1), 0.0001f);
        }

        [Test]
        public void Dirty_water_at_stored_fifty_raises_sewage_floor_illness_by_0_04_per_minute()
        {
            var sim = CreateTowerWithSoldCondoOnFloor(1);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 50f });
            Assert.AreEqual(0f, sim.Health.GetIllness(1));

            sim.TickHealth(1f);

            Assert.AreEqual(0.04f, sim.Health.GetIllness(1), 0.0001f);
        }

        [Test]
        public void Shop_only_floor_gains_no_water_illness()
        {
            var sim = CreateTowerWithShopOnFloor(1);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 0f });

            sim.TickHealth(1f);

            Assert.AreEqual(0f, sim.Health.GetIllness(1), 0.0001f);
        }

        [Test]
        public void Water_illness_does_not_change_decay_when_cleanliness_is_full()
        {
            var sim = CreateTowerWithSoldCondoOnFloor(1);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 100f });
            sim.Health.SetIllness(1, 10f);

            sim.TickHealth(1f);

            Assert.AreEqual(9.95f, sim.Health.GetIllness(1), 0.0001f);
        }

        [Test]
        public void Water_illness_stacks_with_existing_decay_on_sewage_floors()
        {
            var sim = CreateTowerWithSoldCondoOnFloor(1);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 0f });
            sim.Health.SetIllness(1, 10f);

            sim.TickHealth(1f);

            Assert.AreEqual(10.03f, sim.Health.GetIllness(1), 0.0001f);
        }

        [Test]
        public void Fill_gates_scale_with_stored_cleanliness_and_upper_water_pressure()
        {
            var hotelGrid = BaseHotelTower(out var hotel);
            var hotelAgents = CreateFillTestAgents(hotelGrid);
            hotel.Powered = true;
            hotelAgents.SetCleanliness(0f);

            var climate = MarketClimate.Normal;
            var baseLuxury = HotelLuxury.CheckInFillMultiplier(LuxuryBand.Base, climate);
            var hotelGate = baseLuxury * CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 0f);
            Assert.IsTrue(hotelAgents.TryFindHotelRoomForGuest(
                hotelGrid, WealthBand.Basic, climate, new FixedRollRandom(hotelGate - 0.01), out _, out _));
            Assert.IsFalse(hotelAgents.TryFindHotelRoomForGuest(
                hotelGrid, WealthBand.Basic, climate, new FixedRollRandom(hotelGate + 0.01), out _, out _));

            hotelAgents.SetCleanliness(140f);
            Assert.IsTrue(hotelAgents.TryFindHotelRoomForGuest(
                hotelGrid, WealthBand.Basic, climate, new FixedRollRandom(hotelGate + 0.01), out _, out _));

            var officeGrid = MidOfficeTower(out var office);
            var officeAgents = CreateFillTestAgents(officeGrid);
            office.Powered = true;
            officeAgents.SetCleanliness(0f);

            var officeLuxury = LivingLuxury.CheckInFillMultiplier(LuxuryBand.Mid, climate);
            var officeGate = officeLuxury * CleanlinessSystem.FillMultiplier(LuxuryBand.Mid, 0f);
            Assert.IsTrue(officeAgents.TryFindOfficeDeskForWorker(
                officeGrid, WealthBand.Mid, climate, new FixedRollRandom(officeGate - 0.01), null, out _, out _));
            Assert.IsFalse(officeAgents.TryFindOfficeDeskForWorker(
                officeGrid, WealthBand.Mid, climate, new FixedRollRandom(officeGate + 0.01), null, out _, out _));

            officeAgents.SetCleanliness(140f);
            Assert.IsTrue(officeAgents.TryFindOfficeDeskForWorker(
                officeGrid, WealthBand.Mid, climate, new FixedRollRandom(officeGate + 0.01), null, out _, out _));

            var (condoGrid, condoAgents, _) = CreatePoweredUpperCondoSim();

            var condoLuxury = LivingLuxury.CheckInFillMultiplier(LuxuryBand.Upper, climate);
            var condoGate = condoLuxury * CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 0f);
            condoAgents.SetWaterPressure(100f);
            condoAgents.SetCleanliness(0f);
            Assert.IsTrue(condoAgents.TryFindCondoForBuyer(
                condoGrid, WealthBand.Upper, climate, 0, 5, new FixedRollRandom(condoGate - 0.01), out _));
            Assert.IsFalse(condoAgents.TryFindCondoForBuyer(
                condoGrid, WealthBand.Upper, climate, 0, 5, new FixedRollRandom(condoGate + 0.01), out _));

            condoAgents.SetCleanliness(140f);
            Assert.IsTrue(condoAgents.TryFindCondoForBuyer(
                condoGrid, WealthBand.Upper, climate, 0, 5, new FixedRollRandom(condoGate + 0.01), out _));

            condoAgents.SetCleanliness(0f);
            condoAgents.SetWaterPressure(0f);
            var combinedGate = condoGate * WaterSystem.UpperFillMultiplier(0f);
            Assert.Greater(combinedGate, 0.05);
            Assert.Less(combinedGate, condoGate);
            Assert.IsTrue(condoAgents.TryFindCondoForBuyer(
                condoGrid, WealthBand.Upper, climate, 0, 5, new FixedRollRandom(combinedGate - 0.01), out _));
            Assert.IsFalse(condoAgents.TryFindCondoForBuyer(
                condoGrid, WealthBand.Upper, climate, 0, 5, new FixedRollRandom(0.20), out _));
        }

        [Test]
        public void RestoreCleanliness_pushes_percent_into_agent_fill_gates()
        {
            var go = new GameObject("CleanlinessFillSimHost");
            go.SetActive(false);
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;
            var agents = sim.Agents;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(BaseHotelType(), new Vector2Int(4, 1), out var hotel));
            hotel.Powered = true;
            sim.Router.Rebuild(grid);

            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 0f });
            var gate = HotelLuxury.CheckInFillMultiplier(LuxuryBand.Base, MarketClimate.Normal)
                       * CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 0f);
            Assert.IsFalse(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Basic, MarketClimate.Normal, new FixedRollRandom(gate + 0.01), out _, out _));

            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 140f });
            Assert.IsTrue(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Basic, MarketClimate.Normal, new FixedRollRandom(gate + 0.01), out _, out _));
        }

        [Test]
        public void Treatment_upgrade_clock_completes_and_changes_hourly_target()
        {
            var (sim, treatment) = CreateTowerWithTreatmentAndSewage(12);
            sim.RestoreCleanliness(new CleanlinessSnapshotV1 { percent = 55f });
            sim.Power();
            treatment.UpgradeRunning = true;
            treatment.UpgradeMinutesRemaining = CleanlinessSystem.TreatmentUpgradeMinutes(1);

            sim.TickPowerClocks(CleanlinessSystem.TreatmentUpgradeMinutes(1));

            Assert.AreEqual(1, treatment.UtilityLevel);
            Assert.AreEqual(20, CleanlinessSystem.TreatmentSupply(treatment.UtilityLevel));

            sim.TickCleanliness(60f);
            var target = CleanlinessSystem.Target(20, 12);
            Assert.AreEqual(CleanlinessSystem.Step(55f, target), sim.Cleanliness, 0.001f);
        }

        static TowerSimulation CreateTowerWithSoldCondoOnFloor(int floorY)
        {
            var go = new GameObject("CleanlinessCondoSingleHost");
            go.SetActive(false);
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(CondoType(), new Vector2Int(4, floorY), out var condo));
            condo.CondoSold = true;
            sim.Router.Rebuild(grid);
            return sim;
        }

        static TowerSimulation CreateTowerWithShopOnFloor(int floorY)
        {
            var go = new GameObject("CleanlinessShopHost");
            go.SetActive(false);
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(ShopType(), new Vector2Int(4, floorY), out _));
            sim.Router.Rebuild(grid);
            return sim;
        }

        static TowerSimulation CreateTowerWithTwoSoldCondos()
        {
            var go = new GameObject("CleanlinessCondoHost");
            go.SetActive(false);
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(CondoType(), new Vector2Int(4, 1), out var condoA));
            Assert.IsTrue(grid.TryPlace(CondoType(), new Vector2Int(8, 1), out var condoB));
            condoA.CondoSold = true;
            condoB.CondoSold = true;
            sim.Router.Rebuild(grid);
            return sim;
        }

        static (TowerSimulation sim, RoomInstance treatment) CreateTowerWithTreatmentAndSewage(int sewageRooms)
        {
            var go = new GameObject("CleanlinessTreatmentHost");
            go.SetActive(false);
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out _));

            var x = 4;
            for (var i = 0; i < sewageRooms; i++)
            {
                Assert.IsTrue(grid.TryPlace(SmallCondoType(), new Vector2Int(x, 1), out var condo));
                condo.CondoSold = true;
                x++;
                if (x > 35)
                {
                    x = 4;
                }
            }

            for (var sx = 0; sx < 14; sx++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(sx, -1), out _));
            Assert.IsTrue(grid.TryPlace(PlantType(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(TreatmentType(), new Vector2Int(8, -1), out var treatment));

            sim.Router.Rebuild(grid);
            return (sim, treatment);
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

        static RoomTypeSO ShopType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_retail";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Shops;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO CondoType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "condo_base";
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO SmallCondoType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "condo_tick";
            so.category = RoomCategory.Condo;
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

        static RoomTypeSO TreatmentType()
        {
            var loaded = Resources.Load<RoomTypeSO>("Rooms/ServiceTreatment");
            if (loaded != null)
                return loaded;

            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_treatment";
            so.category = RoomCategory.Service;
            so.buildFamily = BuildFamily.Utility;
            so.allowBasement = true;
            so.allowAboveGround = false;
            so.size = new Vector2Int(6, 1);
            return so;
        }

        static AgentSystem CreateFillTestAgents(TowerGrid grid)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            return new AgentSystem(router);
        }

        static (TowerGrid grid, AgentSystem agents, RoomInstance condo) CreatePoweredUpperCondoSim()
        {
            var go = new GameObject("CleanlinessUpperCondoFillHost");
            go.SetActive(false);
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;
            var agents = sim.Agents;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 2, out _));
            Assert.IsTrue(grid.TryPlace(CompactUpperCondoType(), new Vector2Int(1, 1), out var condo));
            sim.Router.Rebuild(grid);
            sim.Power();
            return (grid, agents, condo);
        }

        static TowerGrid BaseHotelTower(out RoomInstance hotel)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorType(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryPlace(BaseHotelType(), new Vector2Int(4, 1), out hotel));
            elevator.Powered = true;
            return grid;
        }

        static TowerGrid MidOfficeTower(out RoomInstance office)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(StairsType(), new Vector2Int(10, 0), out var stairs));
            Assert.IsTrue(grid.TryPlace(MidOfficeType(), new Vector2Int(0, 1), out office));
            stairs.Powered = true;
            return grid;
        }

        static RoomTypeSO StairsType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.isStairs = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            so.size = new Vector2Int(2, 2);
            return so;
        }

        static RoomTypeSO BaseHotelType()
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

        static RoomTypeSO MidOfficeType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = OfficeLuxury.MidStandardId;
            so.category = RoomCategory.Office;
            so.luxuryBand = LuxuryBand.Mid;
            so.size = new Vector2Int(4, 1);
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO CompactUpperCondoType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = CondoLuxury.UpperStandardId;
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Upper;
            so.requiredStars = 3;
            so.size = new Vector2Int(4, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = 180_000;
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
