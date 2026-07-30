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
    }
}
