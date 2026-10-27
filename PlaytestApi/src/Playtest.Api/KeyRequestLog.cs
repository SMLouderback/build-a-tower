using Microsoft.Extensions.Options;

namespace Playtest.Api;

public sealed class KeyRequestLog
{
    readonly PlaytestOptions _options;

    public KeyRequestLog(IOptions<PlaytestOptions> options) =>
        _options = options.Value;

    public void AppendPending(string email, string canonical, string name)
    {
        Write("pending", $"{email} canonical={canonical} name={name}");
    }

    public void AppendConfirmed(PendingKeyRequest pending)
    {
        Write(
            "confirmed",
            $"{pending.Email} canonical={pending.CanonicalEmail} name={pending.Name} ip={pending.RemoteIp}");
    }

    void Write(string status, string detail)
    {
        Directory.CreateDirectory(_options.DataDirectory);
        var line = $"{DateTimeOffset.UtcNow:O} {status} {detail}{Environment.NewLine}";
        File.AppendAllText(Path.Combine(_options.DataDirectory, "key-requests.log"), line);
    }
}
