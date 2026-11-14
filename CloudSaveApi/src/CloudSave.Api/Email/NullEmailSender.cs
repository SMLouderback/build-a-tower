namespace CloudSave.Api.Email;

public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("email_unconfigured correlation_id={CorrelationId}", Guid.NewGuid().ToString("N"));
        return Task.CompletedTask;
    }
}
