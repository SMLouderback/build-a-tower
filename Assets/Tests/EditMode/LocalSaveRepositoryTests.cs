using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class LocalSaveRepositoryTests
    {
        const string SaveId = "tower-a";

        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "bat-local-save-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void Constructor_rejects_null_root()
        {
            Assert.Throws<ArgumentException>(() => new LocalSaveRepository(null));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        public void Constructor_rejects_blank_root(string blankRoot)
        {
            Assert.Throws<ArgumentException>(() => new LocalSaveRepository(blankRoot));
        }

        [Test]
        public void Constructor_rejects_relative_root()
        {
            Assert.Throws<ArgumentException>(() => new LocalSaveRepository(Path.Combine("relative", "saves")));
        }

        [TestCase("C:saves")]
        [TestCase("\\saves")]
        [TestCase("/saves")]
        public void Constructor_rejects_a_root_that_is_not_fully_qualified(string ambientRoot)
        {
            Assert.Throws<ArgumentException>(() => new LocalSaveRepository(ambientRoot));
        }

        [Test]
        public void Constructor_accepts_absolute_root_without_creating_it()
        {
            var repository = new LocalSaveRepository(root);

            Assert.AreEqual(root, repository.RootDirectory);
            Assert.IsFalse(Directory.Exists(root), "The constructor must not create the save root.");
        }

        [Test]
        public void Constructor_stores_a_fully_qualified_normalized_root()
        {
            var repository = new LocalSaveRepository(Path.Combine(root, "sub", "..", "saves"));

            Assert.AreEqual(Path.Combine(root, "saves"), repository.RootDirectory);
        }

        [Test]
        public void CreateDefault_uses_saves_folder_under_persistent_data_path()
        {
            var repository = LocalSaveRepository.CreateDefault();

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Saves")),
                repository.RootDirectory);
            Assert.AreEqual("Saves", Path.GetFileName(repository.RootDirectory));
        }

        [Test]
        public void First_save_creates_current_file_and_leaves_no_temp_file()
        {
            var repository = new LocalSaveRepository(root);

            var result = repository.Save(SaveId, Snapshot(1));

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(SaveWriteError.None, result.Error);
            Assert.IsTrue(File.Exists(CurrentPath(SaveId)));
            Assert.AreEqual(1, Directory.GetFiles(root).Length, "Only the current save must remain.");
            Assert.IsFalse(File.Exists(Path.Combine(root, SaveId + ".tmp")));
        }

        [Test]
        public void Four_saves_create_current_plus_three_recoveries_in_revision_order()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 4);

            Assert.AreEqual(4, Directory.GetFiles(root).Length);
            Assert.IsTrue(File.Exists(CurrentPath(SaveId)));
            Assert.IsTrue(File.Exists(RecoveryPath(SaveId, 1)));
            Assert.IsTrue(File.Exists(RecoveryPath(SaveId, 2)));
            Assert.IsTrue(File.Exists(RecoveryPath(SaveId, 3)));
            Assert.AreEqual(4, WalletOf(repository.Read(SaveId)));
            Assert.AreEqual(3, WalletOf(repository.ReadRecovery(SaveId, 1)), "Recovery 1 is the previous current save.");
            Assert.AreEqual(2, WalletOf(repository.ReadRecovery(SaveId, 2)));
            Assert.AreEqual(1, WalletOf(repository.ReadRecovery(SaveId, 3)), "Recovery 3 is the oldest retained save.");
        }

        [Test]
        public void Fifth_save_discards_only_the_previously_oldest_recovery()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 5);

            Assert.AreEqual(4, Directory.GetFiles(root).Length);
            Assert.AreEqual(5, WalletOf(repository.Read(SaveId)));
            Assert.AreEqual(4, WalletOf(repository.ReadRecovery(SaveId, 1)));
            Assert.AreEqual(3, WalletOf(repository.ReadRecovery(SaveId, 2)));
            Assert.AreEqual(2, WalletOf(repository.ReadRecovery(SaveId, 3)));
        }

        [Test]
        public void Read_returns_the_exact_latest_snapshot()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = Snapshot(7);
            snapshot.rooms = new[]
            {
                new RoomSnapshotV1
                {
                    instanceId = 12,
                    roomTypeId = "office",
                    originX = 3,
                    originY = 4,
                    width = 2,
                    height = 1,
                    evaluation = 55,
                    visitHistory = new[] { 1, 2, 3 }
                }
            };

            Assert.IsTrue(repository.Save(SaveId, snapshot).Success);
            var result = repository.Read(SaveId);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(SaveId, result.Snapshot.saveId);
            Assert.AreEqual("Skyline", result.Snapshot.towerName);
            Assert.AreEqual(nameof(GameDifficulty.Hard), result.Snapshot.difficulty);
            Assert.AreEqual(7, result.Snapshot.walletBalance);
            Assert.AreEqual(2, result.Snapshot.stars);
            Assert.AreEqual(17, result.Snapshot.clock.dayIndex);
            Assert.AreEqual(731, result.Snapshot.clock.minuteOfDay);
            Assert.AreEqual(1, result.Snapshot.rooms.Length);
            Assert.AreEqual("office", result.Snapshot.rooms[0].roomTypeId);
            Assert.AreEqual(12, result.Snapshot.rooms[0].instanceId);
            Assert.AreEqual(new[] { 1, 2, 3 }, result.Snapshot.rooms[0].visitHistory);
        }

        [Test]
        public void Read_reports_a_typed_error_for_a_missing_save()
        {
            var repository = new LocalSaveRepository(root);

            var result = repository.Read(SaveId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.NotFound, result.Error);
            Assert.IsNull(result.Snapshot);
        }

        [Test]
        public void ReadRecovery_reports_a_typed_error_for_a_missing_recovery()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);

            var result = repository.ReadRecovery(SaveId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.NotFound, result.Error);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(int.MaxValue)]
        public void ReadRecovery_rejects_an_index_outside_one_to_three_without_touching_files(int recoveryIndex)
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 4);
            var before = FileFingerprints();

            var result = repository.ReadRecovery(SaveId, recoveryIndex);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidRecoveryIndex, result.Error);
            Assert.IsNull(result.Snapshot);
            Assert.AreEqual(before, FileFingerprints());
        }

        [Test]
        public void Save_rejects_a_null_snapshot_without_creating_files()
        {
            var repository = new LocalSaveRepository(root);

            var result = repository.Save(SaveId, null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.NullSnapshot, result.Error);
            Assert.IsFalse(Directory.Exists(root));
        }

        [Test]
        public void Save_requires_the_snapshot_save_id_to_match_the_requested_save_id()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = Snapshot(1);
            snapshot.saveId = "tower-b";

            var result = repository.Save(SaveId, snapshot);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.SaveIdMismatch, result.Error);
            Assert.IsFalse(Directory.Exists(root));
        }

        [Test]
        public void Save_rejects_a_snapshot_that_cannot_be_read_back_without_replacing_the_current_save()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            var unreadable = Snapshot(2);
            unreadable.towerName = "   ";

            var result = repository.Save(SaveId, unreadable);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.SerializationFailed, result.Error);
            Assert.AreEqual(1, WalletOf(repository.Read(SaveId)));
            Assert.AreEqual(1, Directory.GetFiles(root).Length);
        }

        [TestCaseSource(nameof(UnsafeSaveIds))]
        public void Save_rejects_unsafe_save_ids(string unsafeSaveId)
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = Snapshot(1);
            snapshot.saveId = unsafeSaveId;

            var result = repository.Save(unsafeSaveId, snapshot);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.InvalidSaveId, result.Error);
            Assert.IsFalse(Directory.Exists(root), "An unsafe save ID must never touch the filesystem.");
        }

        [TestCaseSource(nameof(UnsafeSaveIds))]
        public void Read_rejects_unsafe_save_ids(string unsafeSaveId)
        {
            var repository = new LocalSaveRepository(root);

            var result = repository.Read(unsafeSaveId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidSaveId, result.Error);
        }

        [TestCaseSource(nameof(UnsafeSaveIds))]
        public void ReadRecovery_rejects_unsafe_save_ids(string unsafeSaveId)
        {
            var repository = new LocalSaveRepository(root);

            var result = repository.ReadRecovery(unsafeSaveId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidSaveId, result.Error);
        }

        [Test]
        public void A_corrupt_current_save_reports_a_typed_error_and_is_never_rewritten()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 2);
            File.WriteAllText(CurrentPath(SaveId), "{ not a save envelope");
            var corruptBytes = File.ReadAllBytes(CurrentPath(SaveId));

            var result = repository.Read(SaveId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidEnvelope, result.Error);
            Assert.AreEqual(corruptBytes, File.ReadAllBytes(CurrentPath(SaveId)), "A read must never overwrite a save.");
            Assert.AreEqual(1, WalletOf(repository.ReadRecovery(SaveId, 1)), "Valid recoveries stay readable.");
        }

        [Test]
        public void Read_refuses_a_file_larger_than_the_supported_maximum_without_touching_it()
        {
            var repository = new LocalSaveRepository(root);
            var oversizedLength = CreateOversizedSaveFile(SaveId);

            var result = repository.Read(SaveId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.PayloadTooLarge, result.Error);
            Assert.IsNull(result.Snapshot);
            Assert.AreEqual(oversizedLength, new FileInfo(CurrentPath(SaveId)).Length, "An oversized save stays intact.");
        }

        [Test]
        public void List_reports_an_oversized_save_instead_of_omitting_or_reading_it()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save("healthy", Snapshot(11, "healthy", "2026-01-01T00:00:00.0000000Z")).Success);
            var oversizedLength = CreateOversizedSaveFile("huge");

            var summaries = repository.List();

            Assert.AreEqual(2, summaries.Count);
            Assert.AreEqual("healthy", summaries[0].SaveId);
            Assert.AreEqual("huge", summaries[1].SaveId);
            Assert.IsFalse(summaries[1].IsReadable);
            Assert.AreEqual(SaveReadError.PayloadTooLarge, summaries[1].Status);
            Assert.AreEqual(oversizedLength, new FileInfo(CurrentPath("huge")).Length, "An oversized save stays intact.");
        }

        [Test]
        public void Three_consecutive_replacement_failures_preserve_the_current_save_and_every_recovery()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 4);
            var currentBytes = File.ReadAllBytes(CurrentPath(SaveId));
            var firstRecoveryBytes = File.ReadAllBytes(RecoveryPath(SaveId, 1));
            var secondRecoveryBytes = File.ReadAllBytes(RecoveryPath(SaveId, 2));
            var thirdRecoveryBytes = File.ReadAllBytes(RecoveryPath(SaveId, 3));

            using (new FileStream(CurrentPath(SaveId), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                for (var attempt = 5; attempt <= 7; attempt++)
                {
                    var result = repository.Save(SaveId, Snapshot(attempt));

                    Assert.IsFalse(result.Success, "Replacement attempt " + attempt + " must not report success.");
                    Assert.AreEqual(SaveWriteError.ReplaceFailed, result.Error);
                }
            }

            Assert.AreEqual(4, Directory.GetFiles(root).Length, "No temporary or staging file may survive.");
            Assert.AreEqual(currentBytes, File.ReadAllBytes(CurrentPath(SaveId)));
            Assert.AreEqual(firstRecoveryBytes, File.ReadAllBytes(RecoveryPath(SaveId, 1)));
            Assert.AreEqual(secondRecoveryBytes, File.ReadAllBytes(RecoveryPath(SaveId, 2)));
            Assert.AreEqual(thirdRecoveryBytes, File.ReadAllBytes(RecoveryPath(SaveId, 3)));
            Assert.AreEqual(4, WalletOf(repository.Read(SaveId)));
            Assert.AreEqual(3, WalletOf(repository.ReadRecovery(SaveId, 1)));
            Assert.AreEqual(2, WalletOf(repository.ReadRecovery(SaveId, 2)));
            Assert.AreEqual(1, WalletOf(repository.ReadRecovery(SaveId, 3)), "Every genuine recovery revision survives.");
        }

        [Test]
        public void A_rotation_failure_after_publishing_reports_failure_and_preserves_the_staged_previous_save()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 3);
            var publishedPreviousBytes = File.ReadAllBytes(CurrentPath(SaveId));
            var firstRecoveryBytes = File.ReadAllBytes(RecoveryPath(SaveId, 1));
            var secondRecoveryBytes = File.ReadAllBytes(RecoveryPath(SaveId, 2));
            var blockedRecoveryPath = RecoveryPath(SaveId, 3);
            Directory.CreateDirectory(blockedRecoveryPath);

            var result = repository.Save(SaveId, Snapshot(4));

            Assert.IsFalse(result.Success, "A broken rotation must never be reported as success.");
            Assert.AreEqual(SaveWriteError.RecoveryRotationFailed, result.Error);
            Assert.AreEqual(4, WalletOf(repository.Read(SaveId)), "The published current save stays readable.");
            Assert.AreEqual(
                publishedPreviousBytes,
                File.ReadAllBytes(Path.Combine(root, SaveId + ".stage")),
                "The only copy of the previous current save is preserved for recovery.");
            Assert.AreEqual(firstRecoveryBytes, File.ReadAllBytes(RecoveryPath(SaveId, 1)));
            Assert.AreEqual(secondRecoveryBytes, File.ReadAllBytes(RecoveryPath(SaveId, 2)));
            Assert.IsTrue(Directory.Exists(blockedRecoveryPath), "A non-regular recovery path must never be deleted.");
        }

        [Test]
        public void A_later_save_installs_a_preserved_stage_instead_of_discarding_it()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 3);
            var blockedRecoveryPath = RecoveryPath(SaveId, 3);
            Directory.CreateDirectory(blockedRecoveryPath);
            Assert.IsFalse(repository.Save(SaveId, Snapshot(4)).Success);
            Directory.Delete(blockedRecoveryPath);

            var result = repository.Save(SaveId, Snapshot(5));

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(5, WalletOf(repository.Read(SaveId)));
            Assert.AreEqual(4, WalletOf(repository.ReadRecovery(SaveId, 1)));
            Assert.AreEqual(3, WalletOf(repository.ReadRecovery(SaveId, 2)), "The staged previous current must become a recovery, not be deleted.");
            Assert.AreEqual(2, WalletOf(repository.ReadRecovery(SaveId, 3)));
            Assert.IsFalse(File.Exists(Path.Combine(root, SaveId + ".stage")));
        }

        [Test]
        public void A_later_save_keeps_a_preserved_stage_when_rotation_is_still_blocked()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 3);
            var previousCurrentBytes = File.ReadAllBytes(CurrentPath(SaveId));
            var blockedRecoveryPath = RecoveryPath(SaveId, 3);
            Directory.CreateDirectory(blockedRecoveryPath);
            Assert.IsFalse(repository.Save(SaveId, Snapshot(4)).Success);

            var result = repository.Save(SaveId, Snapshot(5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.RecoveryRotationFailed, result.Error);
            Assert.AreEqual(4, WalletOf(repository.Read(SaveId)), "The already-published current save must stay.");
            Assert.AreEqual(
                previousCurrentBytes,
                File.ReadAllBytes(Path.Combine(root, SaveId + ".stage")),
                "The sole copy of the previous current save must survive a later blocked save.");
        }

        [Test]
        public void A_blocked_temp_keeps_a_leftover_stage_and_the_current_save()
        {
            var repository = new LocalSaveRepository(root);
            var leftoverBytes = LeaveUnpublishedStage(repository);
            var currentBytes = File.ReadAllBytes(CurrentPath(SaveId));
            Directory.CreateDirectory(Path.Combine(root, SaveId + ".tmp"));

            var result = repository.Save(SaveId, Snapshot(5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.TempWriteFailed, result.Error);
            Assert.AreEqual(currentBytes, File.ReadAllBytes(CurrentPath(SaveId)));
            Assert.AreEqual(
                leftoverBytes,
                File.ReadAllBytes(Path.Combine(root, SaveId + ".stage")),
                "A leftover stage must survive an unrelated temp-write failure.");
        }

        [Test]
        public void A_missing_current_installs_a_leftover_stage_then_publishes()
        {
            var repository = new LocalSaveRepository(root);
            LeaveUnpublishedStage(repository);
            File.Delete(CurrentPath(SaveId));

            var result = repository.Save(SaveId, Snapshot(5));

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(5, WalletOf(repository.Read(SaveId)));
            Assert.AreEqual(3, WalletOf(repository.ReadRecovery(SaveId, 1)), "The leftover stage must become recovery 1.");
            Assert.AreEqual(2, WalletOf(repository.ReadRecovery(SaveId, 2)));
            Assert.AreEqual(1, WalletOf(repository.ReadRecovery(SaveId, 3)));
            Assert.IsFalse(File.Exists(Path.Combine(root, SaveId + ".stage")));
        }

        [Test]
        public void A_directory_at_the_current_path_keeps_a_leftover_stage()
        {
            var repository = new LocalSaveRepository(root);
            var leftoverBytes = LeaveUnpublishedStage(repository);
            File.Delete(CurrentPath(SaveId));
            Directory.CreateDirectory(CurrentPath(SaveId));

            var result = repository.Save(SaveId, Snapshot(5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.ReplaceFailed, result.Error);
            Assert.AreEqual(
                leftoverBytes,
                File.ReadAllBytes(Path.Combine(root, SaveId + ".stage")),
                "A leftover stage must survive when the current path is a directory.");
            Assert.IsTrue(Directory.Exists(CurrentPath(SaveId)));
        }

        [Test]
        public void Leftover_rotation_does_not_succeed_when_the_stage_file_is_missing()
        {
            var repository = new LocalSaveRepository(root);
            Directory.CreateDirectory(root);
            var rotate = typeof(LocalSaveRepository).GetMethod(
                "TryRotateRecoveries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rotate);
            var arguments = new object[] { SaveId, Path.Combine(root, SaveId + ".stage"), null };

            var succeeded = (bool)rotate.Invoke(repository, arguments);

            Assert.IsFalse(succeeded, "A vanished stage-to-bak1 source must not count as a successful rotation.");
            Assert.IsFalse(File.Exists(RecoveryPath(SaveId, 1)));
        }

        [Test]
        public void Save_writes_valid_bom_free_utf8_for_text_containing_an_unpaired_surrogate()
        {
            var repository = new LocalSaveRepository(root);
            var snapshot = Snapshot(1);
            snapshot.towerName = "Sky\ud800line";

            var result = repository.Save(SaveId, snapshot);

            Assert.AreNotEqual(
                SaveWriteError.TempWriteFailed,
                result.Error,
                "Text that cannot be encoded is a serialization problem, never a filesystem problem.");
            Assert.IsTrue(result.Success, result.ErrorMessage);

            var bytes = File.ReadAllBytes(CurrentPath(SaveId));
            Assert.AreEqual((byte)'{', bytes[0], "The save file must not start with a byte order mark.");
            Assert.DoesNotThrow(
                () => new UTF8Encoding(false, true).GetString(bytes),
                "The published save must always be valid UTF-8.");
            Assert.IsTrue(repository.Read(SaveId).Success);
        }

        [Test]
        public void A_save_file_that_belongs_to_another_save_id_is_rejected()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            File.Copy(CurrentPath(SaveId), CurrentPath("tower-b"));

            var result = repository.Read("tower-b");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveReadError.InvalidPayload, result.Error);
        }

        [Test]
        public void List_sorts_valid_summaries_by_modified_utc_descending_with_complete_fields()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save("older", Snapshot(11, "older", "2026-01-01T00:00:00.0000000Z")).Success);
            Assert.IsTrue(repository.Save("newest", Snapshot(22, "newest", "2026-03-03T00:00:00.0000000Z")).Success);
            Assert.IsTrue(repository.Save("middle", Snapshot(33, "middle", "2026-02-02T00:00:00.0000000Z")).Success);

            var summaries = repository.List();

            Assert.AreEqual(3, summaries.Count);
            Assert.AreEqual("newest", summaries[0].SaveId);
            Assert.AreEqual("middle", summaries[1].SaveId);
            Assert.AreEqual("older", summaries[2].SaveId);

            var newest = summaries[0];
            Assert.IsTrue(newest.IsReadable);
            Assert.AreEqual(SaveReadError.None, newest.Status);
            Assert.AreEqual("Skyline", newest.TowerName);
            Assert.AreEqual(nameof(GameDifficulty.Hard), newest.Difficulty);
            Assert.AreEqual(17, newest.DayIndex);
            Assert.AreEqual(731, newest.MinuteOfDay);
            Assert.AreEqual(22, newest.WalletBalance);
            Assert.AreEqual(2, newest.Stars);
            Assert.AreEqual(
                DateTime.Parse("2026-03-03T00:00:00.0000000Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                newest.ModifiedUtc);
            Assert.AreEqual(DateTimeKind.Utc, newest.ModifiedUtc.Kind);
        }

        [Test]
        public void List_keeps_corrupt_saves_visible_with_an_error_status()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save("healthy", Snapshot(11, "healthy", "2026-01-01T00:00:00.0000000Z")).Success);
            Assert.IsTrue(repository.Save("broken", Snapshot(22, "broken", "2026-02-02T00:00:00.0000000Z")).Success);
            File.WriteAllText(CurrentPath("broken"), "corrupted bytes");

            var summaries = repository.List();

            Assert.AreEqual(2, summaries.Count);
            Assert.AreEqual("healthy", summaries[0].SaveId);
            Assert.IsTrue(summaries[0].IsReadable);
            Assert.AreEqual("broken", summaries[1].SaveId);
            Assert.IsFalse(summaries[1].IsReadable);
            Assert.AreEqual(SaveReadError.InvalidEnvelope, summaries[1].Status);
            Assert.AreEqual(string.Empty, summaries[1].TowerName);
            Assert.AreEqual(0, summaries[1].WalletBalance);
            Assert.IsTrue(File.Exists(CurrentPath("broken")), "A corrupt save stays on disk.");
        }

        [Test]
        public void List_reports_unsupported_saves_without_removing_them()
        {
            var repository = new LocalSaveRepository(root);
            var future = Snapshot(5);
            future.schemaVersion = SaveSchema.CurrentVersion + 1;
            Directory.CreateDirectory(root);
            File.WriteAllText(CurrentPath(SaveId), SaveSerializer.Serialize(future));

            var summaries = repository.List();

            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual(SaveReadError.UnsupportedSchema, summaries[0].Status);
            Assert.IsTrue(File.Exists(CurrentPath(SaveId)));
        }

        [Test]
        public void List_ignores_temp_and_recovery_files()
        {
            var repository = new LocalSaveRepository(root);
            SaveRevisions(repository, 4);
            File.WriteAllText(Path.Combine(root, SaveId + ".tmp"), "partial");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "unrelated");

            var summaries = repository.List();

            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual(SaveId, summaries[0].SaveId);
        }

        [Test]
        public void List_returns_no_summaries_when_the_root_is_missing()
        {
            var repository = new LocalSaveRepository(root);

            Assert.AreEqual(0, repository.List().Count);
        }

        [Test]
        public void List_never_exposes_the_save_payload()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            var payloadFragment = PayloadFragment(CurrentPath(SaveId));

            var summary = repository.List()[0];

            foreach (var property in typeof(LocalSaveSummary).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                StringAssert.DoesNotContain("payload", property.Name.ToLowerInvariant());
                StringAssert.DoesNotContain("base64", property.Name.ToLowerInvariant());
                StringAssert.DoesNotContain("checksum", property.Name.ToLowerInvariant());

                var value = property.GetValue(summary) as string;
                if (value != null)
                    StringAssert.DoesNotContain(payloadFragment, value);
            }

            Assert.AreEqual(
                0,
                typeof(LocalSaveSummary).GetFields(BindingFlags.Public | BindingFlags.Instance).Length,
                "The summary must not expose public fields.");
        }

        [Test]
        public void A_blocked_temp_path_fails_the_save_and_preserves_the_current_file()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            var currentBytes = File.ReadAllBytes(CurrentPath(SaveId));
            var blockedTempPath = Path.Combine(root, SaveId + ".tmp");
            Directory.CreateDirectory(blockedTempPath);

            var result = repository.Save(SaveId, Snapshot(2));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.TempWriteFailed, result.Error);
            Assert.AreEqual(currentBytes, File.ReadAllBytes(CurrentPath(SaveId)));
            Assert.AreEqual(1, WalletOf(repository.Read(SaveId)));
            Assert.IsTrue(Directory.Exists(blockedTempPath), "A non-regular temp path must never be deleted.");
        }

        [Test]
        public void A_locked_current_file_fails_the_save_and_leaves_the_previous_save_readable()
        {
            var repository = new LocalSaveRepository(root);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            var currentBytes = File.ReadAllBytes(CurrentPath(SaveId));

            SaveWriteResult result;
            using (new FileStream(CurrentPath(SaveId), FileMode.Open, FileAccess.Read, FileShare.Read))
                result = repository.Save(SaveId, Snapshot(2));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveWriteError.ReplaceFailed, result.Error);
            Assert.AreEqual(currentBytes, File.ReadAllBytes(CurrentPath(SaveId)), "The current save must survive unchanged.");
            Assert.AreEqual(1, WalletOf(repository.Read(SaveId)));
            Assert.IsFalse(
                File.Exists(RecoveryPath(SaveId, 1)),
                "A failed replacement must not consume a recovery slot.");
            Assert.IsFalse(File.Exists(Path.Combine(root, SaveId + ".tmp")), "A regular temp file must be cleaned up.");
            Assert.IsFalse(File.Exists(Path.Combine(root, SaveId + ".stage")), "Staging files must be cleaned up.");
        }

        [Test]
        public void List_returns_a_read_only_view()
        {
            var repository = new LocalSaveRepository(root);
            var empty = repository.List() as IList<LocalSaveSummary>;
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            var populated = repository.List() as IList<LocalSaveSummary>;

            Assert.IsNotNull(empty);
            Assert.IsNotNull(populated);
            Assert.IsTrue(empty.IsReadOnly);
            Assert.IsTrue(populated.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => populated.Add(populated[0]));
            Assert.Throws<NotSupportedException>(() => populated.Clear());
        }

        static IEnumerable<TestCaseData> UnsafeSaveIds()
        {
            yield return UnsafeSaveId(null, "null");
            yield return UnsafeSaveId(string.Empty, "empty");
            yield return UnsafeSaveId("   ", "blank");
            yield return UnsafeSaveId("..", "parent_directory");
            yield return UnsafeSaveId("../evil", "forward_traversal");
            yield return UnsafeSaveId("..\\evil", "backward_traversal");
            yield return UnsafeSaveId("nested/child", "forward_separator");
            yield return UnsafeSaveId("nested\\child", "backward_separator");
            yield return UnsafeSaveId("C:\\Windows\\system32\\config", "rooted_windows_path");
            yield return UnsafeSaveId("/etc/passwd", "rooted_posix_path");
            yield return UnsafeSaveId("drive:name", "drive_separator");
            yield return UnsafeSaveId("star*name", "wildcard_star");
            yield return UnsafeSaveId("quest?ion", "wildcard_question");
            yield return UnsafeSaveId("pipe|name", "pipe");
            yield return UnsafeSaveId("quote\"name", "quote");
            yield return UnsafeSaveId("trailing.", "trailing_dot");
            yield return UnsafeSaveId("-leading", "leading_dash");
            yield return UnsafeSaveId("_leading", "leading_underscore");
            yield return UnsafeSaveId("with space", "space");
            yield return UnsafeSaveId("unicode\u00e9", "non_ascii");
            yield return UnsafeSaveId("line\nbreak", "line_break");
            yield return UnsafeSaveId("null\0byte", "null_byte");
            yield return UnsafeSaveId("CON", "reserved_device");
            yield return UnsafeSaveId("com1", "reserved_port_device");
            yield return UnsafeSaveId(new string('a', 65), "too_long");
        }

        static TestCaseData UnsafeSaveId(string saveId, string caseName)
        {
            return new TestCaseData(saveId).SetName("{m}_" + caseName);
        }

        static TowerSnapshotV1 Snapshot(int walletBalance)
        {
            return Snapshot(walletBalance, SaveId, "2026-09-08T01:02:03.0000000Z");
        }

        static TowerSnapshotV1 Snapshot(int walletBalance, string saveId, string modifiedUtc)
        {
            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = saveId,
                towerName = "Skyline",
                difficulty = nameof(GameDifficulty.Hard),
                modifiedUtc = modifiedUtc,
                walletBalance = walletBalance,
                stars = 2,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 17,
                    minuteOfDay = 731,
                    minuteAccumulator = 0.5f,
                    minutesPerRealSecond = 10f
                },
                rooms = new RoomSnapshotV1[0]
            };
        }

        byte[] LeaveUnpublishedStage(LocalSaveRepository repository)
        {
            SaveRevisions(repository, 3);
            var leftoverBytes = File.ReadAllBytes(CurrentPath(SaveId));
            var blockedRecoveryPath = RecoveryPath(SaveId, 3);
            Directory.CreateDirectory(blockedRecoveryPath);
            Assert.IsFalse(repository.Save(SaveId, Snapshot(4)).Success);
            Directory.Delete(blockedRecoveryPath);
            return leftoverBytes;
        }

        static void SaveRevisions(LocalSaveRepository repository, int revisions)
        {
            for (var revision = 1; revision <= revisions; revision++)
            {
                var result = repository.Save(SaveId, Snapshot(revision));
                Assert.IsTrue(result.Success, result.ErrorMessage);
            }
        }

        static int WalletOf(SaveReadResult result)
        {
            Assert.IsTrue(result.Success, result.ErrorMessage);
            return result.Snapshot.walletBalance;
        }

        /// <summary>
        /// Reserves an oversized file without materializing its bytes, so the guard can be proved
        /// without allocating the file in memory or writing it out.
        /// </summary>
        long CreateOversizedSaveFile(string saveId)
        {
            var length = LocalSaveRepository.MaxSaveFileBytes + 1;
            Directory.CreateDirectory(root);
            using (var stream = new FileStream(CurrentPath(saveId), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.SetLength(length);

            return length;
        }

        static string PayloadFragment(string path)
        {
            const string marker = "\"payloadBase64\":\"";
            var text = File.ReadAllText(path);
            var start = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.Greater(start, -1, "The save file must contain a Base64 payload.");
            return text.Substring(start + marker.Length, 24);
        }

        string CurrentPath(string saveId)
        {
            return Path.Combine(root, saveId + ".batsave");
        }

        string RecoveryPath(string saveId, int recoveryIndex)
        {
            return Path.Combine(root, saveId + ".bak" + recoveryIndex.ToString(CultureInfo.InvariantCulture));
        }

        string FileFingerprints()
        {
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
