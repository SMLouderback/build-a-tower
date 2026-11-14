using System.Net;
using System.Net.Http.Json;
using CloudSave.Api.Data;
using CloudSave.Api.Invites;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CloudSave.Api.Tests;

public class InviteTests
{
    [Fact]
    public async Task Register_without_invite_is_rejected_when_public_registration_is_disabled()
    {
        await using var factory = await InviteApiFactory.StartAsync(publicRegistration: false);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "CorrectHorseBatteryStaple!42"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("invite_required", body?.Code);
    }

    [Fact]
    public async Task Register_with_valid_invite_consumes_one_use()
    {
        await using var factory = await InviteApiFactory.StartAsync(publicRegistration: false);
        var client = factory.CreateClient();
        var inviteCode = await factory.MintInviteAsync(uses: 1);

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "CorrectHorseBatteryStaple!42",
            inviteCode
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await factory.GetRemainingUsesAsync(inviteCode));
        Assert.Equal(1, await factory.CountUsersAsync());
    }

    [Fact]
    public async Task Register_rejects_exhausted_single_use_invite()
    {
        await using var factory = await InviteApiFactory.StartAsync(publicRegistration: false);
        var client = factory.CreateClient();
        var inviteCode = await factory.MintInviteAsync(uses: 1);

        var first = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "CorrectHorseBatteryStaple!42",
            inviteCode
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "CorrectHorseBatteryStaple!42",
            inviteCode
        });

        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("invite_invalid", body?.Code);
        Assert.Equal(1, await factory.CountUsersAsync());
    }

    [Fact]
    public async Task Public_registration_allows_register_without_invite()
    {
        await using var factory = await InviteApiFactory.StartAsync(publicRegistration: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = "CorrectHorseBatteryStaple!42"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Mint_invite_cli_prints_one_plaintext_code_and_stores_only_hash()
    {
        await using var factory = await InviteApiFactory.StartAsync(publicRegistration: false);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await InviteCli.RunMintInviteAsync(
            factory.Services,
            ["--mint-invite", "--uses", "1"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());

        var lines = output.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var plaintextCode = Assert.Single(lines);
        Assert.False(string.IsNullOrWhiteSpace(plaintextCode));
        Assert.Equal(1, await factory.GetRemainingUsesAsync(plaintextCode));

        var hashes = await factory.GetInviteHashesAsync();
        var storedHash = Assert.Single(hashes);
        Assert.Equal(InviteService.HashCode(plaintextCode), storedHash);
        Assert.NotEqual(plaintextCode, storedHash);
    }

    private static string UniqueEmail()
    {
        return $"invite-player-{Guid.NewGuid():N}@example.com";
    }

    private sealed record ErrorResponse(string Code);

    private sealed class InviteApiFactory : WebApplicationFactory<Program>
    {
        private const string SigningKey = "invite-tests-signing-key-32-bytes-minimum";
        private readonly PostgreSqlContainer? _postgres;
        private readonly bool _publicRegistration;
        private readonly string _databaseName = "cloudsave-invite-tests-" + Guid.NewGuid().ToString("N");

        private InviteApiFactory(PostgreSqlContainer? postgres, bool publicRegistration)
        {
            _postgres = postgres;
            _publicRegistration = publicRegistration;
        }

        public static async Task<InviteApiFactory> StartAsync(bool publicRegistration)
        {
            try
            {
                var postgres = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();
                await postgres.StartAsync();
                return new InviteApiFactory(postgres, publicRegistration);
            }
            catch (Exception)
            {
                return new InviteApiFactory(postgres: null, publicRegistration);
            }
        }

        public async Task<string> MintInviteAsync(int uses)
        {
            using var scope = Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<InviteService>();
            return await service.MintAsync(uses, expiresUtc: null, CancellationToken.None);
        }

        public async Task<int> GetRemainingUsesAsync(string rawCode)
        {
            if (_postgres is not null)
            {
                await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    """SELECT "RemainingUses" FROM "InviteCodes" WHERE "CodeHash" = @codeHash""",
                    connection);
                command.Parameters.AddWithValue("codeHash", InviteService.HashCode(rawCode));
                var result = await command.ExecuteScalarAsync();
                Assert.NotNull(result);
                return Convert.ToInt32(result);
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var codeHash = InviteService.HashCode(rawCode);
            var invite = await db.InviteCodes.SingleAsync(code => code.CodeHash == codeHash);
            return invite.RemainingUses;
        }

        public async Task<IReadOnlyList<string>> GetInviteHashesAsync()
        {
            if (_postgres is not null)
            {
                await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand("""SELECT "CodeHash" FROM "InviteCodes" """, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var hashes = new List<string>();
                while (await reader.ReadAsync())
                    hashes.Add(reader.GetString(0));
                return hashes;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.InviteCodes.Select(code => code.CodeHash).ToListAsync();
        }

        public async Task<int> CountUsersAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.Users.CountAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["PublicRegistration"] = _publicRegistration.ToString(),
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
