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
using Npgsql;
using Testcontainers.PostgreSql;

namespace CloudSave.Api.Tests;

public class EmailGateTests
{
    [Fact]
    public async Task Unverified_user_cannot_put_save_until_email_is_verified()
    {
        await using var factory = await EmailGateApiFactory.StartAsync();
        var client = factory.CreateClient();
        var (email, password) = UniqueCredentials();

        await Register(client, email, password);
        var tokens = await Login(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var save = SaveRequest.Valid();

        var beforeVerify = await client.PutAsJsonAsync("/v1/saves/1", save);

        Assert.Equal(HttpStatusCode.Forbidden, beforeVerify.StatusCode);
        var beforeBody = await beforeVerify.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("email_unverified", beforeBody?.Code);

        var verifyToken = ExtractToken(await factory.SingleEmailToAsync(email, "verify"));
        await factory.AssertEmailTokensAreHashed("verify", verifyToken);

        var verify = await client.PostAsJsonAsync("/v1/auth/verify", new
        {
            email,
            token = verifyToken
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var afterVerify = await client.PutAsJsonAsync("/v1/saves/1", save);

        Assert.Equal(HttpStatusCode.OK, afterVerify.StatusCode);
    }

    [Fact]
    public async Task Forgot_always_returns_ok_and_sends_reset_only_for_existing_accounts()
    {
        await using var factory = await EmailGateApiFactory.StartAsync();
        var client = factory.CreateClient();
        var (email, password) = UniqueCredentials();
        await Register(client, email, password);
        factory.ClearEmails();

        var missing = await client.PostAsJsonAsync("/v1/auth/forgot", new
        {
            email = "missing-" + email
        });
        var existing = await client.PostAsJsonAsync("/v1/auth/forgot", new
        {
            email
        });

        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Empty(await factory.EmailsToAsync("missing-" + email));

        var resetEmail = await factory.SingleEmailToAsync(email, "reset");
        var resetToken = ExtractToken(resetEmail);
        await factory.AssertEmailTokensAreHashed("reset", resetToken);
    }

    [Fact]
    public async Task Reset_with_emailed_token_changes_password_without_exposing_account_lookup()
    {
        await using var factory = await EmailGateApiFactory.StartAsync();
        var client = factory.CreateClient();
        var (email, oldPassword) = UniqueCredentials();
        const string newPassword = "ChangedHorseBatteryStaple!42";
        await Register(client, email, oldPassword);
        factory.ClearEmails();

        var forgot = await client.PostAsJsonAsync("/v1/auth/forgot", new { email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var resetToken = ExtractToken(await factory.SingleEmailToAsync(email, "reset"));

        var reset = await client.PostAsJsonAsync("/v1/auth/reset", new
        {
            email,
            token = resetToken,
            newPassword
        });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            email,
            password = oldPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            email,
            password = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    private static async Task Register(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<AuthTokens> Login(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private static (string Email, string Password) UniqueCredentials()
    {
        return ($"email-gate-{Guid.NewGuid():N}@example.com", "CorrectHorseBatteryStaple!42");
    }

    private static string ExtractToken(SentEmail email)
    {
        var match = Regex.Match(email.Body, @"token:\s*(?<token>[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Email body did not contain a token: {email.Body}");
        return match.Groups["token"].Value;
    }

    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record ErrorResponse(string Code);

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
        public static SaveRequest Valid()
        {
            var payload = Encoding.UTF8.GetBytes("""{"towerName":"Verified Tower"}""");
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(payload, 0, payload.Length);
            }

            var compressed = output.ToArray();
            return new SaveRequest(
                0,
                "Verified Tower",
                1,
                Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant(),
                Convert.ToBase64String(compressed),
                "Test Device",
                3,
                "0.1-test",
                Guid.NewGuid().ToString("N"));
        }
    }

    public sealed record SentEmail(string To, string Subject, string Body);

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

        public void Clear()
        {
            lock (_emails)
                _emails.Clear();
        }
    }

    private sealed class EmailGateApiFactory : WebApplicationFactory<Program>
    {
        private const string SigningKey = "email-gate-tests-signing-key-32-bytes-minimum";
        private readonly PostgreSqlContainer? _postgres;
        private readonly CapturingEmailSender _emailSender = new();
        private readonly string _databaseName = "cloudsave-email-gate-tests-" + Guid.NewGuid().ToString("N");

        private EmailGateApiFactory(PostgreSqlContainer? postgres)
        {
            _postgres = postgres;
        }

        public static async Task<EmailGateApiFactory> StartAsync()
        {
            try
            {
                var postgres = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();
                await postgres.StartAsync();
                return new EmailGateApiFactory(postgres);
            }
            catch (Exception)
            {
                return new EmailGateApiFactory(postgres: null);
            }
        }

        public async Task<SentEmail> SingleEmailToAsync(string email, string subjectFragment)
        {
            await Task.Yield();
            return Assert.Single(_emailSender.Emails.Where(sent =>
                sent.To == email &&
                sent.Subject.Contains(subjectFragment, StringComparison.OrdinalIgnoreCase)));
        }

        public async Task<IReadOnlyList<SentEmail>> EmailsToAsync(string email)
        {
            await Task.Yield();
            return _emailSender.Emails.Where(sent => sent.To == email).ToList();
        }

        public void ClearEmails()
        {
            _emailSender.Clear();
        }

        public async Task AssertEmailTokensAreHashed(string purpose, string rawToken)
        {
            var now = DateTimeOffset.UtcNow;
            var hashes = new List<string>();
            var expirations = new List<DateTimeOffset>();

            if (_postgres is not null)
            {
                await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    """SELECT "TokenHash", "ExpiresUtc" FROM "EmailTokens" WHERE "Purpose" = @purpose""",
                    connection);
                command.Parameters.AddWithValue("purpose", purpose);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    hashes.Add(reader.GetString(0));
                    expirations.Add(reader.GetFieldValue<DateTimeOffset>(1));
                }
            }
            else
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var tokens = await db.EmailTokens
                    .Where(token => token.Purpose == purpose)
                    .Select(token => new { token.TokenHash, token.ExpiresUtc })
                    .ToListAsync();
                hashes.AddRange(tokens.Select(token => token.TokenHash));
                expirations.AddRange(tokens.Select(token => token.ExpiresUtc));
            }

            Assert.NotEmpty(hashes);
            foreach (var hash in hashes)
            {
                Assert.Matches("^[a-f0-9]{64}$", hash);
                Assert.NotEqual(rawToken, hash);
            }

            foreach (var expiresUtc in expirations)
            {
                var ttl = expiresUtc - now;
                Assert.InRange(ttl, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(20));
            }
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
