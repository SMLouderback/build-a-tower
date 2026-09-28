using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Playtest.Api;
using Playtest.Api.Email;

namespace Playtest.Api.Tests;

public sealed class MailFormTests : IAsyncLifetime
{
    WebApplicationFactory<Program> _factory = null!;
    HttpClient _client = null!;
    RecordingEmailSender _mailbox = null!;
    string _dataDir = null!;

    public Task InitializeAsync()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "playtest-mail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);
        _mailbox = new RecordingEmailSender();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Playtest:DataDirectory", _dataDir);
            builder.UseSetting("Playtest:OperatorEmail", "escapemobileproductions@gmail.com");
            builder.ConfigureTestServices(services =>
            {
                var existing = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
                if (existing != null)
                    services.Remove(existing);
                services.AddSingleton<IEmailSender>(_mailbox);
            });
        });
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FamilyKeyStore>().SetPlaintext("family-test-key");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        Directory.Delete(_dataDir, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Request_key_invalid_email_does_not_mail()
    {
        var response = await _client.PostAsJsonAsync("/playtest/request-key", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_mailbox.Sent);
    }

    [Fact]
    public async Task Request_key_valid_email_mails_operator_without_key()
    {
        var response = await _client.PostAsJsonAsync("/playtest/request-key", new { email = "friend@example.com" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(_mailbox.Sent);
        Assert.Equal("escapemobileproductions@gmail.com", _mailbox.Sent[0].To);
        Assert.Contains("friend@example.com", _mailbox.Sent[0].Body);
        Assert.DoesNotContain("family-test-key", _mailbox.Sent[0].Body);
    }

    [Fact]
    public async Task Feedback_empty_message_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/playtest/feedback", new { message = "  ", version = "0.1.0" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_mailbox.Sent);
    }

    [Fact]
    public async Task Request_key_is_logged_when_smtp_is_down()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Playtest:DataDirectory", _dataDir);
            builder.UseSetting("Playtest:OperatorEmail", "escapemobileproductions@gmail.com");
            builder.ConfigureTestServices(services =>
            {
                var existing = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
                if (existing != null)
                    services.Remove(existing);
                services.AddSingleton<IEmailSender, NullEmailSender>();
            });
        });
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/playtest/request-key", new { email = "friend@example.com" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var log = File.ReadAllText(Path.Combine(_dataDir, "key-requests.log"));
        Assert.Contains("friend@example.com", log);
    }

    [Fact]
    public async Task Feedback_sends_note_and_version()
    {
        var response = await _client.PostAsJsonAsync("/playtest/feedback", new
        {
            message = "Stairs feel slow",
            name = "Pat",
            email = "pat@example.com",
            version = "0.1.0"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("Stairs feel slow", _mailbox.Sent[0].Body);
        Assert.Contains("0.1.0", _mailbox.Sent[0].Body);
    }
}
