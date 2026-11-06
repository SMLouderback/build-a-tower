using System;
using System.Collections;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class TowerSnapshotMapperTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var build in UnityEngine.Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(build.gameObject);

            GameSession.ResetForTests();
        }

        [TestCase(0)]
        [TestCase(123456)]
        public void RestoreBalance_restores_exact_non_negative_balance(int restoredBalance)
        {
            var wallet = new FundsWallet(50);

            wallet.RestoreBalance(restoredBalance);

            Assert.AreEqual(restoredBalance, wallet.Balance);
        }

        [Test]
        public void RestoreBalance_rejects_negative_without_changing_balance()
        {
            var wallet = new FundsWallet(50);

            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.RestoreBalance(-1));
            Assert.AreEqual(50, wallet.Balance);
        }

        [Test]
        public void Clock_capture_includes_complete_runtime_state()
        {
            var clock = new GameClock(2.5f, 1438);
            clock.AdvanceMinutes(2.75f);
            clock.Paused = true;

            var snapshot = clock.CaptureSnapshot();

            Assert.AreEqual(1, snapshot.dayIndex);
            Assert.AreEqual(0, snapshot.minuteOfDay);
            Assert.AreEqual(0.75f, snapshot.minuteAccumulator);
            Assert.AreEqual(2.5f, snapshot.minutesPerRealSecond);
            Assert.IsTrue(snapshot.paused);
        }

        [Test]
        public void Clock_restore_restores_complete_state_without_progression_events()
        {
            var clock = new GameClock();
            clock.Tick(1f);
            var dayRolls = 0;
            var monthRolls = 0;
            clock.DayRolled += () => dayRolls++;
            clock.MonthRolled += () => monthRolls++;

            clock.RestoreSnapshot(ValidClock(
                dayIndex: 31,
                minuteOfDay: 721,
                accumulator: 0.625f,
                speed: 12.5f,
                paused: true));

            Assert.AreEqual(31, clock.DayIndex);
            Assert.AreEqual(721, clock.MinuteOfDay);
            Assert.AreEqual(0.625f, clock.CaptureSnapshot().minuteAccumulator);
            Assert.AreEqual(12.5f, clock.MinutesPerRealSecond);
            Assert.IsTrue(clock.Paused);
            Assert.AreEqual(0f, clock.LastTickGameMinutes);
            Assert.AreEqual(0, dayRolls);
            Assert.AreEqual(0, monthRolls);
        }

        [Test]
        public void Clock_restore_rejects_null_without_changing_state()
        {
            var clock = SeedClock();
            var before = clock.CaptureSnapshot();

            Assert.Throws<ArgumentNullException>(() => clock.RestoreSnapshot(null));

            AssertClockUnchanged(clock, before);
        }

        static IEnumerable InvalidClockSnapshots()
        {
            yield return new TestCaseData(ValidClock(dayIndex: -1)).SetName("Clock_restore_rejects_negative_day");
            yield return new TestCaseData(ValidClock(minuteOfDay: -1)).SetName("Clock_restore_rejects_negative_minute");
            yield return new TestCaseData(ValidClock(minuteOfDay: 1440)).SetName("Clock_restore_rejects_minute_after_day");
            yield return new TestCaseData(ValidClock(accumulator: -0.01f)).SetName("Clock_restore_rejects_negative_accumulator");
            yield return new TestCaseData(ValidClock(accumulator: 1f)).SetName("Clock_restore_rejects_whole_accumulator");
            yield return new TestCaseData(ValidClock(accumulator: float.NaN)).SetName("Clock_restore_rejects_NaN_accumulator");
            yield return new TestCaseData(ValidClock(accumulator: float.PositiveInfinity)).SetName("Clock_restore_rejects_infinite_accumulator");
            yield return new TestCaseData(ValidClock(speed: 0f)).SetName("Clock_restore_rejects_zero_speed");
            yield return new TestCaseData(ValidClock(speed: -1f)).SetName("Clock_restore_rejects_negative_speed");
            yield return new TestCaseData(ValidClock(speed: float.NaN)).SetName("Clock_restore_rejects_NaN_speed");
            yield return new TestCaseData(ValidClock(speed: float.PositiveInfinity)).SetName("Clock_restore_rejects_infinite_speed");
        }

        [TestCaseSource(nameof(InvalidClockSnapshots))]
        public void Clock_restore_rejects_invalid_values_atomically(ClockSnapshotV1 invalid)
        {
            var clock = SeedClock();
            var before = clock.CaptureSnapshot();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.RestoreSnapshot(invalid));

            AssertClockUnchanged(clock, before);
        }

        [Test]
        public void Capture_maps_core_state_and_room_order()
        {
            GameSession.Difficulty = GameDifficulty.Hard;
            var build = CreateBuild(out var simulation);
            build.Wallet.RestoreBalance(765432);
            simulation.Clock.RestoreSnapshot(ValidClock(
                dayIndex: 12,
                minuteOfDay: 456,
                accumulator: 0.25f,
                speed: 10f,
                paused: true));
            simulation.Stars.ForceStars(3);
            var lobby = RoomType("lobby", isLobby: true);
            var room = RoomType("office");
            Assert.IsTrue(build.Grid.TryPlaceLobby(lobby, 0, 2, TowerGrid.LobbyFloor, out var first));
            Assert.IsTrue(build.Grid.TryPlace(room, new Vector2Int(1, 1), out var second));
            var modified = new DateTime(2026, 9, 8, 16, 30, 45, DateTimeKind.Local);

            var snapshot = TowerSnapshotMapper.Capture("save_01", "  Skyline Tower  ", build, simulation, modified);

            Assert.AreEqual(SaveSchema.CurrentVersion, snapshot.schemaVersion);
            Assert.AreEqual("save_01", snapshot.saveId);
            Assert.AreEqual("  Skyline Tower  ", snapshot.towerName);
            Assert.AreEqual(nameof(GameDifficulty.Hard), snapshot.difficulty);
            Assert.AreEqual(modified.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture), snapshot.modifiedUtc);
            Assert.AreEqual(765432, snapshot.walletBalance);
            Assert.AreEqual(3, snapshot.stars);
            Assert.AreEqual(12, snapshot.clock.dayIndex);
            Assert.AreEqual(456, snapshot.clock.minuteOfDay);
            Assert.AreEqual(0.25f, snapshot.clock.minuteAccumulator);
            Assert.AreEqual(10f, snapshot.clock.minutesPerRealSecond);
            Assert.IsTrue(snapshot.clock.paused);
            Assert.AreEqual(2, snapshot.rooms.Length);
            Assert.AreEqual(first.InstanceId, snapshot.rooms[0].instanceId);
            Assert.AreEqual(second.InstanceId, snapshot.rooms[1].instanceId);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("-bad")]
        [TestCase("bad.name")]
        [TestCase("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-x")]
        public void Capture_rejects_invalid_save_id(string saveId)
        {
            var build = CreateBuild(out var simulation);

            Assert.Throws<ArgumentException>(
                () => TowerSnapshotMapper.Capture(saveId, "Tower", build, simulation, DateTime.UtcNow));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Capture_rejects_blank_tower_name(string towerName)
        {
            var build = CreateBuild(out var simulation);

            Assert.Throws<ArgumentException>(
                () => TowerSnapshotMapper.Capture("save", towerName, build, simulation, DateTime.UtcNow));
        }

        [Test]
        public void Capture_rejects_tower_name_over_eighty_trimmed_characters()
        {
            var build = CreateBuild(out var simulation);

            Assert.Throws<ArgumentException>(
                () => TowerSnapshotMapper.Capture("save", " " + new string('x', 81) + " ", build, simulation, DateTime.UtcNow));
        }

        [Test]
        public void Capture_counts_unicode_text_elements_for_tower_name_limit()
        {
            var build = CreateBuild(out var simulation);
            var towerName = string.Concat(System.Linq.Enumerable.Repeat("\U0001F3E2", 80));

            var snapshot = TowerSnapshotMapper.Capture("save", towerName, build, simulation, DateTime.UtcNow);

            Assert.AreEqual(towerName, snapshot.towerName);
        }

        [Test]
        public void Capture_rejects_null_build_or_simulation()
        {
            var build = CreateBuild(out var simulation);

            Assert.Throws<ArgumentNullException>(
                () => TowerSnapshotMapper.Capture("save", "Tower", null, simulation, DateTime.UtcNow));
            Assert.Throws<ArgumentNullException>(
                () => TowerSnapshotMapper.Capture("save", "Tower", build, null, DateTime.UtcNow));
        }

        [TestCase("Grid")]
        [TestCase("Wallet")]
        public void Capture_rejects_missing_build_dependency(string propertyName)
        {
            var build = CreateBuild(out var simulation);
            SetAutoProperty(build, propertyName, null);

            Assert.Throws<InvalidOperationException>(
                () => TowerSnapshotMapper.Capture("save", "Tower", build, simulation, DateTime.UtcNow));
        }

        [TestCase("_clock")]
        [TestCase("_stars")]
        public void Capture_rejects_missing_simulation_dependency(string fieldName)
        {
            var build = CreateBuild(out var simulation);
            SetField(simulation, fieldName, null);

            Assert.Throws<InvalidOperationException>(
                () => TowerSnapshotMapper.Capture("save", "Tower", build, simulation, DateTime.UtcNow));
        }

        static IEnumerable InvalidSnapshots()
        {
            yield return Invalid(null, SnapshotValidationError.NullSnapshot);
            yield return Invalid(s => s.schemaVersion = 0, SnapshotValidationError.UnsupportedSchema);
            yield return Invalid(s => s.schemaVersion = 2, SnapshotValidationError.UnsupportedSchema);
            yield return Invalid(s => s.saveId = "-bad", SnapshotValidationError.InvalidSaveId);
            yield return Invalid(s => s.towerName = " ", SnapshotValidationError.InvalidTowerName);
            yield return Invalid(s => s.modifiedUtc = "not-a-date", SnapshotValidationError.InvalidModifiedUtc);
            yield return Invalid(s => s.modifiedUtc = "2026-09-08T12:00:00.0000000", SnapshotValidationError.InvalidModifiedUtc);
            yield return Invalid(s => s.difficulty = "normal", SnapshotValidationError.InvalidDifficulty);
            yield return Invalid(s => s.difficulty = "Unknown", SnapshotValidationError.InvalidDifficulty);
            yield return Invalid(s => s.walletBalance = -1, SnapshotValidationError.InvalidWalletBalance);
            yield return Invalid(s => s.clock = null, SnapshotValidationError.InvalidClock);
            yield return Invalid(s => s.clock.dayIndex = -1, SnapshotValidationError.InvalidClock);
            yield return Invalid(s => s.clock.minuteOfDay = 1440, SnapshotValidationError.InvalidClock);
            yield return Invalid(s => s.clock.minuteAccumulator = float.NaN, SnapshotValidationError.InvalidClock);
            yield return Invalid(s => s.clock.minutesPerRealSecond = 0f, SnapshotValidationError.InvalidClock);
            yield return Invalid(s => s.stars = -1, SnapshotValidationError.InvalidStars);
            yield return Invalid(s => s.stars = StarSystem.MaxStars + 1, SnapshotValidationError.InvalidStars);
            yield return Invalid(s => s.rooms = null, SnapshotValidationError.NullRooms);
        }

        [TestCaseSource(nameof(InvalidSnapshots))]
        public void ValidateForRestore_returns_typed_failure_for_invalid_core_data(
            TowerSnapshotV1 snapshot,
            SnapshotValidationError expectedError)
        {
            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expectedError, result.ErrorCode);
            Assert.IsNotEmpty(result.ErrorMessage);
        }

        [Test]
        public void ValidateForRestore_accepts_minimal_valid_schema_one_snapshot()
        {
            var result = TowerSnapshotMapper.ValidateForRestore(ValidSnapshot());

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(SnapshotValidationError.None, result.ErrorCode);
        }

        [Test]
        public void Research_capture_and_restore_round_trips_completed_and_active_progress()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Marketing, 1));
            research.TickProgress(700f, researcherPool: 2);
            Assert.IsTrue(research.TryStart(ResearchBranch.Elevator, 1));
            research.TickProgress(2000f, researcherPool: 4);
            Assert.IsTrue(research.IsComplete(ResearchBranch.Elevator, 1));
            Assert.IsTrue(research.TryStart(ResearchBranch.Elevator, 2));
            research.TickProgress(100f, researcherPool: 1);
            research.Pause();

            var snapshot = research.CaptureSnapshot();
            var restored = new ResearchSystem();
            restored.RestoreSnapshot(snapshot);

            Assert.IsTrue(restored.IsComplete(ResearchBranch.Elevator, 1));
            Assert.IsFalse(restored.IsComplete(ResearchBranch.Marketing, 1));
            Assert.AreEqual(ResearchBranch.Elevator, restored.ActiveBranch);
            Assert.AreEqual(2, restored.ActiveLevel);
            Assert.IsTrue(restored.IsPaused);
            Assert.AreEqual(
                research.GetNodeProgress(ResearchBranch.Marketing, 1),
                restored.GetNodeProgress(ResearchBranch.Marketing, 1),
                0.001f);
            Assert.AreEqual(
                research.GetNodeProgress(ResearchBranch.Elevator, 2),
                restored.GetNodeProgress(ResearchBranch.Elevator, 2),
                0.001f);
        }

        [Test]
        public void Capture_includes_research_progression_from_simulation()
        {
            var build = CreateBuild(out var simulation);
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Security, 1));
            research.TickProgress(50f, researcherPool: 1);
            SetField(simulation, "_research", research);

            var snapshot = TowerSnapshotMapper.Capture("save", "Tower", build, simulation, DateTime.UtcNow);

            Assert.IsNotNull(snapshot.research);
            Assert.AreEqual(nameof(ResearchBranch.Security), snapshot.research.activeBranch);
            Assert.AreEqual(1, snapshot.research.activeLevel);
            Assert.AreEqual(1, snapshot.research.progress.Length);
            Assert.Greater(snapshot.research.progress[0].workMinutes, 0f);
        }

        [Test]
        public void ValidateForRestore_accepts_null_research_for_legacy_saves()
        {
            var snapshot = ValidSnapshot();
            snapshot.research = null;

            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);

            Assert.IsTrue(result.Success, result.ErrorMessage);
        }

        [Test]
        public void ValidateForRestore_rejects_invalid_research_active_branch()
        {
            var snapshot = ValidSnapshot();
            snapshot.research = new ResearchSnapshotV1
            {
                completed = Array.Empty<ResearchCompletedNodeV1>(),
                progress = Array.Empty<ResearchProgressNodeV1>(),
                activeBranch = "NotABranch",
                activeLevel = 1,
                paused = false
            };

            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SnapshotValidationError.InvalidResearch, result.ErrorCode);
        }

        [Test]
        public void ValidateForRestore_accepts_zero_offset_round_trip_timestamp()
        {
            var snapshot = ValidSnapshot();
            snapshot.modifiedUtc = "2026-09-08T16:30:45.0000000+00:00";

            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);

            Assert.IsTrue(result.Success, result.ErrorMessage);
        }

        static TestCaseData Invalid(Action<TowerSnapshotV1> mutate, SnapshotValidationError error)
        {
            var snapshot = mutate == null ? null : ValidSnapshot();
            mutate?.Invoke(snapshot);
            return new TestCaseData(snapshot, error);
        }

        static TowerSnapshotV1 ValidSnapshot()
        {
            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = "save-1",
                towerName = "Tower",
                difficulty = nameof(GameDifficulty.Normal),
                modifiedUtc = "2026-09-08T16:30:45.0000000Z",
                walletBalance = 0,
                stars = 0,
                clock = ValidClock(),
                rooms = Array.Empty<RoomSnapshotV1>()
            };
        }

        static ClockSnapshotV1 ValidClock(
            int dayIndex = 1,
            int minuteOfDay = 360,
            float accumulator = 0.5f,
            float speed = 1f,
            bool paused = false)
        {
            return new ClockSnapshotV1
            {
                dayIndex = dayIndex,
                minuteOfDay = minuteOfDay,
                minuteAccumulator = accumulator,
                minutesPerRealSecond = speed,
                paused = paused
            };
        }

        static GameClock SeedClock()
        {
            var clock = new GameClock(2f, 500);
            clock.Tick(1.25f);
            clock.Paused = true;
            return clock;
        }

        static void AssertClockUnchanged(GameClock clock, ClockSnapshotV1 before)
        {
            var after = clock.CaptureSnapshot();
            Assert.AreEqual(before.dayIndex, after.dayIndex);
            Assert.AreEqual(before.minuteOfDay, after.minuteOfDay);
            Assert.AreEqual(before.minuteAccumulator, after.minuteAccumulator);
            Assert.AreEqual(before.minutesPerRealSecond, after.minutesPerRealSecond);
            Assert.AreEqual(before.paused, after.paused);
        }

        static BuildController CreateBuild(out TowerSimulation simulation)
        {
            var gameObject = new GameObject("Task5 Test Tower");
            var build = gameObject.AddComponent<BuildController>();
            SetAutoProperty(build, "Grid", new TowerGrid());
            SetAutoProperty(build, "Wallet", new FundsWallet(100));
            simulation = gameObject.AddComponent<TowerSimulation>();
            SetField(simulation, "_clock", new GameClock());
            SetField(simulation, "_stars", new StarSystem());
            return build;
        }

        static RoomTypeSO RoomType(string id, bool isLobby = false)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = id;
            type.isLobby = isLobby;
            type.size = Vector2Int.one;
            type.allowAboveGround = true;
            return type;
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing test setup field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
