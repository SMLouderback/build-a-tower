using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CloudSave.Api.Auth;
using CloudSave.Api.Data;
using CloudSave.Api.Saves;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Account;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/account")
            .RequireAuthorization();

        group.MapGet("/export", ExportAccount);
        group.MapDelete("", DeleteAccount);

        return endpoints;
    }

    private static async Task<IResult> ExportAccount(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return Results.Unauthorized();

        var slots = await db.SaveSlots
            .Where(slot => slot.CloudUserId == user.Id)
            .OrderBy(slot => slot.SlotId)
            .Select(slot => new ExportSlot(
                slot.SlotId,
                slot.Revision,
                slot.TowerName,
                slot.SchemaVersion,
                slot.Checksum,
                Convert.ToBase64String(slot.Payload),
                slot.DeviceName,
                slot.PlayMinutes,
                slot.GameVersion,
                slot.ClientInstallId,
                slot.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new AccountExport(
            new ExportAccount(user.Id, user.Email ?? string.Empty, user.EmailConfirmed, null),
            slots));
    }

    private static async Task<IResult> DeleteAccount(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        AppDbContext db,
        UserManager<CloudUser> users,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!httpContext.Request.Headers.TryGetValue("X-Confirm", out var confirmation) ||
            !string.Equals(confirmation.ToString(), "DELETE", StringComparison.Ordinal))
        {
            return Results.Json(new { code = "confirmation_required" }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!HasFreshAccessToken(principal, timeProvider.GetUtcNow()))
        {
            return Results.Json(new { code = "recent_auth_required" }, statusCode: StatusCodes.Status403Forbidden);
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return Results.Unauthorized();

        var archives = await db.RevisionArchives.Where(archive => archive.CloudUserId == user.Id).ToListAsync(cancellationToken);
        var slots = await db.SaveSlots.Where(slot => slot.CloudUserId == user.Id).ToListAsync(cancellationToken);
        var refreshTokens = await db.RefreshTokens.Where(token => token.CloudUserId == user.Id).ToListAsync(cancellationToken);
        var emailTokens = await db.EmailTokens.Where(token => token.CloudUserId == user.Id).ToListAsync(cancellationToken);

        db.RevisionArchives.RemoveRange(archives);
        db.SaveSlots.RemoveRange(slots);
        db.RefreshTokens.RemoveRange(refreshTokens);
        db.EmailTokens.RemoveRange(emailTokens);
        await db.SaveChangesAsync(cancellationToken);

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray()));
        }

        return Results.Ok(new { status = "ok" });
    }

    private static bool HasFreshAccessToken(ClaimsPrincipal principal, DateTimeOffset now)
    {
        var issuedAt = principal.FindFirstValue(JwtRegisteredClaimNames.Iat);
        return long.TryParse(issuedAt, out var issuedAtSeconds) &&
               now - DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds) <= TokenService.AccessTokenLifetime;
    }
}

public sealed record AccountExport(ExportAccount Account, IReadOnlyList<ExportSlot> Slots);

public sealed record ExportAccount(string UserId, string Email, bool EmailConfirmed, string? CreatedUtc);

public sealed record ExportSlot(
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
    DateTimeOffset ModifiedUtc);
