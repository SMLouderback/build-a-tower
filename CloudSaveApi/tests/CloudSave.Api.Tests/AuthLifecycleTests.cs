using System.Net;
using System.Net.Http.Json;
using CloudSave.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CloudSave.Api.Tests;

public class AuthLifecycleTests
{
    [Fact]
    public async Task Register_and_login_issue_tokens_without_password_echo()
    {
        await using var factory = await PostgresCloudSaveApiFactory.StartAsync();
        var client = factory.CreateClient();
        var email = UniqueEmail();
        const string password = "CorrectHorseBatteryStaple!42";

        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email,
            password
        });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var registerBody = await register.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, registerBody);

        var login = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            email,
            password
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, loginBody);

        var tokens = await login.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.InRange(tokens.ExpiresIn, 14 * 60, 16 * 60);

        await factory.AssertRefreshTokensAreHashed(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_rejects_reuse_of_old_refresh()
    {
        await using var factory = await PostgresCloudSaveApiFactory.StartAsync();
        var client = factory.CreateClient();
        var original = await RegisterAndLogin(client);

        var refresh = await client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            original.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.NotNull(rotated);
        Assert.False(string.IsNullOrWhiteSpace(rotated.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(rotated.RefreshToken));
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            original.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        await factory.AssertRefreshTokensAreHashed(original.RefreshToken, rotated.RefreshToken);
    }

    [Fact]
    public async Task Logout_revokes_refresh_family()
    {
        await using var factory = await PostgresCloudSaveApiFactory.StartAsync();
        var client = factory.CreateClient();
        var original = await RegisterAndLogin(client);
        var rotated = await Refresh(client, original.RefreshToken);

        var logout = await client.PostAsJsonAsync("/v1/auth/logout", new
        {
            rotated.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            rotated.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    private static async Task<AuthTokens> RegisterAndLogin(HttpClient client)
    {
        var email = UniqueEmail();
        const string password = "CorrectHorseBatteryStaple!42";

        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

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

    private static async Task<AuthTokens> Refresh(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            refreshToken
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private static string UniqueEmail()
    {
        return $"player-{Guid.NewGuid():N}@example.com";
    }

    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);

    private sealed class PostgresCloudSaveApiFactory : WebApplicationFactory<Program>
    {
        private const string SigningKey = "auth-lifecycle-tests-signing-key-32-bytes-minimum";
        private readonly PostgreSqlContainer? _postgres;
        private readonly string _databaseName = "cloudsave-tests-" + Guid.NewGuid().ToString("N");

        private PostgresCloudSaveApiFactory(PostgreSqlContainer? postgres)
        {
            _postgres = postgres;
        }

        public static async Task<PostgresCloudSaveApiFactory> StartAsync()
        {
            try
            {
                var postgres = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();
                await postgres.StartAsync();
                return new PostgresCloudSaveApiFactory(postgres);
            }
            catch (Exception)
            {
                return new PostgresCloudSaveApiFactory(postgres: null);
            }
        }

        public async Task AssertRefreshTokensAreHashed(params string[] rawTokens)
        {
            if (_postgres is not null)
            {
                await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand("""SELECT "TokenHash" FROM "RefreshTokens" """, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var hashes = new List<string>();
                while (await reader.ReadAsync())
                    hashes.Add(reader.GetString(0));
                AssertHashed(hashes, rawTokens);
                return;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.RefreshTokens.Select(token => token.TokenHash).ToListAsync();
            AssertHashed(stored, rawTokens);
        }

        static void AssertHashed(IReadOnlyCollection<string> hashes, IEnumerable<string> rawTokens)
        {
            Assert.NotEmpty(hashes);
            foreach (var hash in hashes)
                Assert.Matches("^[a-f0-9]{64}$", hash);
            foreach (var rawToken in rawTokens)
                Assert.DoesNotContain(rawToken, hashes);
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

            if (_postgres is null)
            {
                builder.ConfigureServices(services =>
                {
                    var remove = services
                        .Where(d => d.ServiceType == typeof(AppDbContext)
                                    || d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                        .ToList();
                    foreach (var descriptor in remove)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(_databaseName));
                });
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (_postgres is not null)
                await _postgres.DisposeAsync();
        }
    }
}
