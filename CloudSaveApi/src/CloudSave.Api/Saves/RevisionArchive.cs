using CloudSave.Api.Auth;

namespace CloudSave.Api.Saves;

public sealed class RevisionArchive
{
    public Guid Id { get; set; }
    public string CloudUserId { get; set; } = string.Empty;
    public CloudUser? CloudUser { get; set; }
    public int SlotId { get; set; }
    public long Revision { get; set; }
    public string TowerName { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public byte[] Payload { get; set; } = [];
    public string Checksum { get; set; } = string.Empty;
    public DateTimeOffset ModifiedUtc { get; set; }
    public DateTimeOffset ArchivedUtc { get; set; }
    public DateTimeOffset KeepUntil { get; set; }
    public string? DeviceName { get; set; }
    public int? PlayMinutes { get; set; }
    public string? GameVersion { get; set; }
    public string? ClientInstallId { get; set; }
    public long CompressedBytes { get; set; }
    public long DecompressedBytes { get; set; }
}
