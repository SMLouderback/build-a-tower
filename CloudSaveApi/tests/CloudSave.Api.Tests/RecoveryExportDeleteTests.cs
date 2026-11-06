using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CloudSave.Api.Data;
using CloudSave.Api.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace CloudSave.Api.Tests;

public class RecoveryExportDeleteTests
{
    [Fact]
    public async Task Replacing_slot_archives_previous_revision_and_restore_recovers_it()
    {
        await using var factory = await RecoveryApiFactory.StartAsync();
        var client = factory.CreateClient();
        var account = await factory.RegisterVerifiedAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var firstRequest = SaveRequest.Valid("Original Tower", "Office PC", payloadText: "original-payload");
        var first = await client.PutAsJsonAsync("/v1/saves/1", firstRequest);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var secondRequest = SaveRequest.Valid("Renovated Tower", "Laptop", expectedRevision: 1, payloadText: "renovated-payload");
        var second = await client.PutAsJsonAsync("/v1/saves/1", secondRequest);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var revisions = await client.GetFromJsonAsync<RevisionListResponse>("/v1/saves/1/revisions");
        Assert.NotNull(revisions);
        var archived = Assert.Single(revisions.Revisions);
        Assert.Equal(1, archived.Revision);
        Assert.Equal("Original Tower", archived.TowerName);
        Assert.Equal("Office PC", archived.DeviceName);
        Assert.True(archived.KeepUntil > DateTimeOffset.UtcNow.AddDays(29));

        var restore = await client.PostAsync($"/v1/saves/1/restore/{archived.RevisionId}", content: null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var restoreBody = await restore.Content.ReadFromJsonAsync<RevisionResponse>();
        Assert.Equal(3, restoreBody?.Revision);

        var download = await client.GetFromJsonAsync<SlotDownloadResponse>("/v1/saves/1");
        Assert.NotNull(download);
        Assert.Equal(3, download.Revision);
        Assert.Equal(firstRequest.PayloadBase64, download.PayloadBase64);
        Assert.Equal("Original Tower", download.TowerName);
    }

    [Fact]
    public async Task Cross_account_restore_returns_not_found()
    {
        await using var factory = await RecoveryApiFactory.StartAsync();
        var client = factory.CreateClient();
        var owner = await factory.RegisterVerifiedAndLogin(client);
        var other = await factory.RegisterVerifiedAndLogin(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/v1/saves/2", SaveRequest.Valid("Private", "Owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/v1/saves/2", SaveRequest.Valid("Private v2", "Owner", expectedRevision: 1))).StatusCode);
        var revisions = await client.GetFromJsonAsync<RevisionListResponse>("/v1/saves/2/revisions");
        var revisionId = Assert.Single(revisions!.Revisions).RevisionId;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
        var restore = await client.PostAsync($"/v1/saves/2/restore/{revisionId}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, restore.StatusCode);
    }

    [Fact]
    public async Task Export_includes_account_and_current_slots_without_identity_secrets()
    {
        await using var factory = await RecoveryApiFactory.StartAsync();
        var client = factory.CreateClient();
        var account = await factory.RegisterVerifiedAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var uploadRequest = SaveRequest.Valid("Export Tower", "Desktop");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/v1/saves/3", uploadRequest)).StatusCode);

        var export = await client.GetAsync("/v1/account/export");

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var body = await export.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TokenHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshToken", body, StringComparison.OrdinalIgnoreCase);

        var parsed = await export.Content.ReadFromJsonAsync<AccountExportResponse>();
        Assert.NotNull(parsed);
        Assert.Equal(account.Email, parsed.Account.Email);
        var slot = Assert.Single(parsed.Slots);
        Assert.Equal(3, slot.SlotId);
        Assert.Equal("Export Tower", slot.TowerName);
        Assert.Equal(uploadRequest.PayloadBase64, slot.PayloadBase64);
    }

    [Fact]
    public async Task Delete_account_requires_confirmation_and_removes_owned_data()
    {
        await using var factory = await RecoveryApiFactory.StartAsync();
        var client = factory.CreateClient();
        var account = await factory.RegisterVerifiedAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/v1/saves/1", SaveRequest.Valid("Delete Me", "Desktop"))).StatusCode);

        var missingConfirm = await client.DeleteAsync("/v1/account");
        Assert.Equal(HttpStatusCode.BadRequest, missingConfirm.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/account");
        request.Headers.Add("X-Confirm", "DELETE");
        var delete = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        await factory.AssertAccountDataRemoved(account.Email);

        var afterDelete = await client.GetAsync("/v1/saves");
        Assert.Equal(HttpStatusCode.Unauthorized, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Auth_and_put_rate_limits_return_rate_limited()
    {
        await using var authFactory = await RecoveryApiFactory.StartAsync();
        var authClient = authFactory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var login = await authClient.PostAsJsonAsync("/v1/auth/login", new
            {
                email = $"missing-{attempt}@example.com",
                password = "WrongPassword!42"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }

        var limitedLogin = await authClient.PostAsJsonAsync("/v1/auth/login", new
        {
            email = "limited@example.com",
            password = "WrongPassword!42"
        });
        Assert.Equal((HttpStatusCode)429, limitedLogin.StatusCode);
        Assert.Equal("rate_limited", (await limitedLogin.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);

        await using var putFactory = await RecoveryApiFactory.StartAsync();
        var putClient = putFactory.CreateClient();
        var account = await putFactory.RegisterVerifiedAndLogin(putClient);
        putClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        for (var revision = 0; revision < 30; revision++)
        {
            var put = await putClient.PutAsJsonAsync("/v1/saves/1", SaveRequest.Valid($"Tower {revision}", "Desktop", expectedRevision: revision));
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        var limitedPut = await putClient.PutAsJsonAsync("/v1/saves/1", SaveRequest.Valid("Limited Tower", "Desktop", expectedRevision: 30));
        Assert.Equal((HttpStatusCode)429, limitedPut.StatusCode);
        Assert.Equal("rate_limited", (await limitedPut.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }

    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, string Email);
    private sealed record ErrorResponse(string Code);
    private sealed record RevisionResponse(long Revision);
    private sealed record RevisionListResponse(IReadOnlyList<ArchivedRevisionResponse> Revisions);
    private sealed record ArchivedRevisionResponse(Guid RevisionId, long Revision, string TowerName, string? DeviceName, int? PlayMinutes, DateTimeOffset ModifiedUtc, DateTimeOffset KeepUntil);
    private sealed record AccountExportResponse(ExportAccountResponse Account, IReadOnlyList<ExportSlotResponse> Slots);
    private sealed record ExportAccountResponse(string UserId, string Email, bool EmailConfirmed, string? CreatedUtc);
    private sealed record ExportSlotResponse(int SlotId, long Revision, string TowerName, int SchemaVersion, string Checksum, string PayloadBase64);

    private sealed record SlotDownloadResponse(
        int SlotId,
        long Revision,
        string TowerName,
        int SchemaVersion,
        string Checksum,
        string PayloadBase64,
        string? DeviceName,
        int? PlayMinutes,
        string? GameVersion,
        string? ClientInstallId,
        string? ModifiedUtc);

    private sealed record SaveRequest(
        long ExpectedRevision,
        string TowerName,
        int SchemaVersion,
        string Checksum,
        string PayloadBase64,
        string DeviceName,
        int PlayMinutes,
        string GameVersion,
        string ClientInstallId)
    {
        public static SaveRequest Valid(
            string towerName,
            string deviceName,
            long expectedRevision = 0,
            string payloadText = "payload")
        {
            var json = Encoding.UTF8.GetBytes($$"""{"towerName":"{{towerName}}","data":"{{payloadText}}"}""");
            var compressed = Compress(json);
            return new SaveRequest(
                expectedRevision,
                towerName,
                1,
                Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant(),
                Convert.ToBase64String(compressed),
                deviceName,
                5,
                "0.1-test",
                Guid.NewGuid().ToString("N"));
        }

        private static byte[] Compress(byte[] bytes)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }

            return output.ToArray();
        }
    }

    private sealed record SentEmail(string To, string Subject, string Body);

    private sealed class CapturingEmailSender : IEmailSender
    {
        private readonly List<SentEmail> _emails = [];

        public IReadOnlyList<SentEmail> Emails
        {
            get
            {
                lock (_emails)
                    return _emails.ToList();
            }
        }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            lock (_emails)
                _emails.Add(new SentEmail(to, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class RecoveryApiFactory : WebApplicationFactory<Program>
    {
        private const string SigningKey = "recovery-tests-signing-key-32-bytes-minimum";
        private readonly PostgreSqlContainer? _postgres;
        private readonly CapturingEmailSender _emailSender = new();
        private readonly string _databaseName = "cloudsave-recovery-tests-" + Guid.NewGuid().ToString("N");

        private RecoveryApiFactory(PostgreSqlContainer? postgres)
        {
            _postgres = postgres;
        }

        public static async Task<RecoveryApiFactory> StartAsync()
        {
            try
            {
                var postgres = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();
                await postgres.StartAsync();
                return new RecoveryApiFactory(postgres);
            }
            catch (Exception)
            {
                return new RecoveryApiFactory(postgres: null);
            }
        }

        public async Task<AuthTokens> RegisterVerifiedAndLogin(HttpClient client)
        {
            var email = $"recovery-{Guid.NewGuid():N}@example.com";
            const string password = "CorrectHorseBatteryStaple!42";

            var register = await client.PostAsJsonAsync("/v1/auth/register", new
            {
                email,
                password
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);

            var verifyToken = ExtractToken(SingleEmailTo(email, "verify"));
            var verify = await client.PostAsJsonAsync("/v1/auth/verify", new
            {
                email,
                token = verifyToken
            });
            Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

            var login = await client.PostAsJsonAsync("/v1/auth/login", new
            {
                email,
                password
            });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var tokens = await login.Content.ReadFromJsonAsync<AuthTokens>();
            Assert.NotNull(tokens);
            return tokens with { Email = email };
        }

        public async Task AssertAccountDataRemoved(string email)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            Assert.False(await db.Users.AnyAsync(user => user.Email == email));
            Assert.Empty(await db.SaveSlots.ToListAsync());
            Assert.Empty(await db.RefreshTokens.ToListAsync());
        }

        private SentEmail SingleEmailTo(string email, string subjectFragment)
        {
            return Assert.Single(_emailSender.Emails.Where(sent =>
                sent.To == email &&
                sent.Subject.Contains(subjectFragment, StringComparison.OrdinalIgnoreCase)));
        }

        private static string ExtractToken(SentEmail email)
        {
            var match = Regex.Match(email.Body, @"token:\s*(?<token>[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            Assert.True(match.Success, $"Email body did not contain a token: {email.Body}");
            return match.Groups["token"].Value;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["PublicRegistration"] = "true",
                    ["Jwt:Audience"] = "CloudSave.Tests",
                    ["Jwt:Issuer"] = "CloudSave.Tests",
                    ["Jwt:SigningKey"] = SigningKey
                };
                if (_postgres is not null)
                    values["ConnectionStrings:CloudSavePostgres"] = _postgres.GetConnectionString();
                config.AddInMemoryCollection(values);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(_emailSender);

                if (_postgres is null)
                {
                    var remove = services
                        .Where(d => d.ServiceType == typeof(AppDbContext)
                                    || d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                        .ToList();
                    foreach (var descriptor in remove)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(_databaseName));
                }
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (_postgres is not null)
                await _postgres.DisposeAsync();
        }
    }
}
