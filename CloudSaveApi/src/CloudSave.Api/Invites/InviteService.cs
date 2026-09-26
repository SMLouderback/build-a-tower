using System.Security.Cryptography;
using System.Text;
using CloudSave.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Invites;

public sealed class InviteService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public InviteService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<string> MintAsync(int uses, DateTimeOffset? expiresUtc, CancellationToken cancellationToken)
    {
        if (uses <= 0)
            throw new ArgumentOutOfRangeException(nameof(uses), "Invite uses must be greater than zero.");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var code = CreateCode();
            _db.InviteCodes.Add(new InviteCode
            {
                Id = Guid.NewGuid(),
                CodeHash = HashCode(code),
                RemainingUses = uses,
                ExpiresUtc = expiresUtc,
                CreatedUtc = _timeProvider.GetUtcNow()
            });

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return code;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                _db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Failed to mint a unique invite code.");
    }

    public async Task<bool> TryConsumeAsync(string rawCode, CancellationToken cancellationToken)
    {
        var codeHash = HashCode(rawCode);
        var now = _timeProvider.GetUtcNow();
        var invite = await _db.InviteCodes.SingleOrDefaultAsync(code => code.CodeHash == codeHash, cancellationToken);

        if (invite is null)
            return false;
        if (invite.RemainingUses <= 0)
            return false;
        if (invite.ExpiresUtc is not null && invite.ExpiresUtc <= now)
            return false;

        invite.RemainingUses--;
        return true;
    }

    public static string HashCode(string rawCode)
    {
        var normalized = rawCode.Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateCode()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
