using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class PowerSnapshotTests
    {
        static RoomTypeSO PlantType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "service_power";
            type.size = new Vector2Int(8, 1);
            return type;
        }

        static RoomInstance Plant()
        {
            return new RoomInstance(7, PlantType(), new Vector2Int(0, -1), new Vector2Int(8, 1));
        }

        static RoomSnapshotV1 MinimalSnapshot()
        {
            return new RoomSnapshotV1
            {
                instanceId = 7,
                roomTypeId = "service_power",
                originX = 0,
                originY = -1,
                width = 8,
                height = 1
            };
        }

        [Test]
        public void Capture_restore_round_trips_utility_level_upgrade_clock_and_maintenance()
        {
            var room = Plant();
            room.UtilityLevel = 2;
            room.UpgradeMinutesRemaining = 100f;
            room.UpgradeRunning = true;
            room.PlayerMaintenance = true;

            var snapshot = room.CaptureSnapshot(nowRealtime: 0f);
            Assert.AreEqual(2, snapshot.utilityLevel);
            Assert.AreEqual(100f, snapshot.upgradeMinutesRemaining);
            Assert.IsTrue(snapshot.upgradeRunning);
            Assert.IsTrue(snapshot.playerMaintenance);

            var restored = Plant();
            restored.RestoreSnapshot(snapshot, nowRealtime: 0f);

            Assert.AreEqual(2, restored.UtilityLevel);
            Assert.AreEqual(100f, restored.UpgradeMinutesRemaining);
            Assert.IsTrue(restored.UpgradeRunning);
            Assert.IsTrue(restored.PlayerMaintenance);
        }

        [Test]
        public void Snapshot_without_utility_fields_restores_defaults()
        {
            var room = Plant();
            room.UtilityLevel = 3;
            room.UpgradeMinutesRemaining = 500f;
            room.UpgradeRunning = true;
            room.PlayerMaintenance = true;

            room.RestoreSnapshot(MinimalSnapshot(), nowRealtime: 0f);

            Assert.AreEqual(0, room.UtilityLevel);
            Assert.AreEqual(0f, room.UpgradeMinutesRemaining);
            Assert.IsFalse(room.UpgradeRunning);
            Assert.IsFalse(room.PlayerMaintenance);
        }

        [Test]
        public void Powered_is_not_written_to_snapshot()
        {
            var room = Plant();
            room.Powered = true;

            var snapshot = room.CaptureSnapshot(nowRealtime: 0f);
            var restored = Plant();
            restored.RestoreSnapshot(snapshot, nowRealtime: 0f);

            Assert.IsFalse(restored.Powered);
        }
    }
}
