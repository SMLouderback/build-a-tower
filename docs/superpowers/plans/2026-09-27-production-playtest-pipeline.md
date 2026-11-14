# Production Playtest Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a keyed Windows zip of Build-A-Tower from `escapeproductions.biz` so friends and family play a frozen production build, get an in-game update notice, and can send Feedback — while Git/Unity work stays unpublished until a deliberate PC publish.

**Architecture:** A small ASP.NET 8 sidecar on `edge-proxy` (not the cloud-save API) serves a public version document, streams the zip after a constant-time family-key check, and emails Request-a-Key / Feedback to `escapemobileproductions@gmail.com`. The marketing site exposes Download + Request a Key. The Unity client fetches the version file and POSTs Feedback. You File → Build Windows 64-bit, then run `PlaytestApi/deploy/publish.ps1` over SSH to replace zip + version on disk.

**Tech Stack:** ASP.NET 8 minimal API + xUnit, existing static site (`homelab/escapeproductions-site`), Unity 6000.4.7f1 EditMode NUnit, PowerShell publish to `ubuntu@192.168.0.35`.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-27-production-playtest-pipeline-design.md`
- Windows 64-bit standalone zip only; no Steam/itch/macOS/WebGL/CI Unity builds
- Family key: one shared secret, hash on disk, required to download, never required to play
- Site does not auto-email the key; Request-a-Key is waitlist only
- Version document is public; zip is only streamed after a successful key POST
- Game never downloads the zip and never asks for the key
- Account/cloud save unchanged; sidecar must not call `api.escapeproductions.biz`
- Operator mail: `escapemobileproductions@gmail.com`
- Public version URL: `https://escapeproductions.biz/playtest/version.json`
- Download POST: `https://escapeproductions.biz/playtest/download` JSON `{"key":"..."}` → `application/zip`
- Request-a-Key POST: `https://escapeproductions.biz/playtest/request-key` JSON `{"email":"..."}`
- Feedback POST: `https://escapeproductions.biz/playtest/feedback` JSON `{"message":"...","name":"...","email":"...","version":"..."}`
- Wrong/empty key → `403` JSON `{"error":"invalid_key"}` (no extra hint)
- Semver `MAJOR.MINOR.PATCH` in Unity `Application.version` and version.json `version`
- Rate limit Request-a-Key, Feedback, and download: 5 / IP / minute → `429` `{"error":"rate_limited"}`
- SMTP missing: version + download still work; mail endpoints fail closed
- Commit in the Build-A-Tower repo for PlaytestApi + Unity; site files live at `../homelab/escapeproductions-site` from that repo (Escape workspace). Include site files in the same commit if that path is inside the BAT git root; otherwise commit site files in the Escape parent repo and note it in the task commit message.

## File map

| Path | Role |
|------|------|
| `PlaytestApi/src/Playtest.Api/Program.cs` | Endpoints, rate limiter, mail wiring |
| `PlaytestApi/src/Playtest.Api/PlaytestOptions.cs` | Data dir, operator to-address, zip filename |
| `PlaytestApi/src/Playtest.Api/FamilyKeyStore.cs` | Hash/verify/rotate family key |
| `PlaytestApi/src/Playtest.Api/VersionDocument.cs` | Read/write `version.json` |
| `PlaytestApi/src/Playtest.Api/Email/*` | SMTP + null sender |
| `PlaytestApi/tests/Playtest.Api.Tests/*` | WebApplicationFactory tests |
| `PlaytestApi/docker-compose.yml` + `Dockerfile` | edge-proxy container |
| `PlaytestApi/deploy/publish.ps1` | Zip player + scp to `/opt/playtest` |
| `PlaytestApi/deploy/set-key.sh` | Operator rotate key |
| `PlaytestApi/deploy/npm-playtest.md` | NPM `/playtest` → sidecar `:5080` |
| `../homelab/escapeproductions-site/index.html` + `main.js` + `styles.css` | Download + Request a Key UI |
| `Assets/Scripts/Playtest/PlaytestSemVer.cs` | Compare versions |
| `Assets/Scripts/Playtest/PlaytestConfig.cs` | Base URL default `https://escapeproductions.biz` |
| `Assets/Scripts/Playtest/PlaytestUpdateClient.cs` | GET version, decide notice |
| `Assets/Scripts/Playtest/PlaytestFeedbackClient.cs` | POST feedback |
| `Assets/Scripts/UI/MainMenu.uxml` + `MainMenuController.cs` | Feedback panel + update banner |
| `Assets/Scripts/UI/TowerHudController.cs` | Pause Feedback + update banner |
| `Assets/Tests/EditMode/Playtest*.cs` | Client + UI tests |

---

### Task 1: Sidecar version document + keyed zip download

**Files:**
- Create: `PlaytestApi/src/Playtest.Api/Playtest.Api.csproj`
- Create: `PlaytestApi/src/Playtest.Api/PlaytestOptions.cs`
- Create: `PlaytestApi/src/Playtest.Api/FamilyKeyStore.cs`
- Create: `PlaytestApi/src/Playtest.Api/VersionDocument.cs`
- Create: `PlaytestApi/src/Playtest.Api/Program.cs`
- Create: `PlaytestApi/src/Playtest.Api/appsettings.json`
- Create: `PlaytestApi/Playtest.Api.sln`
- Create: `PlaytestApi/tests/Playtest.Api.Tests/Playtest.Api.Tests.csproj`
- Create: `PlaytestApi/tests/Playtest.Api.Tests/DownloadTests.cs`
- Test: `dotnet test PlaytestApi/Playtest.Api.sln`

**Interfaces:**
- Consumes: none
- Produces: `GET /playtest/version.json` → `{ "version", "releasedAt", "downloadPage" }`; `POST /playtest/download` `{ "key" }` streams zip or `403 {"error":"invalid_key"}`; `FamilyKeyStore.SetPlaintext(string)` / `Verify(string)`; `VersionDocument.Write(string version, DateTimeOffset releasedAt)`

- [ ] **Step 1: Write the failing download tests**

Create `PlaytestApi/tests/Playtest.Api.Tests/Playtest.Api.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Playtest.Api\Playtest.Api.csproj" />
  </ItemGroup>
</Project>
```

Create `PlaytestApi/src/Playtest.Api/Playtest.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

Create `PlaytestApi/tests/Playtest.Api.Tests/DownloadTests.cs`:

```csharp
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
```

Add a solution that includes both projects (`dotnet new sln -n Playtest.Api -o PlaytestApi` then `dotnet sln add` both csprojs).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PlaytestApi/Playtest.Api.sln --filter DownloadTests`

Expected: FAIL (no `Program` / endpoints).

- [ ] **Step 3: Implement version + download**

`PlaytestOptions.cs`:

```csharp
namespace Playtest.Api;

public sealed class PlaytestOptions
{
    public string DataDirectory { get; set; } = "/data";
    public string ZipFileName { get; set; } = "Build-A-Tower.zip";
    public string OperatorEmail { get; set; } = "escapemobileproductions@gmail.com";
    public string DownloadPage { get; set; } = "https://escapeproductions.biz/#download";
}
```

`FamilyKeyStore.cs` — persist hash in `{DataDirectory}/family-key.hash`. Use `PasswordHasher<object>`. `SetPlaintext` overwrites. `Verify` uses `PasswordHasher.VerifyHashedPassword` and returns false when no hash or empty key. Compare/verify must not leak timing via early substring checks on the submitted key vs the plaintext (never store plaintext).

`VersionDocument.cs` — `{DataDirectory}/version.json` with camelCase `version`, `releasedAt` (ISO UTC), `downloadPage`. Missing file → GET returns `404`.

`Program.cs` — `WebApplication.CreateBuilder`; bind `Playtest`; singleton `FamilyKeyStore` + `VersionDocument`; map:

```csharp
app.MapGet("/playtest/version.json", (VersionDocument versions) =>
    versions.TryRead(out var doc) ? Results.Json(doc) : Results.NotFound());

app.MapPost("/playtest/download", async (DownloadRequest body, FamilyKeyStore keys, PlaytestOptions options) =>
{
    if (!keys.Verify(body.Key ?? ""))
        return Results.Json(new { error = "invalid_key" }, statusCode: 403);
    var path = Path.Combine(options.DataDirectory, options.ZipFileName);
    if (!File.Exists(path))
        return Results.Json(new { error = "zip_missing" }, statusCode: 503);
    return Results.File(path, "application/zip", options.ZipFileName);
});
```

Expose `partial class Program { }` for WAF. `appsettings.json`: `{ "Playtest": { "DataDirectory": "/data" } }`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PlaytestApi/Playtest.Api.sln --filter DownloadTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PlaytestApi
git commit -m "feat: serve playtest version and keyed zip download"
```

---

### Task 2: Request-a-Key, Feedback mail, rate limits

**Files:**
- Create: `PlaytestApi/src/Playtest.Api/Email/IEmailSender.cs`
- Create: `PlaytestApi/src/Playtest.Api/Email/SmtpEmailSender.cs`
- Create: `PlaytestApi/src/Playtest.Api/Email/NullEmailSender.cs`
- Create: `PlaytestApi/src/Playtest.Api/Email/RecordingEmailSender.cs` (tests only — put in test project)
- Modify: `PlaytestApi/src/Playtest.Api/Program.cs`
- Create: `PlaytestApi/tests/Playtest.Api.Tests/MailFormTests.cs`
- Test: `PlaytestApi/tests/Playtest.Api.Tests/MailFormTests.cs`

**Interfaces:**
- Consumes: `FamilyKeyStore`, `PlaytestOptions` from Task 1
- Produces: `POST /playtest/request-key` `{email}` → `204` and operator mail **without** the family key; invalid email → `400 {"error":"invalid_email"}`; `POST /playtest/feedback` `{message,name,email,version}` empty message → `400 {"error":"empty_message"}` else `204` + mail; rate limit policy `playtest-forms` 5/IP/min; `IEmailSender.SendAsync(string to, string subject, string body, CancellationToken)`

- [ ] **Step 1: Write failing mail tests**

```csharp
[Fact]
public async Task Request_key_invalid_email_does_not_mail()
{
    var response = await _client.PostAsJsonAsync("/playtest/request-key", new { email = "not-an-email" });
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Empty(_mailbox.Sent);
}

[Fact]
public async Task Request_key_valid_email_mails_operator_without_key()
{
    var response = await _client.PostAsJsonAsync("/playtest/request-key", new { email = "friend@example.com" });
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Single(_mailbox.Sent);
    Assert.Equal("escapemobileproductions@gmail.com", _mailbox.Sent[0].To);
    Assert.Contains("friend@example.com", _mailbox.Sent[0].Body);
    Assert.DoesNotContain("family-test-key", _mailbox.Sent[0].Body);
}

[Fact]
public async Task Feedback_empty_message_is_rejected()
{
    var response = await _client.PostAsJsonAsync("/playtest/feedback", new { message = "  ", version = "0.1.0" });
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Empty(_mailbox.Sent);
}

[Fact]
public async Task Feedback_sends_note_and_version()
{
    var response = await _client.PostAsJsonAsync("/playtest/feedback", new
    {
        message = "Stairs feel slow",
        name = "Pat",
        email = "pat@example.com",
        version = "0.1.0"
    });
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Contains("Stairs feel slow", _mailbox.Sent[0].Body);
    Assert.Contains("0.1.0", _mailbox.Sent[0].Body);
}
```

Register `RecordingEmailSender` via `ConfigureTestServices`. Email validation: must contain `@` and a `.` after `@`, trim, max 254 chars.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PlaytestApi/Playtest.Api.sln --filter MailFormTests`

Expected: FAIL (endpoints missing).

- [ ] **Step 3: Implement mail + rate limits**

Copy the CloudSave SMTP shape (`Host`, `Port`, `User`, `Password`, `From` default `no-reply@escapeproductions.biz`, `EnableSsl`). If `Smtp:Host` is empty, register `NullEmailSender` that throws `InvalidOperationException("smtp_unconfigured")` so mail endpoints return `503 {"error":"mail_unavailable"}`.

`AddRateLimiter` policy `playtest-forms` on Request-a-Key, Feedback, and download: `PermitLimit = 5`, `Window = 1 minute`, partition by remote IP, `429 {"error":"rate_limited"}`.

Request-a-Key subject: `Build-A-Tower key request`. Body: requester email only + timestamp. Never include the family key hash or plaintext.

Feedback subject: `Build-A-Tower feedback (version {version})`. Body: message, optional name/email, version.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PlaytestApi/Playtest.Api.sln`

Expected: all PASS (DownloadTests + MailFormTests).

- [ ] **Step 5: Commit**

```bash
git add PlaytestApi
git commit -m "feat: email playtest key requests and feedback"
```

---

### Task 3: Marketing site Download + Request a Key

**Files:**
- Modify: `../homelab/escapeproductions-site/index.html` (Build-A-Tower card ~lines 108–114)
- Modify: `../homelab/escapeproductions-site/main.js`
- Modify: `../homelab/escapeproductions-site/styles.css`
- Modify: `../homelab/escapeproductions-site/README.md` functionality map
- Create: `PlaytestApi/deploy/npm-playtest.md`

**Interfaces:**
- Consumes: `GET /playtest/version.json`, `POST /playtest/download`, `POST /playtest/request-key`
- Produces: public `#download` section with key field, Download button, Request a Key email field; invalid key shows exactly `Invalid key`; successful download saves `Build-A-Tower.zip`; Request a Key success: `Thanks — we'll be in touch if a key is available.` (does not reveal a key)

- [ ] **Step 1: Replace the Build-A-Tower teaser with the download UI**

In `index.html`, add nav link `Download`. Replace the upcoming card copy and `Get notified` mailto with:

```html
<article id="download" class="game-row upcoming">
  <div class="game-copy">
    <p class="eyebrow">Playtest</p>
    <h3>Build-A-Tower</h3>
    <p>Windows playtest build. Enter the shared key to download. Need a key? Request one with your email — we send keys manually.</p>
    <p class="playtest-version" data-playtest-version>Version …</p>
    <form id="playtest-download-form" class="playtest-form">
      <label for="playtest-key">Playtest key</label>
      <input id="playtest-key" name="key" type="password" autocomplete="off" required />
      <button type="submit" class="btn btn-primary">Download</button>
      <p class="playtest-error" data-download-error hidden></p>
    </form>
    <form id="playtest-request-form" class="playtest-form">
      <label for="playtest-request-email">Request a Key</label>
      <input id="playtest-request-email" name="email" type="email" required />
      <button type="submit" class="btn btn-ghost">Request a Key</button>
      <p class="playtest-status" data-request-status hidden></p>
    </form>
  </div>
</article>
```

Add the same Download link in the header and mobile nav.

- [ ] **Step 2: Wire fetch in `main.js`**

On load: `GET /playtest/version.json` → fill `[data-playtest-version]` with `Version {version}` or hide if 404.

Download submit: `POST /playtest/download` JSON `{key}`. On 403 set error text to `Invalid key` only. On 200, `blob()` → object URL → `<a download="Build-A-Tower.zip">`. On 429: `Too many tries. Wait a minute.`

Request submit: `POST /playtest/request-key` JSON `{email}`. 204 → thanks copy above. 400 → `Enter a valid email.` Never display a key.

Use same-origin `/playtest/...` (NPM will proxy).

- [ ] **Step 3: Match existing site CSS**

Add `.playtest-form` stacked label/input/button using existing Sora/Fraunces and `.btn` colors. No magenta except if the site already uses it for structure. Keep the warm architectural look.

- [ ] **Step 4: Write `PlaytestApi/deploy/npm-playtest.md`**

Document: NPM proxy host `escapeproductions.biz` + `www` — location `/playtest` → `http://127.0.0.1:5080` (sidecar on edge-proxy). Static site remains the root. Force HTTPS. Websockets off.

- [ ] **Step 5: Commit**

```bash
git add ../homelab/escapeproductions-site PlaytestApi/deploy/npm-playtest.md
git commit -m "feat: add keyed Build-A-Tower download on the studio site"
```

If `../homelab` is outside the BAT repo, copy the three site files into the commit that Escape parent can take, or commit from `C:/OldPC/Importaint Docs/Work/Steve/Escape` for those paths only.

---

### Task 4: Unity version check client

**Files:**
- Create: `Assets/Scripts/Playtest/PlaytestConfig.cs`
- Create: `Assets/Scripts/Playtest/PlaytestSemVer.cs`
- Create: `Assets/Scripts/Playtest/PlaytestUpdateClient.cs`
- Create: `Assets/Tests/EditMode/PlaytestSemVerTests.cs`
- Create: `Assets/Tests/EditMode/PlaytestUpdateClientTests.cs`
- Test: Unity EditMode `PlaytestSemVerTests` + `PlaytestUpdateClientTests`

**Interfaces:**
- Consumes: `GET {BaseUrl}/playtest/version.json`
- Produces: `PlaytestSemVer.TryParse` / `Compare`; `PlaytestUpdateClient.CheckAsync` → `PlaytestUpdateCheck` with `Kind` = `Current` | `Available` | `SilentFailure`; `Available` includes `HostedVersion` and `DownloadPage`; never throws for network errors

- [ ] **Step 1: Write failing SemVer + client tests**

```csharp
[Test]
public void Newer_hosted_patch_is_greater()
{
    Assert.Greater(PlaytestSemVer.Compare("0.2.0", "0.1.9"), 0);
}

[Test]
public async Task Check_newer_hosted_version_is_available()
{
    var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
        "{\"version\":\"0.2.0\",\"releasedAt\":\"2026-09-27T00:00:00Z\",\"downloadPage\":\"https://escapeproductions.biz/#download\"}"));
    var client = new PlaytestUpdateClient(new PlaytestConfig { BaseUrl = "https://escapeproductions.biz" }, new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz")
    });
    var check = await client.CheckAsync("0.1.0");
    Assert.AreEqual(PlaytestUpdateKind.Available, check.Kind);
    Assert.AreEqual("0.2.0", check.HostedVersion);
}

[Test]
public async Task Check_offline_is_silent_failure()
{
    var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
    var client = new PlaytestUpdateClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz")
    });
    var check = await client.CheckAsync("0.1.0");
    Assert.AreEqual(PlaytestUpdateKind.SilentFailure, check.Kind);
}

[Test]
public async Task Check_same_version_is_current()
{
    var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
        "{\"version\":\"0.1.0\",\"releasedAt\":\"2026-09-27T00:00:00Z\",\"downloadPage\":\"https://escapeproductions.biz/#download\"}"));
    var client = new PlaytestUpdateClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz")
    });
    var check = await client.CheckAsync("0.1.0");
    Assert.AreEqual(PlaytestUpdateKind.Current, check.Kind);
}
```

Copy `StubHandler` / `Json` helpers from `AccountClientTests.cs` (same pattern: `HttpMessageHandler` that returns a canned `HttpResponseMessage`).

- [ ] **Step 2: Run Unity EditMode filter `PlaytestSemVerTests|PlaytestUpdateClientTests`**

Run: `unity --json --no-banner --quiet command --project-path . --timeout 360 run_tests -- --mode editor --filter PlaytestUpdateClientTests --timeout 300`

Expected: FAIL (types missing). If the Editor is locked, write the tests anyway and compile via the same command when the lock clears — do not skip the red run.

- [ ] **Step 3: Implement**

`PlaytestConfig.BaseUrl` default `https://escapeproductions.biz`.

`PlaytestSemVer`: parse `^\d+\.\d+\.\d+`; `Compare` left vs right as (major, minor, patch). Invalid parse is not greater than a valid hosted version (treat as `Current` / no notice).

`PlaytestUpdateClient.CheckAsync(string runningVersion, CancellationToken)` GETs `/playtest/version.json`, compares hosted > running → `Available`, else `Current`. Catch `HttpRequestException`, non-success, and bad JSON → `SilentFailure`.

- [ ] **Step 4: Re-run tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Playtest Assets/Tests/EditMode/PlaytestSemVerTests.cs Assets/Tests/EditMode/PlaytestUpdateClientTests.cs
git commit -m "feat: check hosted playtest version without blocking play"
```

---

### Task 5: Unity Feedback client + main menu and pause UI

**Files:**
- Create: `Assets/Scripts/Playtest/PlaytestFeedbackClient.cs`
- Create: `Assets/Tests/EditMode/PlaytestFeedbackClientTests.cs`
- Modify: `Assets/Scripts/UI/MainMenu.uxml`
- Modify: `Assets/Scripts/UI/MainMenuController.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (`PauseUiState`, `DrawPauseOverlay`, Esc back)
- Create: `Assets/Tests/EditMode/PlaytestMenuFlowTests.cs`
- Test: `PlaytestFeedbackClientTests`, `PlaytestMenuFlowTests`

**Interfaces:**
- Consumes: `POST /playtest/feedback`; `PlaytestUpdateClient.CheckAsync`
- Produces: `PlaytestFeedbackClient.SendAsync(PlaytestFeedbackDraft)` → `Sent` | `RejectedEmpty` | `FailedKeepDraft`; main-menu `btn-feedback` + `panel-feedback`; pause **Feedback** button; update notice copy: `Version {hosted} is available. Download it from escapeproductions.biz.` with Dismiss; `Application.OpenURL` only if they click **Open download page**

- [ ] **Step 1: Write failing Feedback client + menu tests**

```csharp
[Test]
public async Task Empty_note_does_not_post()
{
    var posted = false;
    var handler = new StubHandler(_ => { posted = true; return Json(HttpStatusCode.NoContent, ""); });
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz")
    });
    var result = await client.SendAsync(new PlaytestFeedbackDraft { Message = "  ", Version = "0.1.0" });
    Assert.AreEqual(PlaytestFeedbackSendKind.RejectedEmpty, result.Kind);
    Assert.IsFalse(posted);
}

[Test]
public async Task Failed_send_keeps_draft()
{
    var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz")
    });
    var draft = new PlaytestFeedbackDraft { Message = "Hello", Version = "0.1.0" };
    var result = await client.SendAsync(draft);
    Assert.AreEqual(PlaytestFeedbackSendKind.FailedKeepDraft, result.Kind);
    Assert.AreEqual("Hello", draft.Message);
}
```

`PlaytestMenuFlowTests`: bind `MainMenu.uxml`, click `btn-feedback`, set message, assert `panel-feedback` visible. After a stubbed successful send, status label is `Thanks — sent.` Failed send status is `Couldn't send.` (apostrophe as in the spec’s “couldn’t send”).

- [ ] **Step 2: Run tests (expect FAIL)**

- [ ] **Step 3: Implement client + UI**

`PlaytestFeedbackDraft`: `Message`, `Name`, `Email`, `Version` (set from `Application.version` in UI).

Main menu: button **Feedback** on `panel-root` (after Account, before Contact). Panel fields: note (`TextField` multiline), optional name, optional email, Send, Back. On enable / Bind, fire-and-forget `CheckAsync(Application.version)` and if `Available`, show a banner `Label` on `panel-root` plus **Open download page** (`Application.OpenURL(check.DownloadPage)`).

Pause: add `PauseUiState.Feedback` and `UpdateNotice`. On opening pause (`Esc` / Menu), start the same version check; if `Available`, show a short banner above the pause buttons (not a blocking modal) with Dismiss. Add **Feedback** button on the root pause list (between Account and Resume). Feedback view: multiline, optional name/email, Send, Back to Paused. Empty note: inline `Write a note first.` without POST. Failure: `Couldn't send.` and keep fields. Esc from Feedback returns to Paused (same as Account).

Increase pause `panelH` enough for the new button + optional banner.

Do not add an in-game key field.

- [ ] **Step 4: Re-run EditMode filters**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Playtest Assets/Scripts/UI/MainMenu.uxml Assets/Scripts/UI/MainMenuController.cs Assets/Scripts/UI/TowerHudController.cs Assets/Tests/EditMode/PlaytestFeedbackClientTests.cs Assets/Tests/EditMode/PlaytestMenuFlowTests.cs
git commit -m "feat: add playtest update notice and in-game feedback"
```

---

### Task 6: Publish script, Docker, operator key rotate

**Files:**
- Create: `PlaytestApi/Dockerfile`
- Create: `PlaytestApi/docker-compose.yml`
- Create: `PlaytestApi/deploy/publish.ps1`
- Create: `PlaytestApi/deploy/set-key.sh`
- Create: `PlaytestApi/deploy/edge-proxy.md`
- Modify: `PlaytestApi/src/Playtest.Api/Program.cs` (optional `/playtest/health` `{"status":"ok"}`)

**Interfaces:**
- Consumes: Windows player folder from Unity Build; SSH `ubuntu@192.168.0.35`
- Produces: `/opt/playtest/data/Build-A-Tower.zip`, `/opt/playtest/data/version.json`; container `playtest-api` port `5080`; `set-key.sh` writes a new hash without changing the zip

- [ ] **Step 1: Add health + Docker files**

`GET /playtest/health` → `{"status":"ok"}` (no auth).

`Dockerfile`: `FROM mcr.microsoft.com/dotnet/aspnet:8.0` publish the API, listen `8080`.

`docker-compose.yml`: service `playtest-api`, ports `5080:8080`, volume `/opt/playtest/data:/data`, env `Playtest__DataDirectory=/data`, SMTP from `/opt/playtest/.env`.

- [ ] **Step 2: Write `publish.ps1`**

Parameters: `-PlayerDir` (Unity output folder), `-Version` (semver), `-Host ubuntu@192.168.0.35`. Steps: validate `^\d+\.\d+\.\d+$`; zip contents to a temp `Build-A-Tower.zip`; write local `version.json` with `downloadPage` `https://escapeproductions.biz/#download` and `releasedAt` UTC now; `scp` zip + version.json to `ubuntu@host:/opt/playtest/data/`; do not upload or print a family key. If scp fails, exit nonzero and do not claim publish succeeded.

- [ ] **Step 3: Write `set-key.sh`**

On the VM: read plaintext from stdin, hash via a one-shot `dotnet` tool **or** POST to a LAN-only endpoint. Prefer: small `FamilyKeyStore` invoked by `docker exec playtest-api` with env `SET_FAMILY_KEY` on a documented CLI flag `--set-key` that reads stdin and writes the hash file, then exits. Do not echo the key.

Document in `edge-proxy.md`: mkdir `/opt/playtest/data`, compose up, NPM path `/playtest`, first `set-key.sh`, first `publish.ps1`. Failed publish leaves previous zip + version in place (scp to a `.tmp` name then `mv`).

- [ ] **Step 4: Implement atomic replace in `publish.ps1`**

`scp` to `Build-A-Tower.zip.tmp` and `version.json.tmp`, then `ssh ... mv` both into place.

- [ ] **Step 5: Commit**

```bash
git add PlaytestApi/Dockerfile PlaytestApi/docker-compose.yml PlaytestApi/deploy PlaytestApi/src/Playtest.Api/Program.cs
git commit -m "feat: publish playtest zip to edge-proxy without rotating the key"
```

---

### Task 7: Slice verification

**Files:** none new unless a test failed

- [ ] **Step 1: `dotnet test PlaytestApi/Playtest.Api.sln`** — all green
- [ ] **Step 2: Unity EditMode** filters `PlaytestSemVerTests`, `PlaytestUpdateClientTests`, `PlaytestFeedbackClientTests`, `PlaytestMenuFlowTests` — all green
- [ ] **Step 3: Manual checklist in `PlaytestApi/deploy/edge-proxy.md`** (do not apply live unless the operator is this session and `edge-proxy` is reachable): set key, publish a dummy zip, GET version.json, POST wrong key → invalid_key, POST right key → zip, Request a Key → operator mail, game notice when version bumps
- [ ] **Step 4: Commit** only if verification required doc tweaks

```bash
git add PlaytestApi/deploy/edge-proxy.md
git commit -m "docs: record playtest publish verification"
```

---

## Spec coverage

| Spec requirement | Task |
|------------------|------|
| Frozen zip isolation / no Git auto-deploy | 6 |
| Public Download + shared key | 1, 3 |
| Request a Key waitlist email | 2, 3 |
| Version public, zip not guessable | 1 |
| In-game notice, dismiss, silent offline | 4, 5 |
| Feedback email with version | 2, 5 |
| Account unchanged | 5 (no Account edits except pause layout) |
| Rate limits / generic invalid key | 1, 2 |
| SMTP down does not break download | 2 |
| Windows-only publish from PC | 6 |
| Out-of-scope items not implemented | all |

## Self-review

- No TBD/TODO placeholders.
- Types match across tasks: `PlaytestConfig.BaseUrl`, `PlaytestUpdateKind`, `PlaytestFeedbackDraft`, `FamilyKeyStore.Verify`.
- Site path is `../homelab/escapeproductions-site` from the Build-A-Tower repo root.
