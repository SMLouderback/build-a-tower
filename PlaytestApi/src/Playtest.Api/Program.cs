using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Playtest.Api;
using Playtest.Api.Email;

if (args.Contains("--set-key", StringComparer.Ordinal))
{
    var host = Host.CreateApplicationBuilder(args);
    host.Services.Configure<PlaytestOptions>(host.Configuration.GetSection("Playtest"));
    host.Services.AddSingleton<FamilyKeyStore>();
    using var appHost = host.Build();
    var key = Console.In.ReadToEnd().Trim();
    if (string.IsNullOrEmpty(key))
    {
        Console.Error.WriteLine("empty_key");
        return 1;
    }

    appHost.Services.GetRequiredService<FamilyKeyStore>().SetPlaintext(key);
    Console.WriteLine("family_key_updated");
    return 0;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<PlaytestOptions>(builder.Configuration.GetSection("Playtest"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PlaytestOptions>>().Value);
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton<FamilyKeyStore>();
builder.Services.AddSingleton<VersionDocument>();
builder.Services.AddSingleton<KeyRequestLog>();
builder.Services.AddSingleton<IEmailSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
    return string.IsNullOrWhiteSpace(smtp.Host)
        ? new NullEmailSender()
        : new SmtpEmailSender(sp.GetRequiredService<IOptions<SmtpOptions>>());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "rate_limited" }, cancellationToken);
    };
    options.AddPolicy("playtest-forms", httpContext =>
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(remoteIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();
app.UseRateLimiter();

app.MapGet("/playtest/health", () => Results.Json(new { status = "ok" }));

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
}).RequireRateLimiting("playtest-forms");

app.MapPost("/playtest/request-key", async (RequestKeyBody body, IEmailSender mail, KeyRequestLog requests, PlaytestOptions options, CancellationToken cancellationToken) =>
{
    if (!EmailValidation.TryNormalize(body.Email, out var email))
        return Results.Json(new { error = "invalid_email" }, statusCode: 400);
    requests.Append(email);
    try
    {
        await mail.SendAsync(
            options.OperatorEmail,
            "Build-A-Tower key request",
            $"Requester: {email}{Environment.NewLine}At: {DateTimeOffset.UtcNow:O}{Environment.NewLine}",
            cancellationToken);
    }
    catch (InvalidOperationException)
    {
        return Results.Json(new { error = "mail_unavailable" }, statusCode: 503);
    }

    return Results.NoContent();
}).RequireRateLimiting("playtest-forms");

app.MapPost("/playtest/feedback", async (FeedbackBody body, IEmailSender mail, PlaytestOptions options, CancellationToken cancellationToken) =>
{
    var message = (body.Message ?? "").Trim();
    if (message.Length == 0)
        return Results.Json(new { error = "empty_message" }, statusCode: 400);
    try
    {
        await mail.SendAsync(
            options.OperatorEmail,
            $"Build-A-Tower feedback (version {body.Version ?? "unknown"})",
            $"Version: {body.Version}{Environment.NewLine}Name: {body.Name}{Environment.NewLine}Email: {body.Email}{Environment.NewLine}{Environment.NewLine}{message}{Environment.NewLine}",
            cancellationToken);
    }
    catch (InvalidOperationException)
    {
        return Results.Json(new { error = "mail_unavailable" }, statusCode: 503);
    }

    return Results.NoContent();
}).RequireRateLimiting("playtest-forms");

app.Run();
return 0;

public sealed record DownloadRequest(string? Key);
public sealed record RequestKeyBody(string? Email);
public sealed record FeedbackBody(string? Message, string? Name, string? Email, string? Version);

public partial class Program { }
