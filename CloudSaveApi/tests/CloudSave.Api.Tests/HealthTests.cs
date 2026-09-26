using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CloudSave.Api.Tests;

public sealed class CloudSaveApiFactory : WebApplicationFactory<Program>
{
}

public class HealthTests
{
    [Fact]
    public async Task Health_returns_ok()
    {
        await using var factory = new CloudSaveApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }
}
