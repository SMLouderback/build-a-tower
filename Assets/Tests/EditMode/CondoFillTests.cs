using System;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CondoFillTests
    {
        [Test]
        public void TryFindCondo_rejects_premium_for_studio()
        {
            var grid = StudioCondoTower(out _);
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsFalse(agents.TryFindCondoForBuyer(
                grid, WealthBand.Premium, MarketClimate.Normal, 0, 0, rng, out _));
        }

        [Test]
        public void TryFindCondo_accepts_basic_for_studio()
        {
            var grid = StudioCondoTower(out var studio);
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsTrue(agents.TryFindCondoForBuyer(
                grid, WealthBand.Basic, MarketClimate.Normal, 0, 0, rng, out var room));
            Assert.AreSame(studio, room);
        }

        [Test]
        public void FillCondoVacancies_spawns_full_household_same_wealth()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(MidCondo(3), new Vector2Int(0, 1), out var condo));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 1), out _));

            var agents = CreateAgents(grid);
            // Stars 2 keeps Mid band healthy in the mix.
            agents.SyncHomes(grid, currentStars: 2, averageCrime: 0f);

            var residents = agents.Agents.Where(a => a.Role == AgentRole.CondoResident).ToList();
            Assert.AreEqual(3, residents.Count);
            Assert.IsTrue(residents.All(a => ReferenceEquals(a.HomeRoom, condo)));
            var wealth = residents[0].Wealth;
            Assert.IsTrue(wealth == WealthBand.Mid || wealth == WealthBand.Upper,
                "Mid condo accepts Mid buyers; Upper may take Family only — MidStandard is Mid-only so expect Mid");
            Assert.IsTrue(residents.All(a => a.Wealth == wealth));
            Assert.AreEqual(WealthBand.Mid, wealth);
        }

        static TowerGrid StudioCondoTower(out RoomInstance studio)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(StudioCondo(), new Vector2Int(0, 1), out studio));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 1), out _));
            return grid;
        }

        static AgentSystem CreateAgents(TowerGrid grid)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            return new AgentSystem(router);
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Stairs()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.isStairs = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            so.size = new Vector2Int(2, 2);
            return so;
        }

        static RoomTypeSO StudioCondo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = CondoLuxury.StudioId;
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(4, 1);
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = 65_000;
            return so;
        }

        static RoomTypeSO MidCondo(int maxOccupants)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = CondoLuxury.MidStandardId;
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Mid;
            so.requiredStars = 2;
            so.size = new Vector2Int(10, 1);
            so.maxOccupants = maxOccupants;
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = 200_000;
            return so;
        }
    }
}
