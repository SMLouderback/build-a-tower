namespace CloudSave.Api.Auth;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public required string CloudUserId { get; set; }
    public CloudUser? CloudUser { get; set; }
    public required string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTimeOffset now)
    {
        return RevokedUtc is null && ExpiresUtc > now;
    }
}
