using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BuildATower
{
    public sealed class LocalSaveRowPresentation
    {
        public string SaveId { get; set; }
        public string TowerName { get; set; }
        public string DifficultyName { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public DateTime ModifiedLocal { get; set; }
        public DateTime GameCalendarDate { get; set; }
        public int GameHour { get; set; }
        public int GameMinute { get; set; }
        public int Funds { get; set; }
        public int Stars { get; set; }
        public bool CanLoadCurrent { get; set; }
        public string StatusMessage { get; set; }
        public string ModifiedLocalText { get; set; }
        public string GameDateText { get; set; }
        public string MetaText { get; set; }
    }

    public sealed class CloudSaveRowPresentation
    {
        public int SlotId { get; set; }
        public string TowerName { get; set; }
        public string MetaText { get; set; }
        public bool CanLoad { get; set; }
        public string StatusMessage { get; set; }
    }

    public sealed class LocalRecoveryChoice
    {
        public LocalRecoveryChoice(int recoveryIndex, string saveId, string towerName, DateTime modifiedUtc)
        {
            RecoveryIndex = recoveryIndex;
            SaveId = saveId ?? string.Empty;
            TowerName = towerName ?? string.Empty;
            ModifiedUtc = DateTime.SpecifyKind(modifiedUtc, DateTimeKind.Utc);
            CanLoad = true;
        }

        public int RecoveryIndex { get; }
        public string SaveId { get; }
        public string TowerName { get; }
        public DateTime ModifiedUtc { get; }
        public bool CanLoad { get; }
        public string LabelText { get; set; }
    }

    public static class LocalSavePresentation
    {
        public static string FormatModifiedLocal(DateTime utc, TimeZoneInfo zone)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                zone ?? TimeZoneInfo.Local);
            return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static string FormatGameDate(int dayIndex, int minuteOfDay)
        {
            var date = GameClock.DateForDayIndex(dayIndex);
            var wrapped = ((minuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) % GameClock.MinutesPerDay;
            var hour = wrapped / 60;
            var minute = wrapped % 60;
            return date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                   + " "
                   + hour.ToString("00", CultureInfo.InvariantCulture)
                   + ":"
                   + minute.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string ReadError(SaveReadError error)
        {
            switch (error)
            {
                case SaveReadError.InvalidEnvelope:
                case SaveReadError.InvalidBase64:
                case SaveReadError.ChecksumMismatch:
                    return "This save file is damaged and cannot be loaded.";
                case SaveReadError.UnsupportedSchema:
                    return "This save uses an unsupported version.";
                case SaveReadError.PayloadTooLarge:
                    return "This save file is too large to load.";
                case SaveReadError.InvalidPayload:
                    return "This save file cannot be restored.";
                case SaveReadError.InvalidSaveId:
                    return "This save cannot be loaded.";
                case SaveReadError.InvalidRecoveryIndex:
                    return "The recovery index must be between 1 and 3.";
                case SaveReadError.NotFound:
                    return "That save could not be found.";
                case SaveReadError.ReadFailed:
                    return "This save file could not be read.";
                case SaveReadError.AlreadyPending:
                    return "A load is already pending.";
                default:
                    return "This save cannot be loaded.";
            }
        }

        public static string WriteError(SaveWriteError error)
        {
            switch (error)
            {
                case SaveWriteError.InvalidSaveId:
                    return "The save ID is not a safe local file name.";
                case SaveWriteError.NullSnapshot:
                    return "The tower snapshot is missing.";
                case SaveWriteError.SaveIdMismatch:
                    return "The snapshot save ID does not match the requested save ID.";
                case SaveWriteError.SerializationFailed:
                    return "The tower snapshot could not be serialized.";
                case SaveWriteError.RootUnavailable:
                    return "The save directory could not be created.";
                case SaveWriteError.TempWriteFailed:
                    return "The temporary save file could not be written completely.";
                case SaveWriteError.ReplaceFailed:
                    return "The current save could not be replaced, so the previous save and its recoveries were kept.";
                case SaveWriteError.RecoveryRotationFailed:
                    return "The new save was published, but the recovery copies could not be rotated.";
                case SaveWriteError.NotInitialized:
                    return "The tower is not ready to save.";
                case SaveWriteError.CaptureFailed:
                    return "The current tower could not be captured.";
                default:
                    return "The tower could not be saved.";
            }
        }

        public static string PlayerFacingWriteError(SaveWriteResult result)
        {
            if (result == null)
                return WriteError(SaveWriteError.CaptureFailed);

            var message = result.ErrorMessage;
            if (!string.IsNullOrEmpty(message)
                && message.IndexOf('\\') < 0
                && message.IndexOf("exception", StringComparison.OrdinalIgnoreCase) < 0)
                return message;

            return WriteError(result.Error);
        }
    }

    /// <summary>
    /// Testable pause-menu save identity and dirty state. Time-only clock progress is not dirty;
    /// grid mutations, price-tier edits, gameplay wallet debit/credit, and midnight day-roll
    /// mutations after a successful save are. Trusted RestoreBalance is not dirty.
    /// </summary>
    public sealed class PauseSaveService
    {
        public const string DefaultTowerName = "My Tower";
        public const string SavedLocallyMessage = "Saved locally";
        public const string CleanQuitWarning = "Leave tower? Your latest local save is complete.";
        public const string DirtyQuitWarning = "Leave tower? Changes since the last successful save will be lost.";

        readonly SaveCoordinator _coordinator;
        readonly Func<string> _saveIdFactory;

        public PauseSaveService(SaveCoordinator coordinator, Func<string> saveIdFactory = null)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _saveIdFactory = saveIdFactory ?? CreateRandomSaveId;
        }

        public bool IsDirty => GameSession.IsSaveDirty;

        public string QuitWarning =>
            !GameSession.IsSaveDirty && !string.IsNullOrEmpty(GameSession.CurrentSaveId)
                ? CleanQuitWarning
                : DirtyQuitWarning;

        public void MarkDirty()
        {
            GameSession.MarkSaveDirty();
        }

        public void SyncFromSession()
        {
        }

        public string Save()
        {
            var saveId = GameSession.CurrentSaveId;
            var towerName = GameSession.CurrentTowerName;
            if (string.IsNullOrEmpty(saveId))
            {
                saveId = _saveIdFactory();
                towerName = DefaultTowerName;
            }

            var result = _coordinator.SaveCurrent(saveId, towerName);
            if (!result.Success)
            {
                GameSession.MarkSaveDirty();
                return LocalSavePresentation.PlayerFacingWriteError(result);
            }

            GameSession.RecordSaveIdentity(saveId, towerName);
            return SavedLocallyMessage;
        }

        static string CreateRandomSaveId()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            var chars = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var value = bytes[i];
                chars[i * 2] = ToHexChar(value >> 4);
                chars[i * 2 + 1] = ToHexChar(value & 0xF);
            }

            return new string(chars);
        }

        static char ToHexChar(int nibble)
        {
            return (char)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10));
        }
    }

    /// <summary>
    /// Testable HUD-visible restore fallback notice. Peek does not consume;
    /// dismiss/consume is explicit so startup ordering can finish first.
    /// </summary>
    public sealed class RestoreFallbackPresenter
    {
        public const string PlayerMessage = GameSession.RestoreFallbackPlayerMessage;

        public bool HasNotice => GameSession.HasRestoreFallbackNotice;

        public string Message => GameSession.RestoreFallbackNotice;

        public string Peek() => GameSession.RestoreFallbackNotice;

        public void Dismiss()
        {
            GameSession.ConsumeRestoreFallbackNotice();
        }
    }

    public sealed class LocalSaveMenuPresenter
    {
        public const string EmptySavesMessage = "No local saves yet.";
        public const string SaveDisabledTooltip =
            "Saves are created while you play. Pause the tower to save a local copy.";
        public const string LoadFailedFallback = "This save cannot be loaded.";

        readonly LocalSaveRepository _repository;
        readonly SaveCoordinator _coordinator;
        readonly Action _loadTowerScene;
        readonly TimeZoneInfo _displayTimeZone;
        ICloudSlotSource _cloudSlots;
        IReadOnlyList<CloudSlotSummary> _lastCloudSlots = Array.Empty<CloudSlotSummary>();

        public LocalSaveMenuPresenter(
            LocalSaveRepository repository,
            SaveCoordinator coordinator,
            Action loadTowerScene,
            TimeZoneInfo displayTimeZone)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _loadTowerScene = loadTowerScene ?? throw new ArgumentNullException(nameof(loadTowerScene));
            _displayTimeZone = displayTimeZone ?? TimeZoneInfo.Local;
        }

        public void ConfigureCloudSlots(ICloudSlotSource cloudSlots)
        {
            _cloudSlots = cloudSlots;
        }

        public IReadOnlyList<LocalSaveSummary> RefreshLocalSaves()
        {
            return _repository.List();
        }

        public async Task<IReadOnlyList<CloudSlotSummary>> RefreshCloudSlots(
            CancellationToken cancellationToken = default)
        {
            if (_cloudSlots == null || string.IsNullOrWhiteSpace(GameSession.CurrentAccountId))
            {
                _lastCloudSlots = Array.Empty<CloudSlotSummary>();
                return _lastCloudSlots;
            }

            var result = await _cloudSlots.ListSlots(cancellationToken);
            _lastCloudSlots = result.Success
                ? result.Slots
                : Array.Empty<CloudSlotSummary>();
            return _lastCloudSlots;
        }

        public IReadOnlyList<CloudSlotSummary> LastCloudSlots => _lastCloudSlots;

        public bool TryPrepareLoad(string saveId, out string errorMessage)
        {
            return FinishPreparation(_coordinator.PrepareLocalLoad(saveId), out errorMessage);
        }

        public bool TryPrepareRecovery(string saveId, int recoveryIndex, out string errorMessage)
        {
            return FinishPreparation(
                _coordinator.PrepareLocalRecovery(saveId, recoveryIndex),
                out errorMessage);
        }

        public IReadOnlyList<LocalRecoveryChoice> ListRecoveries(string saveId)
        {
            var options = new List<LocalRecoveryChoice>();
            for (var index = 1; index <= LocalSaveRepository.RecoveryCount; index++)
            {
                var read = _repository.ReadRecovery(saveId, index);
                if (!read.Success)
                    continue;
                if (!TowerSnapshotMapper.ValidateForRestore(read.Snapshot).Success)
                    continue;

                DateTimeOffset parsed;
                if (!DateTimeOffset.TryParseExact(
                        read.Snapshot.modifiedUtc,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out parsed)
                    || parsed.Offset != TimeSpan.Zero)
                    continue;

                var choice = new LocalRecoveryChoice(
                    index,
                    saveId,
                    read.Snapshot.towerName,
                    parsed.UtcDateTime)
                {
                    LabelText = "Recovery "
                                + index.ToString(CultureInfo.InvariantCulture)
                                + ": "
                                + (read.Snapshot.towerName ?? string.Empty)
                                + "  "
                                + LocalSavePresentation.FormatModifiedLocal(parsed.UtcDateTime, _displayTimeZone)
                };
                options.Add(choice);
            }

            return options;
        }

        public LocalSaveRowPresentation PresentRow(LocalSaveSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            var row = new LocalSaveRowPresentation
            {
                SaveId = summary.SaveId,
                TowerName = summary.TowerName ?? string.Empty,
                DifficultyName = summary.Difficulty ?? string.Empty,
                ModifiedUtc = summary.ModifiedUtc,
                Funds = summary.WalletBalance,
                Stars = summary.Stars,
                CanLoadCurrent = summary.IsReadable,
                StatusMessage = summary.IsReadable
                    ? string.Empty
                    : LocalSavePresentation.ReadError(summary.Status)
            };

            if (!summary.IsReadable)
            {
                row.ModifiedLocalText = string.Empty;
                row.GameDateText = string.Empty;
                row.MetaText = string.Empty;
                return row;
            }

            var utc = DateTime.SpecifyKind(summary.ModifiedUtc, DateTimeKind.Utc);
            row.ModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, _displayTimeZone);
            row.GameCalendarDate = GameClock.DateForDayIndex(summary.DayIndex);
            var minute = ((summary.MinuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) %
                         GameClock.MinutesPerDay;
            row.GameHour = minute / 60;
            row.GameMinute = minute % 60;
            row.ModifiedLocalText = row.ModifiedLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            row.GameDateText = LocalSavePresentation.FormatGameDate(summary.DayIndex, summary.MinuteOfDay);
            row.MetaText = row.DifficultyName
                           + "  "
                           + row.ModifiedLocalText
                           + "  "
                           + row.GameDateText
                           + "  "
                           + row.Funds.ToString(CultureInfo.InvariantCulture)
                           + "  "
                           + row.Stars.ToString(CultureInfo.InvariantCulture);
            return row;
        }

        public CloudSaveRowPresentation PresentCloudRow(CloudSlotSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            if (!summary.Occupied)
            {
                return new CloudSaveRowPresentation
                {
                    SlotId = summary.SlotId,
                    TowerName = "Cloud slot " + summary.SlotId.ToString(CultureInfo.InvariantCulture),
                    MetaText = "Empty cloud slot",
                    CanLoad = false,
                    StatusMessage = "Empty"
                };
            }

            var modified = summary.ModifiedUtc.Ticks == 0
                ? "unknown time"
                : LocalSavePresentation.FormatModifiedLocal(summary.ModifiedUtc, _displayTimeZone);
            var device = string.IsNullOrWhiteSpace(summary.DeviceName) ? "unknown device" : summary.DeviceName;
            return new CloudSaveRowPresentation
            {
                SlotId = summary.SlotId,
                TowerName = summary.TowerName,
                MetaText = "Cloud slot "
                           + summary.SlotId.ToString(CultureInfo.InvariantCulture)
                           + "  "
                           + device
                           + "  "
                           + modified
                           + "  rev "
                           + summary.Revision.ToString(CultureInfo.InvariantCulture),
                CanLoad = true,
                StatusMessage = string.Empty
            };
        }

        public async Task<bool> TryPrepareCloudLoad(
            int slotId,
            CancellationToken cancellationToken,
            Action<string> onError)
        {
            if (_cloudSlots == null)
            {
                onError?.Invoke("Cloud saves are not available.");
                return false;
            }

            var download = await _cloudSlots.DownloadSlot(slotId, cancellationToken);
            if (!download.Success)
            {
                onError?.Invoke(string.IsNullOrEmpty(download.ErrorMessage)
                    ? "Cloud save could not be downloaded."
                    : download.ErrorMessage);
                return false;
            }

            TowerSnapshotV1 snapshot;
            try
            {
                snapshot = DecodeCloudDownload(download.Download);
            }
            catch (Exception exception) when (exception is ArgumentException
                                             || exception is FormatException
                                             || exception is IOException)
            {
                onError?.Invoke("Cloud save could not be read.");
                return false;
            }

            snapshot.accountId = GameSession.CurrentAccountId;
            snapshot.slotId = download.Download.slotId;
            snapshot.cloudRevision = download.Download.revision;

            var prepared = GameSession.PrepareLoad(snapshot);
            if (!prepared.Success)
            {
                onError?.Invoke(prepared.Error == SessionLoadError.LoadAlreadyPending
                    ? "A load is already pending."
                    : "Cloud save could not be restored.");
                return false;
            }

            return FinishPreparation(SaveReadResult.Succeeded(snapshot), out var error)
                   || ReportError(error, onError);
        }

        bool FinishPreparation(SaveReadResult result, out string errorMessage)
        {
            if (result == null || !result.Success)
            {
                if (result == null)
                    errorMessage = LoadFailedFallback;
                else if (result.Error == SaveReadError.InvalidRecoveryIndex
                         || result.Error == SaveReadError.AlreadyPending)
                    errorMessage = result.ErrorMessage;
                else
                    errorMessage = LocalSavePresentation.ReadError(result.Error);

                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = LoadFailedFallback;
                return false;
            }

            errorMessage = string.Empty;
            _loadTowerScene();
            return true;
        }

        static bool ReportError(string error, Action<string> onError)
        {
            onError?.Invoke(error);
            return false;
        }

        static TowerSnapshotV1 DecodeCloudDownload(CloudSlotDownload download)
        {
            if (download == null)
                throw new InvalidDataException("The cloud download is missing.");

            var compressedPayload = Convert.FromBase64String(download.payloadBase64);
            if (!ConstantTimeEquals(ComputeChecksum(compressedPayload), download.checksum))
                throw new InvalidDataException("The cloud payload checksum does not match.");

            using (var input = new MemoryStream(compressedPayload, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                var snapshot = JsonUtility.FromJson<TowerSnapshotV1>(
                    Encoding.UTF8.GetString(output.ToArray()));
                if (snapshot == null)
                    throw new InvalidDataException("The cloud payload is empty.");

                snapshot.clientInstallId = download.clientInstallId;
                snapshot.deviceName = download.deviceName;
                snapshot.gameVersion = download.gameVersion;
                return snapshot;
            }
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
    }
}
