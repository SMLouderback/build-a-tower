using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Playtest.Api;

public sealed class VersionDocument
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    readonly PlaytestOptions _options;

    public VersionDocument(IOptions<PlaytestOptions> options) =>
        _options = options.Value;

    string DocumentPath => Path.Combine(_options.DataDirectory, "version.json");

    public void Write(string version, DateTimeOffset releasedAt)
    {
        Directory.CreateDirectory(_options.DataDirectory);
        var doc = new VersionDoc(version, releasedAt.ToUniversalTime().ToString("O"), _options.DownloadPage);
        File.WriteAllText(DocumentPath, JsonSerializer.Serialize(doc, JsonOptions));
    }

    public bool TryRead(out VersionDoc? doc)
    {
        if (!File.Exists(DocumentPath))
        {
            doc = null;
            return false;
        }

        doc = JsonSerializer.Deserialize<VersionDoc>(File.ReadAllText(DocumentPath), JsonOptions);
        return doc is not null;
    }
}

public sealed record VersionDoc(string Version, string ReleasedAt, string DownloadPage);
