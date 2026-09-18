using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BuildATower.Tests
{
    public sealed class MainMenuLoadFlowTests
    {
        const string SaveId = "tower-a";
        const string DefaultTowerName = "My Tower";
        static readonly Regex SafeGeneratedId = new Regex(
            "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$",
            RegexOptions.CultureInvariant);
        static readonly TimeZoneInfo DisplayZone = TimeZoneInfo.CreateCustomTimeZone(
            "bat-task8",
            TimeSpan.FromHours(-5),
            "BAT Task 8",
            "BAT Task 8");

        string root;
        int sceneLoads;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "bat-task8-" + Guid.NewGuid().ToString("N"));
            sceneLoads = 0;
            GameSession.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var build in UnityEngine.Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(build.gameObject);

            foreach (var menu in UnityEngine.Object.FindObjectsByType<MainMenuController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(menu.gameObject);

            foreach (var hud in UnityEngine.Object.FindObjectsByType<TowerHudController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(hud.gameObject);

            GameSession.ResetForTests();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void RefreshLocalSaves_returns_empty_when_no_saves_exist()
        {
            var menu = CreateMenu();

            var summaries = menu.RefreshLocalSaves();

            Assert.AreEqual(0, summaries.Count);
            Assert.AreEqual(0, sceneLoads);
        }

        [Test]
        public void RefreshLocalSaves_returns_repository_order_and_semantic_fields()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save("older", ValidSnapshot("older", "2026-01-01T00:00:00.0000000Z", 11)).Success);
            Assert.IsTrue(repository.Save("newest", ValidSnapshot("newest", "2026-03-03T12:00:00.0000000Z", 22)).Success);
            Assert.IsTrue(repository.Save("middle", ValidSnapshot("middle", "2026-02-02T00:00:00.0000000Z", 33)).Success);
            var menu = CreateMenu(repository);

            var summaries = menu.RefreshLocalSaves();

            Assert.AreEqual(3, summaries.Count);
            Assert.AreEqual("newest", summaries[0].SaveId);
            Assert.AreEqual("middle", summaries[1].SaveId);
            Assert.AreEqual("older", summaries[2].SaveId);
            Assert.AreEqual("Skyline", summaries[0].TowerName);
            Assert.AreEqual(nameof(GameDifficulty.Hard), summaries[0].Difficulty);
            Assert.AreEqual(17, summaries[0].DayIndex);
            Assert.AreEqual(731, summaries[0].MinuteOfDay);
            Assert.AreEqual(22, summaries[0].WalletBalance);
            Assert.AreEqual(2, summaries[0].Stars);
            Assert.AreEqual(DateTimeKind.Utc, summaries[0].ModifiedUtc.Kind);
            Assert.AreEqual(0, sceneLoads);
        }

        [Test]
        public void RefreshLocalSaves_keeps_corrupt_oversized_and_unsupported_saves_listed()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save("healthy", ValidSnapshot("healthy", "2026-01-01T00:00:00.0000000Z", 11)).Success);
            Assert.IsTrue(repository.Save("broken", ValidSnapshot("broken", "2026-02-02T00:00:00.0000000Z", 22)).Success);
            File.WriteAllText(CurrentPath("broken"), "{not-json");
            CreateOversizedSaveFile("huge");
            var future = ValidSnapshot("future", "2026-04-04T00:00:00.0000000Z", 5);
            future.schemaVersion = SaveSchema.CurrentVersion + 1;
            File.WriteAllText(CurrentPath("future"), SaveSerializer.Serialize(future));
            var menu = CreateMenu(repository);

            var summaries = menu.RefreshLocalSaves();

            Assert.AreEqual(4, summaries.Count);
            Assert.IsTrue(Find(summaries, "healthy").IsReadable);
            Assert.AreEqual(SaveReadError.InvalidEnvelope, Find(summaries, "broken").Status);
            Assert.AreEqual(SaveReadError.PayloadTooLarge, Find(summaries, "huge").Status);
            Assert.AreEqual(SaveReadError.UnsupportedSchema, Find(summaries, "future").Status);
            Assert.IsFalse(Find(summaries, "broken").IsReadable);
            Assert.IsFalse(Find(summaries, "huge").IsReadable);
            Assert.IsFalse(Find(summaries, "future").IsReadable);
        }

        [Test]
        public void PresentRow_uses_injected_local_time_and_game_calendar_utility()
        {
            var summary = LocalSaveSummary.Readable(
                SaveId,
                "Harbor",
                nameof(GameDifficulty.Easy),
                59,
                360,
                42_000,
                3,
                new DateTime(2026, 3, 3, 16, 30, 0, DateTimeKind.Utc));
            var menu = CreateMenu();

            var row = menu.PresentRow(summary);

            Assert.AreEqual("Harbor", row.TowerName);
            Assert.AreEqual("Easy", row.DifficultyName);
            Assert.AreEqual(new DateTime(2026, 3, 3, 11, 30, 0, DateTimeKind.Unspecified), row.ModifiedLocal);
            Assert.AreEqual(new DateTime(2000, 2, 29), row.GameCalendarDate);
            Assert.AreEqual(GameClock.DateForDayIndex(59), row.GameCalendarDate);
            Assert.AreEqual(6, row.GameHour);
            Assert.AreEqual(0, row.GameMinute);
            Assert.AreEqual(42_000, row.Funds);
            Assert.AreEqual(3, row.Stars);
            Assert.IsTrue(row.CanLoadCurrent);
            Assert.AreEqual("2026-03-03 11:30", row.ModifiedLocalText);
            StringAssert.Contains("29", row.GameDateText);
            StringAssert.Contains("2000", row.GameDateText);
        }

        [Test]
        public void PresentRow_keeps_markup_characters_as_plain_text()
        {
            var summary = LocalSaveSummary.Readable(
                SaveId,
                "<b>evil</b>",
                nameof(GameDifficulty.Normal),
                1,
                0,
                1,
                0,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var menu = CreateMenu();

            var row = menu.PresentRow(summary);

            Assert.AreEqual("<b>evil</b>", row.TowerName);
            StringAssert.DoesNotContain("payload", row.StatusMessage.ToLowerInvariant());
        }

        [Test]
        public void WriteError_maps_every_save_write_error_to_a_path_free_message()
        {
            foreach (SaveWriteError error in Enum.GetValues(typeof(SaveWriteError)))
            {
                if (error == SaveWriteError.None)
                    continue;

                var text = LocalSavePresentation.WriteError(error);
                Assert.IsFalse(string.IsNullOrEmpty(text), error.ToString());
                Assert.AreNotEqual("The tower could not be saved.", text, error.ToString());
                StringAssert.DoesNotContain("\\", text);
                StringAssert.DoesNotContain("exception", text.ToLowerInvariant());
            }
        }

        [Test]
        public void Load_row_labels_treat_markup_like_names_as_plain_text()
        {
            var repository = new LocalSaveRepository(root);
            var valid = ValidSnapshot("harbor", "2026-03-03T16:30:00.0000000Z", 88_000);
            valid.towerName = "<b>evil</b>";
            Assert.IsTrue(repository.Save("harbor", valid).Success);
            var menu = CreateBoundMenu(out var rootVisual, repository);

            menu.ShowLocalSavesPanel();

            var name = rootVisual.Q<Label>("save-name-harbor");
            var meta = rootVisual.Q<Label>("save-meta-harbor");
            Assert.IsNotNull(name);
            Assert.IsNotNull(meta);
            Assert.AreEqual("<b>evil</b>", name.text);
            Assert.IsFalse(name.enableRichText);
            Assert.IsFalse(meta.enableRichText);
            StringAssert.Contains("<b>evil</b>", name.text);
        }

        [Test]
        public void Recovery_labels_treat_markup_like_names_as_plain_text()
        {
            var repository = new LocalSaveRepository(root);
            var first = ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1);
            first.towerName = "<i>backup</i>";
            Assert.IsTrue(repository.Save(SaveId, first).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-02-02T00:00:00.0000000Z", 2)).Success);
            File.WriteAllText(CurrentPath(SaveId), "{broken");
            var menu = CreateBoundMenu(out var rootVisual, repository);

            menu.ShowLocalSavesPanel();

            var status = rootVisual.Q<Label>("save-status-tower-a");
            var recovery = rootVisual.Q<Label>("recovery-label-tower-a-1");
            Assert.IsNotNull(status);
            Assert.IsNotNull(recovery);
            Assert.IsFalse(status.enableRichText);
            Assert.IsFalse(recovery.enableRichText);
            StringAssert.Contains("<i>backup</i>", recovery.text);
        }

        [Test]
        public void TryPrepareLoad_success_invokes_scene_action_once_and_leaves_exact_snapshot_pending()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = ValidSnapshot(SaveId, "2026-09-08T16:30:45.0000000Z", 4242);
            snapshot.towerName = "Harbor Tower";
            Assert.IsTrue(repository.Save(SaveId, snapshot).Success);
            var before = FileFingerprints();
            var menu = CreateMenu(repository);

            string error;
            var prepared = menu.TryPrepareLoad(SaveId, out error);

            Assert.IsTrue(prepared, error);
            Assert.IsTrue(string.IsNullOrEmpty(error));
            Assert.AreEqual(1, sceneLoads);
            Assert.AreEqual(SaveId, GameSession.PendingLoad.saveId);
            Assert.AreEqual("Harbor Tower", GameSession.PendingLoad.towerName);
            Assert.AreEqual(4242, GameSession.PendingLoad.walletBalance);
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
            Assert.AreEqual(before, FileFingerprints());
        }

        [Test]
        public void TryPrepareLoad_failure_returns_safe_message_without_scene_or_file_changes()
        {
            var repository = new LocalSaveRepository(root);
            Directory.CreateDirectory(root);
            File.WriteAllText(CurrentPath(SaveId), "{not-json");
            var before = File.ReadAllBytes(CurrentPath(SaveId));
            GameSession.StartNewGame(GameDifficulty.Easy);
            var menu = CreateMenu(repository);

            string error;
            var prepared = menu.TryPrepareLoad(SaveId, out error);

            Assert.IsFalse(prepared);
            Assert.IsFalse(string.IsNullOrEmpty(error));
            StringAssert.DoesNotContain("\\", error);
            StringAssert.DoesNotContain("/", error);
            StringAssert.DoesNotContain("payload", error.ToLowerInvariant());
            StringAssert.DoesNotContain("checksum", error.ToLowerInvariant());
            StringAssert.DoesNotContain("exception", error.ToLowerInvariant());
            Assert.AreEqual(0, sceneLoads);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(CurrentPath(SaveId)));
        }

        [Test]
        public void Unreadable_current_exposes_each_valid_recovery()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-02-02T00:00:00.0000000Z", 2)).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-03-03T00:00:00.0000000Z", 3)).Success);
            File.WriteAllText(CurrentPath(SaveId), "{broken");
            var menu = CreateMenu(repository);

            var recoveries = menu.ListRecoveries(SaveId);

            Assert.AreEqual(2, recoveries.Count);
            Assert.AreEqual(1, recoveries[0].RecoveryIndex);
            Assert.AreEqual(2, recoveries[1].RecoveryIndex);
            Assert.AreEqual(SaveId, recoveries[0].SaveId);
            Assert.AreEqual("Skyline", recoveries[0].TowerName);
            Assert.AreEqual(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), recoveries[0].ModifiedUtc);
            Assert.IsTrue(recoveries[0].CanLoad);
        }

        [Test]
        public void TryPrepareRecovery_does_not_overwrite_current_and_invokes_scene_once()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-02-02T00:00:00.0000000Z", 2)).Success);
            var currentBytes = File.ReadAllBytes(CurrentPath(SaveId));
            File.WriteAllText(CurrentPath(SaveId), "{broken-current");
            var brokenBytes = File.ReadAllBytes(CurrentPath(SaveId));
            var menu = CreateMenu(repository);
            var coordinator = new SaveCoordinator(repository);

            var result = coordinator.PrepareLocalRecovery(SaveId, 1);
            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(brokenBytes, File.ReadAllBytes(CurrentPath(SaveId)));
            Assert.AreEqual(1, GameSession.PendingLoad.walletBalance);
            GameSession.ResetForTests();

            string error;
            var prepared = menu.TryPrepareRecovery(SaveId, 1, out error);

            Assert.IsTrue(prepared, error);
            Assert.AreEqual(1, sceneLoads);
            Assert.AreEqual(1, GameSession.PendingLoad.walletBalance);
            CollectionAssert.AreEqual(brokenBytes, File.ReadAllBytes(CurrentPath(SaveId)));
            Assert.AreNotEqual(currentBytes.Length, brokenBytes.Length);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void TryPrepareRecovery_rejects_invalid_index_without_scene_or_file_changes(int recoveryIndex)
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            var before = FileFingerprints();
            var menu = CreateMenu(repository);

            string error;
            var prepared = menu.TryPrepareRecovery(SaveId, recoveryIndex, out error);

            Assert.IsFalse(prepared);
            Assert.AreEqual("The recovery index must be between 1 and 3.", error);
            Assert.AreEqual(0, sceneLoads);
            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(before, FileFingerprints());
        }

        [Test]
        public void TryPrepareRecovery_rejects_corrupt_backup_and_already_pending()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-02-02T00:00:00.0000000Z", 2)).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-03-03T00:00:00.0000000Z", 3)).Success);
            File.WriteAllText(RecoveryPath(SaveId, 1), "{bad-backup");
            var before = File.ReadAllBytes(RecoveryPath(SaveId, 1));
            var currentBefore = File.ReadAllBytes(CurrentPath(SaveId));
            var menu = CreateMenu(repository);

            string error;
            var corrupt = menu.TryPrepareRecovery(SaveId, 1, out error);

            Assert.IsFalse(corrupt);
            Assert.IsFalse(string.IsNullOrEmpty(error));
            Assert.AreEqual(0, sceneLoads);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(RecoveryPath(SaveId, 1)));
            CollectionAssert.AreEqual(currentBefore, File.ReadAllBytes(CurrentPath(SaveId)));

            Assert.IsTrue(menu.TryPrepareLoad(SaveId, out _));
            Assert.AreEqual(1, sceneLoads);
            var pending = GameSession.PendingLoad;

            var alreadyPending = menu.TryPrepareRecovery(SaveId, 2, out var pendingError);

            Assert.IsFalse(alreadyPending);
            Assert.AreEqual("A load is already pending.", pendingError);
            Assert.AreEqual(1, sceneLoads);
            Assert.AreSame(pending, GameSession.PendingLoad);
            CollectionAssert.AreEqual(currentBefore, File.ReadAllBytes(CurrentPath(SaveId)));
        }

        [Test]
        public void Load_opens_empty_state_panel_without_loading_a_scene()
        {
            var menu = CreateBoundMenu(out var rootVisual);
            var load = rootVisual.Q<Button>("btn-load-game");

            menu.ShowLocalSavesPanel();

            Assert.IsFalse(rootVisual.Q("panel-local-saves").ClassListContains("hidden"));
            Assert.IsTrue(rootVisual.Q("panel-root").ClassListContains("hidden"));
            var empty = rootVisual.Q<Label>("local-saves-empty");
            Assert.IsFalse(empty.ClassListContains("hidden"));
            Assert.AreEqual("No local saves yet.", empty.text);
            Assert.AreEqual(0, rootVisual.Q("local-saves-rows").childCount);
            Assert.AreEqual(0, sceneLoads);
            Assert.IsNotNull(load);
            Assert.IsTrue(load.enabledSelf);
        }

        [Test]
        public void Load_renders_valid_rows_and_disables_unreadable_current_load()
        {
            var repository = new LocalSaveRepository(root);
            var valid = ValidSnapshot("harbor", "2026-03-03T16:30:00.0000000Z", 88_000);
            valid.towerName = "Harbor";
            valid.difficulty = nameof(GameDifficulty.Easy);
            valid.stars = 3;
            valid.clock.dayIndex = 59;
            valid.clock.minuteOfDay = 360;
            Assert.IsTrue(repository.Save("harbor", valid).Success);
            Assert.IsTrue(repository.Save("broken", ValidSnapshot("broken", "2026-01-01T00:00:00.0000000Z", 1)).Success);
            File.WriteAllText(CurrentPath("broken"), "{nope");
            var menu = CreateBoundMenu(out var rootVisual, repository);

            menu.ShowLocalSavesPanel();

            var harbor = rootVisual.Q("save-row-harbor");
            Assert.IsNotNull(harbor);
            Assert.AreEqual("Harbor", rootVisual.Q<Label>("save-name-harbor").text);
            StringAssert.Contains("Easy", rootVisual.Q<Label>("save-meta-harbor").text);
            StringAssert.Contains("11:30", rootVisual.Q<Label>("save-meta-harbor").text);
            StringAssert.Contains("2000", rootVisual.Q<Label>("save-meta-harbor").text);
            StringAssert.Contains("88000", rootVisual.Q<Label>("save-meta-harbor").text);
            StringAssert.Contains("3", rootVisual.Q<Label>("save-meta-harbor").text);
            Assert.IsTrue(rootVisual.Q<Button>("btn-load-harbor").enabledSelf);

            var brokenLoad = rootVisual.Q<Button>("btn-load-broken");
            Assert.IsFalse(brokenLoad.enabledSelf);
            Assert.IsFalse(string.IsNullOrEmpty(rootVisual.Q<Label>("save-status-broken").text));
            Assert.IsTrue(rootVisual.Q("local-saves-empty").ClassListContains("hidden"));
        }

        [Test]
        public void Unreadable_row_shows_recovery_load_actions()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-02-02T00:00:00.0000000Z", 2)).Success);
            File.WriteAllText(CurrentPath(SaveId), "{broken");
            var menu = CreateBoundMenu(out var rootVisual, repository);

            menu.ShowLocalSavesPanel();

            Assert.IsFalse(rootVisual.Q<Button>("btn-load-tower-a").enabledSelf);
            var recovery = rootVisual.Q<Button>("btn-load-recovery-tower-a-1");
            Assert.IsNotNull(recovery);
            Assert.IsTrue(recovery.enabledSelf);
            StringAssert.Contains("Skyline", rootVisual.Q<Label>("recovery-label-tower-a-1").text);
        }

        [Test]
        public void Back_returns_to_root_and_clears_dynamic_rows()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            var menu = CreateBoundMenu(out var rootVisual, repository);
            menu.ShowLocalSavesPanel();
            Assert.Greater(rootVisual.Q("local-saves-rows").childCount, 0);

            menu.HideLocalSavesPanel();

            Assert.IsFalse(rootVisual.Q("panel-root").ClassListContains("hidden"));
            Assert.IsTrue(rootVisual.Q("panel-local-saves").ClassListContains("hidden"));
            Assert.AreEqual(0, rootVisual.Q("local-saves-rows").childCount);
        }

        [Test]
        public void Opening_and_refreshing_does_not_duplicate_rows()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            var menu = CreateBoundMenu(out var rootVisual, repository);

            menu.ShowLocalSavesPanel();
            menu.ShowLocalSavesPanel();
            menu.RefreshLocalSaves();
            menu.ShowLocalSavesPanel();

            Assert.AreEqual(1, rootVisual.Q("local-saves-rows").childCount);
            Assert.AreEqual(1, CountNamed(rootVisual, "save-row-tower-a"));
            Assert.AreEqual(1, CountNamed(rootVisual, "btn-load-tower-a"));
        }

        [Test]
        public void Main_menu_save_button_is_disabled_with_an_explanatory_tooltip()
        {
            var menu = CreateBoundMenu(out var rootVisual);
            var save = rootVisual.Q<Button>("btn-save-game");

            Assert.IsFalse(save.enabledSelf);
            StringAssert.Contains("play", save.tooltip.ToLowerInvariant());
            StringAssert.DoesNotContain("cloud", save.tooltip.ToLowerInvariant());
            Assert.IsNotNull(menu);
        }

        [Test]
        public void Existing_new_game_contact_and_about_panels_still_open()
        {
            CreateBoundMenu(out var rootVisual);

            Assert.IsNotNull(rootVisual.Q<Button>("btn-new-game"));
            Assert.IsNotNull(rootVisual.Q<Button>("btn-contact"));
            Assert.IsNotNull(rootVisual.Q<Button>("btn-about"));
            Assert.IsNotNull(rootVisual.Q("panel-difficulty"));
            Assert.IsNotNull(rootVisual.Q("panel-contact"));
            Assert.IsNotNull(rootVisual.Q("panel-about"));
        }

        [Test]
        public void StartNewGame_clears_current_save_identity_and_starts_dirty()
        {
            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            GameSession.CompleteLoad();
            Assert.AreEqual(SaveId, GameSession.CurrentSaveId);

            GameSession.StartNewGame(GameDifficulty.Easy);

            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentSaveId));
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentTowerName));
            Assert.IsTrue(GameSession.IsSaveDirty);
            Assert.AreEqual(GameDifficulty.Easy, GameSession.Difficulty);
        }

        [Test]
        public void CompleteLoad_promotes_pending_identity_before_clearing()
        {
            var snapshot = ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1);
            snapshot.towerName = "Harbor";
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);

            GameSession.CompleteLoad();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(SaveId, GameSession.CurrentSaveId);
            Assert.AreEqual("Harbor", GameSession.CurrentTowerName);
            Assert.IsFalse(GameSession.IsSaveDirty);
        }

        [Test]
        public void FailLoad_and_ResetForTests_clear_identity()
        {
            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            GameSession.CompleteLoad();

            GameSession.FailLoad(SessionLoadError.GridRestoreFailed, "The tower could not be restored from the save.");
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentSaveId));
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentTowerName));

            Assert.IsTrue(GameSession.PrepareLoad(ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1)).Success);
            GameSession.CompleteLoad();
            GameSession.ResetForTests();
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentSaveId));
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentTowerName));
            Assert.IsFalse(GameSession.IsSaveDirty);
        }

        [Test]
        public void Loaded_tower_uses_identity_immediately_after_startup_completion()
        {
            var snapshot = ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 88_000);
            snapshot.towerName = "Harbor";
            snapshot.rooms = new[] { LobbyRoom(7, 5) };
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            var build = CreateInactiveTower(out var simulation, withLobby: true);

            build.InitializeTower();
            simulation.InitializeSimulation();
            simulation.BeginPlay();

            Assert.IsNull(GameSession.PendingLoad);
            Assert.AreEqual(SaveId, GameSession.CurrentSaveId);
            Assert.AreEqual("Harbor", GameSession.CurrentTowerName);
            Assert.IsFalse(GameSession.IsSaveDirty);
        }

        [Test]
        public void First_new_game_save_uses_safe_random_id_and_default_name()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out var repository, out _);

            var message = hud.SavePausedTower();

            Assert.AreEqual("Saved locally", message);
            Assert.IsTrue(SafeGeneratedId.IsMatch(GameSession.CurrentSaveId));
            Assert.AreEqual(32, GameSession.CurrentSaveId.Length);
            Assert.AreEqual(DefaultTowerName, GameSession.CurrentTowerName);
            var loaded = repository.Read(GameSession.CurrentSaveId);
            Assert.IsTrue(loaded.Success, loaded.ErrorMessage);
            Assert.AreEqual(DefaultTowerName, loaded.Snapshot.towerName);
            Assert.IsFalse(GameSession.IsSaveDirty);
            Assert.IsTrue(hud.IsLatestLocalSaveComplete);
        }

        [Test]
        public void Later_saves_reuse_the_same_identity()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out var repository, out var build);
            hud.SavePausedTower();
            var id = GameSession.CurrentSaveId;
            MutateGrid(build);

            var message = hud.SavePausedTower();

            Assert.AreEqual("Saved locally", message);
            Assert.AreEqual(id, GameSession.CurrentSaveId);
            Assert.AreEqual(DefaultTowerName, GameSession.CurrentTowerName);
            Assert.AreEqual(1, CountCurrentSaves(repository));
            Assert.AreEqual(id, repository.List()[0].SaveId);
        }

        [Test]
        public void Failed_first_save_does_not_permanently_claim_identity()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            Directory.CreateDirectory(root);
            var blockedRoot = Path.Combine(root, "blocked-file");
            File.WriteAllText(blockedRoot, "not-a-directory");
            var hud = CreateHud(out _, out _, new LocalSaveRepository(blockedRoot));

            var failure = hud.SavePausedTower();

            Assert.AreNotEqual("Saved locally", failure);
            Assert.AreEqual("The save directory could not be created.", failure);
            Assert.IsFalse(string.IsNullOrEmpty(failure));
            StringAssert.DoesNotContain("\\", failure);
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentSaveId));
            Assert.IsTrue(GameSession.IsSaveDirty);
            Assert.IsFalse(hud.IsLatestLocalSaveComplete);
        }

        [Test]
        public void Save_failure_stays_paused_and_dirty()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            hud.SavePausedTower();
            MutateGrid(build);
            SetPauseUi(hud, "Paused");
            var clock = build.GetComponent<TowerSimulation>().Clock;
            clock.Paused = true;
            var blocked = Path.Combine(root, GameSession.CurrentSaveId + LocalSaveRepository.CurrentExtension);
            using (new FileStream(blocked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var message = hud.SavePausedTower();
                Assert.AreNotEqual("Saved locally", message);
                Assert.IsTrue(GameSession.IsSaveDirty);
                Assert.AreEqual("Paused", GetPauseUi(hud));
                Assert.IsTrue(clock.Paused);
            }
        }

        [Test]
        public void Grid_mutation_marks_dirty_and_time_only_progress_does_not()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);
            var simulation = build.GetComponent<TowerSimulation>();
            simulation.Clock.Paused = false;
            simulation.Clock.Tick(60f);

            Assert.IsFalse(GameSession.IsSaveDirty, "Time-only clock progress is not treated as unsaved dirty state.");

            MutateGrid(build);
            Assert.IsTrue(GameSession.IsSaveDirty);
            Assert.IsFalse(hud.IsLatestLocalSaveComplete);
        }

        [Test]
        public void Price_tier_edit_after_clean_save_marks_dirty()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            PlacePricedOffice(build, out _);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);
            Assert.AreEqual(
                PauseSaveService.CleanQuitWarning,
                hud.PauseQuitWarningText);

            Assert.IsTrue(build.TrySelectAt(new Vector2Int(1, 1)));
            Assert.IsTrue(build.TrySetSelectedPriceTier(PricePricing.TierHigh));

            Assert.IsTrue(GameSession.IsSaveDirty);
            Assert.AreEqual(
                PauseSaveService.DirtyQuitWarning,
                hud.PauseQuitWarningText);
        }

        [Test]
        public void Wallet_mutation_after_clean_save_marks_dirty()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);
            var before = build.Wallet.Balance;

            Assert.IsTrue(build.Wallet.TrySpend(1));
            Assert.AreEqual(before - 1, build.Wallet.Balance);
            Assert.IsTrue(GameSession.IsSaveDirty);

            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);
            build.Wallet.Add(25);
            Assert.IsTrue(GameSession.IsSaveDirty);
        }

        [Test]
        public void Day_roll_after_clean_save_marks_dirty_without_wallet_transfer()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);
            var simulation = build.GetComponent<TowerSimulation>();
            var funds = build.Wallet.Balance;
            simulation.Clock.Paused = false;
            LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");

            simulation.Clock.AdvanceMinutes(GameClock.MinutesPerDay);

            Assert.AreEqual(1, simulation.Clock.DayIndex);
            Assert.AreEqual(funds, build.Wallet.Balance, "Empty-tower midnight must not transfer wallet funds.");
            Assert.IsTrue(GameSession.IsSaveDirty);
            Assert.AreEqual(
                PauseSaveService.DirtyQuitWarning,
                hud.PauseQuitWarningText);
        }

        [Test]
        public void Trusted_RestoreBalance_after_clean_save_stays_clean()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);

            build.Wallet.RestoreBalance(88_000);

            Assert.AreEqual(88_000, build.Wallet.Balance);
            Assert.IsFalse(GameSession.IsSaveDirty);
            Assert.AreEqual(
                PauseSaveService.CleanQuitWarning,
                hud.PauseQuitWarningText);
        }

        [Test]
        public void Condo_sale_and_staff_edits_after_clean_save_mark_dirty()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            var simulation = build.GetComponent<TowerSimulation>();
            PlaceCondoAndHousekeeping(build, out var condo, out var housekeeping);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);

            Assert.IsTrue(simulation.Economy.TrySellCondo(condo, build.Wallet));
            Assert.IsTrue(GameSession.IsSaveDirty);

            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);
            Assert.IsTrue(build.TrySelectAt(housekeeping.Origin));
            Assert.IsTrue(build.TrySetStaffedWorkers(3));
            Assert.IsTrue(GameSession.IsSaveDirty);
        }

        [Test]
        public void Star_restore_after_clean_save_stays_clean()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            hud.SavePausedTower();
            Assert.IsFalse(GameSession.IsSaveDirty);

            build.GetComponent<TowerSimulation>().Stars.ForceStars(4);

            Assert.IsFalse(GameSession.IsSaveDirty);
        }

        [Test]
        public void Quit_warning_matches_clean_and_dirty_copy()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out var build);
            Assert.AreEqual(
                "Leave tower? Changes since the last successful save will be lost.",
                hud.PauseQuitWarningText);

            hud.SavePausedTower();
            Assert.AreEqual(
                "Leave tower? Your latest local save is complete.",
                hud.PauseQuitWarningText);

            MutateGrid(build);
            Assert.AreEqual(
                "Leave tower? Changes since the last successful save will be lost.",
                hud.PauseQuitWarningText);
        }

        [Test]
        public void Failed_save_after_clean_save_without_mutation_uses_dirty_quit_warning()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out _);
            hud.SavePausedTower();
            Assert.AreEqual(
                "Leave tower? Your latest local save is complete.",
                hud.PauseQuitWarningText);
            Assert.IsFalse(GameSession.IsSaveDirty);

            var blocked = Path.Combine(root, GameSession.CurrentSaveId + LocalSaveRepository.CurrentExtension);
            using (new FileStream(blocked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var message = hud.SavePausedTower();
                Assert.AreNotEqual("Saved locally", message);
                Assert.AreNotEqual("The tower could not be saved.", message);
                Assert.AreEqual(
                    "The current save could not be replaced, so the previous save and its recoveries were kept.",
                    message);
                StringAssert.DoesNotContain("\\", message);
                StringAssert.DoesNotContain("exception", message.ToLowerInvariant());
                Assert.IsTrue(GameSession.IsSaveDirty);
                Assert.IsFalse(hud.IsLatestLocalSaveComplete);
                Assert.AreEqual(
                    "Leave tower? Changes since the last successful save will be lost.",
                    hud.PauseQuitWarningText);
            }
        }

        [Test]
        public void Pause_clears_stale_save_message_when_entering_and_leaving()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hud = CreateHud(out _, out _);
            hud.SavePausedTower();
            Assert.AreEqual("Saved locally", GetPauseSaveMessage(hud));

            InvokeHud(hud, "ResumeFromPause");
            Assert.IsTrue(string.IsNullOrEmpty(GetPauseSaveMessage(hud)));

            SetField(hud, "_pauseSaveMessage", "Saved locally");
            InvokeHud(hud, "EnterPause");
            Assert.IsTrue(string.IsNullOrEmpty(GetPauseSaveMessage(hud)));
        }

        [Test]
        public void New_game_starts_dirty_and_loaded_tower_starts_clean_after_complete()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            Assert.IsTrue(GameSession.IsSaveDirty);

            var snapshot = ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 1);
            snapshot.towerName = "Harbor";
            Assert.IsTrue(GameSession.PrepareLoad(snapshot).Success);
            Assert.IsTrue(string.IsNullOrEmpty(GameSession.CurrentSaveId));
            GameSession.CompleteLoad();
            Assert.IsFalse(GameSession.IsSaveDirty);
            Assert.AreEqual(SaveId, GameSession.CurrentSaveId);
        }

        [Test]
        public void Pause_load_prepares_pending_snapshot_and_invokes_tower_scene()
        {
            var snapshot = ValidSnapshot(SaveId, "2026-01-01T00:00:00.0000000Z", 44_000);
            snapshot.towerName = "Dockside";
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, snapshot).Success);

            var sceneLoads = 0;
            var hud = CreateHud(out _, out _);
            var loadPresenter = new LocalSaveMenuPresenter(
                repository,
                new SaveCoordinator(repository),
                () => sceneLoads++,
                DisplayZone);
            hud.ConfigurePauseLoad(loadPresenter, repository, () => sceneLoads++);

            Assert.IsTrue(hud.TryLoadPausedTower(SaveId, out var error), error);
            Assert.AreEqual(1, sceneLoads);
            Assert.IsNotNull(GameSession.PendingLoad);
            Assert.AreEqual(SaveId, GameSession.PendingLoad.saveId);
            Assert.AreEqual("Dockside", GameSession.PendingLoad.towerName);
        }

        MainMenuController CreateMenu(LocalSaveRepository repository = null)
        {
            repository ??= new LocalSaveRepository(root);
            var gameObject = new GameObject("Task8 Menu");
            gameObject.SetActive(false);
            var menu = gameObject.AddComponent<MainMenuController>();
            menu.ConfigureLocalSaves(
                repository,
                new SaveCoordinator(repository),
                () => sceneLoads++,
                DisplayZone);
            return menu;
        }

        MainMenuController CreateBoundMenu(out VisualElement rootVisual, LocalSaveRepository repository = null)
        {
            var menu = CreateMenu(repository);
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/UI/MainMenu.uxml");
            Assert.IsNotNull(asset, "MainMenu.uxml must be importable.");
            rootVisual = asset.CloneTree();
            menu.Bind(rootVisual);
            return menu;
        }

        TowerHudController CreateHud(
            out LocalSaveRepository repository,
            out BuildController build,
            LocalSaveRepository forcedRepository = null)
        {
            repository = forcedRepository ?? new LocalSaveRepository(root);
            build = CreateInactiveTower(out var simulation);
            build.InitializeTower();
            simulation.InitializeSimulation();
            var hud = build.gameObject.AddComponent<TowerHudController>();
            SetField(hud, "build", build);
            SetField(hud, "simulation", simulation);
            hud.BindPauseSave(new SaveCoordinator(repository, build, simulation), repository);
            return hud;
        }

        static void MutateGrid(BuildController build)
        {
            Assert.IsTrue(build.Grid.TryPlaceLobby(LobbyType(), 0, 2, TowerGrid.LobbyFloor, out _));
            var field = typeof(BuildController).GetField(
                "GridChanged",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            ((Action)field.GetValue(build))?.Invoke();
        }

        static void PlacePricedOffice(BuildController build, out RoomInstance office)
        {
            Assert.IsTrue(build.Grid.TryPlaceLobby(LobbyType(), 0, 2, TowerGrid.LobbyFloor, out _));
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "office_test";
            type.displayName = "Office";
            type.category = RoomCategory.Office;
            type.size = Vector2Int.one;
            type.allowAboveGround = true;
            type.incomeModel = IncomeModel.QuarterlyRent;
            type.baseIncome = 100;
            Assert.IsTrue(build.Grid.TryPlace(type, new Vector2Int(1, 1), out office));
        }

        static void PlaceCondoAndHousekeeping(
            BuildController build,
            out RoomInstance condo,
            out RoomInstance housekeeping)
        {
            Assert.IsTrue(build.Grid.TryPlaceLobby(LobbyType(), 0, 4, TowerGrid.LobbyFloor, out _));
            var condoType = ScriptableObject.CreateInstance<RoomTypeSO>();
            condoType.id = "condo_test";
            condoType.displayName = "Condo";
            condoType.category = RoomCategory.Condo;
            condoType.size = Vector2Int.one;
            condoType.allowAboveGround = true;
            condoType.incomeModel = IncomeModel.UpfrontSale;
            condoType.baseIncome = 500;
            Assert.IsTrue(build.Grid.TryPlace(condoType, new Vector2Int(1, 1), out condo));

            var staffType = ScriptableObject.CreateInstance<RoomTypeSO>();
            staffType.id = "service_housekeeping";
            staffType.displayName = "Housekeeping";
            staffType.category = RoomCategory.Service;
            staffType.size = Vector2Int.one;
            staffType.allowAboveGround = true;
            Assert.IsTrue(build.Grid.TryPlace(staffType, new Vector2Int(3, 1), out housekeeping));
        }

        static BuildController CreateInactiveTower(
            out TowerSimulation simulation,
            bool withLobby = false)
        {
            var gameObject = new GameObject("Task8 Tower");
            gameObject.SetActive(false);
            var build = gameObject.AddComponent<BuildController>();
            if (withLobby)
                SetField(build, "lobbyType", LobbyType());
            simulation = gameObject.AddComponent<TowerSimulation>();
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

        static TowerSnapshotV1 ValidSnapshot(string saveId, string modifiedUtc, int wallet)
        {
            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = saveId,
                towerName = "Skyline",
                difficulty = nameof(GameDifficulty.Hard),
                modifiedUtc = modifiedUtc,
                walletBalance = wallet,
                stars = 2,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 17,
                    minuteOfDay = 731,
                    minuteAccumulator = 0.5f,
                    minutesPerRealSecond = 1f
                },
                rooms = Array.Empty<RoomSnapshotV1>()
            };
        }

        static LocalSaveSummary Find(IReadOnlyList<LocalSaveSummary> summaries, string saveId)
        {
            foreach (var summary in summaries)
            {
                if (summary.SaveId == saveId)
                    return summary;
            }

            Assert.Fail("Missing save " + saveId);
            return null;
        }

        static int CountNamed(VisualElement root, string name)
        {
            var count = 0;
            root.Query(name).ForEach(_ => count++);
            return count;
        }

        static int CountCurrentSaves(LocalSaveRepository repository)
        {
            var count = 0;
            foreach (var path in Directory.GetFiles(repository.RootDirectory))
            {
                if (path.EndsWith(LocalSaveRepository.CurrentExtension, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }

        static void SetPauseUi(TowerHudController hud, string stateName)
        {
            var enumType = typeof(TowerHudController).GetNestedType("PauseUiState", BindingFlags.NonPublic);
            Assert.IsNotNull(enumType);
            SetField(hud, "_pauseUi", Enum.Parse(enumType, stateName));
        }

        static string GetPauseUi(TowerHudController hud)
        {
            var field = typeof(TowerHudController).GetField("_pauseUi", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(hud).ToString();
        }

        static string GetPauseSaveMessage(TowerHudController hud)
        {
            var field = typeof(TowerHudController).GetField(
                "_pauseSaveMessage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(hud) as string;
        }

        static void InvokeHud(TowerHudController hud, string methodName)
        {
            var method = typeof(TowerHudController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method " + methodName);
            method.Invoke(hud, null);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + fieldName);
            field.SetValue(target, value);
        }

        long CreateOversizedSaveFile(string saveId)
        {
            var length = LocalSaveRepository.MaxSaveFileBytes + 1;
            Directory.CreateDirectory(root);
            using (var stream = new FileStream(CurrentPath(saveId), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.SetLength(length);
            return length;
        }

        string CurrentPath(string saveId)
        {
            return Path.Combine(root, saveId + LocalSaveRepository.CurrentExtension);
        }

        string RecoveryPath(string saveId, int recoveryIndex)
        {
            return Path.Combine(root, saveId + ".bak" + recoveryIndex.ToString(CultureInfo.InvariantCulture));
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
