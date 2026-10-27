using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomStateSnapshotTests
    {
        static RoomTypeSO Type(string id = "shop_cafe")
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = id;
            type.incomeModel = IncomeModel.TrafficVariable;
            type.maxOccupants = 3;
            return type;
        }

        static RoomSnapshotV1 Snapshot()
        {
            return new RoomSnapshotV1
            {
                instanceId = 42,
                roomTypeId = "shop_cafe",
                originX = -3,
                originY = 7,
                width = 4,
                height = 2,
                evaluation = 73,
                condition = 61,
                dirty = true,
                cleanWorkRemaining = 45.5f,
                repairJobsRemaining = 3,
                repairJobMinutes = 22.5f,
                staffedWorkers = 2,
                condoSold = true,
                priceTier = PricePricing.TierHigh,
                artVariant = 2,
                buildGraceSecondsRemaining = 4f,
                constructionSpent = 1200,
                lifetimeIncome = 3400,
                lifetimeExpense = 560,
                visitsToday = 8,
                shopEarningsToday = 90,
                shopRevenueYesterday = 700,
                shopUpkeepYesterday = 80,
                visitHistory = new[] { 3, 5, 8, 13, 21, 34, 55 }
            };
        }

        static RoomInstance Room(RoomTypeSO type = null)
        {
            return new RoomInstance(
                42,
                type ?? Type(),
                new Vector2Int(-3, 7),
                new Vector2Int(4, 2));
        }

        [Test]
        public void Restore_then_capture_round_trips_every_persisted_field()
        {
            var room = Room();
            Assert.IsTrue(room.TryOccupyVisitorSlot());
            room.RestoreSnapshot(Snapshot(), nowRealtime: 500f);

            Assert.AreEqual(0, room.ConcurrentVisitors);
            Assert.AreEqual(494f, room.PlacedAtRealtime);

            var captured = room.CaptureSnapshot(nowRealtime: 500f);
            Assert.AreEqual(42, captured.instanceId);
            Assert.AreEqual("shop_cafe", captured.roomTypeId);
            Assert.AreEqual(-3, captured.originX);
            Assert.AreEqual(7, captured.originY);
            Assert.AreEqual(4, captured.width);
            Assert.AreEqual(2, captured.height);
            Assert.AreEqual(73, captured.evaluation);
            Assert.AreEqual(61, captured.condition);
            Assert.IsTrue(captured.dirty);
            Assert.AreEqual(45.5f, captured.cleanWorkRemaining);
            Assert.AreEqual(3, captured.repairJobsRemaining);
            Assert.AreEqual(22.5f, captured.repairJobMinutes);
            Assert.AreEqual(2, captured.staffedWorkers);
            Assert.IsTrue(captured.condoSold);
            Assert.AreEqual(PricePricing.TierHigh, captured.priceTier);
            Assert.AreEqual(2, captured.artVariant);
            Assert.AreEqual(4f, captured.buildGraceSecondsRemaining);
            Assert.AreEqual(1200, captured.constructionSpent);
            Assert.AreEqual(3400, captured.lifetimeIncome);
            Assert.AreEqual(560, captured.lifetimeExpense);
            Assert.AreEqual(8, captured.visitsToday);
            Assert.AreEqual(90, captured.shopEarningsToday);
            Assert.AreEqual(700, captured.shopRevenueYesterday);
            Assert.AreEqual(80, captured.shopUpkeepYesterday);
            CollectionAssert.AreEqual(new[] { 3, 5, 8, 13, 21, 34, 55 }, captured.visitHistory);
        }

        [Test]
        public void Expired_grace_captures_and_restores_as_absent()
        {
            var room = Room();
            room.RecordConstructionSpend(100, nowRealtime: 10f, isInitialPlace: true);

            var expired = room.CaptureSnapshot(nowRealtime: 20f);
            Assert.AreEqual(0f, expired.buildGraceSecondsRemaining);

            room.RestoreSnapshot(expired, nowRealtime: 100f);
            Assert.AreEqual(-1f, room.PlacedAtRealtime);
            Assert.IsFalse(room.IsInBuildGrace(100f));
        }

        [TestCase("instance")]
        [TestCase("type")]
        [TestCase("origin")]
        [TestCase("size")]
        public void Restore_rejects_mismatched_room_identity_or_geometry(string mismatch)
        {
            var snapshot = Snapshot();
            switch (mismatch)
            {
                case "instance": snapshot.instanceId++; break;
                case "type": snapshot.roomTypeId = "SHOP_CAFE"; break;
                case "origin": snapshot.originX++; break;
                case "size": snapshot.height++; break;
            }

            var room = Room();
            Assert.Throws<ArgumentException>(() => room.RestoreSnapshot(snapshot, 500f));
            Assert.AreEqual(100, room.Evaluation);
        }

        [Test]
        public void Restore_rejects_null_before_mutating()
        {
            var room = Room();
            Assert.Throws<ArgumentNullException>(() => room.RestoreSnapshot(null, 0f));
            Assert.AreEqual(100, room.Condition);
        }

        [Test]
        public void Restore_clamps_bounded_and_non_negative_state()
        {
            var snapshot = Snapshot();
            snapshot.evaluation = 150;
            snapshot.condition = -4;
            snapshot.dirty = false;
            snapshot.cleanWorkRemaining = 20f;
            snapshot.repairJobsRemaining = -2;
            snapshot.repairJobMinutes = -3f;
            snapshot.staffedWorkers = 9;
            snapshot.priceTier = 99;
            snapshot.artVariant = -1;
            snapshot.buildGraceSecondsRemaining = 99f;
            snapshot.constructionSpent = -1;
            snapshot.lifetimeIncome = -2;
            snapshot.lifetimeExpense = -3;
            snapshot.visitsToday = -4;
            snapshot.shopEarningsToday = -5;
            snapshot.shopRevenueYesterday = -6;
            snapshot.shopUpkeepYesterday = -7;

            var room = Room();
            room.RestoreSnapshot(snapshot, nowRealtime: 200f);

            Assert.AreEqual(100, room.Evaluation);
            Assert.AreEqual(0, room.Condition);
            Assert.IsFalse(room.Dirty);
            Assert.AreEqual(0f, room.CleanWorkRemaining);
            Assert.AreEqual(0, room.RepairJobsRemaining);
            Assert.AreEqual(0f, room.RepairJobMinutes);
            Assert.AreEqual(4, room.StaffedWorkers);
            Assert.AreEqual(PricePricing.TierMax, room.PriceTier);
            Assert.AreEqual(0, room.ArtVariant);
            Assert.AreEqual(200f, room.PlacedAtRealtime);
            Assert.AreEqual(0, room.ConstructionSpent);
            Assert.AreEqual(0, room.LifetimeIncome);
            Assert.AreEqual(0, room.LifetimeExpense);
            Assert.AreEqual(0, room.VisitsToday);
            Assert.AreEqual(0, room.ShopEarningsToday);
            Assert.AreEqual(0, room.ShopRevenueYesterday);
            Assert.AreEqual(0, room.ShopUpkeepYesterday);
        }

        [Test]
        public void Visit_history_capture_is_chronological_after_wrap()
        {
            var ring = new VisitHistoryRing();
            for (var day = 1; day <= 9; day++)
                ring.Push(day);

            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7, 8, 9 }, ring.CaptureValues());
        }

        [Test]
        public void Visit_history_restore_keeps_newest_seven_and_push_clamps_negatives()
        {
            var ring = new VisitHistoryRing();
            ring.Push(99);
            ring.RestoreValues(new[] { 1, 2, -3, 4, 5, 6, 7, 8 });

            Assert.AreEqual(7, ring.RecordedDays);
            Assert.AreEqual(8, ring.Yesterday);
            CollectionAssert.AreEqual(new[] { 2, 0, 4, 5, 6, 7, 8 }, ring.CaptureValues());
        }

        [Test]
        public void Visit_history_restore_null_clears_ring()
        {
            var ring = new VisitHistoryRing();
            ring.Push(12);
            ring.RestoreValues(null);

            CollectionAssert.IsEmpty(ring.CaptureValues());
            Assert.AreEqual(0, ring.RecordedDays);
        }
    }
}
