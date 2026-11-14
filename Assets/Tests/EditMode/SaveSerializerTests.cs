using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class SaveSerializerTests
    {
        [Test]
        public void Serialize_then_deserialize_round_trips_schema_one()
        {
            var source = new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = "save-a",
                towerName = "Skyline",
                difficulty = nameof(GameDifficulty.Hard),
                modifiedUtc = "2026-09-08T01:02:03.0000000Z",
                walletBalance = 123456,
                stars = 2,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 17,
                    minuteOfDay = 731,
                    minuteAccumulator = 0.5f,
                    minutesPerRealSecond = 10f,
                    paused = true
                }
            };

            var json = SaveSerializer.Serialize(source);
            var result = SaveSerializer.Deserialize(json);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual("Skyline", result.Snapshot.towerName);
            Assert.AreEqual(123456, result.Snapshot.walletBalance);
            Assert.AreEqual(17, result.Snapshot.clock.dayIndex);
            Assert.AreEqual(0.5f, result.Snapshot.clock.minuteAccumulator, 0.0001f);
        }

        [Test]
        public void Deserialize_rejects_changed_payload_without_destroying_input()
        {
            var source = new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = "save-a",
                towerName = "Original",
                difficulty = nameof(GameDifficulty.Normal),
                modifiedUtc = "2026-09-08T01:02:03.0000000Z",
                clock = new ClockSnapshotV1()
            };

            var json = SaveSerializer.Serialize(source);
            var tampered = json.Replace("payloadChecksum", "payloadChecksumX");
            var result = SaveSerializer.Deserialize(tampered);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidEnvelope, result.Error);
        }

        [Test]
        public void Deserialize_rejects_future_schema()
        {
            var source = new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion + 1,
                saveId = "future",
                towerName = "Future",
                difficulty = nameof(GameDifficulty.Normal),
                modifiedUtc = "2026-09-08T01:02:03.0000000Z",
                clock = new ClockSnapshotV1()
            };

            var result = SaveSerializer.Deserialize(SaveSerializer.Serialize(source));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.UnsupportedSchema, result.Error);
        }
    }
}
