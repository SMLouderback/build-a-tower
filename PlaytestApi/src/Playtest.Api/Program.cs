using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trusted reverse proxy sits in front (NPM / escape-web).
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddSingleton<FamilyKeyStore>();
builder.Services.AddSingleton<VersionDocument>();
builder.Services.AddSingleton<KeyRequestLog>();
builder.Services.AddSingleton<PendingKeyRequests>();
builder.Services.AddSingleton<IEmailSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
    return string.IsNullOrWhiteSpace(smtp.Host)
        ? new NullEmailSender()
        : new SmtpEmailSender(sp.GetRequiredService<IOptions<SmtpOptions>>());
});
var relaxLimits = builder.Environment.IsEnvironment("Testing");
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
            PermitLimit = relaxLimits ? 1_000 : 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("key-request", httpContext =>
    {
        // Prefer forwarded client IP (NPM → escape-web); fall back to socket peer.
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter("key:" + remoteIp, _ => new FixedWindowRateLimiterOptions
        {
            // Generous enough for typos/retries; bots still gated by confirm + 24h pending dedupe.
            PermitLimit = relaxLimits ? 1_000 : 8,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();
app.UseForwardedHeaders();
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

app.MapPost("/playtest/request-key", async (
    RequestKeyBody body,
    HttpContext http,
    IEmailSender mail,
    KeyRequestLog requests,
    PendingKeyRequests pendingStore,
    PlaytestOptions options,
    CancellationToken cancellationToken) =>
{
    // Honeypot: bots that fill hidden "website" get a fake success and no mail.
    if (!string.IsNullOrWhiteSpace(body.Website))
        return Results.NoContent();

    if (!EmailValidation.TryNormalizeAndCanonicalize(body.Email, out var email, out var canonical))
        return Results.Json(new { error = "invalid_email" }, statusCode: 400);

    var name = (body.Name ?? "").Trim();
    if (name.Length < 2 || name.Length > 80)
        return Results.Json(new { error = "invalid_name" }, statusCode: 400);

    var note = (body.Note ?? "").Trim();
    if (note.Length > 500)
        return Results.Json(new { error = "note_too_long" }, statusCode: 400);

    pendingStore.PruneExpired();
    if (pendingStore.HasRecentPending(canonical, TimeSpan.FromHours(24)))
        return Results.Json(new { error = "already_pending" }, statusCode: 409);

    var remoteIp = http.Connection.RemoteIpAddress?.ToString();
    var pending = pendingStore.Create(email, canonical, name, note, remoteIp);

    var confirmUrl = $"{options.PublicBaseUrl.TrimEnd('/')}/playtest/confirm-key-request?token={pending.Token}";
    var confirmBody =
        $"Hi {name},{Environment.NewLine}{Environment.NewLine}" +
        $"Confirm this Build-A-Tower playtest key request by opening the link below within {options.ConfirmTokenHours} hours:{Environment.NewLine}{Environment.NewLine}" +
        $"{confirmUrl}{Environment.NewLine}{Environment.NewLine}" +
        $"If you did not request a key, ignore this email.{Environment.NewLine}";

    try
    {
        await mail.SendAsync(
            email,
            "Confirm your Build-A-Tower key request",
            confirmBody,
            cancellationToken);
    }
    catch (InvalidOperationException)
    {
        pendingStore.Discard(pending.Token);
        return Results.Json(new { error = "mail_unavailable" }, statusCode: 503);
    }

    requests.AppendPending(email, canonical, name);
    return Results.Json(new { status = "confirm_email_sent" });
}).RequireRateLimiting("key-request");

app.MapGet("/playtest/confirm-key-request", async (
    string? token,
    IEmailSender mail,
    KeyRequestLog requests,
    PendingKeyRequests pendingStore,
    PlaytestOptions options,
    CancellationToken cancellationToken) =>
{
    var pending = pendingStore.Take(token ?? "");
    if (pending == null)
    {
        return Results.Content(
            ConfirmHtml(
                "Link expired or already used",
                "Request a key again from the download page if you still need access.",
                options.DownloadPage),
            "text/html");
    }

    requests.AppendConfirmed(pending);
    var operatorBody =
        $"Confirmed key request{Environment.NewLine}" +
        $"Name: {pending.Name}{Environment.NewLine}" +
        $"Requester: {pending.Email}{Environment.NewLine}" +
        $"Canonical: {pending.CanonicalEmail}{Environment.NewLine}" +
        $"Note: {(string.IsNullOrEmpty(pending.Note) ? "(none)" : pending.Note)}{Environment.NewLine}" +
        $"IP: {pending.RemoteIp}{Environment.NewLine}" +
        $"At: {DateTimeOffset.UtcNow:O}{Environment.NewLine}";

    try
    {
        await mail.SendAsync(
            options.OperatorEmail,
            "Build-A-Tower key request (confirmed)",
            operatorBody,
            cancellationToken);
    }
    catch (InvalidOperationException)
    {
        return Results.Content(
            ConfirmHtml(
                "Confirmed, but studio mail failed",
                "Your email is verified. If you do not hear back, reply to the studio contact address on the site.",
                options.DownloadPage),
            "text/html");
    }

    return Results.Content(
        ConfirmHtml(
            "Request confirmed",
            "Thanks — your email is verified. If a key is available, we will send it from the studio inbox.",
            options.DownloadPage),
        "text/html");
});

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

static string ConfirmHtml(string title, string body, string downloadPage) =>
    "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"/>" +
    "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>" +
    $"<title>{System.Net.WebUtility.HtmlEncode(title)}</title>" +
    "<style>body{font-family:system-ui,sans-serif;max-width:36rem;margin:3rem auto;padding:0 1rem;line-height:1.5;color:#122}" +
    "a{color:#0b5}h1{font-size:1.4rem}</style></head><body>" +
    $"<h1>{System.Net.WebUtility.HtmlEncode(title)}</h1>" +
    $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p>" +
    $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(downloadPage)}\">Back to download</a></p>" +
    "</body></html>";

public sealed record DownloadRequest(string? Key);
public sealed record RequestKeyBody(string? Email, string? Name, string? Note, string? Website);
public sealed record FeedbackBody(string? Message, string? Name, string? Email, string? Version);

public partial class Program { }
