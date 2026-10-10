using System;

namespace BuildATower
{
    public static class SaveSchema
    {
        public const int CurrentVersion = 1;
    }

    [Serializable]
    public sealed class SaveEnvelope
    {
        public int schemaVersion;
        public string saveId;
        public string towerName;
        public string difficulty;
        public string modifiedUtc;
        public int walletBalance;
        public int stars;
        public int dayIndex;
        public string accountId;
        public int slotId;
        public long cloudRevision;
        public string clientInstallId;
        public string deviceName;
        public string gameVersion;
        public string payloadBase64;
        public string payloadChecksum;
    }

    [Serializable]
    public sealed class TowerSnapshotV1
    {
        public int schemaVersion;
        public string saveId;
        public string towerName;
        public string difficulty;
        public string modifiedUtc;
        public int walletBalance;
        public int stars;
        public string accountId;
        public int slotId;
        public long cloudRevision;
        public string clientInstallId;
        public string deviceName;
        public string gameVersion;
        public ClockSnapshotV1 clock;
        public ResearchSnapshotV1 research;
        /// <summary>Optional: null (older saves) restores default Clear weather.</summary>
        public WeatherSnapshotV1 weather;
        /// <summary>Optional: null or absent (older saves) restores an empty metro.</summary>
        public MetroSnapshotV1 metro;
        /// <summary>Optional: null or absent (older saves) restores neutral weekly market pulse.</summary>
        public MarketPulseSnapshotV1 marketPulse;
        public RoomSnapshotV1[] rooms;
    }

    [Serializable]
    public sealed class MarketPulseSnapshotV1
    {
        public float livingPulseMult;
        public float commercialPulseMult;
        public int nextPulseDayIndex;
    }

    [Serializable]
    public sealed class WeatherSnapshotV1
    {
        /// <summary><see cref="WeatherKind"/> enum name.</summary>
        public string kind;
        public int segmentEndDayIndex;
        public int segmentEndMinuteOfDay;
        /// <summary><see cref="WeatherHangover"/> enum name.</summary>
        public string hangover;
        public int hangoverEndDayIndex;
    }

    [Serializable]
    public sealed class MetroStationFootprintV1
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class MetroSnapshotV1
    {
        public MetroStationFootprintV1[] stations;
        public bool hasTunnel;
    }

    [Serializable]
    public sealed class ResearchSnapshotV1
    {
        public ResearchCompletedNodeV1[] completed;
        public ResearchProgressNodeV1[] progress;
        public string activeBranch;
        public int activeLevel;
        public bool paused;
    }

    [Serializable]
    public sealed class ResearchCompletedNodeV1
    {
        public string branch;
        public int level;
    }

    [Serializable]
    public sealed class ResearchProgressNodeV1
    {
        public string branch;
        public int level;
        public float workMinutes;
    }

    [Serializable]
    public sealed class ClockSnapshotV1
    {
        public int dayIndex;
        public int minuteOfDay;
        public float minuteAccumulator;
        public float minutesPerRealSecond;
        public bool paused;
    }

    [Serializable]
    public sealed class RoomSnapshotV1
    {
        public int instanceId;
        public string roomTypeId;
        public int originX;
        public int originY;
        public int width;
        public int height;
        public int evaluation;
        public int condition;
        public bool dirty;
        public float cleanWorkRemaining;
        public int repairJobsRemaining;
        public float repairJobMinutes;
        public int staffedWorkers;
        public bool condoSold;
        public int priceTier;
        public int artVariant;
        public float buildGraceSecondsRemaining;
        public int constructionSpent;
        public int lifetimeIncome;
        public int lifetimeExpense;
        public int visitsToday;
        public int shopEarningsToday;
        public int shopRevenueYesterday;
        public int shopUpkeepYesterday;
        public int[] visitHistory;
    }

    public enum GridRestoreError
    {
        None,
        NullSnapshots,
        NullRegistry,
        NullRoom,
        InvalidInstanceId,
        DuplicateInstanceId,
        BlankRoomTypeId,
        UnknownRoomTypeId,
        InvalidRoomSize,
        GeometryMismatch,
        MultipleGroundLobbies,
        GroundLobbyFloorMismatch,
        RoomOverlap,
        TransitOverlap,
        RoomStateRestoreFailed,
        InstanceIdOverflow,
        RoomFootprintTooLarge,
        RestoredFootprintTooLarge,
        StairsRoleConflict
    }

    public sealed class GridRestoreResult
    {
        private GridRestoreResult(GridRestoreError errorCode, string errorMessage)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success => ErrorCode == GridRestoreError.None;
        public GridRestoreError ErrorCode { get; }
        public string ErrorMessage { get; }

        public static GridRestoreResult Succeeded()
        {
            return new GridRestoreResult(GridRestoreError.None, string.Empty);
        }

        public static GridRestoreResult Failure(GridRestoreError errorCode, string errorMessage)
        {
            if (errorCode == GridRestoreError.None)
                throw new ArgumentException("A failed grid restore must have an error.", nameof(errorCode));

            return new GridRestoreResult(errorCode, errorMessage ?? string.Empty);
        }
    }

    public enum SnapshotValidationError
    {
        None,
        NullSnapshot,
        UnsupportedSchema,
        InvalidSaveId,
        InvalidTowerName,
        InvalidModifiedUtc,
        InvalidDifficulty,
        InvalidWalletBalance,
        InvalidClock,
        InvalidStars,
        NullRooms,
        InvalidResearch,
        InvalidWeather,
        InvalidMetro,
        InvalidMarketPulse
    }

    public sealed class SnapshotValidationResult
    {
        private SnapshotValidationResult(SnapshotValidationError errorCode, string errorMessage)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success => ErrorCode == SnapshotValidationError.None;
        public SnapshotValidationError ErrorCode { get; }
        public string ErrorMessage { get; }

        public static SnapshotValidationResult Succeeded()
        {
            return new SnapshotValidationResult(SnapshotValidationError.None, string.Empty);
        }

        public static SnapshotValidationResult Failure(
            SnapshotValidationError errorCode,
            string errorMessage)
        {
            if (errorCode == SnapshotValidationError.None)
                throw new ArgumentException("A failed snapshot validation must have an error.", nameof(errorCode));

            return new SnapshotValidationResult(errorCode, errorMessage ?? string.Empty);
        }
    }

    public enum SaveReadError
    {
        None,
        InvalidEnvelope,
        InvalidBase64,
        ChecksumMismatch,
        UnsupportedSchema,
        PayloadTooLarge,
        InvalidPayload,
        InvalidSaveId,
        InvalidRecoveryIndex,
        NotFound,
        ReadFailed,
        AlreadyPending
    }

    public sealed class SaveReadResult
    {
        private SaveReadResult(TowerSnapshotV1 snapshot, SaveReadError error, string errorMessage)
        {
            Snapshot = snapshot;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Success => Error == SaveReadError.None;
        public TowerSnapshotV1 Snapshot { get; }
        public SaveReadError Error { get; }
        public string ErrorMessage { get; }

        public static SaveReadResult Succeeded(TowerSnapshotV1 snapshot)
        {
            return new SaveReadResult(snapshot, SaveReadError.None, string.Empty);
        }

        public static SaveReadResult Failed(SaveReadError error, string errorMessage)
        {
            if (error == SaveReadError.None)
                throw new ArgumentException("A failed save read must have an error.", nameof(error));

            return new SaveReadResult(null, error, errorMessage ?? string.Empty);
        }
    }

    public enum SaveWriteError
    {
        None,
        InvalidSaveId,
        NullSnapshot,
        SaveIdMismatch,
        SerializationFailed,
        RootUnavailable,
        TempWriteFailed,
        ReplaceFailed,
        RecoveryRotationFailed,
        NotInitialized,
        CaptureFailed
    }

    public sealed class SaveWriteResult
    {
        private SaveWriteResult(SaveWriteError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Success => Error == SaveWriteError.None;
        public SaveWriteError Error { get; }
        public string ErrorMessage { get; }

        public static SaveWriteResult Succeeded()
        {
            return new SaveWriteResult(SaveWriteError.None, string.Empty);
        }

        public static SaveWriteResult Failed(SaveWriteError error, string errorMessage)
        {
            if (error == SaveWriteError.None)
                throw new ArgumentException("A failed save write must have an error.", nameof(error));

            return new SaveWriteResult(error, errorMessage ?? string.Empty);
        }
    }

    /// <summary>
    /// Presentation-safe description of one local save slot. It never carries the save payload,
    /// its Base64 text, or its checksum, so a corrupt file can be listed without exposing its bytes.
    /// </summary>
    public sealed class LocalSaveSummary
    {
        private LocalSaveSummary(
            string saveId,
            string towerName,
            string difficulty,
            int dayIndex,
            int minuteOfDay,
            int walletBalance,
            int stars,
            DateTime modifiedUtc,
            SaveReadError status,
            string statusMessage)
        {
            SaveId = saveId;
            TowerName = towerName;
            Difficulty = difficulty;
            DayIndex = dayIndex;
            MinuteOfDay = minuteOfDay;
            WalletBalance = walletBalance;
            Stars = stars;
            ModifiedUtc = modifiedUtc;
            Status = status;
            StatusMessage = statusMessage;
        }

        public string SaveId { get; }
        public string TowerName { get; }
        public string Difficulty { get; }
        public int DayIndex { get; }
        public int MinuteOfDay { get; }
        public int WalletBalance { get; }
        public int Stars { get; }
        public DateTime ModifiedUtc { get; }
        public SaveReadError Status { get; }
        public string StatusMessage { get; }
        public bool IsReadable => Status == SaveReadError.None;

        public static LocalSaveSummary Readable(
            string saveId,
            string towerName,
            string difficulty,
            int dayIndex,
            int minuteOfDay,
            int walletBalance,
            int stars,
            DateTime modifiedUtc)
        {
            return new LocalSaveSummary(
                saveId ?? string.Empty,
                towerName ?? string.Empty,
                difficulty ?? string.Empty,
                dayIndex,
                minuteOfDay,
                walletBalance,
                stars,
                DateTime.SpecifyKind(modifiedUtc, DateTimeKind.Utc),
                SaveReadError.None,
                string.Empty);
        }

        public static LocalSaveSummary Unreadable(string saveId, SaveReadError status, string statusMessage)
        {
            if (status == SaveReadError.None)
                throw new ArgumentException("An unreadable save summary must have an error.", nameof(status));

            return new LocalSaveSummary(
                saveId ?? string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                new DateTime(0L, DateTimeKind.Utc),
                status,
                statusMessage ?? string.Empty);
        }
    }

    public enum SessionLoadError
    {
        None,
        NullSnapshot,
        InvalidDifficulty,
        LoadAlreadyPending,
        GridRestoreFailed,
        SimulationRestoreFailed,
        WalletRestoreFailed,
        RoomTypeRegistryFailed
    }

    public sealed class SessionLoadResult
    {
        private SessionLoadResult(
            SessionLoadError error,
            GridRestoreError restoreError,
            string errorMessage)
        {
            Error = error;
            RestoreError = restoreError;
            ErrorMessage = errorMessage;
        }

        public bool Success => Error == SessionLoadError.None;
        public SessionLoadError Error { get; }
        public GridRestoreError RestoreError { get; }
        public string ErrorMessage { get; }

        public static SessionLoadResult Succeeded()
        {
            return new SessionLoadResult(SessionLoadError.None, GridRestoreError.None, string.Empty);
        }

        public static SessionLoadResult Failed(SessionLoadError error, string errorMessage)
        {
            if (error == SessionLoadError.None)
                throw new ArgumentException("A failed session load must have an error.", nameof(error));

            return new SessionLoadResult(error, GridRestoreError.None, errorMessage ?? string.Empty);
        }

        public static SessionLoadResult FailedRestore(GridRestoreError restoreError, string errorMessage)
        {
            if (restoreError == GridRestoreError.None)
                throw new ArgumentException("A failed grid restore must have an error.", nameof(restoreError));

            return new SessionLoadResult(
                SessionLoadError.GridRestoreFailed,
                restoreError,
                errorMessage ?? string.Empty);
        }
    }
}
