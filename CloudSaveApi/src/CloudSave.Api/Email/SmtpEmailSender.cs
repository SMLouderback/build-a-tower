using System.Net;
using System.Net.Mail;

namespace CloudSave.Api.Email;

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "no-reply@escapeproductions.biz";
    public bool EnableSsl { get; set; } = true;
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _options = configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host!, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
            client.Credentials = new NetworkCredential(_options.User, _options.Password);

        using var message = new MailMessage(_options.From, to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        var smtpHost = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
            services.AddSingleton<IEmailSender, NullEmailSender>();
        else
            services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
