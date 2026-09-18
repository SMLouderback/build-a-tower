using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BuildATower
{
    public static class TowerSnapshotMapper
    {
        const int MaxTowerNameLength = 80;
        static readonly Regex SafeSaveId = new Regex(
            "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$",
            RegexOptions.CultureInvariant);

        public static TowerSnapshotV1 Capture(
            string saveId,
            string towerName,
            BuildController build,
            TowerSimulation simulation,
            DateTime modifiedUtc)
        {
            if (!IsValidSaveId(saveId))
                throw new ArgumentException("The save ID is not safe for a repository filename.", nameof(saveId));
            if (!IsValidTowerName(towerName))
                throw new ArgumentException("The tower name is blank, unsafe, or longer than 80 characters.", nameof(towerName));
            if (build == null)
                throw new ArgumentNullException(nameof(build));
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));
            if (build.Grid == null)
                throw new InvalidOperationException("The build controller has no tower grid.");
            if (build.Wallet == null)
                throw new InvalidOperationException("The build controller has no funds wallet.");
            if (simulation.Clock == null)
                throw new InvalidOperationException("The tower simulation has no game clock.");
            if (simulation.Stars == null)
                throw new InvalidOperationException("The tower simulation has no star system.");

            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = saveId,
                towerName = towerName,
                difficulty = GameSession.Difficulty.ToString(),
                modifiedUtc = modifiedUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture),
                walletBalance = build.Wallet.Balance,
                stars = simulation.Stars.CurrentStars,
                clock = simulation.Clock.CaptureSnapshot(),
                research = simulation.Research != null
                    ? simulation.Research.CaptureSnapshot()
                    : new ResearchSnapshotV1
                    {
                        completed = Array.Empty<ResearchCompletedNodeV1>(),
                        progress = Array.Empty<ResearchProgressNodeV1>(),
                        activeBranch = string.Empty,
                        activeLevel = 0,
                        paused = false
                    },
                rooms = build.Grid.CaptureRooms(Time.realtimeSinceStartup).ToArray()
            };
        }

        public static SnapshotValidationResult ValidateForRestore(TowerSnapshotV1 snapshot)
        {
            if (snapshot == null)
                return Failure(SnapshotValidationError.NullSnapshot, "The tower snapshot is missing.");
            if (snapshot.schemaVersion != SaveSchema.CurrentVersion)
                return Failure(SnapshotValidationError.UnsupportedSchema, "The tower snapshot schema is unsupported.");
            if (!IsValidSaveId(snapshot.saveId))
                return Failure(SnapshotValidationError.InvalidSaveId, "The tower snapshot save ID is invalid.");
            if (!IsValidTowerName(snapshot.towerName))
                return Failure(SnapshotValidationError.InvalidTowerName, "The tower snapshot name is invalid.");
            if (!IsUtcRoundTripTimestamp(snapshot.modifiedUtc))
                return Failure(SnapshotValidationError.InvalidModifiedUtc, "The modified timestamp is not a UTC round-trip value.");
            if (!IsNamedDifficulty(snapshot.difficulty))
                return Failure(SnapshotValidationError.InvalidDifficulty, "The saved difficulty is unknown or has incorrect casing.");
            if (snapshot.walletBalance < 0)
                return Failure(SnapshotValidationError.InvalidWalletBalance, "The saved wallet balance cannot be negative.");
            if (!IsValidClock(snapshot.clock))
                return Failure(SnapshotValidationError.InvalidClock, "The saved clock state is invalid.");
            if (snapshot.stars < 0 || snapshot.stars > StarSystem.MaxStars)
                return Failure(SnapshotValidationError.InvalidStars, "The saved star count is outside the supported range.");
            if (snapshot.rooms == null)
                return Failure(SnapshotValidationError.NullRooms, "The room snapshot list is missing.");
            if (snapshot.research != null
                && !ResearchSystem.TryValidateSnapshot(snapshot.research, out _))
                return Failure(SnapshotValidationError.InvalidResearch, "The saved research state is invalid.");

            return SnapshotValidationResult.Succeeded();
        }

        static bool IsValidSaveId(string value)
        {
            return value != null && SafeSaveId.IsMatch(value);
        }

        static bool IsValidTowerName(string value)
        {
            if (value == null)
                return false;

            var trimmed = value.Trim();
            if (trimmed.Length == 0 ||
                StringInfo.ParseCombiningCharacters(trimmed).Length > MaxTowerNameLength)
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                    return false;
            }

            return true;
        }

        static bool IsUtcRoundTripTimestamp(string value)
        {
            return DateTimeOffset.TryParseExact(
                       value,
                       "O",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out var parsed)
                   && parsed.Offset == TimeSpan.Zero;
        }

        static bool IsNamedDifficulty(string value)
        {
            return Enum.TryParse(value, false, out GameDifficulty parsed)
                   && Enum.IsDefined(typeof(GameDifficulty), parsed)
                   && string.Equals(Enum.GetName(typeof(GameDifficulty), parsed), value, StringComparison.Ordinal);
        }

        static bool IsValidClock(ClockSnapshotV1 clock)
        {
            return clock != null
                   && clock.dayIndex >= 0
                   && clock.minuteOfDay >= 0
                   && clock.minuteOfDay < GameClock.MinutesPerDay
                   && IsFinite(clock.minuteAccumulator)
                   && clock.minuteAccumulator >= 0f
                   && clock.minuteAccumulator < 1f
                   && IsFinite(clock.minutesPerRealSecond)
                   && clock.minutesPerRealSecond > 0f;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static SnapshotValidationResult Failure(SnapshotValidationError error, string message)
        {
            return SnapshotValidationResult.Failure(error, message);
        }
    }
}
