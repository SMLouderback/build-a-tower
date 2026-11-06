using CloudSave.Api.Auth;

namespace CloudSave.Api.Email;

public sealed class EmailToken
{
    public Guid Id { get; set; }
    public string CloudUserId { get; set; } = string.Empty;
    public CloudUser? CloudUser { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? ConsumedUtc { get; set; }
}
