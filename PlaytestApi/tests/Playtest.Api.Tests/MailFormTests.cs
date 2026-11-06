using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
            builder.UseSetting("Playtest:DataDirectory", _dataDir);
            builder.UseSetting("Playtest:OperatorEmail", "escapemobileproductions@gmail.com");
            builder.UseSetting("Playtest:PublicBaseUrl", "https://escapeproductions.biz");
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
        var response = await _client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Friend",
            email = "not-an-email"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_mailbox.Sent);
    }

    [Fact]
    public async Task Request_key_sends_confirmation_to_requester_not_operator()
    {
        var response = await _client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Friend",
            email = "friend@example.com",
            note = "Love SimTower"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_mailbox.Sent);
        Assert.Equal("friend@example.com", _mailbox.Sent[0].To);
        Assert.Contains("confirm-key-request?token=", _mailbox.Sent[0].Body);
        Assert.DoesNotContain("family-test-key", _mailbox.Sent[0].Body);
        Assert.NotEqual("escapemobileproductions@gmail.com", _mailbox.Sent[0].To);
    }

    [Fact]
    public async Task Confirm_notifies_operator_once()
    {
        await _client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Pat Tester",
            email = "v.a.le.nc.ia.o.p.r.a.pl@gmail.com",
            note = "please"
        });
        var match = Regex.Match(_mailbox.Sent[0].Body, @"token=([0-9a-f]+)");
        Assert.True(match.Success);
        var token = match.Groups[1].Value;

        var confirm = await _client.GetAsync("/playtest/confirm-key-request?token=" + token);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(2, _mailbox.Sent.Count);
        Assert.Equal("escapemobileproductions@gmail.com", _mailbox.Sent[1].To);
        Assert.Contains("confirmed", _mailbox.Sent[1].Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pat Tester", _mailbox.Sent[1].Body);
        Assert.Contains("valenciaoprapl@gmail.com", _mailbox.Sent[1].Body);

        var again = await _client.GetAsync("/playtest/confirm-key-request?token=" + token);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, _mailbox.Sent.Count);
    }

    [Fact]
    public async Task Request_key_honeypot_is_silent()
    {
        var response = await _client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Bot",
            email = "bot@example.com",
            website = "http://spam.example"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(_mailbox.Sent);
    }

    [Fact]
    public async Task Request_key_gmail_dot_variants_share_pending_window()
    {
        var first = await _client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Val",
            email = "v.a.le.nc.ia.o.p.r.a.pl@gmail.com"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Val",
            email = "va.le.n.c.ia.opra.pl@gmail.com"
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(_mailbox.Sent);
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
        var response = await client.PostAsJsonAsync("/playtest/request-key", new
        {
            name = "Friend",
            email = "friend@example.com"
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var logPath = Path.Combine(_dataDir, "key-requests.log");
        Assert.False(File.Exists(logPath));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_dataDir, "pending-key-requests"), "*.json"));
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
