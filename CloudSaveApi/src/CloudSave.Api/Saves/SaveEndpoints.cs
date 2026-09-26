using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using CloudSave.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Saves;

public static class SaveEndpoints
{
    private const int FirstSlotId = 1;
    private const int LastSlotId = 3;
    private const long MaxCompressedBytes = 10L * 1024 * 1024;
    private const long MaxDecompressedBytes = 50L * 1024 * 1024;

    public static IEndpointRouteBuilder MapSaveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/saves", ListSaves)
            .RequireAuthorization();
        endpoints.MapGet("/v1/saves/{slotId:int}", GetSave)
            .RequireAuthorization();
        endpoints.MapPut("/v1/saves/{slotId:int}", PutSave)
            .RequireAuthorization()
            .RequireRateLimiting("save-put");
        endpoints.MapGet("/v1/saves/{slotId:int}/revisions", GetRevisions)
            .RequireAuthorization();
        endpoints.MapPost("/v1/saves/{slotId:int}/restore/{revisionId:guid}", RestoreRevision)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ListSaves(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GetVerifiedUserId(principal, db, cancellationToken);
        if (gate.Result is not null)
            return gate.Result;

        var records = await db.SaveSlots
            .Where(slot => slot.CloudUserId == gate.UserId)
            .ToDictionaryAsync(slot => slot.SlotId, cancellationToken);

        var slots = Enumerable.Range(FirstSlotId, LastSlotId)
            .Select(slotId => records.TryGetValue(slotId, out var record)
                ? SlotSummary.Occupied(record)
                : SlotSummary.Empty(slotId))
            .ToList();

        return Results.Ok(new SlotListResponse(slots));
    }

    private static async Task<IResult> GetSave(
        [FromRoute] int slotId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (!IsValidSlot(slotId))
            return Results.NotFound();

        var gate = await GetVerifiedUserId(principal, db, cancellationToken);
        if (gate.Result is not null)
            return gate.Result;

        var record = await db.SaveSlots
            .SingleOrDefaultAsync(slot => slot.CloudUserId == gate.UserId && slot.SlotId == slotId, cancellationToken);
        if (record is null)
            return Results.NotFound();

        return Results.Ok(SlotDownload.FromRecord(record));
    }

    private static async Task<IResult> PutSave(
        [FromRoute] int slotId,
        SaveUploadRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!IsValidSlot(slotId))
            return Results.NotFound();

        var gate = await GetVerifiedUserId(principal, db, cancellationToken);
        if (gate.Result is not null)
            return gate.Result;

        var validation = ValidatePayload(request, out var payload, out var decompressedBytes);
        if (validation is not null)
            return validation;

        var now = timeProvider.GetUtcNow();
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await PutSaveRelational(
                slotId,
                gate.UserId!,
                request,
                payload,
                decompressedBytes,
                now,
                db,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        return await PutSaveInMemory(
            slotId,
            gate.UserId!,
            request,
            payload,
            decompressedBytes,
            now,
            db,
            cancellationToken);
    }

    private static async Task<IResult> GetRevisions(
        [FromRoute] int slotId,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!IsValidSlot(slotId))
            return Results.NotFound();

        var gate = await GetVerifiedUserId(principal, db, cancellationToken);
        if (gate.Result is not null)
            return gate.Result;

        var now = timeProvider.GetUtcNow();
        var revisions = await db.RevisionArchives
            .Where(archive => archive.CloudUserId == gate.UserId && archive.SlotId == slotId && archive.KeepUntil > now)
            .OrderByDescending(archive => archive.Revision)
            .Select(archive => new ArchivedRevisionSummary(
                archive.Id,
                archive.Revision,
                archive.TowerName,
                archive.DeviceName,
                archive.PlayMinutes,
                archive.ModifiedUtc,
                archive.KeepUntil))
            .ToListAsync(cancellationToken);

        return Results.Ok(new RevisionListResponse(revisions));
    }

    private static async Task<IResult> RestoreRevision(
        [FromRoute] int slotId,
        [FromRoute] Guid revisionId,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!IsValidSlot(slotId))
            return Results.NotFound();

        var gate = await GetVerifiedUserId(principal, db, cancellationToken);
        if (gate.Result is not null)
            return gate.Result;

        var now = timeProvider.GetUtcNow();
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var archive = await db.RevisionArchives
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == revisionId &&
                candidate.CloudUserId == gate.UserId &&
                candidate.SlotId == slotId &&
                candidate.KeepUntil > now,
                cancellationToken);
        if (archive is null)
            return Results.NotFound();

        var current = await db.SaveSlots
            .SingleOrDefaultAsync(slot => slot.CloudUserId == gate.UserId && slot.SlotId == slotId, cancellationToken);
        if (current is null)
        {
            db.SaveSlots.Add(CreateRecordFromArchive(slotId, gate.UserId!, archive, now, revision: 1));
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new RevisionResponse(1));
        }

        db.RevisionArchives.Add(CreateArchive(current, now));
        var nextRevision = current.Revision + 1;
        current.Revision = nextRevision;
        current.TowerName = archive.TowerName;
        current.SchemaVersion = archive.SchemaVersion;
        current.Payload = archive.Payload;
        current.Checksum = archive.Checksum;
        current.ModifiedUtc = now;
        current.DeviceName = archive.DeviceName;
        current.PlayMinutes = archive.PlayMinutes;
        current.GameVersion = archive.GameVersion;
        current.ClientInstallId = archive.ClientInstallId;
        current.CompressedBytes = archive.CompressedBytes;
        current.DecompressedBytes = archive.DecompressedBytes;

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new RevisionResponse(nextRevision));
    }

    private static async Task<IResult> PutSaveRelational(
        int slotId,
        string userId,
        SaveUploadRequest request,
        byte[] payload,
        long decompressedBytes,
        DateTimeOffset now,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = await db.SaveSlots
            .AsNoTracking()
            .SingleOrDefaultAsync(slot => slot.CloudUserId == userId && slot.SlotId == slotId, cancellationToken);

        if (existing is null)
        {
            if (request.ExpectedRevision != 0)
                return Results.NotFound();

            db.SaveSlots.Add(CreateRecord(slotId, userId, request, payload, decompressedBytes, now, revision: 1));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new RevisionResponse(1));
        }

        var nextRevision = request.ExpectedRevision + 1;
        var rows = await db.SaveSlots
            .Where(slot => slot.CloudUserId == userId && slot.SlotId == slotId && slot.Revision == request.ExpectedRevision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(slot => slot.Revision, nextRevision)
                .SetProperty(slot => slot.TowerName, request.TowerName)
                .SetProperty(slot => slot.SchemaVersion, request.SchemaVersion)
                .SetProperty(slot => slot.Payload, payload)
                .SetProperty(slot => slot.Checksum, request.Checksum)
                .SetProperty(slot => slot.ModifiedUtc, now)
                .SetProperty(slot => slot.DeviceName, request.DeviceName)
                .SetProperty(slot => slot.PlayMinutes, request.PlayMinutes)
                .SetProperty(slot => slot.GameVersion, request.GameVersion)
                .SetProperty(slot => slot.ClientInstallId, request.ClientInstallId)
                .SetProperty(slot => slot.CompressedBytes, payload.LongLength)
                .SetProperty(slot => slot.DecompressedBytes, decompressedBytes),
                cancellationToken);

        if (rows == 1)
        {
            db.RevisionArchives.Add(CreateArchive(existing, now));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new RevisionResponse(nextRevision));
        }

        var current = await db.SaveSlots
            .AsNoTracking()
            .SingleOrDefaultAsync(slot => slot.CloudUserId == userId && slot.SlotId == slotId, cancellationToken);
        return current is null ? Results.NotFound() : Conflict(current);
    }

    private static async Task<IResult> PutSaveInMemory(
        int slotId,
        string userId,
        SaveUploadRequest request,
        byte[] payload,
        long decompressedBytes,
        DateTimeOffset now,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = await db.SaveSlots
            .SingleOrDefaultAsync(slot => slot.CloudUserId == userId && slot.SlotId == slotId, cancellationToken);

        if (existing is null)
        {
            if (request.ExpectedRevision != 0)
                return Results.NotFound();

            db.SaveSlots.Add(CreateRecord(slotId, userId, request, payload, decompressedBytes, now, revision: 1));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new RevisionResponse(1));
        }

        if (existing.Revision != request.ExpectedRevision)
            return Conflict(existing);

        db.RevisionArchives.Add(CreateArchive(existing, now));
        existing.Revision++;
        existing.TowerName = request.TowerName;
        existing.SchemaVersion = request.SchemaVersion;
        existing.Payload = payload;
        existing.Checksum = request.Checksum;
        existing.ModifiedUtc = now;
        existing.DeviceName = request.DeviceName;
        existing.PlayMinutes = request.PlayMinutes;
        existing.GameVersion = request.GameVersion;
        existing.ClientInstallId = request.ClientInstallId;
        existing.CompressedBytes = payload.LongLength;
        existing.DecompressedBytes = decompressedBytes;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new RevisionResponse(existing.Revision));
    }

    private static RevisionArchive CreateArchive(SlotRecord record, DateTimeOffset archivedUtc)
    {
        return new RevisionArchive
        {
            Id = Guid.NewGuid(),
            CloudUserId = record.CloudUserId,
            SlotId = record.SlotId,
            Revision = record.Revision,
            TowerName = record.TowerName,
            SchemaVersion = record.SchemaVersion,
            Payload = record.Payload,
            Checksum = record.Checksum,
            ModifiedUtc = record.ModifiedUtc,
            ArchivedUtc = archivedUtc,
            KeepUntil = archivedUtc.AddDays(30),
            DeviceName = record.DeviceName,
            PlayMinutes = record.PlayMinutes,
            GameVersion = record.GameVersion,
            ClientInstallId = record.ClientInstallId,
            CompressedBytes = record.CompressedBytes,
            DecompressedBytes = record.DecompressedBytes
        };
    }

    private static SlotRecord CreateRecord(
        int slotId,
        string userId,
        SaveUploadRequest request,
        byte[] payload,
        long decompressedBytes,
        DateTimeOffset now,
        long revision)
    {
        return new SlotRecord
        {
            Id = Guid.NewGuid(),
            CloudUserId = userId,
            SlotId = slotId,
            Revision = revision,
            TowerName = request.TowerName,
            SchemaVersion = request.SchemaVersion,
            Payload = payload,
            Checksum = request.Checksum,
            ModifiedUtc = now,
            DeviceName = request.DeviceName,
            PlayMinutes = request.PlayMinutes,
            GameVersion = request.GameVersion,
            ClientInstallId = request.ClientInstallId,
            CompressedBytes = payload.LongLength,
            DecompressedBytes = decompressedBytes
        };
    }

    private static SlotRecord CreateRecordFromArchive(
        int slotId,
        string userId,
        RevisionArchive archive,
        DateTimeOffset now,
        long revision)
    {
        return new SlotRecord
        {
            Id = Guid.NewGuid(),
            CloudUserId = userId,
            SlotId = slotId,
            Revision = revision,
            TowerName = archive.TowerName,
            SchemaVersion = archive.SchemaVersion,
            Payload = archive.Payload,
            Checksum = archive.Checksum,
            ModifiedUtc = now,
            DeviceName = archive.DeviceName,
            PlayMinutes = archive.PlayMinutes,
            GameVersion = archive.GameVersion,
            ClientInstallId = archive.ClientInstallId,
            CompressedBytes = archive.CompressedBytes,
            DecompressedBytes = archive.DecompressedBytes
        };
    }

    private static async Task<(string? UserId, IResult? Result)> GetVerifiedUserId(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return (null, Results.Unauthorized());

        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return (null, Results.Unauthorized());

        return user.EmailConfirmed
            ? (user.Id, null)
            : (null, Results.Json(new { code = "email_unverified" }, statusCode: StatusCodes.Status403Forbidden));
    }

    private static IResult? ValidatePayload(SaveUploadRequest request, out byte[] payload, out long decompressedBytes)
    {
        payload = [];
        decompressedBytes = 0;

        if (request.ExpectedRevision < 0 || request.SchemaVersion < 1)
            return BadRequest("invalid_save");

        try
        {
            payload = Convert.FromBase64String(request.PayloadBase64);
        }
        catch (FormatException)
        {
            return BadRequest("invalid_payload");
        }

        if (payload.LongLength > MaxCompressedBytes)
            return BadRequest("payload_too_large");

        var checksum = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        if (!string.Equals(checksum, request.Checksum, StringComparison.OrdinalIgnoreCase))
            return BadRequest("checksum_mismatch");

        try
        {
            decompressedBytes = CountGzipBytes(payload);
        }
        catch (InvalidDataException)
        {
            return BadRequest("invalid_payload");
        }

        if (decompressedBytes > MaxDecompressedBytes)
            return BadRequest("payload_too_large");

        return null;
    }

    private static long CountGzipBytes(byte[] payload)
    {
        using var input = new MemoryStream(payload);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = gzip.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return total;

            total += read;
            if (total > MaxDecompressedBytes)
                return total;
        }
    }

    private static IResult Conflict(SlotRecord record)
    {
        return Results.Json(
            new ConflictResponse(
                "conflict",
                record.Revision,
                record.TowerName,
                record.ModifiedUtc,
                record.DeviceName,
                record.PlayMinutes),
            statusCode: StatusCodes.Status409Conflict);
    }

    private static IResult BadRequest(string code)
    {
        return Results.Json(new { code }, statusCode: StatusCodes.Status400BadRequest);
    }

    private static bool IsValidSlot(int slotId)
    {
        return slotId is >= FirstSlotId and <= LastSlotId;
    }
}

public sealed record SaveUploadRequest(
    long ExpectedRevision,
    string TowerName,
    int SchemaVersion,
    string Checksum,
    string PayloadBase64,
    string? DeviceName,
    int? PlayMinutes,
    string? GameVersion,
    string? ClientInstallId);

public sealed record RevisionResponse(long Revision);
public sealed record SlotListResponse(IReadOnlyList<SlotSummary> Slots);
public sealed record RevisionListResponse(IReadOnlyList<ArchivedRevisionSummary> Revisions);

public sealed record ArchivedRevisionSummary(
    Guid RevisionId,
    long Revision,
    string TowerName,
    string? DeviceName,
    int? PlayMinutes,
    DateTimeOffset ModifiedUtc,
    DateTimeOffset KeepUntil);

public sealed record SlotSummary(
    int SlotId,
    bool IsOccupied,
    long Revision,
    string? TowerName,
    DateTimeOffset? ModifiedUtc,
    string? DeviceName,
    int? PlayMinutes,
    string? GameVersion,
    string? ClientInstallId)
{
    public static SlotSummary Empty(int slotId)
    {
        return new SlotSummary(slotId, false, 0, null, null, null, null, null, null);
    }

    public static SlotSummary Occupied(SlotRecord record)
    {
        return new SlotSummary(
            record.SlotId,
            true,
            record.Revision,
            record.TowerName,
            record.ModifiedUtc,
            record.DeviceName,
            record.PlayMinutes,
            record.GameVersion,
            record.ClientInstallId);
    }
}

public sealed record SlotDownload(
    int SlotId,
    long Revision,
    string TowerName,
    int SchemaVersion,
    string Checksum,
    string PayloadBase64,
    string? DeviceName,
    int? PlayMinutes,
    string? GameVersion,
    string? ClientInstallId,
    DateTimeOffset ModifiedUtc)
{
    public static SlotDownload FromRecord(SlotRecord record)
    {
        return new SlotDownload(
            record.SlotId,
            record.Revision,
            record.TowerName,
            record.SchemaVersion,
            record.Checksum,
            Convert.ToBase64String(record.Payload),
            record.DeviceName,
            record.PlayMinutes,
            record.GameVersion,
            record.ClientInstallId,
            record.ModifiedUtc);
    }
}

public sealed record ConflictResponse(
    string Code,
    long CurrentRevision,
    string? TowerName,
    DateTimeOffset ModifiedUtc,
    string? DeviceName,
    int? PlayMinutes);
