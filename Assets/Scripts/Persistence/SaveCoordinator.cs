using System;

namespace BuildATower
{
    public sealed class SaveCoordinator
    {
        readonly LocalSaveRepository _repository;
        readonly BuildController _build;
        readonly TowerSimulation _simulation;
        readonly SyncCoordinator _sync;

        public SaveCoordinator(LocalSaveRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public SaveCoordinator(LocalSaveRepository repository, SyncCoordinator sync)
            : this(repository)
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        }

        public SaveCoordinator(
            LocalSaveRepository repository,
            BuildController build,
            TowerSimulation simulation)
            : this(repository)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        public SaveCoordinator(
            LocalSaveRepository repository,
            BuildController build,
            TowerSimulation simulation,
            SyncCoordinator sync)
            : this(repository, build, simulation)
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        }

        public SaveReadResult PrepareLocalLoad(string saveId)
        {
            return PrepareReadResult(_repository.Read(saveId));
        }

        public SaveReadResult PrepareLocalRecovery(string saveId, int recoveryIndex)
        {
            return PrepareReadResult(_repository.ReadRecovery(saveId, recoveryIndex));
        }

        public SaveWriteResult SaveCurrent(string saveId, string towerName)
        {
            if (_build == null ||
                _simulation == null ||
                _build.Grid == null ||
                _build.Wallet == null ||
                _simulation.Clock == null ||
                _simulation.Stars == null)
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.NotInitialized,
                    "The tower is not ready to save.");
            }

            TowerSnapshotV1 snapshot;
            try
            {
                snapshot = TowerSnapshotMapper.Capture(
                    saveId,
                    towerName,
                    _build,
                    _simulation,
                    DateTime.UtcNow);
            }
            catch (ArgumentException)
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.CaptureFailed,
                    "The current tower could not be captured.");
            }
            catch (InvalidOperationException)
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.NotInitialized,
                    "The tower is not ready to save.");
            }

            var cloudSlotId = snapshot.slotId <= 0 ? 1 : snapshot.slotId;
            var lastSeenRevision = snapshot.cloudRevision;
            if (_sync != null && lastSeenRevision <= 0)
                lastSeenRevision = _sync.CloudRevision;

            if (_sync != null)
            {
                snapshot.slotId = cloudSlotId;
                snapshot.cloudRevision = lastSeenRevision;
            }

            var result = _repository.Save(saveId, snapshot);
            if (result.Success && _sync != null)
                _ = _sync.EnqueueUploadAfterLocalSuccess(
                    result,
                    cloudSlotId,
                    snapshot,
                    lastSeenRevision);

            return result;
        }

        SaveReadResult PrepareReadResult(SaveReadResult read)
        {
            if (!read.Success)
                return read;

            var validation = TowerSnapshotMapper.ValidateForRestore(read.Snapshot);
            if (!validation.Success)
                return MapValidation(validation);

            var prepared = GameSession.PrepareLoad(read.Snapshot);
            if (!prepared.Success)
                return MapPrepare(prepared);

            return SaveReadResult.Succeeded(read.Snapshot);
        }

        static SaveReadResult MapValidation(SnapshotValidationResult validation)
        {
            if (validation.ErrorCode == SnapshotValidationError.UnsupportedSchema)
                return SaveReadResult.Failed(
                    SaveReadError.UnsupportedSchema,
                    "The tower snapshot schema is unsupported.");

            return SaveReadResult.Failed(
                SaveReadError.InvalidPayload,
                "The tower snapshot is not valid to restore.");
        }

        static SaveReadResult MapPrepare(SessionLoadResult prepared)
        {
            if (prepared.Error == SessionLoadError.LoadAlreadyPending)
                return SaveReadResult.Failed(
                    SaveReadError.AlreadyPending,
                    "A load is already pending.");

            return SaveReadResult.Failed(
                SaveReadError.InvalidPayload,
                "The tower snapshot is not valid to restore.");
        }
    }
}
