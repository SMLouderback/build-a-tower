using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BuildATower
{
    public sealed class SyncCoordinator
    {
        const string ClientInstallIdKey = "BuildATower.CloudSave.ClientInstallId";

        readonly ICloudSaveTransport transport;
        readonly LocalSaveRepository repository;

        TowerSnapshotV1 pendingSnapshot;
        int pendingSlotId;
        int consecutiveFailures;

        public SyncCoordinator(AccountClient accountClient, LocalSaveRepository repository)
            : this(new AccountClientCloudSaveTransport(accountClient), repository)
        {
        }

        public SyncCoordinator(ICloudSaveTransport transport, LocalSaveRepository repository)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Status = SyncStatus.Synced;
        }

        public SyncStatus Status { get; private set; }
        public CloudConflictInfo LastConflict { get; private set; }
        public long CloudRevision { get; private set; }
        public int NextRetryDelaySeconds { get; private set; }

        public async Task<SyncResult> EnqueueUploadAfterLocalSuccess(
            SaveWriteResult localWrite,
            int slotId,
            TowerSnapshotV1 snapshot,
            long lastSeenRevision,
            CancellationToken cancellationToken = default)
        {
            if (localWrite == null || !localWrite.Success)
                return SetStatus(SyncStatus.Error, "Cloud upload skipped because the local save failed.");

            if (string.IsNullOrWhiteSpace(GameSession.CurrentAccountId))
                return SetStatus(SyncStatus.Synced);

            if (snapshot == null)
                return SetStatus(SyncStatus.Error, "Cloud upload skipped because the snapshot is missing.");

            pendingSlotId = slotId;
            pendingSnapshot = snapshot;
            ApplyCloudMetadata(snapshot, slotId, lastSeenRevision);

            var upload = CreateUpload(snapshot, lastSeenRevision);
            var result = await transport.UploadSlot(slotId, upload, cancellationToken);
            return ApplyUploadResult(result, snapshot);
        }

        public async Task<SyncResult> TryResolveKeepLocal(CancellationToken cancellationToken = default)
        {
            if (LastConflict == null || pendingSnapshot == null)
                return SetStatus(SyncStatus.Error, "No cloud conflict is waiting to be resolved.");

            var expectedRevision = LastConflict.CurrentRevision;
            ApplyCloudMetadata(pendingSnapshot, pendingSlotId, expectedRevision);
            var upload = CreateUpload(pendingSnapshot, expectedRevision);
            var result = await transport.UploadSlot(pendingSlotId, upload, cancellationToken);
            return ApplyUploadResult(result, pendingSnapshot);
        }

        public async Task<SyncResult> TryResolveKeepCloud(CancellationToken cancellationToken = default)
        {
            if (LastConflict == null)
                return SetStatus(SyncStatus.Error, "No cloud conflict is waiting to be resolved.");

            var result = await transport.DownloadSlot(pendingSlotId, cancellationToken);
            if (!result.Success)
                return MarkPending(result.ErrorMessage);

            TowerSnapshotV1 snapshot;
            try
            {
                snapshot = DecodeDownload(result.Download);
            }
            catch (Exception exception) when (IsExpectedDataException(exception))
            {
                return SetStatus(SyncStatus.Error, "The cloud save payload could not be read.");
            }

            var localWrite = repository.Save(snapshot.saveId, snapshot);
            if (!localWrite.Success)
                return SetStatus(SyncStatus.Error, localWrite.ErrorMessage);

            CloudRevision = result.Download.revision;
            pendingSnapshot = null;
            LastConflict = null;
            consecutiveFailures = 0;
            NextRetryDelaySeconds = 0;
            return SetStatus(SyncStatus.Synced);
        }

        SyncResult ApplyUploadResult(CloudSlotUploadResult result, TowerSnapshotV1 snapshot)
        {
            if (result.IsUnverified)
            {
                LastConflict = null;
                return SetStatus(SyncStatus.Synced);
            }

            if (result.ConflictInfo != null)
            {
                LastConflict = result.ConflictInfo;
                return SetStatus(SyncStatus.Conflict);
            }

            if (!result.Success)
            {
                if (result.Error == CloudError.Offline)
                    return MarkPending(result.ErrorMessage);

                return SetStatus(SyncStatus.Error, result.ErrorMessage);
            }

            CloudRevision = result.Revision;
            if (snapshot != null)
                snapshot.cloudRevision = result.Revision;

            pendingSnapshot = null;
            LastConflict = null;
            consecutiveFailures = 0;
            NextRetryDelaySeconds = 0;
            return SetStatus(SyncStatus.Synced);
        }

        SyncResult MarkPending(string message)
        {
            consecutiveFailures++;
            NextRetryDelaySeconds = consecutiveFailures == 1
                ? 2
                : consecutiveFailures == 2
                    ? 8
                    : 32;
            return SetStatus(SyncStatus.Pending, message);
        }

        SyncResult SetStatus(SyncStatus status, string message = null)
        {
            Status = status;
            return SyncResult.FromStatus(status, message);
        }

        static CloudSlotUpload CreateUpload(TowerSnapshotV1 snapshot, long expectedRevision)
        {
            var envelope = JsonUtility.FromJson<SaveEnvelope>(SaveSerializer.Serialize(snapshot));
            return new CloudSlotUpload
            {
                expectedRevision = expectedRevision,
                towerName = snapshot.towerName,
                schemaVersion = snapshot.schemaVersion,
                checksum = envelope.payloadChecksum,
                payloadBase64 = envelope.payloadBase64,
                deviceName = string.IsNullOrWhiteSpace(snapshot.deviceName) ? SystemInfo.deviceName : snapshot.deviceName,
                playMinutes = snapshot.clock == null ? 0 : snapshot.clock.dayIndex * 1440 + snapshot.clock.minuteOfDay,
                gameVersion = string.IsNullOrWhiteSpace(snapshot.gameVersion) ? Application.version : snapshot.gameVersion,
                clientInstallId = string.IsNullOrWhiteSpace(snapshot.clientInstallId)
                    ? GetClientInstallId()
                    : snapshot.clientInstallId
            };
        }

        static void ApplyCloudMetadata(TowerSnapshotV1 snapshot, int slotId, long cloudRevision)
        {
            snapshot.accountId = GameSession.CurrentAccountId;
            snapshot.slotId = slotId;
            snapshot.cloudRevision = cloudRevision;
            snapshot.clientInstallId = string.IsNullOrWhiteSpace(snapshot.clientInstallId)
                ? GetClientInstallId()
                : snapshot.clientInstallId;
            snapshot.deviceName = string.IsNullOrWhiteSpace(snapshot.deviceName)
                ? SystemInfo.deviceName
                : snapshot.deviceName;
            snapshot.gameVersion = string.IsNullOrWhiteSpace(snapshot.gameVersion)
                ? Application.version
                : snapshot.gameVersion;
        }

        static TowerSnapshotV1 DecodeDownload(CloudSlotDownload download)
        {
            if (download == null)
                throw new InvalidDataException("The cloud download is missing.");

            var compressedPayload = Convert.FromBase64String(download.payloadBase64);
            var checksum = ComputeChecksum(compressedPayload);
            if (!ConstantTimeEquals(checksum, download.checksum))
                throw new InvalidDataException("The cloud payload checksum does not match.");

            using (var input = new MemoryStream(compressedPayload, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                var json = Encoding.UTF8.GetString(output.ToArray());
                var snapshot = JsonUtility.FromJson<TowerSnapshotV1>(json);
                if (snapshot == null)
                    throw new InvalidDataException("The cloud payload is empty.");

                snapshot.accountId = GameSession.CurrentAccountId;
                snapshot.slotId = download.slotId;
                snapshot.cloudRevision = download.revision;
                snapshot.clientInstallId = download.clientInstallId;
                snapshot.deviceName = download.deviceName;
                snapshot.gameVersion = download.gameVersion;
                return snapshot;
            }
        }

        static string GetClientInstallId()
        {
            var existing = PlayerPrefs.GetString(ClientInstallIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            var created = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(ClientInstallIdKey, created);
            PlayerPrefs.Save();
            return created;
        }

        static string ComputeChecksum(byte[] payload)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(payload);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                    builder.Append(value.ToString("x2"));

                return builder.ToString();
            }
        }

        static bool ConstantTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];

            return difference == 0;
        }

        static bool IsExpectedDataException(Exception exception)
        {
            return exception is ArgumentException
                || exception is FormatException
                || exception is IOException
                || exception is InvalidDataException;
        }
    }

    sealed class AccountClientCloudSaveTransport : ICloudSaveTransport
    {
        readonly AccountClient accountClient;

        public AccountClientCloudSaveTransport(AccountClient accountClient)
        {
            this.accountClient = accountClient ?? throw new ArgumentNullException(nameof(accountClient));
        }

        public async Task<CloudSlotUploadResult> UploadSlot(
            int slotId,
            CloudSlotUpload request,
            CancellationToken cancellationToken)
        {
            var result = await accountClient.SendAuthorized(
                () => new HttpRequestMessage(HttpMethod.Put, "/v1/saves/" + slotId)
                {
                    Content = new StringContent(JsonUtility.ToJson(request), Encoding.UTF8, "application/json")
                },
                cancellationToken);

            if (result.Success)
            {
                var revision = JsonUtility.FromJson<CloudRevisionDto>(result.Body);
                return CloudSlotUploadResult.Succeeded(revision == null ? 0 : revision.revision);
            }

            if (result.StatusCode == HttpStatusCode.Conflict)
            {
                var conflict = JsonUtility.FromJson<CloudConflictDto>(result.Body);
                return CloudSlotUploadResult.Conflict(
                    new CloudConflictInfo(
                        conflict == null ? 0 : conflict.currentRevision,
                        conflict == null ? null : conflict.towerName,
                        conflict == null ? null : conflict.deviceName,
                        conflict == null ? 0 : conflict.playMinutes));
            }

            if (result.StatusCode == HttpStatusCode.Forbidden
                && result.Body.IndexOf("email_unverified", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CloudSlotUploadResult.Unverified();
            }

            return CloudSlotUploadResult.Failed(result.Error, result.ErrorMessage);
        }

        public async Task<CloudSlotDownloadResult> DownloadSlot(int slotId, CancellationToken cancellationToken)
        {
            var result = await accountClient.SendAuthorized(
                () => new HttpRequestMessage(HttpMethod.Get, "/v1/saves/" + slotId),
                cancellationToken);

            if (result.Success)
            {
                var download = JsonUtility.FromJson<CloudSlotDownload>(result.Body);
                return download == null
                    ? CloudSlotDownloadResult.Failed(CloudError.InvalidResponse, "Cloud save download response was invalid.")
                    : CloudSlotDownloadResult.Succeeded(download);
            }

            return CloudSlotDownloadResult.Failed(result.Error, result.ErrorMessage);
        }
    }
}
