using System.Security.Cryptography;
using System.Text;
using CloudSave.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Email;

public sealed class EmailTokenService
{
    public const string VerifyPurpose = "verify";
    public const string ResetPurpose = "reset";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public EmailTokenService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<string> IssueAsync(string userId, string purpose, CancellationToken cancellationToken)
    {
        var rawToken = CreateToken();
        var now = _timeProvider.GetUtcNow();

        _db.EmailTokens.Add(new EmailToken
        {
            Id = Guid.NewGuid(),
            CloudUserId = userId,
            Purpose = purpose,
            TokenHash = HashToken(rawToken),
            CreatedUtc = now,
            ExpiresUtc = now.Add(TokenLifetime)
        });

        await _db.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<bool> TryConsumeAsync(string userId, string purpose, string rawToken, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var hash = HashToken(rawToken);
        var token = await _db.EmailTokens
            .Where(candidate =>
                candidate.CloudUserId == userId &&
                candidate.Purpose == purpose &&
                candidate.TokenHash == hash)
            .OrderByDescending(candidate => candidate.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null || token.ConsumedUtc is not null || token.ExpiresUtc <= now)
            return false;

        token.ConsumedUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
