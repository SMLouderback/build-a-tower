using System;

namespace BuildATower
{
    /// <summary>
    /// Cross-scene run settings. Survives MainMenu → TowerSandbox via static state.
    /// </summary>
    public static class GameSession
    {
        static bool _hasDifficulty;
        static GameDifficulty _difficulty;
        static TowerSnapshotV1 _pendingLoad;
        static SessionLoadResult _lastLoadError;
        static string _currentSaveId;
        static string _currentTowerName;
        static bool _isSaveDirty;
        static string _restoreFallbackNotice;

        public const string RestoreFallbackPlayerMessage =
            "The saved tower could not be restored, so a new Normal tower was started. Your original save was kept.";

        public static bool HasDifficulty => _hasDifficulty;

        public static GameDifficulty Difficulty
        {
            get
            {
                EnsureDefault();
                return _difficulty;
            }
            set
            {
                _difficulty = value;
                _hasDifficulty = true;
            }
        }

        public static bool IsSandbox => Difficulty == GameDifficulty.Sandbox;

        public static TowerSnapshotV1 PendingLoad => _pendingLoad;

        public static SessionLoadResult LastLoadError => _lastLoadError;

        public static string CurrentSaveId => _currentSaveId;

        public static string CurrentTowerName => _currentTowerName;

        public static bool IsSaveDirty => _isSaveDirty;

        public static bool HasRestoreFallbackNotice => !string.IsNullOrEmpty(_restoreFallbackNotice);

        public static string RestoreFallbackNotice => _restoreFallbackNotice;

        public static string ConsumeRestoreFallbackNotice()
        {
            var notice = _restoreFallbackNotice;
            _restoreFallbackNotice = null;
            return notice;
        }

        public static void MarkGameplaySaveDirty()
        {
            if (_pendingLoad != null) return;
            _isSaveDirty = true;
        }

        public static void EnsureDefault()
        {
            if (_hasDifficulty) return;
            _difficulty = GameDifficulty.Normal;
            _hasDifficulty = true;
        }

        public static void StartNewGame(GameDifficulty difficulty)
        {
            ClearIdentity();
            _pendingLoad = null;
            _lastLoadError = null;
            _restoreFallbackNotice = null;
            _isSaveDirty = true;
            _difficulty = difficulty;
            _hasDifficulty = true;
        }

        public static SessionLoadResult PrepareLoad(TowerSnapshotV1 snapshot)
        {
            if (snapshot == null)
                return SessionLoadResult.Failed(SessionLoadError.NullSnapshot, "The tower snapshot is missing.");
            if (_pendingLoad != null)
                return SessionLoadResult.Failed(
                    SessionLoadError.LoadAlreadyPending,
                    "A load is already pending.");
            if (!TryParseNamedDifficulty(snapshot.difficulty, out var difficulty))
                return SessionLoadResult.Failed(
                    SessionLoadError.InvalidDifficulty,
                    "The saved difficulty is unknown or has incorrect casing.");

            _pendingLoad = snapshot;
            _difficulty = difficulty;
            _hasDifficulty = true;
            _lastLoadError = null;
            _restoreFallbackNotice = null;
            return SessionLoadResult.Succeeded();
        }

        public static void CompleteLoad()
        {
            if (_pendingLoad != null)
            {
                _currentSaveId = _pendingLoad.saveId;
                _currentTowerName = _pendingLoad.towerName;
                _isSaveDirty = false;
                _pendingLoad = null;
            }

            _lastLoadError = null;
            _restoreFallbackNotice = null;
        }

        public static void FailLoad(SessionLoadError error, string errorMessage)
        {
            RecordFailure(SessionLoadResult.Failed(error, errorMessage));
        }

        public static void FailLoad(GridRestoreError restoreError, string errorMessage)
        {
            RecordFailure(SessionLoadResult.FailedRestore(restoreError, errorMessage));
        }

        public static void MarkSaveDirty()
        {
            _isSaveDirty = true;
        }

        public static void RecordSuccessfulSave(string saveId, string towerName)
        {
            RecordSaveIdentity(saveId, towerName);
        }

        public static void RecordSaveIdentity(string saveId, string towerName)
        {
            _currentSaveId = saveId;
            _currentTowerName = towerName;
            _isSaveDirty = false;
        }

        public static void ResetForTests()
        {
            _hasDifficulty = false;
            _difficulty = GameDifficulty.Normal;
            _pendingLoad = null;
            _lastLoadError = null;
            ClearIdentity();
            _isSaveDirty = false;
            _restoreFallbackNotice = null;
        }

        static void RecordFailure(SessionLoadResult failure)
        {
            _pendingLoad = null;
            _hasDifficulty = false;
            _difficulty = GameDifficulty.Normal;
            _lastLoadError = failure;
            _restoreFallbackNotice = RestoreFallbackPlayerMessage;
            ClearIdentity();
            _isSaveDirty = true;
        }

        static void ClearIdentity()
        {
            _currentSaveId = null;
            _currentTowerName = null;
        }

        static bool TryParseNamedDifficulty(string value, out GameDifficulty difficulty)
        {
            difficulty = default;
            return Enum.TryParse(value, false, out difficulty)
                   && Enum.IsDefined(typeof(GameDifficulty), difficulty)
                   && string.Equals(
                       Enum.GetName(typeof(GameDifficulty), difficulty),
                       value,
                       StringComparison.Ordinal);
        }
    }
}
