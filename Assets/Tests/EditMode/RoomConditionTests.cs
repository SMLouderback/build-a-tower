using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomConditionTests
    {
        static RoomTypeSO Type(string id = "office", bool lobby = false, bool elevator = false, bool stairs = false, int requiredStars = 0)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.isLobby = lobby;
            so.isElevatorShaft = elevator;
            so.isStairs = stairs;
            so.requiredStars = requiredStars;
            return so;
        }

        static RoomInstance Room(RoomTypeSO type, int condition = 100)
        {
            var room = new RoomInstance(1, type, Vector2Int.zero, Vector2Int.one);
            room.Condition = condition;
            return room;
        }

        [Test]
        public void Defaults_condition_100_not_dirty_not_broken_zero_staff()
        {
            var room = new RoomInstance(1, Type(), Vector2Int.zero, Vector2Int.one);
            Assert.AreEqual(100, room.Condition);
            Assert.IsFalse(room.Dirty);
            Assert.IsFalse(room.IsBroken);
            Assert.AreEqual(0, room.StaffedWorkers);
        }

        [Test]
        public void IsBroken_when_condition_zero_or_less()
        {
            var room = Room(Type(), 1);
            Assert.IsFalse(room.IsBroken);
            room.Condition = 0;
            Assert.IsTrue(room.IsBroken);
            room.Condition = -1;
            Assert.IsTrue(room.IsBroken);
        }

        [Test]
        public void MarkDirty_and_ClearDirty()
        {
            var room = Room(Type());
            room.MarkDirty();
            Assert.IsTrue(room.Dirty);
            room.ClearDirty();
            Assert.IsFalse(room.Dirty);
            Assert.AreEqual(0f, room.CleanWorkRemaining);
        }

        [Test]
        public void QueueCleanWork_and_ApplyCleanWork_track_pool()
        {
            var room = Room(Type());
            room.QueueCleaning(2, 180f);
            Assert.IsTrue(room.Dirty);
            Assert.AreEqual(360f, room.CleanWorkRemaining);

            room.ApplyCleanWork(30f);
            Assert.IsTrue(room.Dirty);
            Assert.AreEqual(330f, room.CleanWorkRemaining);

            room.ApplyCleanWork(330f);
            Assert.IsFalse(room.Dirty);
            Assert.AreEqual(0f, room.CleanWorkRemaining);
        }

        [Test]
        public void QueueRepairs_and_CompleteRepairJob_track_shifts()
        {
            var room = Room(Type());
            room.QueueRepairs(1, 180f);
            Assert.AreEqual(1, room.RepairJobsRemaining);
            Assert.AreEqual(180f, room.RepairJobMinutes);
            room.CompleteRepairJob();
            Assert.AreEqual(0, room.RepairJobsRemaining);
            Assert.AreEqual(0f, room.RepairJobMinutes);
        }

        [Test]
        public void SetStaffedWorkers_clamps_0_to_4()
        {
            var room = Room(Type());
            room.SetStaffedWorkers(2);
            Assert.AreEqual(2, room.StaffedWorkers);
            room.SetStaffedWorkers(-3);
            Assert.AreEqual(0, room.StaffedWorkers);
            room.SetStaffedWorkers(9);
            Assert.AreEqual(4, room.StaffedWorkers);
        }

        [Test]
        public void CanDegrade_false_for_lobby_elevator_stairs_and_null()
        {
            Assert.IsFalse(RoomConditionRules.CanDegrade(null));
            Assert.IsFalse(RoomConditionRules.CanDegrade(Type(lobby: true)));
            Assert.IsFalse(RoomConditionRules.CanDegrade(Type(elevator: true)));
            Assert.IsFalse(RoomConditionRules.CanDegrade(Type(stairs: true)));
            Assert.IsTrue(RoomConditionRules.CanDegrade(Type("office")));
            Assert.IsTrue(RoomConditionRules.CanDegrade(Type("service_housekeeping")));
        }

        [Test]
        public void ApplyMidnightDecay_decrements_degradable_floors_at_zero()
        {
            var office = Room(Type("office"), 5);
            RoomConditionRules.ApplyMidnightDecay(office);
            Assert.AreEqual(4, office.Condition);

            office.Condition = 0;
            RoomConditionRules.ApplyMidnightDecay(office);
            Assert.AreEqual(0, office.Condition);
            Assert.IsTrue(office.IsBroken);

            var lobby = Room(Type(lobby: true), 50);
            RoomConditionRules.ApplyMidnightDecay(lobby);
            Assert.AreEqual(50, lobby.Condition);
        }

        [Test]
        public void IncomePaused_when_condition_below_40_or_broken()
        {
            var room = Room(Type(), 40);
            Assert.IsFalse(RoomConditionRules.IncomePaused(room));
            room.Condition = 39;
            Assert.IsTrue(RoomConditionRules.IncomePaused(room));
            room.Condition = 0;
            Assert.IsTrue(RoomConditionRules.IncomePaused(room));
            Assert.IsFalse(RoomConditionRules.IncomePaused(null));
        }

        [Test]
        public void CleanMinutes_uses_explicit_field()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.category = RoomCategory.Hotel;
            so.cleanMinutes = 55f;
            so.requiredStars = 0;
            Assert.AreEqual(55f, RoomConditionRules.CleanMinutes(so));
        }

        [Test]
        public void CleanMinutes_hotel_band_fallback_when_cleanMinutes_zero()
        {
            var mid = ScriptableObject.CreateInstance<RoomTypeSO>();
            mid.category = RoomCategory.Hotel;
            mid.cleanMinutes = 0f;
            mid.luxuryBand = LuxuryBand.Mid;
            mid.requiredStars = 0;
            Assert.AreEqual(HotelLuxury.CleanFallbackMidMinutes, RoomConditionRules.CleanMinutes(mid));
        }

        [Test]
        public void CleanMinutes_non_hotel_keeps_legacy_star_fallback()
        {
            // Non-hotel (or uncategorized) rooms keep star≥2 premium minutes for one release.
            Assert.AreEqual(RoomConditionRules.CleanBasicMinutes, RoomConditionRules.CleanMinutes(Type(requiredStars: 0)));
            Assert.AreEqual(RoomConditionRules.CleanBasicMinutes, RoomConditionRules.CleanMinutes(Type(requiredStars: 1)));
            Assert.AreEqual(RoomConditionRules.CleanPremiumMinutes, RoomConditionRules.CleanMinutes(Type(requiredStars: 2)));
            Assert.AreEqual(RoomConditionRules.CleanPremiumMinutes, RoomConditionRules.CleanMinutes(Type(requiredStars: 3)));
            Assert.AreEqual(RoomConditionRules.CleanBasicMinutes, RoomConditionRules.CleanMinutes(null));
        }

        [Test]
        public void ApplyRepairTick_adds_chunk_capped_at_100()
        {
            var room = Room(Type(), 85);
            Assert.IsTrue(RoomConditionRules.ApplyRepairTick(room));
            Assert.AreEqual(95, room.Condition);
            Assert.IsTrue(RoomConditionRules.ApplyRepairTick(room));
            Assert.AreEqual(100, room.Condition);
            Assert.IsTrue(RoomConditionRules.ApplyRepairTick(room));
            Assert.AreEqual(100, room.Condition);
        }

        [Test]
        public void ApplyRepairTick_noop_when_broken_or_null()
        {
            Assert.IsFalse(RoomConditionRules.ApplyRepairTick(null));
            var broken = Room(Type(), 0);
            Assert.IsFalse(RoomConditionRules.ApplyRepairTick(broken));
            Assert.AreEqual(0, broken.Condition);
            Assert.IsTrue(broken.IsBroken);
        }
    }
}
