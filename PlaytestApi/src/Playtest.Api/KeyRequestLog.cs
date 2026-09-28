using Microsoft.Extensions.Options;

namespace Playtest.Api;

public sealed class KeyRequestLog
{
    readonly PlaytestOptions _options;

    public KeyRequestLog(IOptions<PlaytestOptions> options) =>
        _options = options.Value;

    public void Append(string email)
    {
        Directory.CreateDirectory(_options.DataDirectory);
        var line = $"{DateTimeOffset.UtcNow:O} {email}{Environment.NewLine}";
        File.AppendAllText(Path.Combine(_options.DataDirectory, "key-requests.log"), line);
    }
}
