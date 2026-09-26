using System.Data;
using CloudSave.Api.Data;
using CloudSave.Api.Invites;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);

        return endpoints;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<CloudUser> users,
        AppDbContext db,
        InviteService invites,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(db, cancellationToken);

        var publicRegistration = configuration.GetValue<bool>("PublicRegistration");
        if (!publicRegistration && string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return InviteError("invite_required");
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (!publicRegistration && !await invites.TryConsumeAsync(request.InviteCode!, cancellationToken))
        {
            return InviteError("invite_invalid");
        }

        var email = request.Email.Trim();
        var user = new CloudUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(result));
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new RegisterResponse(user.Id, email, user.EmailConfirmed));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<CloudUser> users,
        AppDbContext db,
        TokenService tokenService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(db, cancellationToken);

        var email = request.Email.Trim();
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized();
        }

        var refreshToken = await AddRefreshToken(db, user.Id, Guid.NewGuid(), timeProvider, cancellationToken);
        return Results.Ok(CreateTokenResponse(tokenService, user, refreshToken));
    }

    private static async Task<IResult> Refresh(
        RefreshRequest request,
        AppDbContext db,
        TokenService tokenService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(db, cancellationToken);

        var tokenHash = TokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await db.RefreshTokens
            .Include(token => token.CloudUser)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken?.CloudUser is null)
        {
            return Unauthorized();
        }

        var now = timeProvider.GetUtcNow();
        if (!storedToken.IsActive(now))
        {
            await RevokeFamily(db, storedToken.FamilyId, now, cancellationToken);
            return Unauthorized();
        }

        var replacementRefreshToken = TokenService.CreateRefreshToken();
        var replacementHash = TokenService.HashRefreshToken(replacementRefreshToken);

        storedToken.RevokedUtc = now;
        storedToken.ReplacedByTokenHash = replacementHash;
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            CloudUserId = storedToken.CloudUserId,
            TokenHash = replacementHash,
            FamilyId = storedToken.FamilyId,
            CreatedUtc = now,
            ExpiresUtc = now.Add(TokenService.RefreshTokenLifetime)
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(CreateTokenResponse(tokenService, storedToken.CloudUser, replacementRefreshToken));
    }

    private static async Task<IResult> Logout(
        RefreshRequest request,
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(db, cancellationToken);

        var tokenHash = TokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await db.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null)
        {
            await RevokeFamily(db, storedToken.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        }

        return Results.Ok(new { status = "ok" });
    }

    private static async Task<string> AddRefreshToken(
        AppDbContext db,
        string userId,
        Guid familyId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var refreshToken = TokenService.CreateRefreshToken();
        var now = timeProvider.GetUtcNow();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            CloudUserId = userId,
            TokenHash = TokenService.HashRefreshToken(refreshToken),
            FamilyId = familyId,
            CreatedUtc = now,
            ExpiresUtc = now.Add(TokenService.RefreshTokenLifetime)
        });

        await db.SaveChangesAsync(cancellationToken);
        return refreshToken;
    }

    private static AuthResponse CreateTokenResponse(TokenService tokenService, CloudUser user, string refreshToken)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthResponse(accessToken.Token, refreshToken, accessToken.ExpiresIn);
    }

    private static async Task RevokeFamily(
        AppDbContext db,
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var familyTokens = await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.RevokedUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDatabaseAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    private static IResult Unauthorized()
    {
        return Results.Json(new { code = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult InviteError(string code)
    {
        return Results.Json(new { code }, statusCode: StatusCodes.Status403Forbidden);
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result)
    {
        return result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray());
    }
}

public sealed record RegisterRequest(string Email, string Password, string? InviteCode);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record RegisterResponse(string UserId, string Email, bool EmailConfirmed);
public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);
