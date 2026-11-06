namespace Playtest.Api.Email;

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("smtp_unconfigured");
    }
}
