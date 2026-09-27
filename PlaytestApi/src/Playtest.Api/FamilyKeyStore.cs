using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Playtest.Api;

public sealed class FamilyKeyStore
{
    static readonly object HasherUser = new();
    readonly PlaytestOptions _options;
    readonly PasswordHasher<object> _hasher = new();

    public FamilyKeyStore(IOptions<PlaytestOptions> options) =>
        _options = options.Value;

    string HashPath => Path.Combine(_options.DataDirectory, "family-key.hash");

    public void SetPlaintext(string plaintext)
    {
        Directory.CreateDirectory(_options.DataDirectory);
        var hash = _hasher.HashPassword(HasherUser, plaintext);
        File.WriteAllText(HashPath, hash);
    }

    public bool Verify(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        if (!File.Exists(HashPath))
            return false;
        var stored = File.ReadAllText(HashPath);
        return _hasher.VerifyHashedPassword(HasherUser, stored, key) != PasswordVerificationResult.Failed;
    }
}
