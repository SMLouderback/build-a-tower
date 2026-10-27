using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Playtest.Api;

namespace Playtest.Api.Tests;

public sealed class DownloadTests : IAsyncLifetime
{
    WebApplicationFactory<Program> _factory = null!;
    HttpClient _client = null!;
    string _dataDir = null!;

    public Task InitializeAsync()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "playtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);
        File.WriteAllBytes(Path.Combine(_dataDir, "Build-A-Tower.zip"), new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA });
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Playtest:DataDirectory", _dataDir);
            builder.UseSetting("Playtest:OperatorEmail", "escapemobileproductions@gmail.com");
        });
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FamilyKeyStore>().SetPlaintext("family-test-key");
        scope.ServiceProvider.GetRequiredService<VersionDocument>().Write("0.1.0", new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero));
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
    public async Task Version_is_public_and_has_no_key()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/playtest/version.json");
        Assert.Equal("0.1.0", json.GetProperty("version").GetString());
        Assert.Equal("https://escapeproductions.biz/#download", json.GetProperty("downloadPage").GetString());
        Assert.False(json.TryGetProperty("key", out _));
    }

    [Fact]
    public async Task Download_wrong_key_is_generic_403()
    {
        var response = await _client.PostAsJsonAsync("/playtest/download", new { key = "nope" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_key", body);
        Assert.DoesNotContain("family-test-key", body);
    }

    [Fact]
    public async Task Download_correct_key_streams_zip()
    {
        var response = await _client.PostAsJsonAsync("/playtest/download", new { key = "family-test-key" });
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA }, bytes);
    }
}
