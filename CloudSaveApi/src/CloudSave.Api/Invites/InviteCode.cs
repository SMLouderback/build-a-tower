namespace CloudSave.Api.Invites;

public sealed class InviteCode
{
    public Guid Id { get; set; }
    public required string CodeHash { get; set; }
    public int RemainingUses { get; set; }
    public DateTimeOffset? ExpiresUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
