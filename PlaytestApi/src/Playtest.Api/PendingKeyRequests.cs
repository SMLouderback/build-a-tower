using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Playtest.Api;

public sealed class PendingKeyRequest
{
    public string Token { get; set; } = "";
    public string Email { get; set; } = "";
    public string CanonicalEmail { get; set; } = "";
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string? RemoteIp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PendingKeyRequests
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    readonly PlaytestOptions _options;

    public PendingKeyRequests(IOptions<PlaytestOptions> options) =>
        _options = options.Value;

    string Dir => Path.Combine(_options.DataDirectory, "pending-key-requests");

    public PendingKeyRequest Create(string email, string canonicalEmail, string name, string note, string? remoteIp)
    {
        Directory.CreateDirectory(Dir);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var pending = new PendingKeyRequest
        {
            Token = token,
            Email = email,
            CanonicalEmail = canonicalEmail,
            Name = name,
            Note = note,
            RemoteIp = remoteIp,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var path = Path.Combine(Dir, token + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(pending, JsonOptions));
        return pending;
    }

    public bool HasRecentPending(string canonicalEmail, TimeSpan window)
    {
        if (!Directory.Exists(Dir))
            return false;
        var cutoff = DateTimeOffset.UtcNow - window;
        foreach (var path in Directory.EnumerateFiles(Dir, "*.json"))
        {
            try
            {
                var pending = JsonSerializer.Deserialize<PendingKeyRequest>(File.ReadAllText(path), JsonOptions);
                if (pending == null)
                    continue;
                if (!string.Equals(pending.CanonicalEmail, canonicalEmail, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (pending.CreatedAt >= cutoff)
                    return true;
            }
            catch (JsonException)
            {
                // ignore corrupt files
            }
        }

        return false;
    }

    public void Discard(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;
        var path = Path.Combine(Dir, token.ToLowerInvariant() + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }

    public PendingKeyRequest? Take(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 80)
            return null;
        foreach (var ch in token)
        {
            if (!char.IsAsciiHexDigit(ch))
                return null;
        }

        var path = Path.Combine(Dir, token.ToLowerInvariant() + ".json");
        if (!File.Exists(path))
            return null;

        PendingKeyRequest? pending;
        try
        {
            pending = JsonSerializer.Deserialize<PendingKeyRequest>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (pending == null)
            return null;

        File.Delete(path);
        if (DateTimeOffset.UtcNow - pending.CreatedAt > TimeSpan.FromHours(_options.ConfirmTokenHours))
            return null;

        return pending;
    }

    public void PruneExpired()
    {
        if (!Directory.Exists(Dir))
            return;
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(_options.ConfirmTokenHours);
        foreach (var path in Directory.EnumerateFiles(Dir, "*.json"))
        {
            try
            {
                var pending = JsonSerializer.Deserialize<PendingKeyRequest>(File.ReadAllText(path), JsonOptions);
                if (pending == null || pending.CreatedAt < cutoff)
                    File.Delete(path);
            }
            catch
            {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }
    }
}
