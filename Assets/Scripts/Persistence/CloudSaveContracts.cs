using System;
using System.Collections.Generic;
using System.Net;

namespace BuildATower
{
    [Serializable]
    public sealed class CloudSaveConfig
    {
        public const string DefaultApiBaseUrl = "https://api.escapeproductions.biz";

        public CloudSaveConfig()
            : this(DefaultApiBaseUrl)
        {
        }

        public CloudSaveConfig(string apiBaseUrl)
        {
            ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                ? DefaultApiBaseUrl
                : apiBaseUrl.TrimEnd('/');
        }

        public string ApiBaseUrl { get; }
    }

    public enum CloudError
    {
        None,
        Offline,
        Unauthorized,
        MissingCredentials,
        InvalidResponse,
        HttpError
    }

    public enum SyncStatus
    {
        Synced,
        Pending,
        Offline,
        Conflict,
        Error
    }

    public sealed class SyncResult
    {
        private SyncResult(SyncStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public SyncStatus Status { get; }
        public string Message { get; }

        public static SyncResult FromStatus(SyncStatus status, string message = null)
        {
            return new SyncResult(status, message);
        }
    }

    [Serializable]
    public sealed class CloudSlotUpload
    {
        public long expectedRevision;
        public string towerName;
        public int schemaVersion;
        public string checksum;
        public string payloadBase64;
        public string deviceName;
        public int playMinutes;
        public string gameVersion;
        public string clientInstallId;
    }

    [Serializable]
    public sealed class CloudSlotDownload
    {
        public int slotId;
        public long revision;
        public string towerName;
        public int schemaVersion;
        public string checksum;
        public string payloadBase64;
        public string deviceName;
        public int playMinutes;
        public string gameVersion;
        public string clientInstallId;
        public string modifiedUtc;
    }

    [Serializable]
    public sealed class CloudConflictDto
    {
        public string code;
        public long currentRevision;
        public string towerName;
        public string modifiedUtc;
        public string deviceName;
        public int playMinutes;
    }

    [Serializable]
    public sealed class CloudRevisionDto
    {
        public long revision;
    }

    public sealed class CloudConflictInfo
    {
        public CloudConflictInfo(long currentRevision, string towerName, string deviceName, int playMinutes)
        {
            CurrentRevision = currentRevision;
            TowerName = towerName;
            DeviceName = deviceName;
            PlayMinutes = playMinutes;
        }

        public long CurrentRevision { get; }
        public string TowerName { get; }
        public string DeviceName { get; }
        public int PlayMinutes { get; }
    }

    public interface ICloudSaveTransport
    {
        System.Threading.Tasks.Task<CloudSlotUploadResult> UploadSlot(
            int slotId,
            CloudSlotUpload request,
            System.Threading.CancellationToken cancellationToken);

        System.Threading.Tasks.Task<CloudSlotDownloadResult> DownloadSlot(
            int slotId,
            System.Threading.CancellationToken cancellationToken);
    }

    public interface ICloudSlotSource
    {
        System.Threading.Tasks.Task<CloudSlotListResult> ListSlots(
            System.Threading.CancellationToken cancellationToken = default);

        System.Threading.Tasks.Task<CloudSlotDownloadResult> DownloadSlot(
            int slotId,
            System.Threading.CancellationToken cancellationToken = default);
    }

    public sealed class CloudSlotSummary
    {
        private CloudSlotSummary(
            int slotId,
            bool occupied,
            string towerName,
            long revision,
            DateTime modifiedUtc,
            string deviceName,
            int playMinutes)
        {
            SlotId = slotId;
            Occupied = occupied;
            TowerName = towerName ?? string.Empty;
            Revision = revision;
            ModifiedUtc = DateTime.SpecifyKind(modifiedUtc, DateTimeKind.Utc);
            DeviceName = deviceName ?? string.Empty;
            PlayMinutes = playMinutes;
        }

        public int SlotId { get; }
        public bool Occupied { get; }
        public string TowerName { get; }
        public long Revision { get; }
        public DateTime ModifiedUtc { get; }
        public string DeviceName { get; }
        public int PlayMinutes { get; }

        public static CloudSlotSummary Empty(int slotId)
        {
            return new CloudSlotSummary(slotId, false, string.Empty, 0, new DateTime(0L, DateTimeKind.Utc), string.Empty, 0);
        }

        public static CloudSlotSummary OccupiedSlot(
            int slotId,
            string towerName,
            long revision,
            DateTime modifiedUtc,
            string deviceName,
            int playMinutes)
        {
            return new CloudSlotSummary(slotId, true, towerName, revision, modifiedUtc, deviceName, playMinutes);
        }
    }

    public sealed class CloudSlotListResult : CloudResult
    {
        CloudSlotListResult(CloudError error, string errorMessage, IReadOnlyList<CloudSlotSummary> slots)
            : base(error, errorMessage)
        {
            Slots = slots ?? Array.Empty<CloudSlotSummary>();
        }

        public IReadOnlyList<CloudSlotSummary> Slots { get; }

        public static CloudSlotListResult Succeeded(IReadOnlyList<CloudSlotSummary> slots)
        {
            return new CloudSlotListResult(CloudError.None, string.Empty, slots);
        }

        public new static CloudSlotListResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed cloud slot list must have an error.", nameof(error));

            return new CloudSlotListResult(error, errorMessage, Array.Empty<CloudSlotSummary>());
        }
    }

    public sealed class CloudSlotUploadResult
    {
        private CloudSlotUploadResult(
            CloudError error,
            string errorMessage,
            long revision,
            CloudConflictInfo conflict,
            bool unverified)
        {
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
            Revision = revision;
            ConflictInfo = conflict;
            IsUnverified = unverified;
        }

        public bool Success => Error == CloudError.None && ConflictInfo == null && !IsUnverified;
        public CloudError Error { get; }
        public string ErrorMessage { get; }
        public long Revision { get; }
        public CloudConflictInfo ConflictInfo { get; }
        public bool IsUnverified { get; }

        public static CloudSlotUploadResult Succeeded(long revision)
        {
            return new CloudSlotUploadResult(CloudError.None, string.Empty, revision, null, false);
        }

        public static CloudSlotUploadResult Conflict(CloudConflictInfo conflict)
        {
            return new CloudSlotUploadResult(CloudError.None, string.Empty, 0, conflict, false);
        }

        public static CloudSlotUploadResult Unverified()
        {
            return new CloudSlotUploadResult(CloudError.None, string.Empty, 0, null, true);
        }

        public static CloudSlotUploadResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed cloud upload must have an error.", nameof(error));

            return new CloudSlotUploadResult(error, errorMessage, 0, null, false);
        }
    }

    public sealed class CloudSlotDownloadResult
    {
        private CloudSlotDownloadResult(CloudError error, string errorMessage, CloudSlotDownload download)
        {
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
            Download = download;
        }

        public bool Success => Error == CloudError.None;
        public CloudError Error { get; }
        public string ErrorMessage { get; }
        public CloudSlotDownload Download { get; }

        public static CloudSlotDownloadResult Succeeded(CloudSlotDownload download)
        {
            if (download == null)
                throw new ArgumentNullException(nameof(download));

            return new CloudSlotDownloadResult(CloudError.None, string.Empty, download);
        }

        public static CloudSlotDownloadResult Offline(string errorMessage)
        {
            return Failed(CloudError.Offline, errorMessage);
        }

        public static CloudSlotDownloadResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed cloud download must have an error.", nameof(error));

            return new CloudSlotDownloadResult(error, errorMessage, null);
        }
    }

    public class CloudResult
    {
        protected CloudResult(CloudError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success => Error == CloudError.None;
        public CloudError Error { get; }
        public string ErrorMessage { get; }

        public static CloudResult Succeeded()
        {
            return new CloudResult(CloudError.None, string.Empty);
        }

        public static CloudResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed cloud result must have an error.", nameof(error));

            return new CloudResult(error, errorMessage);
        }
    }

    public sealed class CloudAuthResult : CloudResult
    {
        CloudAuthResult(
            CloudError error,
            string errorMessage,
            string accountId,
            string accessToken,
            int expiresIn)
            : base(error, errorMessage)
        {
            AccountId = accountId;
            AccessToken = accessToken;
            ExpiresIn = expiresIn;
        }

        public string AccountId { get; }
        public string AccessToken { get; }
        public int ExpiresIn { get; }

        public static CloudAuthResult Succeeded(string accountId, string accessToken, int expiresIn)
        {
            return new CloudAuthResult(CloudError.None, string.Empty, accountId, accessToken, expiresIn);
        }

        public new static CloudAuthResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed auth result must have an error.", nameof(error));

            return new CloudAuthResult(error, errorMessage, null, null, 0);
        }
    }

    public sealed class CloudRegisterResult : CloudResult
    {
        CloudRegisterResult(
            CloudError error,
            string errorMessage,
            string userId,
            string email,
            bool emailConfirmed)
            : base(error, errorMessage)
        {
            UserId = userId;
            Email = email;
            EmailConfirmed = emailConfirmed;
        }

        public string UserId { get; }
        public string Email { get; }
        public bool EmailConfirmed { get; }

        public static CloudRegisterResult Succeeded(string userId, string email, bool emailConfirmed)
        {
            return new CloudRegisterResult(
                CloudError.None,
                string.Empty,
                userId,
                email,
                emailConfirmed);
        }

        public new static CloudRegisterResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed register result must have an error.", nameof(error));

            return new CloudRegisterResult(error, errorMessage, null, null, false);
        }
    }

    public sealed class CloudHttpResult : CloudResult
    {
        CloudHttpResult(CloudError error, string errorMessage, HttpStatusCode statusCode, string body)
            : base(error, errorMessage)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public HttpStatusCode StatusCode { get; }
        public string Body { get; }

        public static CloudHttpResult Succeeded(HttpStatusCode statusCode, string body)
        {
            return new CloudHttpResult(CloudError.None, string.Empty, statusCode, body);
        }

        public new static CloudHttpResult Failed(CloudError error, string errorMessage)
        {
            return Failed(error, errorMessage, 0, string.Empty);
        }

        public static CloudHttpResult Failed(
            CloudError error,
            string errorMessage,
            HttpStatusCode statusCode,
            string body)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed HTTP result must have an error.", nameof(error));

            return new CloudHttpResult(error, errorMessage, statusCode, body);
        }
    }
}
