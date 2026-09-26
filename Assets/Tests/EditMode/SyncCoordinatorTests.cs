using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class SyncCoordinatorTests
    {
        const string AccountId = "account-123";
        const string SaveId = "tower-a";

        string root;
        LocalSaveRepository repository;
        StubCloudSaveTransport transport;

        [SetUp]
        public void SetUp()
        {
            GameSession.ResetForTests();
            root = Path.Combine(Path.GetTempPath(), "bat-sync-" + Guid.NewGuid().ToString("N"));
            repository = new LocalSaveRepository(root);
            transport = new StubCloudSaveTransport();
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetForTests();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public async Task Local_save_failure_skips_upload_and_never_reports_synced()
        {
            GameSession.SetCurrentAccountId(AccountId);
            var coordinator = new SyncCoordinator(transport, repository);
            var localFailure = SaveWriteResult.Failed(SaveWriteError.ReplaceFailed, "Local write failed.");

            var result = await coordinator.EnqueueUploadAfterLocalSuccess(
                localFailure,
                1,
                Snapshot(10),
                4);

            Assert.AreEqual(SyncStatus.Error, result.Status);
            Assert.AreEqual(0, transport.Uploads.Count);
            Assert.AreNotEqual(SyncStatus.Synced, coordinator.Status);
        }

        [Test]
        public async Task Signed_out_upload_is_no_op()
        {
            var coordinator = new SyncCoordinator(transport, repository);

            var result = await coordinator.EnqueueUploadAfterLocalSuccess(
                SaveWriteResult.Succeeded(),
                1,
                Snapshot(10),
                0);

            Assert.AreEqual(SyncStatus.Synced, result.Status);
            Assert.AreEqual(0, transport.Uploads.Count);
        }

        [Test]
        public async Task Email_unverified_upload_is_no_op()
        {
            GameSession.SetCurrentAccountId(AccountId);
            transport.UploadResult = CloudSlotUploadResult.Unverified();
            var coordinator = new SyncCoordinator(transport, repository);

            var result = await coordinator.EnqueueUploadAfterLocalSuccess(
                SaveWriteResult.Succeeded(),
                1,
                Snapshot(10),
                0);

            Assert.AreEqual(SyncStatus.Synced, result.Status);
            Assert.AreEqual(1, transport.Uploads.Count);
            Assert.IsNull(coordinator.LastConflict);
        }

        [Test]
        public async Task Upload_conflict_sets_conflict_status_and_metadata()
        {
            GameSession.SetCurrentAccountId(AccountId);
            transport.UploadResult = CloudSlotUploadResult.Conflict(
                new CloudConflictInfo(8, "Cloud Tower", "CloudPC", 42));
            var coordinator = new SyncCoordinator(transport, repository);

            var result = await coordinator.EnqueueUploadAfterLocalSuccess(
                SaveWriteResult.Succeeded(),
                2,
                Snapshot(25),
                3);

            Assert.AreEqual(SyncStatus.Conflict, result.Status);
            Assert.AreEqual(SyncStatus.Conflict, coordinator.Status);
            Assert.AreEqual(8, coordinator.LastConflict.CurrentRevision);
            Assert.AreEqual("Cloud Tower", coordinator.LastConflict.TowerName);
            Assert.AreEqual(2, transport.Uploads[0].SlotId);
            Assert.AreEqual(3, transport.Uploads[0].Request.expectedRevision);
        }

        [Test]
        public async Task Keep_local_resolves_conflict_with_cloud_current_revision()
        {
            GameSession.SetCurrentAccountId(AccountId);
            transport.UploadResult = CloudSlotUploadResult.Conflict(
                new CloudConflictInfo(5, "Cloud Tower", "CloudPC", 30));
            var coordinator = new SyncCoordinator(transport, repository);

            await coordinator.EnqueueUploadAfterLocalSuccess(
                SaveWriteResult.Succeeded(),
                1,
                Snapshot(50),
                2);

            transport.UploadResult = CloudSlotUploadResult.Succeeded(6);
            var result = await coordinator.TryResolveKeepLocal();

            Assert.AreEqual(SyncStatus.Synced, result.Status);
            Assert.AreEqual(2, transport.Uploads.Count);
            Assert.AreEqual(5, transport.Uploads[1].Request.expectedRevision);
            Assert.AreEqual(6, coordinator.CloudRevision);
            Assert.IsNull(coordinator.LastConflict);
        }

        [Test]
        public async Task Keep_cloud_downloads_cloud_snapshot_and_writes_local_save()
        {
            GameSession.SetCurrentAccountId(AccountId);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            transport.UploadResult = CloudSlotUploadResult.Conflict(
                new CloudConflictInfo(7, "Cloud Tower", "Laptop", 90));
            transport.DownloadResult = CloudSlotDownloadResult.Succeeded(CloudDownload(Snapshot(99), 7));
            var coordinator = new SyncCoordinator(transport, repository);

            await coordinator.EnqueueUploadAfterLocalSuccess(
                SaveWriteResult.Succeeded(),
                1,
                Snapshot(2),
                4);
            var result = await coordinator.TryResolveKeepCloud();

            Assert.AreEqual(SyncStatus.Synced, result.Status);
            Assert.AreEqual(99, repository.Read(SaveId).Snapshot.walletBalance);
            Assert.AreEqual(7, coordinator.CloudRevision);
            Assert.IsNull(coordinator.LastConflict);
        }

        [Test]
        public async Task Keep_cloud_download_failure_does_not_replace_local_save()
        {
            GameSession.SetCurrentAccountId(AccountId);
            Assert.IsTrue(repository.Save(SaveId, Snapshot(1)).Success);
            transport.UploadResult = CloudSlotUploadResult.Conflict(
                new CloudConflictInfo(7, "Cloud Tower", "Laptop", 90));
            transport.DownloadResult = CloudSlotDownloadResult.Offline("offline");
            var coordinator = new SyncCoordinator(transport, repository);

            await coordinator.EnqueueUploadAfterLocalSuccess(
                SaveWriteResult.Succeeded(),
                1,
                Snapshot(2),
                4);
            var result = await coordinator.TryResolveKeepCloud();

            Assert.AreEqual(SyncStatus.Pending, result.Status);
            Assert.AreEqual(1, repository.Read(SaveId).Snapshot.walletBalance);
            Assert.AreEqual(SyncStatus.Pending, coordinator.Status);
        }

        [Test]
        public void CreateDefault_uses_account_specific_save_root_when_signed_in()
        {
            var repository = LocalSaveRepository.CreateDefault("account-123");

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Saves", "account-123")),
                repository.RootDirectory);
        }

        static CloudSlotDownload CloudDownload(TowerSnapshotV1 snapshot, long revision)
        {
            var envelope = JsonUtility.FromJson<SaveEnvelope>(SaveSerializer.Serialize(snapshot));
            return new CloudSlotDownload
            {
                slotId = 1,
                revision = revision,
                towerName = snapshot.towerName,
                schemaVersion = snapshot.schemaVersion,
                checksum = envelope.payloadChecksum,
                payloadBase64 = envelope.payloadBase64,
                deviceName = "CloudPC",
                playMinutes = 60,
                gameVersion = "test",
                clientInstallId = "cloud-install",
                modifiedUtc = snapshot.modifiedUtc
            };
        }

        static TowerSnapshotV1 Snapshot(int walletBalance)
        {
            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = SaveId,
                towerName = "Skyline",
                difficulty = nameof(GameDifficulty.Hard),
                modifiedUtc = "2026-09-08T01:02:03.0000000Z",
                walletBalance = walletBalance,
                stars = 2,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 17,
                    minuteOfDay = 731,
                    minuteAccumulator = 0.5f,
                    minutesPerRealSecond = 10f
                },
                research = new ResearchSnapshotV1
                {
                    completed = new ResearchCompletedNodeV1[0],
                    progress = new ResearchProgressNodeV1[0],
                    activeBranch = string.Empty,
                    activeLevel = 0,
                    paused = false
                },
                rooms = new RoomSnapshotV1[0]
            };
        }

        sealed class StubCloudSaveTransport : ICloudSaveTransport
        {
            public readonly List<CloudUploadCall> Uploads = new List<CloudUploadCall>();
            public CloudSlotUploadResult UploadResult = CloudSlotUploadResult.Succeeded(1);
            public CloudSlotDownloadResult DownloadResult = CloudSlotDownloadResult.Offline("No download configured.");

            public Task<CloudSlotUploadResult> UploadSlot(
                int slotId,
                CloudSlotUpload request,
                CancellationToken cancellationToken)
            {
                Uploads.Add(new CloudUploadCall(slotId, request));
                return Task.FromResult(UploadResult);
            }

            public Task<CloudSlotDownloadResult> DownloadSlot(int slotId, CancellationToken cancellationToken)
            {
                return Task.FromResult(DownloadResult);
            }
        }

        sealed class CloudUploadCall
        {
            public CloudUploadCall(int slotId, CloudSlotUpload request)
            {
                SlotId = slotId;
                Request = request;
            }

            public int SlotId { get; }
            public CloudSlotUpload Request { get; }
        }
    }
}
