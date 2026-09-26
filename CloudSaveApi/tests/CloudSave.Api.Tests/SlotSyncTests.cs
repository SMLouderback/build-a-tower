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

public class SlotSyncTests
{
    [Fact]
    public async Task List_returns_three_slots_and_isolates_accounts()
    {
        await using var factory = await SlotSyncApiFactory.StartAsync();
        var client = factory.CreateClient();
        var accountA = await factory.RegisterVerifiedAndLogin(client);
        var accountB = await factory.RegisterVerifiedAndLogin(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accountA.AccessToken);
        var upload = await client.PutAsJsonAsync("/v1/saves/1", SaveRequest.Valid("Tower A", "Laptop"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var accountAList = await client.GetFromJsonAsync<SlotListResponse>("/v1/saves");
        Assert.NotNull(accountAList);
        Assert.Equal(3, accountAList.Slots.Count);
        Assert.Equal(new[] { 1, 2, 3 }, accountAList.Slots.Select(slot => slot.SlotId));
        Assert.True(accountAList.Slots[0].IsOccupied);
        Assert.Equal(1, accountAList.Slots[0].Revision);
        Assert.Equal("Tower A", accountAList.Slots[0].TowerName);
        Assert.False(accountAList.Slots[1].IsOccupied);
        Assert.False(accountAList.Slots[2].IsOccupied);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accountB.AccessToken);
        var accountBList = await client.GetFromJsonAsync<SlotListResponse>("/v1/saves");
        Assert.NotNull(accountBList);
        Assert.Equal(3, accountBList.Slots.Count);
        Assert.All(accountBList.Slots, slot => Assert.False(slot.IsOccupied));
    }

    [Fact]
    public async Task First_fill_uses_expected_revision_zero_and_matching_update_bumps_revision()
    {
        await using var factory = await SlotSyncApiFactory.StartAsync();
        var client = factory.CreateClient();
        var account = await factory.RegisterVerifiedAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var firstRequest = SaveRequest.Valid("First Tower", "Desktop");
        var first = await client.PutAsJsonAsync("/v1/saves/2", firstRequest);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<RevisionResponse>();
        Assert.Equal(1, firstBody?.Revision);

        var download = await client.GetFromJsonAsync<SlotDownloadResponse>("/v1/saves/2");
        Assert.NotNull(download);
        Assert.Equal(1, download.Revision);
        Assert.Equal(firstRequest.PayloadBase64, download.PayloadBase64);
        Assert.Equal(firstRequest.Checksum, download.Checksum);

        var secondRequest = SaveRequest.Valid("Renovated Tower", "Desktop", expectedRevision: 1);
        var second = await client.PutAsJsonAsync("/v1/saves/2", secondRequest);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<RevisionResponse>();
        Assert.Equal(2, secondBody?.Revision);
    }

    [Fact]
    public async Task Revision_mismatch_returns_conflict_metadata_without_overwriting()
    {
        await using var factory = await SlotSyncApiFactory.StartAsync();
        var client = factory.CreateClient();
        var account = await factory.RegisterVerifiedAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var first = await client.PutAsJsonAsync("/v1/saves/1", SaveRequest.Valid("Original Tower", "Office PC", playMinutes: 17));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await client.PutAsJsonAsync("/v1/saves/1", SaveRequest.Valid("Stale Tower", "Steam Deck", expectedRevision: 0));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var conflict = await stale.Content.ReadFromJsonAsync<ConflictResponse>();
        Assert.NotNull(conflict);
        Assert.Equal("conflict", conflict.Code);
        Assert.Equal(1, conflict.CurrentRevision);
        Assert.Equal("Original Tower", conflict.TowerName);
        Assert.Equal("Office PC", conflict.DeviceName);
        Assert.Equal(17, conflict.PlayMinutes);
        Assert.NotNull(conflict.ModifiedUtc);

        var download = await client.GetFromJsonAsync<SlotDownloadResponse>("/v1/saves/1");
        Assert.Equal("Original Tower", download?.TowerName);
    }

    [Fact]
    public async Task Rejects_checksum_mismatch_and_payload_size_limits()
    {
        await using var factory = await SlotSyncApiFactory.StartAsync();
        var client = factory.CreateClient();
        var account = await factory.RegisterVerifiedAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        var invalidChecksum = SaveRequest.Valid("Checksum Tower", "Laptop") with
        {
            Checksum = new string('0', 64)
        };
        var checksum = await client.PutAsJsonAsync("/v1/saves/1", invalidChecksum);
        Assert.Equal(HttpStatusCode.BadRequest, checksum.StatusCode);
        Assert.Equal("checksum_mismatch", (await checksum.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);

        var tooLargeCompressed = SaveRequest.FromPayload(new byte[10 * 1024 * 1024 + 1], compress: false);
        var compressed = await client.PutAsJsonAsync("/v1/saves/1", tooLargeCompressed);
        Assert.Equal(HttpStatusCode.BadRequest, compressed.StatusCode);
        Assert.Equal("payload_too_large", (await compressed.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);

        var tooLargeDecompressed = SaveRequest.FromPayload(new byte[50 * 1024 * 1024 + 1], compress: true);
        var decompressed = await client.PutAsJsonAsync("/v1/saves/1", tooLargeDecompressed);
        Assert.Equal(HttpStatusCode.BadRequest, decompressed.StatusCode);
        Assert.Equal("payload_too_large", (await decompressed.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }

    [Fact]
    public async Task Cross_account_get_and_put_return_not_found_without_leaking_slot_metadata()
    {
        await using var factory = await SlotSyncApiFactory.StartAsync();
        var client = factory.CreateClient();
        var owner = await factory.RegisterVerifiedAndLogin(client);
        var other = await factory.RegisterVerifiedAndLogin(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        var upload = await client.PutAsJsonAsync("/v1/saves/3", SaveRequest.Valid("Private Tower", "Owner Device"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
        var get = await client.GetAsync("/v1/saves/3");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var put = await client.PutAsJsonAsync("/v1/saves/3", SaveRequest.Valid("Intrusion", "Other Device", expectedRevision: 1));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record ErrorResponse(string Code);
    private sealed record RevisionResponse(long Revision);
    private sealed record SlotListResponse(IReadOnlyList<SlotSummaryResponse> Slots);

    private sealed record SlotSummaryResponse(
        int SlotId,
        bool IsOccupied,
        long Revision,
        string? TowerName,
        string? ModifiedUtc,
        string? DeviceName,
        int? PlayMinutes);

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

    private sealed record ConflictResponse(
        string Code,
        long CurrentRevision,
        string? TowerName,
        string? ModifiedUtc,
        string? DeviceName,
        int? PlayMinutes);

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
            int playMinutes = 5)
        {
            var json = Encoding.UTF8.GetBytes($$"""{"towerName":"{{towerName}}","day":12,"stars":4}""");
            var compressed = Compress(json);
            return FromCompressed(compressed, towerName, deviceName, expectedRevision, playMinutes);
        }

        public static SaveRequest FromPayload(byte[] payload, bool compress)
        {
            var bytes = compress ? Compress(payload) : payload;
            return FromCompressed(bytes, "Payload Tower", "Payload Device", expectedRevision: 0, playMinutes: 1);
        }

        private static SaveRequest FromCompressed(
            byte[] compressed,
            string towerName,
            string deviceName,
            long expectedRevision,
            int playMinutes)
        {
            return new SaveRequest(
                expectedRevision,
                towerName,
                1,
                Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant(),
                Convert.ToBase64String(compressed),
                deviceName,
                playMinutes,
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

    private sealed class SlotSyncApiFactory : WebApplicationFactory<Program>
    {
        private const string SigningKey = "slot-sync-tests-signing-key-32-bytes-minimum";
        private readonly PostgreSqlContainer? _postgres;
        private readonly CapturingEmailSender _emailSender = new();
        private readonly string _databaseName = "cloudsave-slot-sync-tests-" + Guid.NewGuid().ToString("N");

        private SlotSyncApiFactory(PostgreSqlContainer? postgres)
        {
            _postgres = postgres;
        }

        public static async Task<SlotSyncApiFactory> StartAsync()
        {
            try
            {
                var postgres = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();
                await postgres.StartAsync();
                return new SlotSyncApiFactory(postgres);
            }
            catch (Exception)
            {
                return new SlotSyncApiFactory(postgres: null);
            }
        }

        public async Task<AuthTokens> RegisterVerifiedAndLogin(HttpClient client)
        {
            var email = $"slot-sync-{Guid.NewGuid():N}@example.com";
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
            return tokens;
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
