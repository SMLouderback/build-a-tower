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
    }
}
