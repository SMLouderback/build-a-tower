using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Stores one current save plus three recovery copies per save ID on the local filesystem.
    /// Every expected filesystem, security, or corruption problem is reported as a typed result,
    /// and no read path ever rewrites a file it could not understand.
    /// </summary>
    public sealed class LocalSaveRepository
    {
        public const string CurrentExtension = ".batsave";
        public const int RecoveryCount = 3;

        /// <summary>
        /// Ceiling on the file size this repository will pull into memory. The serializer refuses a
        /// payload larger than 50 MiB once decompressed; such a payload can grow marginally under
        /// gzip and then by four thirds under Base64, so 96 MiB clears the largest legal envelope
        /// with headroom while still bounding what an untrusted file can make the game allocate.
        /// </summary>
        public const long MaxSaveFileBytes = 96L * 1024L * 1024L;

        private const string DefaultFolderName = "Saves";
        private const string TempExtension = ".tmp";
        private const string StagingExtension = ".stage";
        private const string RecoveryExtensionPrefix = ".bak";

        private static readonly Regex SafeSaveId = new Regex(
            "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$",
            RegexOptions.CultureInvariant);

        private static readonly UTF8Encoding StrictUtf8NoBom = new UTF8Encoding(false, true);
        private static readonly UTF8Encoding TolerantUtf8NoBom = new UTF8Encoding(false, false);

        private static readonly ReadOnlyCollection<LocalSaveSummary> NoSummaries =
            new ReadOnlyCollection<LocalSaveSummary>(Array.Empty<LocalSaveSummary>());

        private static readonly string[] ReservedDeviceNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public LocalSaveRepository(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("The save root directory is required.", nameof(rootDirectory));

            if (!Path.IsPathFullyQualified(rootDirectory))
                throw new ArgumentException(
                    "The save root directory must be fully qualified, not relative to the working directory or to a drive.",
                    nameof(rootDirectory));

            try
            {
                RootDirectory = Path.GetFullPath(rootDirectory);
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                throw new ArgumentException("The save root directory is not a usable path.", nameof(rootDirectory));
            }
        }

        public string RootDirectory { get; }

        public static LocalSaveRepository CreateDefault()
        {
            return new LocalSaveRepository(Path.Combine(Application.persistentDataPath, DefaultFolderName));
        }

        public SaveWriteResult Save(string saveId, TowerSnapshotV1 snapshot)
        {
            if (!IsSafeSaveId(saveId))
                return SaveWriteResult.Failed(SaveWriteError.InvalidSaveId, "The save ID is not a safe local file name.");

            if (snapshot == null)
                return SaveWriteResult.Failed(SaveWriteError.NullSnapshot, "The tower snapshot is missing.");

            if (!string.Equals(snapshot.saveId, saveId, StringComparison.Ordinal))
                return SaveWriteResult.Failed(
                    SaveWriteError.SaveIdMismatch,
                    "The snapshot save ID does not match the requested save ID.");

            string text;
            try
            {
                text = SaveSerializer.Serialize(snapshot);
            }
            catch (Exception exception) when (IsExpectedDataException(exception))
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.SerializationFailed,
                    "The tower snapshot could not be serialized.");
            }

            if (!SaveSerializer.Deserialize(text).Success)
                return SaveWriteResult.Failed(
                    SaveWriteError.SerializationFailed,
                    "The serialized tower snapshot could not be read back, so the current save was kept.");

            byte[] bytes;
            try
            {
                bytes = StrictUtf8NoBom.GetBytes(text);
            }
            catch (EncoderFallbackException)
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.SerializationFailed,
                    "The tower snapshot contains text that cannot be encoded as UTF-8.");
            }

            try
            {
                Directory.CreateDirectory(RootDirectory);
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.RootUnavailable,
                    "The save directory could not be created.");
            }

            return WriteDurableSave(saveId, bytes);
        }

        public SaveReadResult Read(string saveId)
        {
            if (!IsSafeSaveId(saveId))
                return SaveReadResult.Failed(SaveReadError.InvalidSaveId, "The save ID is not a safe local file name.");

            return ReadFile(SavePath(saveId, CurrentExtension), saveId);
        }

        public SaveReadResult ReadRecovery(string saveId, int recoveryIndex)
        {
            if (!IsSafeSaveId(saveId))
                return SaveReadResult.Failed(SaveReadError.InvalidSaveId, "The save ID is not a safe local file name.");

            if (recoveryIndex < 1 || recoveryIndex > RecoveryCount)
                return SaveReadResult.Failed(
                    SaveReadError.InvalidRecoveryIndex,
                    "The recovery index must be between 1 and " + RecoveryCount + ".");

            return ReadFile(RecoveryPath(saveId, recoveryIndex), saveId);
        }

        public IReadOnlyList<LocalSaveSummary> List()
        {
            string[] paths;
            try
            {
                if (!Directory.Exists(RootDirectory))
                    return NoSummaries;

                paths = Directory.GetFiles(RootDirectory, "*" + CurrentExtension, SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                return NoSummaries;
            }

            var summaries = new List<LocalSaveSummary>(paths.Length);
            foreach (var path in paths)
            {
                var fileName = Path.GetFileName(path);
                if (!fileName.EndsWith(CurrentExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                var saveId = fileName.Substring(0, fileName.Length - CurrentExtension.Length);
                if (!IsSafeSaveId(saveId))
                    continue;

                summaries.Add(Summarize(saveId, ReadFile(path, saveId)));
            }

            summaries.Sort(CompareSummaries);
            return summaries.AsReadOnly();
        }

        /// <summary>
        /// Publishes the new current save first and only then ages the recovery copies, so a
        /// replacement that fails - once or repeatedly - leaves every existing recovery revision
        /// exactly as it was.
        /// </summary>
        private SaveWriteResult WriteDurableSave(string saveId, byte[] bytes)
        {
            var currentPath = SavePath(saveId, CurrentExtension);
            var tempPath = SavePath(saveId, TempExtension);
            var stagingPath = SavePath(saveId, StagingExtension);
            var publishedNewCurrent = false;
            var preexistingStage = IsRegularFile(stagingPath);
            try
            {
                string blockMessage;
                if (!TryDeleteRegularFile(tempPath, out blockMessage))
                    return SaveWriteResult.Failed(SaveWriteError.TempWriteFailed, blockMessage);

                try
                {
                    WriteDurably(tempPath, bytes);
                }
                catch (Exception exception) when (IsExpectedFileException(exception))
                {
                    return SaveWriteResult.Failed(
                        SaveWriteError.TempWriteFailed,
                        "The temporary save file could not be written completely.");
                }

                if (Directory.Exists(currentPath))
                    return SaveWriteResult.Failed(
                        SaveWriteError.ReplaceFailed,
                        "A directory blocks the current save file path.");

                if (Directory.Exists(stagingPath))
                    return SaveWriteResult.Failed(
                        SaveWriteError.ReplaceFailed,
                        "A directory blocks a required save file path.");

                if (preexistingStage)
                {
                    string pendingMessage;
                    if (!TryRotateRecoveries(saveId, stagingPath, out pendingMessage))
                        return SaveWriteResult.Failed(
                            SaveWriteError.RecoveryRotationFailed,
                            "A previous recovery copy could not be installed, so the current save was kept. "
                            + pendingMessage);
                }

                if (!File.Exists(currentPath))
                    return PublishFirstSave(tempPath, currentPath);

                try
                {
                    File.Replace(tempPath, currentPath, stagingPath, true);
                }
                catch (Exception exception) when (IsExpectedFileException(exception))
                {
                    return SaveWriteResult.Failed(
                        SaveWriteError.ReplaceFailed,
                        "The current save could not be replaced, so the previous save and its recoveries were kept.");
                }

                publishedNewCurrent = true;

                string rotationMessage;
                if (!TryRotateRecoveries(saveId, stagingPath, out rotationMessage))
                    return SaveWriteResult.Failed(
                        SaveWriteError.RecoveryRotationFailed,
                        "The new save was published, but the recovery copies could not be rotated. " + rotationMessage);

                return SaveWriteResult.Succeeded();
            }
            finally
            {
                DiscardRegularFile(tempPath);

                // A pre-existing regular stage is the sole copy of a previous current save. Never
                // delete it here. Only discard a stage this call created (a failed replacement).
                if (!publishedNewCurrent && !preexistingStage)
                    DiscardRegularFile(stagingPath);
            }
        }

        private static SaveWriteResult PublishFirstSave(string tempPath, string currentPath)
        {
            if (Directory.Exists(currentPath))
                return SaveWriteResult.Failed(
                    SaveWriteError.ReplaceFailed,
                    "A directory blocks the current save file path.");

            try
            {
                File.Move(tempPath, currentPath);
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                return SaveWriteResult.Failed(
                    SaveWriteError.ReplaceFailed,
                    "The first save could not be published.");
            }

            return SaveWriteResult.Succeeded();
        }

        /// <summary>
        /// Ages every recovery copy one slot older, dropping the oldest, and installs the staged
        /// previous current save as recovery 1.
        /// </summary>
        private bool TryRotateRecoveries(string saveId, string stagingPath, out string errorMessage)
        {
            for (var index = RecoveryCount; index > 1; index--)
            {
                if (!TryMoveRegularFile(RecoveryPath(saveId, index - 1), RecoveryPath(saveId, index), out errorMessage))
                    return false;
            }

            return TryMoveRegularFile(
                stagingPath,
                RecoveryPath(saveId, 1),
                out errorMessage,
                requireSource: true);
        }

        private static void WriteDurably(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static SaveReadResult ReadFile(string path, string saveId)
        {
            string text;
            try
            {
                if (!File.Exists(path))
                    return SaveReadResult.Failed(SaveReadError.NotFound, "The requested save file does not exist.");

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaxSaveFileBytes)
                        return SaveReadResult.Failed(
                            SaveReadError.PayloadTooLarge,
                            "The save file is larger than the supported maximum.");

                    using (var reader = new StreamReader(stream, TolerantUtf8NoBom, true))
                        text = reader.ReadToEnd();
                }
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                return SaveReadResult.Failed(SaveReadError.ReadFailed, "The save file could not be read.");
            }

            var result = SaveSerializer.Deserialize(text);
            if (!result.Success)
                return result;

            if (!string.Equals(result.Snapshot.saveId, saveId, StringComparison.Ordinal))
                return SaveReadResult.Failed(
                    SaveReadError.InvalidPayload,
                    "The save file belongs to a different save ID.");

            return result;
        }

        private static LocalSaveSummary Summarize(string saveId, SaveReadResult result)
        {
            if (!result.Success)
                return LocalSaveSummary.Unreadable(saveId, result.Error, result.ErrorMessage);

            var snapshot = result.Snapshot;
            DateTimeOffset modified;
            if (!DateTimeOffset.TryParseExact(
                    snapshot.modifiedUtc,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out modified)
                || modified.Offset != TimeSpan.Zero)
            {
                return LocalSaveSummary.Unreadable(
                    saveId,
                    SaveReadError.InvalidPayload,
                    "The saved modified timestamp is not a UTC round-trip value.");
            }

            return LocalSaveSummary.Readable(
                saveId,
                snapshot.towerName,
                snapshot.difficulty,
                snapshot.clock.dayIndex,
                snapshot.clock.minuteOfDay,
                snapshot.walletBalance,
                snapshot.stars,
                modified.UtcDateTime);
        }

        private static int CompareSummaries(LocalSaveSummary left, LocalSaveSummary right)
        {
            if (left.IsReadable != right.IsReadable)
                return left.IsReadable ? -1 : 1;

            if (left.IsReadable)
            {
                var byModified = right.ModifiedUtc.CompareTo(left.ModifiedUtc);
                if (byModified != 0)
                    return byModified;
            }

            return string.CompareOrdinal(left.SaveId, right.SaveId);
        }

        private static bool TryMoveRegularFile(
            string sourcePath,
            string destinationPath,
            out string errorMessage,
            bool requireSource = false)
        {
            errorMessage = string.Empty;
            try
            {
                if (Directory.Exists(sourcePath))
                {
                    errorMessage = "A directory blocks a recovery file path.";
                    return false;
                }

                if (!File.Exists(sourcePath))
                {
                    if (requireSource)
                    {
                        errorMessage = "The staged previous save is missing.";
                        return false;
                    }

                    return true;
                }

                if (!TryDeleteRegularFile(destinationPath, out errorMessage))
                    return false;

                File.Move(sourcePath, destinationPath);
                return true;
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                errorMessage = "A recovery copy could not be moved.";
                return false;
            }
        }

        private static bool TryDeleteRegularFile(string path, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                if (Directory.Exists(path))
                {
                    errorMessage = "A directory blocks a required save file path.";
                    return false;
                }

                if (!File.Exists(path))
                    return true;

                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    errorMessage = "A link blocks a required save file path.";
                    return false;
                }

                File.Delete(path);
                return true;
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                errorMessage = "A required save file could not be removed.";
                return false;
            }
        }

        private static void DiscardRegularFile(string path)
        {
            string ignoredMessage;
            TryDeleteRegularFile(path, out ignoredMessage);
        }

        private static bool IsRegularFile(string path)
        {
            try
            {
                return File.Exists(path)
                    && !Directory.Exists(path)
                    && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception exception) when (IsExpectedFileException(exception))
            {
                return false;
            }
        }

        private string RecoveryPath(string saveId, int recoveryIndex)
        {
            return SavePath(
                saveId,
                RecoveryExtensionPrefix + recoveryIndex.ToString(CultureInfo.InvariantCulture));
        }

        private string SavePath(string saveId, string extension)
        {
            return Path.Combine(RootDirectory, saveId + extension);
        }

        private static bool IsSafeSaveId(string saveId)
        {
            if (saveId == null || !SafeSaveId.IsMatch(saveId))
                return false;

            foreach (var reserved in ReservedDeviceNames)
            {
                if (string.Equals(saveId, reserved, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool IsExpectedDataException(Exception exception)
        {
            // ArgumentException also covers EncoderFallbackException and DecoderFallbackException.
            return exception is ArgumentException || exception is InvalidDataException;
        }

        private static bool IsExpectedFileException(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is SecurityException
                || exception is NotSupportedException
                || IsExpectedDataException(exception);
        }
    }
}
