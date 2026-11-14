using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class OfficeFillTests
    {
        [Test]
        public void TryFindOfficeDesk_rejects_premium_for_micro()
        {
            var grid = MicroOfficeTower(out _);
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsFalse(agents.TryFindOfficeDeskForWorker(
                grid, WealthBand.Premium, MarketClimate.Normal, rng, reservedDesks: null, out _, out _));
        }

        [Test]
        public void TryFindOfficeDesk_accepts_basic_for_micro()
        {
            var grid = MicroOfficeTower(out var micro);
            var agents = CreateAgents(grid);
            var rng = new System.Random(0);

            Assert.IsTrue(agents.TryFindOfficeDeskForWorker(
                grid, WealthBand.Basic, MarketClimate.Normal, rng, reservedDesks: null, out var room, out var slot));
            Assert.AreSame(micro, room);
            Assert.AreEqual(0, slot);
        }

        [Test]
        public void SyncHomes_stores_wealth_on_office_worker()
        {
            var grid = MicroOfficeTower(out _);
            var agents = CreateAgents(grid);

            agents.SyncHomes(grid, currentStars: 0, averageCrime: 0f);

            Assert.GreaterOrEqual(agents.Agents.Count, 1);
            Assert.AreEqual(AgentRole.OfficeWorker, agents.Agents[0].Role);
            Assert.AreEqual(WealthBand.Basic, agents.Agents[0].Wealth);
        }

        static TowerGrid MicroOfficeTower(out RoomInstance micro)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(MicroOffice(), new Vector2Int(0, 1), out micro));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
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

        static RoomTypeSO MicroOffice()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = OfficeLuxury.MicroId;
            so.category = RoomCategory.Office;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(2, 1);
            so.maxOccupants = 1;
            so.allowAboveGround = true;
            return so;
        }
    }
}
