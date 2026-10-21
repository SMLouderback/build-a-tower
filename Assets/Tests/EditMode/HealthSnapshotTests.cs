using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HealthSnapshotTests
    {
        [Test]
        public void Null_health_snapshot_restores_healthy_defaults()
        {
            var health = new HealthSystem();
            health.SetIllness(4, 55f);
            health.CleanMode = CleanMode.Deep;
            health.Quarantined = true;

            health.RestoreSnapshot(null);

            Assert.AreEqual(CleanMode.Normal, health.CleanMode);
            Assert.AreEqual(0f, health.GetIllness(4), 0.0001f);
            Assert.IsFalse(health.Quarantined);
        }

        [Test]
        public void Legacy_json_without_health_block_is_absent_and_validates()
        {
            var legacy = JsonUtility.FromJson<TowerSnapshotV1>("{\"schemaVersion\":1}");
            Assert.IsTrue(HealthSystem.IsAbsent(legacy.health));

            var snapshot = new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = "save",
                towerName = "Tower",
                difficulty = GameDifficulty.Normal.ToString(),
                modifiedUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                walletBalance = 0,
                stars = 0,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 0,
                    minuteOfDay = 0,
                    minuteAccumulator = 0f,
                    minutesPerRealSecond = 1f,
                    paused = false
                },
                rooms = Array.Empty<RoomSnapshotV1>(),
                health = null
            };
            Assert.IsTrue(TowerSnapshotMapper.ValidateForRestore(snapshot).Success);

            snapshot.health = new HealthSnapshotV1 { cleanMode = "Bogus" };
            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(SnapshotValidationError.InvalidHealth, result.ErrorCode);
        }

        [Test]
        public void Round_trip_preserves_illness_clean_mode_quarantine_and_sick_room()
        {
            var health = new HealthSystem();
            health.SetIllness(4, 40f);
            health.CleanMode = CleanMode.Deep;
            health.Quarantined = true;

            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "hotel_standard";
            type.size = Vector2Int.one;
            var room = new RoomInstance(1, type, Vector2Int.zero, Vector2Int.one);
            room.SetSick(true);

            var healthSnapshot = JsonRoundTrip(health.CaptureSnapshot());
            var roomSnapshot = JsonRoundTrip(room.CaptureSnapshot(0f));

            var restoredHealth = new HealthSystem();
            restoredHealth.RestoreSnapshot(healthSnapshot);

            var restoredRoom = new RoomInstance(1, type, Vector2Int.zero, Vector2Int.one);
            restoredRoom.RestoreSnapshot(roomSnapshot, 0f);

            Assert.AreEqual(40f, restoredHealth.GetIllness(4), 0.0001f);
            Assert.AreEqual(CleanMode.Deep, restoredHealth.CleanMode);
            Assert.IsTrue(restoredHealth.Quarantined);
            Assert.IsTrue(restoredRoom.Sick);
        }

        [Test]
        public void Save_schema_version_remains_one()
        {
            Assert.AreEqual(1, SaveSchema.CurrentVersion);
        }

        static HealthSnapshotV1 JsonRoundTrip(HealthSnapshotV1 snapshot) =>
            JsonUtility.FromJson<HealthSnapshotV1>(JsonUtility.ToJson(snapshot));

        static RoomSnapshotV1 JsonRoundTrip(RoomSnapshotV1 snapshot) =>
            JsonUtility.FromJson<RoomSnapshotV1>(JsonUtility.ToJson(snapshot));
    }
}
