using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BuildATower
{
    public static class SaveSerializer
    {
        private const int MaxPayloadBytes = 50 * 1024 * 1024;

        public static string Serialize(TowerSnapshotV1 snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var payloadJson = JsonUtility.ToJson(snapshot);
            var compressedPayload = Compress(Encoding.UTF8.GetBytes(payloadJson));
            var envelope = new SaveEnvelope
            {
                schemaVersion = snapshot.schemaVersion,
                saveId = snapshot.saveId,
                towerName = snapshot.towerName,
                difficulty = snapshot.difficulty,
                modifiedUtc = snapshot.modifiedUtc,
                walletBalance = snapshot.walletBalance,
                stars = snapshot.stars,
                dayIndex = snapshot.clock != null ? snapshot.clock.dayIndex : 0,
                accountId = snapshot.accountId,
                slotId = snapshot.slotId,
                cloudRevision = snapshot.cloudRevision,
                clientInstallId = snapshot.clientInstallId,
                deviceName = snapshot.deviceName,
                gameVersion = snapshot.gameVersion,
                payloadBase64 = Convert.ToBase64String(compressedPayload),
                payloadChecksum = ComputeChecksum(compressedPayload)
            };

            return JsonUtility.ToJson(envelope);
        }

        public static SaveReadResult Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Failure(SaveReadError.InvalidEnvelope, "The save envelope is empty.");

            SaveEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SaveEnvelope>(text);
            }
            catch (Exception exception) when (IsExpectedDataException(exception))
            {
                return Failure(SaveReadError.InvalidEnvelope, "The save envelope is not valid JSON.");
            }

            if (!IsValidEnvelope(envelope))
                return Failure(SaveReadError.InvalidEnvelope, "The save envelope is missing required fields.");

            if (envelope.schemaVersion > SaveSchema.CurrentVersion)
                return Failure(SaveReadError.UnsupportedSchema, "The save uses a newer schema version.");

            byte[] compressedPayload;
            try
            {
                compressedPayload = Convert.FromBase64String(envelope.payloadBase64);
            }
            catch (FormatException)
            {
                return Failure(SaveReadError.InvalidBase64, "The save payload is not valid Base64.");
            }

            var actualChecksum = ComputeChecksum(compressedPayload);
            if (!ConstantTimeEquals(actualChecksum, envelope.payloadChecksum))
                return Failure(SaveReadError.ChecksumMismatch, "The save payload checksum does not match.");

            byte[] payloadBytes;
            try
            {
                payloadBytes = Decompress(compressedPayload);
            }
            catch (PayloadTooLargeException)
            {
                return Failure(SaveReadError.PayloadTooLarge, "The decompressed save payload exceeds 50 MiB.");
            }
            catch (Exception exception) when (IsExpectedDataException(exception))
            {
                return Failure(SaveReadError.InvalidPayload, "The compressed save payload is invalid.");
            }

            TowerSnapshotV1 snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<TowerSnapshotV1>(Encoding.UTF8.GetString(payloadBytes));
            }
            catch (Exception exception) when (IsExpectedDataException(exception))
            {
                return Failure(SaveReadError.InvalidPayload, "The save payload is not valid JSON.");
            }

            if (!IsValidPayload(snapshot))
                return Failure(SaveReadError.InvalidPayload, "The save payload is missing required fields.");

            if (snapshot.schemaVersion > SaveSchema.CurrentVersion)
                return Failure(SaveReadError.UnsupportedSchema, "The save payload uses a newer schema version.");

            if (snapshot.schemaVersion != envelope.schemaVersion)
                return Failure(SaveReadError.InvalidPayload, "The envelope and payload schema versions do not match.");

            return SaveReadResult.Succeeded(snapshot);
        }

        private static bool IsValidEnvelope(SaveEnvelope envelope)
        {
            return envelope != null
                && envelope.schemaVersion > 0
                && !string.IsNullOrWhiteSpace(envelope.saveId)
                && !string.IsNullOrWhiteSpace(envelope.towerName)
                && !string.IsNullOrWhiteSpace(envelope.difficulty)
                && !string.IsNullOrWhiteSpace(envelope.modifiedUtc)
                && !string.IsNullOrWhiteSpace(envelope.payloadBase64)
                && IsLowercaseSha256(envelope.payloadChecksum);
        }

        private static bool IsValidPayload(TowerSnapshotV1 snapshot)
        {
            return snapshot != null
                && snapshot.schemaVersion > 0
                && !string.IsNullOrWhiteSpace(snapshot.saveId)
                && !string.IsNullOrWhiteSpace(snapshot.towerName)
                && !string.IsNullOrWhiteSpace(snapshot.difficulty)
                && !string.IsNullOrWhiteSpace(snapshot.modifiedUtc)
                && snapshot.clock != null;
        }

        private static bool IsLowercaseSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                    return false;
            }

            return true;
        }

        private static byte[] Compress(byte[] payload)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    gzip.Write(payload, 0, payload.Length);

                return output.ToArray();
            }
        }

        private static byte[] Decompress(byte[] compressedPayload)
        {
            using (var input = new MemoryStream(compressedPayload, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                var totalBytes = 0;
                int bytesRead;
                while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (totalBytes > MaxPayloadBytes - bytesRead)
                        throw new PayloadTooLargeException();

                    output.Write(buffer, 0, bytesRead);
                    totalBytes += bytesRead;
                }

                return output.ToArray();
            }
        }

        private static string ComputeChecksum(byte[] payload)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(payload);
                var result = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                    result.Append(value.ToString("x2"));

                return result.ToString();
            }
        }

        private static bool ConstantTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];

            return difference == 0;
        }

        private static bool IsExpectedDataException(Exception exception)
        {
            return exception is ArgumentException
                || exception is DecoderFallbackException
                || exception is InvalidDataException
                || exception is IOException;
        }

        private static SaveReadResult Failure(SaveReadError error, string message)
        {
            return SaveReadResult.Failed(error, message);
        }

        private sealed class PayloadTooLargeException : Exception
        {
        }
    }
}
