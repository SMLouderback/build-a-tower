using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildGraceRefundTests
    {
        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.buildCost = 40_000;
            so.incomeModel = IncomeModel.QuarterlyRent;
            so.baseIncome = 3000;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            return so;
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

        static RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator_normal";
            so.displayName = "Elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.buildCost = 20000;
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        [Test]
        public void GraceRefundAmount_is_spend_minus_lifetime_net()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, nowRealtime: 100f, isInitialPlace: true);
            room.RecordLifetimeIncome(150_000);
            room.RecordLifetimeExpense(3_000);
            // 40000 - (150000 - 3000) = -107000
            Assert.AreEqual(-107_000, room.GraceRefundAmount());
        }

        [Test]
        public void IsInBuildGrace_only_within_ten_realtime_seconds_of_place()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, nowRealtime: 50f, isInitialPlace: true);
            Assert.IsTrue(room.IsInBuildGrace(59.9f));
            Assert.IsFalse(room.IsInBuildGrace(60.1f));
        }

        [Test]
        public void Extension_spend_does_not_refresh_grace_deadline()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, nowRealtime: 10f, isInitialPlace: true);
            room.RecordConstructionSpend(20_000, nowRealtime: 15f, isInitialPlace: false);
            Assert.AreEqual(60_000, room.ConstructionSpent);
            Assert.IsFalse(room.IsInBuildGrace(20.1f)); // placed at 10 → expires 20
        }

        [Test]
        public void Lobby_and_scaffolding_are_not_grace_eligible()
        {
            var lobby = ScriptableObject.CreateInstance<RoomTypeSO>();
            lobby.isLobby = true;
            var scaffold = ScriptableObject.CreateInstance<RoomTypeSO>();
            scaffold.isScaffolding = true;
            Assert.IsFalse(RoomInstance.IsGraceRefundEligible(lobby));
            Assert.IsFalse(RoomInstance.IsGraceRefundEligible(scaffold));
            Assert.IsTrue(RoomInstance.IsGraceRefundEligible(Office()));
        }

        [Test]
        public void Condo_sale_increments_LifetimeIncome()
        {
            var condo = ScriptableObject.CreateInstance<RoomTypeSO>();
            condo.incomeModel = IncomeModel.UpfrontSale;
            condo.baseIncome = 150_000;
            var room = new RoomInstance(1, condo, Vector2Int.zero, Vector2Int.one);
            var economy = new EconomySystem();
            var wallet = new FundsWallet(0);
            Assert.IsTrue(economy.TrySellCondo(room, wallet));
            Assert.AreEqual(150_000, room.LifetimeIncome);
        }

        [Test]
        public void Demolish_within_grace_refunds_construction_spend()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, 0f, isInitialPlace: true);
            Assert.AreEqual(40_000, BuildGraceRefund.WalletDelta(room, nowRealtime: 5f));
        }

        [Test]
        public void Demolish_after_grace_refunds_zero()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, 0f, isInitialPlace: true);
            Assert.AreEqual(0, BuildGraceRefund.WalletDelta(room, nowRealtime: 11f));
        }

        [Test]
        public void Elevator_resize_preserves_build_grace_ledger()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var shaft));
            shaft.RecordConstructionSpend(20_000, nowRealtime: 100f, isInitialPlace: true);
            shaft.RecordLifetimeIncome(1_000);
            shaft.RecordLifetimeExpense(500);
            var instanceId = shaft.InstanceId;

            Assert.IsTrue(grid.TryResizeElevator(shaft, 0, 3, out var delta));
            Assert.AreEqual(2, delta);

            var resized = grid.Rooms.First(r => r.InstanceId == instanceId);
            Assert.AreEqual(20_000, resized.ConstructionSpent);
            Assert.AreEqual(100f, resized.PlacedAtRealtime);
            Assert.AreEqual(1_000, resized.LifetimeIncome);
            Assert.AreEqual(500, resized.LifetimeExpense);
        }
    }
}
