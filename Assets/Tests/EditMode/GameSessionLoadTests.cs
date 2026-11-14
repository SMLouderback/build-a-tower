using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BuildATower.Tests
{
    public sealed class GameSessionLoadTests
    {
        const string SaveId = "tower-a";
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "bat-task7-" + Guid.NewGuid().ToString("N"));
            GameSession.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var build in UnityEngine.Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(build.gameObject);

            foreach (var backdrop in UnityEngine.Object.FindObjectsByType<ParallaxBackdrop>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(backdrop.gameObject);

            GameSession.ResetForTests();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void PrepareLoad_sets_pending_snapshot_and_case_sensitive_difficulty()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));

            var result = GameSession.PrepareLoad(snapshot);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreSame(snapshot, GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
            Assert.IsNull(GameSession.LastLoadError);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("hard")]
        [TestCase("HARD")]
        [TestCase("Unknown")]
        [TestCase("0")]
        [TestCase("2")]
        [TestCase("Sandbox ")]
        public void PrepareLoad_rejects_invalid_difficulty_without_mutating_session(string difficulty)
        {
            GameSession.StartNewGame(GameDifficulty.Easy);
            var snapshot = ValidSnapshot(difficulty);

            var result = GameSession.PrepareLoad(snapshot);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SessionLoadError.InvalidDifficulty, result.Error);
            Assert.IsNotEmpty(result.ErrorMessage);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);
            Assert.IsNull(GameSession.LastLoadError);
        }

        [Test]
        public void PrepareLoad_rejects_null_snapshot_without_mutating_session()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);

            var result = GameSession.PrepareLoad(null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SessionLoadError.NullSnapshot, result.Error);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Sandbox, GameSession.Difficulty);
        }

        [Test]
        public void PrepareLoad_rejects_a_second_load_and_keeps_the_original_snapshot()
        {
            var first = ValidSnapshot(nameof(GameDifficulty.Hard));
            var second = ValidSnapshot(nameof(GameDifficulty.Easy));
            second.saveId = "tower-b";
            Assert.IsTrue(GameSession.PrepareLoad(first).Success);

            var result = GameSession.PrepareLoad(second);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SessionLoadError.LoadAlreadyPending, result.Error);
            Assert.AreSame(first, GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
            Assert.AreEqual("tower-a", GameSession.PendingLoad.saveId);
        }

        [Test]
        public void CompleteLoad_clears_pending_and_error_but_keeps_difficulty()
        {
            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(nameof(GameDifficulty.Extreme))).Success);

            GameSession.CompleteLoad();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNull(GameSession.LastLoadError);
            Assert.AreEqual(GameDifficulty.Extreme, GameSession.Difficulty);
        }

        [Test]
        public void FailLoad_records_a_typed_safe_error_and_clears_pending()
        {
            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(nameof(GameDifficulty.Hard))).Success);

            GameSession.FailLoad(
                SessionLoadError.GridRestoreFailed,
                "The tower could not be restored from the save.");

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNotNull(GameSession.LastLoadError);
            Assert.IsFalse(GameSession.LastLoadError.Success);
            Assert.AreEqual(SessionLoadError.GridRestoreFailed, GameSession.LastLoadError.Error);
            Assert.AreEqual(
                "The tower could not be restored from the save.",
                GameSession.LastLoadError.ErrorMessage);
            StringAssert.DoesNotContain("evil", GameSession.LastLoadError.ErrorMessage);
        }

        [Test]
        public void ResetForTests_clears_difficulty_pending_and_load_error()
        {
            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(nameof(GameDifficulty.Hard))).Success);
            GameSession.FailLoad(SessionLoadError.GridRestoreFailed, "The tower could not be restored from the save.");

            GameSession.ResetForTests();

            Assert.IsFalse(GameSession.HasDifficulty);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNull(GameSession.LastLoadError);
        }

        [Test]
        public void StartNewGame_clears_pending_and_error_and_keeps_chosen_difficulty()
        {
            GameSession.FailLoad(
                SessionLoadError.GridRestoreFailed,
                "The tower could not be restored from the save.");
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 7, width: 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out _, withLobby: true);

            GameSession.StartNewGame(GameDifficulty.Easy);
            build.InitializeTower();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNull(GameSession.LastLoadError);
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);
            Assert.AreEqual(0, build.Grid.Rooms.Count);
            Assert.AreEqual(DifficultyProfile.StartingFunds(GameDifficulty.Easy), build.Wallet.Balance);
        }

        [Test]
        public void Coordinator_constructor_rejects_null_dependencies()
        {
            var build = CreateInactiveTower(out var simulation);
            var repository = new LocalSaveRepository(root);

            Assert.Throws<ArgumentNullException>(() => new SaveCoordinator(null, build, simulation));
            Assert.Throws<ArgumentNullException>(() => new SaveCoordinator(repository, null, simulation));
            Assert.Throws<ArgumentNullException>(() => new SaveCoordinator(repository, build, null));
        }

        [Test]
        public void PrepareLocalLoad_leaves_the_exact_deserialized_snapshot_pending()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 4242;
            Assert.IsTrue(repository.Save(SaveId, snapshot).Success);
            var coordinator = new SaveCoordinator(repository, CreateInactiveTower(out var simulation), simulation);

            var result = coordinator.PrepareLocalLoad(SaveId);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreSame(result.Snapshot, GameSession.PendingLoad);
            Assert.AreEqual(SaveId, GameSession.PendingLoad.saveId);
            Assert.AreEqual(4242, GameSession.PendingLoad.walletBalance);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
        }

        [Test]
        public void PrepareLocalLoad_rejects_invalid_core_data_without_touching_the_file_or_session()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = -1;
            Assert.IsTrue(repository.Save(SaveId, snapshot).Success);
            GameSession.StartNewGame(GameDifficulty.Easy);
            var before = FileFingerprints();
            var coordinator = new SaveCoordinator(repository, CreateInactiveTower(out var simulation), simulation);

            var result = coordinator.PrepareLocalLoad(SaveId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidPayload, result.Error);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);
            Assert.AreEqual(before, FileFingerprints());
        }

        [Test]
        public void PrepareLocalLoad_rejects_corrupt_unsupported_not_found_and_already_pending_without_mutation()
        {
            var repository = new LocalSaveRepository(root);
            var coordinator = new SaveCoordinator(repository, CreateInactiveTower(out var simulation), simulation);
            GameSession.StartNewGame(GameDifficulty.Easy);

            var missing = coordinator.PrepareLocalLoad(SaveId);
            Assert.AreEqual(SaveReadError.NotFound, missing.Error);
            Assert.IsNull(GameSession.PendingLoad);

            Directory.CreateDirectory(root);
            File.WriteAllText(CurrentPath(SaveId), "{not-json");
            var corruptBefore = FileFingerprints();
            var corrupt = coordinator.PrepareLocalLoad(SaveId);
            Assert.AreEqual(SaveReadError.InvalidEnvelope, corrupt.Error);
            Assert.AreEqual(corruptBefore, FileFingerprints());
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);

            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(nameof(GameDifficulty.Hard))).Success);
            var saveText = File.ReadAllText(CurrentPath(SaveId));
            File.WriteAllText(
                CurrentPath(SaveId),
                saveText.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            var unsupportedBefore = FileFingerprints();
            var unsupportedResult = coordinator.PrepareLocalLoad(SaveId);
            Assert.AreEqual(SaveReadError.UnsupportedSchema, unsupportedResult.Error);
            Assert.AreEqual(unsupportedBefore, FileFingerprints());
            Assert.IsNull(GameSession.PendingLoad);

            var first = ValidSnapshot(nameof(GameDifficulty.Hard));
            first.saveId = "tower-b";
            Assert.IsTrue(repository.Save("tower-b", first).Success);
            Assert.IsTrue(coordinator.PrepareLocalLoad("tower-b").Success);
            var pending = GameSession.PendingLoad;
            var second = ValidSnapshot(nameof(GameDifficulty.Easy));
            Assert.IsTrue(repository.Save(SaveId, second).Success);
            var pendingBefore = FileFingerprints();
            var alreadyPending = coordinator.PrepareLocalLoad(SaveId);
            Assert.AreEqual(SaveReadError.AlreadyPending, alreadyPending.Error);
            Assert.AreSame(pending, GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
            Assert.AreEqual(pendingBefore, FileFingerprints());
        }

        [Test]
        public void SaveCurrent_refuses_until_build_and_simulation_are_initialized()
        {
            var build = CreateInactiveTower(out var simulation);
            var coordinator = new SaveCoordinator(new LocalSaveRepository(root), build, simulation);

            var result = coordinator.SaveCurrent(SaveId, "Skyline");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.NotInitialized, result.Error);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsFalse(Directory.Exists(root), "A refused save must not create the repository root.");
        }

        [Test]
        public void SaveCurrent_treats_a_destroyed_tower_as_uninitialized()
        {
            var build = CreateInactiveTower(out var simulation);
            build.InitializeTower();
            simulation.InitializeSimulation();
            var coordinator = new SaveCoordinator(new LocalSaveRepository(root), build, simulation);
            UnityEngine.Object.DestroyImmediate(build.gameObject);

            var result = coordinator.SaveCurrent(SaveId, "Skyline");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.NotInitialized, result.Error);
            Assert.IsFalse(Directory.Exists(root));
        }

        [Test]
        public void SaveCurrent_captures_current_state_and_preserves_caller_id_and_name()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var build = CreateInactiveTower(out var simulation);
            build.InitializeTower();
            simulation.InitializeSimulation();
            build.Wallet.RestoreBalance(88_000);
            simulation.Stars.ForceStars(2);
            var coordinator = new SaveCoordinator(new LocalSaveRepository(root), build, simulation);

            var result = coordinator.SaveCurrent(SaveId, "  Harbor Tower  ");

            Assert.IsTrue(result.Success, result.ErrorMessage);
            var loaded = new LocalSaveRepository(root).Read(SaveId);
            Assert.IsTrue(loaded.Success, loaded.ErrorMessage);
            Assert.AreEqual(SaveId, loaded.Snapshot.saveId);
            Assert.AreEqual("  Harbor Tower  ", loaded.Snapshot.towerName);
            Assert.AreEqual(nameof(GameDifficulty.Hard), loaded.Snapshot.difficulty);
            Assert.AreEqual(88_000, loaded.Snapshot.walletBalance);
            Assert.AreEqual(2, loaded.Snapshot.stars);
        }

        [Test]
        public void SaveCurrent_returns_typed_capture_failure_without_mutating_session_or_files()
        {
            var build = CreateInactiveTower(out var simulation);
            build.InitializeTower();
            simulation.InitializeSimulation();
            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(nameof(GameDifficulty.Easy))).Success);
            var coordinator = new SaveCoordinator(new LocalSaveRepository(root), build, simulation);

            var result = coordinator.SaveCurrent(SaveId, "   ");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.CaptureFailed, result.Error);
            Assert.IsNotNull(GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);
            Assert.IsFalse(Directory.Exists(root));
        }

        [Test]
        public void InitializeTower_restores_grid_and_wallet_before_simulation_and_adds_components_once()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 7, width: 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out var simulation, withLobby: true);

            build.InitializeTower();

            Assert.AreSame(snapshot, GameSession.PendingLoad);
            Assert.AreEqual(1, build.Grid.Rooms.Count);
            Assert.AreEqual(7, build.Grid.Rooms[0].InstanceId);
            Assert.AreEqual("lobby", build.Grid.Rooms[0].Type.id);
            Assert.AreEqual(250_000, build.Wallet.Balance);
            Assert.IsNull(simulation.Clock);
            Assert.AreEqual(1, build.GetComponents<TowerSimulation>().Length);
            Assert.AreEqual(1, build.GetComponents<TowerMapController>().Length);
            build.InitializeTower();
            Assert.AreEqual(1, build.GetComponents<TowerSimulation>().Length);
            Assert.AreEqual(1, build.GetComponents<TowerMapController>().Length);
            Assert.AreEqual(1, GridChangedSubscriberCount(build));
        }

        [Test]
        public void InitializeTower_falls_back_to_a_clean_tower_when_restore_fails()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.rooms = new[]
            {
                new RoomSnapshotV1
                {
                    instanceId = 1,
                    roomTypeId = "office_of_the_future",
                    originX = 0,
                    originY = 1,
                    width = 1,
                    height = 1
                }
            };
            Assert.IsTrue(repository.Save(SaveId, snapshot).Success);
            var before = File.ReadAllBytes(CurrentPath(SaveId));
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out _);

            build.InitializeTower();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNotNull(GameSession.LastLoadError);
            Assert.AreEqual(SessionLoadError.GridRestoreFailed, GameSession.LastLoadError.Error);
            Assert.AreEqual(GridRestoreError.UnknownRoomTypeId, GameSession.LastLoadError.RestoreError);
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.AreEqual(0, build.Grid.Rooms.Count);
            Assert.AreEqual(DifficultyProfile.StartingFunds(GameDifficulty.Normal), build.Wallet.Balance);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(CurrentPath(SaveId)));
            AssertSafeFallbackNotice("office_of_the_future", CurrentPath(SaveId));
        }

        [Test]
        public void InitializeTower_maps_invalid_wallet_to_clean_normal_fallback()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = -1;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 7, width: 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out _, withLobby: true);

            Assert.DoesNotThrow(() => build.InitializeTower());

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNotNull(GameSession.LastLoadError);
            Assert.AreEqual(SessionLoadError.WalletRestoreFailed, GameSession.LastLoadError.Error);
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.AreEqual(0, build.Grid.Rooms.Count);
            Assert.AreEqual(DifficultyProfile.StartingFunds(GameDifficulty.Normal), build.Wallet.Balance);
            AssertSafeFallbackNotice();
        }

        [Test]
        public void InitializeTower_maps_registry_create_failure_to_clean_fallback()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 7, width: 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out _);
            var blankLobby = LobbyType();
            blankLobby.id = "   ";
            SetField(build, "lobbyType", blankLobby);

            Assert.DoesNotThrow(() => build.InitializeTower());

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNotNull(GameSession.LastLoadError);
            Assert.AreEqual(SessionLoadError.RoomTypeRegistryFailed, GameSession.LastLoadError.Error);
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.AreEqual(0, build.Grid.Rooms.Count);
            Assert.AreEqual(DifficultyProfile.StartingFunds(GameDifficulty.Normal), build.Wallet.Balance);
            AssertSafeFallbackNotice();
        }

        [Test]
        public void New_game_initialization_is_unchanged_when_nothing_is_pending()
        {
            var build = CreateInactiveTower(out var simulation);

            build.InitializeTower();
            simulation.InitializeSimulation();
            LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
            simulation.BeginPlay();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.AreEqual(0, build.Grid.Rooms.Count);
            Assert.AreEqual(DifficultyProfile.StartingFunds(GameDifficulty.Normal), build.Wallet.Balance);
            Assert.AreEqual(0, simulation.Clock.DayIndex);
            Assert.AreEqual(6 * 60, simulation.Clock.MinuteOfDay);
            Assert.AreEqual(0, simulation.Stars.CurrentStars);
            Assert.AreEqual(1, build.GetComponents<TowerSimulation>().Length);
            Assert.IsFalse(GameSession.HasRestoreFallbackNotice);
        }

        [Test]
        public void Simulation_restores_clock_and_stars_before_subscriptions_and_completes_after_rebuild()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 9;
            snapshot.stars = 3;
            snapshot.clock = new ClockSnapshotV1
            {
                dayIndex = 11,
                minuteOfDay = 722,
                minuteAccumulator = 0.25f,
                minutesPerRealSecond = 8f,
                paused = true
            };
            snapshot.rooms = new[] { LobbyRoom(instanceId: 3, width: 4) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out var simulation, withLobby: true);
            var dayRolls = 0;
            var monthRolls = 0;

            build.InitializeTower();
            simulation.InitializeSimulation();
            simulation.Clock.DayRolled += () => dayRolls++;
            simulation.Clock.MonthRolled += () => monthRolls++;

            Assert.AreSame(snapshot, GameSession.PendingLoad);
            Assert.AreEqual(11, simulation.Clock.DayIndex);
            Assert.AreEqual(722, simulation.Clock.MinuteOfDay);
            Assert.AreEqual(0.25f, simulation.Clock.CaptureSnapshot().minuteAccumulator);
            Assert.AreEqual(8f, simulation.Clock.MinutesPerRealSecond);
            Assert.IsTrue(simulation.Clock.Paused);
            Assert.AreEqual(3, simulation.Stars.CurrentStars);
            Assert.AreEqual(0, simulation.Stars.PendingChanges.Count);

            simulation.BeginPlay();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNull(GameSession.LastLoadError);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
            Assert.AreEqual(3, simulation.Stars.CurrentStars);
            Assert.AreEqual(0, simulation.Stars.PendingChanges.Count);
            Assert.AreEqual(0, dayRolls);
            Assert.AreEqual(0, monthRolls);
            Assert.IsFalse(GameSession.HasRestoreFallbackNotice);
        }

        [Test]
        public void Simulation_restore_failure_clears_pending_and_leaves_clean_normal_tower()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.stars = 3;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 7, width: 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            snapshot.clock.dayIndex = -1;
            var build = CreateInactiveTower(out var simulation, withLobby: true);
            var dayRolls = 0;
            var monthRolls = 0;

            build.InitializeTower();
            Assert.AreEqual(1, build.Grid.Rooms.Count);
            simulation.InitializeSimulation();
            simulation.Clock.DayRolled += () => dayRolls++;
            simulation.Clock.MonthRolled += () => monthRolls++;
            LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
            simulation.BeginPlay();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNotNull(GameSession.LastLoadError);
            Assert.AreEqual(SessionLoadError.SimulationRestoreFailed, GameSession.LastLoadError.Error);
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.AreEqual(0, build.Grid.Rooms.Count);
            Assert.AreEqual(DifficultyProfile.StartingFunds(GameDifficulty.Normal), build.Wallet.Balance);
            Assert.AreEqual(0, simulation.Clock.DayIndex);
            Assert.AreEqual(6 * 60, simulation.Clock.MinuteOfDay);
            Assert.AreEqual(0, simulation.Stars.CurrentStars);
            Assert.AreEqual(0, simulation.Stars.PendingChanges.Count);
            Assert.AreEqual(0, dayRolls);
            Assert.AreEqual(0, monthRolls);
            AssertSafeFallbackNotice();
        }

        [Test]
        public void Build_stage_fallback_notice_survives_hud_bind_until_dismissed()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.rooms = new[]
            {
                new RoomSnapshotV1
                {
                    instanceId = 1,
                    roomTypeId = "office_of_the_future",
                    originX = 0,
                    originY = 1,
                    width = 1,
                    height = 1
                }
            };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out var simulation);

            build.InitializeTower();
            var presenter = new RestoreFallbackPresenter();
            var hud = build.gameObject.AddComponent<TowerHudController>();
            SetField(hud, "build", build);
            SetField(hud, "simulation", simulation);
            var repository = new LocalSaveRepository(root);
            hud.BindPauseSave(new SaveCoordinator(repository, build, simulation), repository);

            Assert.IsTrue(presenter.HasNotice);
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, presenter.Message);
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, hud.RestoreFallbackNoticeText);
            Assert.IsTrue(hud.HasRestoreFallbackNotice);
            StringAssert.DoesNotContain("office_of_the_future", presenter.Message);

            presenter.Dismiss();

            Assert.IsFalse(presenter.HasNotice);
            Assert.IsFalse(hud.HasRestoreFallbackNotice);
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.RestoreFallbackNotice));
        }

        [Test]
        public void Simulation_stage_fallback_notice_is_peekable_until_consumed()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 250_000;
            snapshot.stars = 3;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 7, width: 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            snapshot.clock.dayIndex = -1;
            var build = CreateInactiveTower(out var simulation, withLobby: true);

            build.InitializeTower();
            simulation.InitializeSimulation();
            LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
            simulation.BeginPlay();

            var presenter = new RestoreFallbackPresenter();
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, presenter.Peek());
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, presenter.Peek());
            Assert.IsTrue(GameSession.HasRestoreFallbackNotice);

            var consumed = GameSession.ConsumeRestoreFallbackNotice();
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, consumed);
            Assert.IsFalse(GameSession.HasRestoreFallbackNotice);
            Assert.IsTrue(string.IsNullOrEmpty(presenter.Peek()));
        }

        [Test]
        public void Successful_startup_does_not_publish_a_restore_fallback_notice()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 9;
            snapshot.rooms = new[] { LobbyRoom(instanceId: 3, width: 4) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out var simulation, withLobby: true);

            build.InitializeTower();
            simulation.InitializeSimulation();
            simulation.BeginPlay();

            Assert.IsNull(GameSession.LastLoadError);
            Assert.IsFalse(GameSession.HasRestoreFallbackNotice);
            Assert.IsTrue(string.IsNullOrEmpty(new RestoreFallbackPresenter().Message));
        }

        [Test]
        public void Auto_added_simulation_restores_in_order_and_completes()
        {
            var snapshot = ValidSnapshot(nameof(GameDifficulty.Hard));
            snapshot.walletBalance = 88_000;
            snapshot.stars = 2;
            snapshot.clock = new ClockSnapshotV1
            {
                dayIndex = 4,
                minuteOfDay = 700,
                minuteAccumulator = 0.125f,
                minutesPerRealSecond = 3f,
                paused = true
            };
            snapshot.rooms = new[] { LobbyRoom(instanceId: 9, width: 6) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out var preattached, withLobby: true, attachSimulation: false);
            Assert.IsNull(preattached);

            build.InitializeTower();
            var simulation = build.GetComponent<TowerSimulation>();
            Assert.IsNotNull(simulation);
            Assert.AreEqual(1, build.GetComponents<TowerSimulation>().Length);
            Assert.AreEqual(88_000, build.Wallet.Balance);
            Assert.AreEqual(9, build.Grid.Rooms[0].InstanceId);
            Assert.IsNull(simulation.Clock);
            Assert.AreSame(snapshot, GameSession.PendingLoad);

            simulation.InitializeSimulation();
            Assert.AreEqual(4, simulation.Clock.DayIndex);
            Assert.AreEqual(700, simulation.Clock.MinuteOfDay);
            Assert.AreEqual(2, simulation.Stars.CurrentStars);
            Assert.AreSame(snapshot, GameSession.PendingLoad);

            simulation.BeginPlay();
            Assert.IsNull(GameSession.PendingLoad);
            Assert.IsNull(GameSession.LastLoadError);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
            Assert.IsFalse(GameSession.HasRestoreFallbackNotice);
        }

        static void AssertSafeFallbackNotice(params string[] forbiddenTokens)
        {
            var presenter = new RestoreFallbackPresenter();
            Assert.IsTrue(GameSession.HasRestoreFallbackNotice);
            Assert.IsTrue(presenter.HasNotice);
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, GameSession.RestoreFallbackNotice);
            Assert.AreEqual(RestoreFallbackPresenter.PlayerMessage, presenter.Message);
            StringAssert.DoesNotContain("\\", presenter.Message);
            StringAssert.DoesNotContain("/", presenter.Message);
            StringAssert.DoesNotContain("exception", presenter.Message.ToLowerInvariant());
            foreach (var token in forbiddenTokens)
            {
                if (!string.IsNullOrEmpty(token))
                    StringAssert.DoesNotContain(token, presenter.Message);
            }
        }

        static BuildController CreateInactiveTower(
            out TowerSimulation simulation,
            bool withLobby = false,
            bool attachSimulation = true)
        {
            var gameObject = new GameObject("Task7 Tower");
            gameObject.SetActive(false);
            var build = gameObject.AddComponent<BuildController>();
            if (withLobby)
                SetField(build, "lobbyType", LobbyType());
            simulation = attachSimulation ? gameObject.AddComponent<TowerSimulation>() : null;
            return build;
        }

        static RoomTypeSO LobbyType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "lobby";
            type.displayName = "Lobby";
            type.category = RoomCategory.Structure;
            type.size = Vector2Int.one;
            type.isLobby = true;
            type.allowAboveGround = true;
            return type;
        }

        static RoomSnapshotV1 LobbyRoom(int instanceId, int width)
        {
            return new RoomSnapshotV1
            {
                instanceId = instanceId,
                roomTypeId = "lobby",
                originX = 0,
                originY = TowerGrid.LobbyFloor,
                width = width,
                height = 1
            };
        }

        static TowerSnapshotV1 ValidSnapshot(string difficulty)
        {
            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = SaveId,
                towerName = "Skyline",
                difficulty = difficulty,
                modifiedUtc = "2026-09-08T16:30:45.0000000Z",
                walletBalance = 0,
                stars = 0,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 1,
                    minuteOfDay = 360,
                    minuteAccumulator = 0.5f,
                    minutesPerRealSecond = 1f
                },
                rooms = Array.Empty<RoomSnapshotV1>()
            };
        }

        static int GridChangedSubscriberCount(BuildController build)
        {
            var field = typeof(BuildController).GetField(
                "GridChanged",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var handler = (Action)field.GetValue(build);
            return handler == null ? 0 : handler.GetInvocationList().Length;
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }

        string CurrentPath(string saveId)
        {
            return Path.Combine(root, saveId + LocalSaveRepository.CurrentExtension);
        }

        string FileFingerprints()
        {
            if (!Directory.Exists(root))
                return string.Empty;

            var paths = Directory.GetFiles(root);
            Array.Sort(paths, StringComparer.Ordinal);
            var fingerprint = new StringBuilder();
            foreach (var path in paths)
            {
                var info = new FileInfo(path);
                fingerprint
                    .Append(info.Name)
                    .Append('|')
                    .Append(info.Length)
                    .Append('|')
                    .Append(info.LastWriteTimeUtc.Ticks)
                    .Append(';');
            }

            return fingerprint.ToString();
        }
    }
}
