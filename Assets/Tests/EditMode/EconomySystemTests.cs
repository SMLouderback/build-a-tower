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
        }

        [Test]
        public void Midnight_charges_elevator_upkeep()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out _));
            var wallet = new FundsWallet(50_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            Assert.AreEqual(50_000 - EconomySystem.ElevatorDailyUpkeep, wallet.Balance);
            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(EconomySystem.ElevatorDailyUpkeep, economy.LastExpense);
            Assert.AreEqual(-EconomySystem.ElevatorDailyUpkeep, economy.LastNet);
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
        }
    }
}
