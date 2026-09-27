using Playtest.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<PlaytestOptions>(builder.Configuration.GetSection("Playtest"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaytestOptions>>().Value);
builder.Services.AddSingleton<FamilyKeyStore>();
builder.Services.AddSingleton<VersionDocument>();

var app = builder.Build();

app.MapGet("/playtest/version.json", (VersionDocument versions) =>
    versions.TryRead(out var doc) ? Results.Json(doc) : Results.NotFound());

app.MapPost("/playtest/download", (DownloadRequest body, FamilyKeyStore keys, PlaytestOptions options) =>
{
    if (!keys.Verify(body.Key ?? ""))
        return Results.Json(new { error = "invalid_key" }, statusCode: 403);
    var path = Path.Combine(options.DataDirectory, options.ZipFileName);
    if (!File.Exists(path))
        return Results.Json(new { error = "zip_missing" }, statusCode: 503);
    return Results.File(path, "application/zip", options.ZipFileName);
});

app.Run();

public sealed record DownloadRequest(string? Key);

public partial class Program { }
