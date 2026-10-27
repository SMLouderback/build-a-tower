using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class WeatherSnapshotTests
    {
        const int MinutesPerDay = GameClock.MinutesPerDay;

        [Test]
        public void Round_trip_mid_thunderstorm_preserves_kind_and_segment_end()
        {
            var weather = new WeatherSystem(new System.Random(1));
            const int day = 180; // summer
            weather.ForceSegment(WeatherKind.Thunderstorm, day, 14 * 60, 90);

            var snapshot = JsonRoundTrip(weather.CaptureSnapshot());
            var restored = new WeatherSystem(new System.Random(2));
            restored.RestoreSnapshot(snapshot, day, 14 * 60 + 30);

            Assert.AreEqual(WeatherKind.Thunderstorm, restored.Kind);
            Assert.AreEqual(WeatherHangover.None, restored.Hangover);
            Assert.AreEqual(weather.SegmentEndAbsoluteMinute, restored.SegmentEndAbsoluteMinute);
            Assert.AreEqual(WeatherSystem.SevereStreet, restored.StreetTrafficMultiplier);

            // Still mid-storm: advancing inside the segment must not re-roll.
            restored.AdvanceTo(day, 15 * 60);
            Assert.AreEqual(WeatherKind.Thunderstorm, restored.Kind);
            Assert.AreEqual(weather.SegmentEndAbsoluteMinute, restored.SegmentEndAbsoluteMinute);
        }

        [Test]
        public void Round_trip_mid_blizzard_hangover_preserves_state()
        {
            var weather = new WeatherSystem(new System.Random(3));
            const int day = 10; // January
            weather.ForceSegment(WeatherKind.Blizzard, day, 22 * 60, MinutesPerDay - 22 * 60);
            weather.AdvanceTo(day + 1, 0); // blizzard ends exactly at midnight → next-day half
            weather.ForceSegment(WeatherKind.Clear, day + 1, 0, 12 * 60);
            weather.AdvanceTo(day + 1, 8 * 60);
            Assert.AreEqual(WeatherHangover.BlizzardNextDayHalf, weather.Hangover);
            Assert.AreEqual(WeatherKind.Clear, weather.Kind);

            var snapshot = JsonRoundTrip(weather.CaptureSnapshot());
            var restored = new WeatherSystem(new System.Random(4));
            restored.RestoreSnapshot(snapshot, day + 1, 8 * 60);

            Assert.AreEqual(WeatherHangover.BlizzardNextDayHalf, restored.Hangover);
            Assert.AreEqual(weather.HangoverEndDayIndex, restored.HangoverEndDayIndex);
            Assert.AreEqual(weather.Kind, restored.Kind);
            Assert.AreEqual(weather.SegmentEndAbsoluteMinute, restored.SegmentEndAbsoluteMinute);
            Assert.AreEqual(WeatherSystem.HangoverNextDayStreet, restored.StreetTrafficMultiplier);

            // Hangover expires the day after, same as the source system.
            restored.AdvanceTo(day + 2, 8 * 60);
            Assert.AreEqual(WeatherHangover.None, restored.Hangover);
        }

        [Test]
        public void Round_trip_mid_blizzard_same_day_cleanup_preserves_state()
        {
            var weather = new WeatherSystem(new System.Random(5));
            const int day = 20;
            weather.ForceSegment(WeatherKind.Blizzard, day, 23 * 60 + 30, 60);
            weather.AdvanceTo(day + 1, 30);
            weather.ForceSegment(WeatherKind.Clear, day + 1, 30, 6 * 60);
            // Blizzard ended at 00:30 next day → segment end not at midnight → same-day cleanup on day+1.
            Assert.AreEqual(WeatherHangover.BlizzardSameDayCleanup, weather.Hangover);

            var restored = new WeatherSystem(new System.Random(6));
            restored.RestoreSnapshot(JsonRoundTrip(weather.CaptureSnapshot()), day + 1, 60);

            Assert.AreEqual(WeatherHangover.BlizzardSameDayCleanup, restored.Hangover);
            Assert.AreEqual(weather.HangoverEndDayIndex, restored.HangoverEndDayIndex);
            Assert.AreEqual(WeatherSystem.HangoverSameDayStreet, restored.StreetTrafficMultiplier);
        }

        [Test]
        public void Null_snapshot_restores_clear_fair_defaults()
        {
            var weather = new WeatherSystem(new System.Random(7));
            weather.ForceSegment(WeatherKind.Blizzard, 5, 10 * 60, 60);

            weather.RestoreSnapshot(null, 40, 9 * 60);

            Assert.AreEqual(WeatherKind.Clear, weather.Kind);
            Assert.AreEqual(WeatherHangover.None, weather.Hangover);
            Assert.AreEqual(0, weather.HangoverEndDayIndex);
            Assert.AreEqual(WeatherSystem.FairStreet, weather.StreetTrafficMultiplier);
            Assert.AreEqual(WeatherSystem.FairSpend, weather.ShopSpendMultiplier);
            Assert.Greater(weather.SegmentEndAbsoluteMinute, 40L * MinutesPerDay + 9 * 60);

            // Must not immediately re-roll on the next advance.
            weather.AdvanceTo(40, 9 * 60 + 5);
            Assert.AreEqual(WeatherKind.Clear, weather.Kind);
        }

        [Test]
        public void TryValidateSnapshot_rejects_bad_values()
        {
            Assert.IsTrue(WeatherSystem.TryValidateSnapshot(Valid(), out _));

            var bad = Valid();
            bad.kind = "Tornado";
            Assert.IsFalse(WeatherSystem.TryValidateSnapshot(bad, out _));

            bad = Valid();
            bad.hangover = "Nope";
            Assert.IsFalse(WeatherSystem.TryValidateSnapshot(bad, out _));

            bad = Valid();
            bad.segmentEndMinuteOfDay = MinutesPerDay;
            Assert.IsFalse(WeatherSystem.TryValidateSnapshot(bad, out _));

            bad = Valid();
            bad.segmentEndDayIndex = -1;
            Assert.IsFalse(WeatherSystem.TryValidateSnapshot(bad, out _));

            bad = Valid();
            bad.hangoverEndDayIndex = -1;
            Assert.IsFalse(WeatherSystem.TryValidateSnapshot(bad, out _));
        }

        [Test]
        public void Mapper_capture_includes_weather_and_validate_accepts_null_for_legacy_saves()
        {
            var go = new GameObject("WeatherSnapshot Test Tower");
            try
            {
                var simulation = go.AddComponent<TowerSimulation>();
                var weather = new WeatherSystem(new System.Random(8));
                weather.ForceSegment(WeatherKind.Thunderstorm, 200, 12 * 60, 75);
                SetField(simulation, "_weather", weather);

                var captured = simulation.Weather.CaptureSnapshot();
                Assert.AreEqual(nameof(WeatherKind.Thunderstorm), captured.kind);
                Assert.AreEqual(weather.SegmentEndDayIndex, captured.segmentEndDayIndex);
                Assert.AreEqual(weather.SegmentEndMinuteOfDay, captured.segmentEndMinuteOfDay);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

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
                weather = null
            };
            Assert.IsTrue(TowerSnapshotMapper.ValidateForRestore(snapshot).Success);

            snapshot.weather = new WeatherSnapshotV1 { kind = "Bogus", hangover = "None" };
            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(SnapshotValidationError.InvalidWeather, result.ErrorCode);
        }

        [Test]
        public void Legacy_json_without_weather_block_validates_and_restores_clear()
        {
            // JsonUtility materializes a missing [Serializable] field as an all-default object.
            var legacy = JsonUtility.FromJson<TowerSnapshotV1>("{\"schemaVersion\":1}");
            Assert.IsTrue(WeatherSystem.IsAbsent(legacy.weather));

            var weather = new WeatherSystem(new System.Random(9));
            weather.ForceSegment(WeatherKind.Rain, 3, 0, 60);
            weather.RestoreSnapshot(legacy.weather, 50, 0);

            Assert.AreEqual(WeatherKind.Clear, weather.Kind);
            Assert.AreEqual(WeatherHangover.None, weather.Hangover);
        }

        static WeatherSnapshotV1 Valid() => new WeatherSnapshotV1
        {
            kind = nameof(WeatherKind.Rain),
            segmentEndDayIndex = 100,
            segmentEndMinuteOfDay = 600,
            hangover = nameof(WeatherHangover.None),
            hangoverEndDayIndex = 0
        };

        static WeatherSnapshotV1 JsonRoundTrip(WeatherSnapshotV1 snapshot) =>
            JsonUtility.FromJson<WeatherSnapshotV1>(JsonUtility.ToJson(snapshot));

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
