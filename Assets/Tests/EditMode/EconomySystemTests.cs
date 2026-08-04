using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class EconomySystemTests
    {
        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO Office(int baseIncome)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.QuarterlyRent;
            so.baseIncome = baseIncome;
            return so;
        }

        RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Condo(int baseIncome)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "condo";
            so.category = RoomCategory.Condo;
            so.size = Vector2Int.one;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = baseIncome;
            return so;
        }

        [Test]
        public void Midnight_pays_daily_rent_for_occupied_office()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(baseIncome: 3000), new Vector2Int(0, 1), out var office));
            var agents = new List<Agent> { new Agent(1, AgentRole.OfficeWorker, office, office.Origin) };
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, agents, wallet);

            Assert.AreEqual(103_000, wallet.Balance);
            Assert.AreEqual(3000, economy.LastIncome);
            Assert.AreEqual(0, economy.LastExpense);
            Assert.AreEqual(3000, economy.LastNet);
            Assert.AreEqual(3000, economy.GetLastRoomIncome(office));
            Assert.AreEqual(0, economy.GetLastRoomExpense(office));
            Assert.AreEqual(3000, economy.GetLastRoomNet(office));
            Assert.AreEqual(3000, office.LifetimeIncome);
            Assert.AreEqual(0, office.LifetimeExpense);
        }

        [Test]
        public void Midnight_charges_elevator_upkeep()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            var wallet = new FundsWallet(50_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            Assert.AreEqual(50_000 - EconomySystem.ElevatorDailyUpkeep, wallet.Balance);
            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(EconomySystem.ElevatorDailyUpkeep, economy.LastExpense);
            Assert.AreEqual(-EconomySystem.ElevatorDailyUpkeep, economy.LastNet);
            Assert.AreEqual(EconomySystem.ElevatorDailyUpkeep, economy.GetLastRoomExpense(elevator));
            Assert.AreEqual(-EconomySystem.ElevatorDailyUpkeep, economy.GetLastRoomNet(elevator));
            Assert.AreEqual(0, elevator.LifetimeIncome);
            Assert.AreEqual(EconomySystem.ElevatorDailyUpkeep, elevator.LifetimeExpense);
        }

        [Test]
        public void Condo_sale_pays_once()
        {
            var condoRoom = new RoomInstance(1, Condo(150_000), Vector2Int.zero, Vector2Int.one);
            var wallet = new FundsWallet(0);
            var economy = new EconomySystem();

            Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
            Assert.IsFalse(economy.TrySellCondo(condoRoom, wallet));

            Assert.AreEqual(150_000, wallet.Balance);
            Assert.IsTrue(condoRoom.CondoSold);
            Assert.AreEqual(150_000, economy.GetLastRoomIncome(condoRoom));
            Assert.AreEqual(150_000, condoRoom.LifetimeIncome);
            Assert.IsTrue(economy.HasRecordedEconomyEvent);
        }

        [Test]
        public void Midnight_scales_income_by_price_tier()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(baseIncome: 3000), new Vector2Int(0, 1), out var office));
            office.PriceTier = PricePricing.TierHigh;
            var agents = new List<Agent> { new Agent(1, AgentRole.OfficeWorker, office, office.Origin) };
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem(seed: 1);

            economy.OnNewDay(grid, agents, wallet, currentStars: 3);

            Assert.AreEqual(3900, economy.LastIncome);
            Assert.AreEqual(103_900, wallet.Balance);
        }

        [Test]
        public void Condo_sale_scales_by_price_tier()
        {
            var condoRoom = new RoomInstance(1, Condo(150_000), Vector2Int.zero, Vector2Int.one);
            condoRoom.PriceTier = PricePricing.TierLow;
            var wallet = new FundsWallet(0);
            var economy = new EconomySystem();

            Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
            Assert.AreEqual(105_000, wallet.Balance);
        }

        RoomTypeSO FastFoodShop(int baseIncome = 40)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_fast";
            so.category = RoomCategory.Commercial;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.baseIncome = baseIncome;
            so.maxOccupants = 4;
            return so;
        }

        [Test]
        public void Midnight_pays_traffic_from_visits_and_clears_counter()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(FastFoodShop(baseIncome: 40), new Vector2Int(0, 1), out var shop));
            shop.RecordVisit();
            shop.RecordShopSpend(30);
            shop.RecordVisit();
            shop.RecordShopSpend(40);
            shop.RecordVisit();
            shop.RecordShopSpend(50);
            Assert.AreEqual(3, shop.VisitsToday);
            Assert.AreEqual(120, shop.ShopEarningsToday);

            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            Assert.AreEqual(120, economy.LastIncome);
            Assert.AreEqual(100_120, wallet.Balance);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(120, shop.LifetimeIncome);
            Assert.AreEqual(120, economy.GetLastRoomIncome(shop));
            Assert.IsTrue(economy.HasRecordedEconomyEvent);
        }

        [Test]
        public void Overpriced_office_can_skip_income_under_seeded_rng()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(baseIncome: 3000), new Vector2Int(0, 1), out var office));
            office.PriceTier = PricePricing.TierMax;
            var agents = new List<Agent> { new Agent(1, AgentRole.OfficeWorker, office, office.Origin) };
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem(seed: 7);

            economy.OnNewDay(grid, agents, wallet, currentStars: 0);

            // Max at 0★ → 10% demand; seed 7 first roll is ~0.38 so income is skipped.
            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(100_000, wallet.Balance);
        }

        RoomTypeSO Housekeeping()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_housekeeping";
            so.category = RoomCategory.Service;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Maintenance()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_maintenance";
            so.category = RoomCategory.Service;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        [Test]
        public void Midnight_decays_condition_on_degradable_rooms()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out var lobby);
            Assert.IsTrue(grid.TryPlace(Office(baseIncome: 3000), new Vector2Int(0, 1), out var office));
            Assert.AreEqual(100, office.Condition);
            Assert.AreEqual(100, lobby.Condition);

            new EconomySystem().OnNewDay(grid, new List<Agent>(), new FundsWallet(100_000));

            Assert.AreEqual(99, office.Condition);
            Assert.AreEqual(100, lobby.Condition);
        }

        [Test]
        public void Midnight_skips_income_when_condition_paused_or_broken()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(baseIncome: 3000), new Vector2Int(0, 1), out var office));
            office.Condition = 39;
            var agents = new List<Agent> { new Agent(1, AgentRole.OfficeWorker, office, office.Origin) };
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, agents, wallet);

            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(100_000, wallet.Balance);
            Assert.AreEqual(38, office.Condition);

            office.Condition = 0;
            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(0, economy.LastIncome);
            Assert.IsTrue(office.IsBroken);
        }

        [Test]
        public void Midnight_skips_shop_earnings_when_income_paused()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(FastFoodShop(baseIncome: 40), new Vector2Int(0, 1), out var shop));
            shop.RecordVisit();
            shop.RecordShopSpend(80);
            shop.Condition = 20;
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(100_000, wallet.Balance);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
        }

        [Test]
        public void Midnight_debits_housekeeping_and_maintenance_wages()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Housekeeping(), new Vector2Int(0, 1), out var hk));
            Assert.IsTrue(grid.TryPlace(Maintenance(), new Vector2Int(1, 1), out var maint));
            hk.SetStaffedWorkers(2);
            maint.SetStaffedWorkers(1);
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            const int expectedWages = 2 * EconomySystem.MaidWagePerDay + 1 * EconomySystem.HandymanWagePerDay;
            Assert.AreEqual(expectedWages, economy.LastWageExpense);
            Assert.AreEqual(expectedWages, economy.LastExpense);
            Assert.AreEqual(100_000 - expectedWages, wallet.Balance);
            Assert.AreEqual(2 * EconomySystem.MaidWagePerDay, economy.GetLastRoomExpense(hk));
            Assert.AreEqual(EconomySystem.HandymanWagePerDay, economy.GetLastRoomExpense(maint));
            Assert.AreEqual(2 * EconomySystem.MaidWagePerDay, hk.LifetimeExpense);
            Assert.AreEqual(EconomySystem.HandymanWagePerDay, maint.LifetimeExpense);
        }

        [Test]
        public void Midnight_charges_security_wages()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            var security = ScriptableObject.CreateInstance<RoomTypeSO>();
            security.id = "service_security";
            security.size = new Vector2Int(2, 1);
            security.allowAboveGround = true;
            Assert.IsTrue(grid.TryPlace(security, new Vector2Int(0, 1), out var room));
            room.SetStaffedWorkers(2);
            var wallet = new FundsWallet(50_000);
            var economy = new EconomySystem();
            economy.OnNewDay(grid, new List<Agent>(), wallet);
            Assert.AreEqual(50_000 - 2 * EconomySystem.SecurityGuardWagePerDay,
                wallet.Balance);
            Assert.AreEqual(2 * EconomySystem.SecurityGuardWagePerDay, economy.LastWageExpense);
        }

        [Test]
        public void AverageDailyProfit_tracks_running_average_of_LastNet()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(baseIncome: 3000), new Vector2Int(0, 1), out var office));
            var agents = new List<Agent> { new Agent(1, AgentRole.OfficeWorker, office, office.Origin) };
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            Assert.AreEqual(0f, economy.AverageDailyProfit);

            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(3000, economy.LastNet);
            Assert.AreEqual(3000f, economy.AverageDailyProfit);

            // Second midnight with no occupants → LastNet 0; average falls to 1500.
            economy.OnNewDay(grid, new List<Agent>(), wallet);
            Assert.AreEqual(0, economy.LastNet);
            Assert.AreEqual(1500f, economy.AverageDailyProfit);
        }

        [Test]
        public void Security_is_staffed_service_and_auto_hires()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_security";
            Assert.IsTrue(BuildController.IsStaffedServiceRoom(so));
            var room = new RoomInstance(1, so, Vector2Int.zero, new Vector2Int(2, 1));
            BuildController.ApplyAutoHireOnPlace(room);
            Assert.AreEqual(1, room.StaffedWorkers);
        }

        RoomTypeSO ResearchLab()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = EconomySystem.ResearchId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
            so.allowAboveGround = true;
            return so;
        }

        [Test]
        public void Research_is_staffed_service_and_auto_hires()
        {
            var so = ResearchLab();
            Assert.IsTrue(BuildController.IsStaffedServiceRoom(so));
            var room = new RoomInstance(1, so, Vector2Int.zero, so.size);
            BuildController.ApplyAutoHireOnPlace(room);
            Assert.AreEqual(1, room.StaffedWorkers);
        }

        [Test]
        public void Midnight_charges_research_wages()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            lab.SetStaffedWorkers(2);
            var wallet = new FundsWallet(50_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            var wages = 2 * EconomySystem.ResearchWagePerDay;
            var idleBurn = ResearchCatalog.IdlePerLabPerDay;
            Assert.AreEqual(wages, economy.LastWageExpense);
            Assert.AreEqual(idleBurn, economy.LastResearchBurn);
            Assert.AreEqual(50_000 - wages - idleBurn, wallet.Balance);
            Assert.AreEqual(wages, economy.GetLastRoomExpense(lab));
        }

        [Test]
        public void Midnight_charges_idle_research_burn_only_when_paused()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            lab.SetStaffedWorkers(1);
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Marketing, 1));
            research.Pause();

            var wallet = new FundsWallet(50_000);
            var economy = new EconomySystem();
            economy.OnNewDay(grid, new List<Agent>(), wallet, research: research, climateSpendMult: 1f);

            var expectedWage = EconomySystem.ResearchWagePerDay;
            var expectedBurn = ResearchCatalog.IdlePerLabPerDay;
            Assert.AreEqual(expectedBurn, economy.LastResearchBurn);
            Assert.AreEqual(50_000 - expectedWage - expectedBurn, wallet.Balance);
            Assert.IsTrue(research.IsPaused);
        }

        [Test]
        public void Midnight_charges_idle_plus_active_research_burn_when_running()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            lab.SetStaffedWorkers(1);
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Elevator, 1));

            var wallet = new FundsWallet(50_000);
            var economy = new EconomySystem();
            economy.OnNewDay(grid, new List<Agent>(), wallet, research: research, climateSpendMult: 1f);

            var expectedBurn = ResearchCatalog.IdlePerLabPerDay + ResearchCatalog.ActivePerDay;
            Assert.AreEqual(expectedBurn, economy.LastResearchBurn);
            Assert.AreEqual(
                50_000 - EconomySystem.ResearchWagePerDay - expectedBurn,
                wallet.Balance);
            Assert.IsFalse(research.IsPaused);
        }

        [Test]
        public void Midnight_research_burn_cheaper_in_recession_than_boom()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            lab.SetStaffedWorkers(0);

            var researchRecession = new ResearchSystem();
            Assert.IsTrue(researchRecession.TryStart(ResearchBranch.Security, 1));
            var researchBoom = new ResearchSystem();
            Assert.IsTrue(researchBoom.TryStart(ResearchBranch.Security, 1));

            // Match MarketClimate.SpendMultiplier for Recession / Boom.
            const float recessionMult = 0.7f;
            const float boomMult = 1.3f;

            var walletRecession = new FundsWallet(50_000);
            var economyRecession = new EconomySystem();
            economyRecession.OnNewDay(
                grid, new List<Agent>(), walletRecession,
                research: researchRecession, climateSpendMult: recessionMult);

            var walletBoom = new FundsWallet(50_000);
            var economyBoom = new EconomySystem();
            economyBoom.OnNewDay(
                grid, new List<Agent>(), walletBoom,
                research: researchBoom, climateSpendMult: boomMult);

            var baseBurn = ResearchCatalog.IdlePerLabPerDay + ResearchCatalog.ActivePerDay;
            Assert.AreEqual((int)System.Math.Round(baseBurn * recessionMult), economyRecession.LastResearchBurn);
            Assert.AreEqual((int)System.Math.Round(baseBurn * boomMult), economyBoom.LastResearchBurn);
            Assert.Less(economyRecession.LastResearchBurn, economyBoom.LastResearchBurn);
        }

        [Test]
        public void Midnight_unaffordable_research_burn_pauses_and_charges_zero()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            lab.SetStaffedWorkers(1);
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Housekeeping, 1));
            Assert.IsFalse(research.IsPaused);

            // Enough for wage ($350) but not idle+active burn ($2500).
            var wallet = new FundsWallet(EconomySystem.ResearchWagePerDay + 100);
            var economy = new EconomySystem();
            economy.OnNewDay(grid, new List<Agent>(), wallet, research: research, climateSpendMult: 1f);

            Assert.AreEqual(0, economy.LastResearchBurn);
            Assert.IsTrue(research.IsPaused);
            Assert.AreEqual(100, wallet.Balance);
        }

        [Test]
        public void Count_helpers_skip_broken_labs_and_sum_researcher_pool()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var labA));
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(3, 1), out var labB));
            labA.SetStaffedWorkers(2);
            labB.SetStaffedWorkers(3);
            labB.Condition = 0;

            Assert.AreEqual(1, EconomySystem.CountNonBrokenResearchLabs(grid));
            Assert.AreEqual(2, EconomySystem.CountResearcherPool(grid));
        }

        [Test]
        public void OnNewDay_archives_shop_visits_yesterday_and_tower_average()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _);
            Assert.IsTrue(grid.TryPlace(FastFoodShop(), new Vector2Int(0, 1), out var a));
            Assert.IsTrue(grid.TryPlace(FastFoodShop(), new Vector2Int(10, 1), out var b));
            for (var i = 0; i < 5; i++) a.RecordVisit();
            for (var i = 0; i < 3; i++) b.RecordVisit();

            var economy = new EconomySystem();
            economy.OnNewDay(grid, new List<Agent>(), new FundsWallet(0));

            Assert.AreEqual(0, a.VisitsToday);
            Assert.AreEqual(0, b.VisitsToday);
            Assert.AreEqual(5, a.VisitsYesterday);
            Assert.AreEqual(3, b.VisitsYesterday);
            Assert.AreEqual(5f, a.AverageVisitsLast7Days);
            Assert.AreEqual(3f, b.AverageVisitsLast7Days);
            Assert.AreEqual(8, economy.LastShopVisitsYesterday);
            Assert.AreEqual(8f, economy.AverageShopVisitsLast7Days);

            for (var i = 0; i < 2; i++) a.RecordVisit();
            economy.OnNewDay(grid, new List<Agent>(), new FundsWallet(0));
            Assert.AreEqual(2, a.VisitsYesterday);
            Assert.AreEqual(0, b.VisitsYesterday);
            Assert.AreEqual(3.5f, a.AverageVisitsLast7Days); // (5+2)/2
            Assert.AreEqual(5f, economy.AverageShopVisitsLast7Days); // (8+2)/2
        }
    }
}
