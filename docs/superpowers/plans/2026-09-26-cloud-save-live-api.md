# Live Cloud Save API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a live invite-then-public cloud-save API at `https://api.escapeproductions.biz` and wire in-game Account + 3-slot sync + conflict UI so verified players can save and load towers without breaking offline local `.batsave`.

**Architecture:** New ASP.NET Core Identity API + PostgreSQL in `CloudSaveApi/` on a dedicated Harvester VM (Docker Compose). `edge-proxy` Nginx Proxy Manager terminates TLS for `api.escapeproductions.biz` only. Unity `AccountClient` + `SyncCoordinator` sit beside the existing Persistence layer; Save still writes local first, then uploads with a revision precondition.

**Tech Stack:** .NET 8, ASP.NET Core Identity, EF Core + Npgsql, xUnit + `WebApplicationFactory` (API), Docker Compose, NUnit EditMode (Unity 6000.4.7f1), Windows Credential Manager (`ICredentialStore` + OS store), existing `JsonUtility` / GZip / SHA-256 snapshots

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-26-cloud-save-live-api-design.md` (extends `2026-09-17-cloud-save-accounts-design.md`).
- Public hostname only: `https://api.escapeproductions.biz`. Postgres and admin remain LAN/Tailscale-only.
- Local play and local Save must work with the API down, email unconfigured, or the player signed out.
- Cloud sync requires verified email. Invite code required while `PublicRegistration` is false.
- Three named slots per account. Upload supplies last-seen revision; mismatch is a conflict, never a silent overwrite.
- Payload caps: compressed 10 MiB, decompressed 50 MiB. Schema version starts at `1`.
- Access token ~15 minutes; rotating refresh ~30 days; store refresh hashes only.
- No social login, MFA, public admin portal, or marketing-site deploy.
- Unity Editor may be open — use Pipeline `unity command` (`--mode editor`) for EditMode; do not launch batchmode Unity.exe.
- Do not commit `.env`, SMTP passwords, or Identity connection strings.

---

## File Map

**Create — API (`CloudSaveApi/`):**

- `CloudSaveApi/CloudSaveApi.sln`
- `CloudSaveApi/src/CloudSave.Api/CloudSave.Api.csproj` — HTTP host
- `CloudSaveApi/src/CloudSave.Api/Program.cs`
- `CloudSaveApi/src/CloudSave.Api/appsettings.json` — `PublicRegistration: false` default
- `CloudSaveApi/src/CloudSave.Api/Data/AppDbContext.cs`
- `CloudSaveApi/src/CloudSave.Api/Auth/*` — register, login, verify, reset, refresh, logout
- `CloudSaveApi/src/CloudSave.Api/Invites/InviteService.cs` + `InviteCommands` CLI
- `CloudSaveApi/src/CloudSave.Api/Saves/*` — slots, upload, download, conflict, revisions
- `CloudSaveApi/src/CloudSave.Api/Account/ExportDelete.cs`
- `CloudSaveApi/src/CloudSave.Api/Email/IEmailSender.cs` + `SmtpEmailSender` + `NullEmailSender`
- `CloudSaveApi/tests/CloudSave.Api.Tests/CloudSave.Api.Tests.csproj`
- `CloudSaveApi/docker-compose.yml` — `api` + `postgres`
- `CloudSaveApi/deploy/harvester-vm.yaml` — Harvester VM (same pattern as `homelab/trading-agent/trading-agent-vm.yaml`)
- `CloudSaveApi/README.md` — mint codes, NPM proxy notes, backup

**Create — Unity:**

- `Assets/Scripts/Persistence/AccountClient.cs`
- `Assets/Scripts/Persistence/CredentialStore.cs`
- `Assets/Scripts/Persistence/SyncCoordinator.cs`
- `Assets/Scripts/Persistence/CloudSaveContracts.cs` — DTOs matching API JSON
- `Assets/Tests/EditMode/AccountClientTests.cs`
- `Assets/Tests/EditMode/SyncCoordinatorTests.cs`
- `Assets/Tests/EditMode/CloudSaveMenuTests.cs`

**Modify — Unity:**

- `Assets/Scripts/Persistence/SaveContracts.cs` / mapper — additive envelope fields: `accountId`, `slotId`, `cloudRevision`, `clientInstallId`, `deviceName`, `gameVersion` (schema stays `1` if JsonUtility defaults work)
- `Assets/Scripts/Persistence/SaveCoordinator.cs` — after local save, optional `SyncCoordinator.EnqueueUpload`
- `Assets/Scripts/Persistence/LocalSaveRepository.cs` — root path includes account id when signed in
- `Assets/Scripts/Core/GameSession.cs` — current account id (null = offline/anonymous)
- `Assets/Scripts/UI/LocalSaveMenuPresenter.cs` — cloud slot rows + status
- `Assets/Scripts/UI/MainMenuController.cs` + `MainMenu.uxml` + `MainMenu.uss` — Account panel
- `Assets/Scripts/UI/TowerHudController.cs` — Account + cloud Load/Save status on pause
- `README.md` — cloud save + invite-only note

---

### Task 1: API host + health + Compose skeleton

**Files:**
- Create: `CloudSaveApi/src/CloudSave.Api/CloudSave.Api.csproj`
- Create: `CloudSaveApi/src/CloudSave.Api/Program.cs`
- Create: `CloudSaveApi/docker-compose.yml`
- Create: `CloudSaveApi/tests/CloudSave.Api.Tests/HealthTests.cs`

**Interfaces:**
- Produces: `GET /health` → `200` `{ "status": "ok" }`
- Produces: `dotnet test CloudSaveApi/CloudSave.Api.sln` runnable
- Produces: `docker compose up` builds API (health may 503 until Task 2 wires Postgres — health must not require Identity)

- [ ] **Step 1: Write failing health test**

```csharp
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
```

`CloudSaveApiFactory` : `WebApplicationFactory<Program>`.

- [ ] **Step 2: Run test — expect FAIL (no project / no /health)**

```powershell
dotnet test CloudSaveApi/CloudSave.Api.sln --filter Health_returns_ok
```

Expected: FAIL.

- [ ] **Step 3: Minimal `Program.cs` + compose**

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.Run();
```

`docker-compose.yml`: service `api` (build Dockerfile, expose `8080` internally only — no host `80/443`) and placeholder `postgres` image `postgres:16` with volume. Do not publish Postgres past the VM.

- [ ] **Step 4: Re-run test — expect PASS**

- [ ] **Step 5: Commit**

```powershell
git add CloudSaveApi
git commit -m "feat: add cloud-save API host and health endpoint"
```

---

### Task 2: Identity + register/login/refresh (invite not required in tests yet)

**Files:**
- Create: `CloudSaveApi/src/CloudSave.Api/Data/AppDbContext.cs` (`IdentityDbContext<CloudUser>`)
- Create: `CloudSaveApi/src/CloudSave.Api/Auth/AuthEndpoints.cs`
- Create: `CloudSaveApi/src/CloudSave.Api/Auth/TokenService.cs`
- Test: `CloudSaveApi/tests/CloudSave.Api.Tests/AuthLifecycleTests.cs`

**Interfaces:**
- Consumes: Task 1 host
- Produces: `CloudUser : IdentityUser` with `EmailConfirmed`
- Produces: `POST /v1/auth/register` body `{ email, password, inviteCode? }` — Task 2 accepts missing invite only when `PublicRegistration` is true; default config false will 403 until Task 3. **For this task only**, test host sets `PublicRegistration: true`.
- Produces: `POST /v1/auth/login` → `{ accessToken, refreshToken, expiresIn }`
- Produces: `POST /v1/auth/refresh` rotates refresh; reuse of old refresh fails
- Produces: `POST /v1/auth/logout` revokes refresh family
- Produces: access JWT ~15 min; refresh 30 days; persist `RefreshToken` rows as SHA-256 hashes only

- [ ] **Step 1: Failing tests** — register+login; refresh rotation; reused refresh rejected; password not in responses.

Use Testcontainers `PostgreSQLContainer` or `WebApplicationFactory` with EF InMemory **only if** Identity/Npgsql integration is too heavy for CI — prefer real Postgres (spec: real temporary PostgreSQL).

- [ ] **Step 2: Run — expect FAIL**

```powershell
dotnet test CloudSaveApi/CloudSave.Api.sln --filter AuthLifecycleTests
```

- [ ] **Step 3: Implement Identity + token endpoints. Never log passwords or raw tokens.**

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit** `feat: add Identity register login and rotating refresh tokens`

---

### Task 3: Invite codes + PublicRegistration gate

**Files:**
- Create: `CloudSaveApi/src/CloudSave.Api/Invites/InviteCode.cs` (`CodeHash`, `RemainingUses`, `ExpiresUtc`, `CreatedUtc`)
- Create: `CloudSaveApi/src/CloudSave.Api/Invites/InviteService.cs`
- Create: `CloudSaveApi/src/CloudSave.Api/Invites/InviteCli.cs` — `dotnet run -- --mint-invite --uses 1`
- Test: `CloudSaveApi/tests/CloudSave.Api.Tests/InviteTests.cs`

**Interfaces:**
- Consumes: `POST /v1/auth/register`
- Produces: when `PublicRegistration` is false, missing/invalid/exhausted invite → `403` code `invite_required` / `invite_invalid`
- Produces: successful register decrements `RemainingUses` in the same transaction as user insert
- Produces: CLI prints **one** plaintext code to stdout (not stored plaintext)

- [ ] **Step 1: Failing tests** — no code rejected; valid code consumed; second use of single-use fails; `PublicRegistration: true` allows register without code.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement gate + mint CLI (LAN/Tailscale use; no public HTTP mint)**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: gate registration on invite codes until public flip`

---

### Task 4: Email verification + password reset

**Files:**
- Create: `CloudSaveApi/src/CloudSave.Api/Email/IEmailSender.cs`
- Create: `CloudSaveApi/src/CloudSave.Api/Email/SmtpEmailSender.cs`
- Create: `CloudSaveApi/src/CloudSave.Api/Email/NullEmailSender.cs` — logs `email_unconfigured` correlation id; used when SMTP env missing
- Test: `CloudSaveApi/tests/CloudSave.Api.Tests/EmailGateTests.cs`

**Interfaces:**
- Produces: `POST /v1/auth/verify` `{ email, token }`
- Produces: `POST /v1/auth/forgot` `{ email }` always `200` (no account enumeration)
- Produces: `POST /v1/auth/reset` `{ email, token, newPassword }`
- Produces: slot upload/download return `403` `email_unverified` until `EmailConfirmed`
- Produces: `IEmailSender.SendAsync(to, subject, body)` — tests use a capturing fake

- [ ] **Step 1: Failing tests** — unverified cannot PUT slot; verify then PUT allowed (use a stub slot until Task 5, or a `/v1/saves` placeholder that only checks the gate). Forgot does not reveal whether email exists.

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Implement tokens (random, hashed at rest, short TTL). Null sender does not throw on forgot.**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: require verified email for cloud sync and reset via email`

---

### Task 5: Three slots + revision upload/download

**Files:**
- Create: `CloudSaveApi/src/CloudSave.Api/Saves/SaveEndpoints.cs`
- Create: `CloudSaveApi/src/CloudSave.Api/Saves/SlotRecord.cs` (`SlotId` 1–3, `Revision`, `TowerName`, `Payload`, `Checksum`, `ModifiedUtc`, `DeviceName`, `PlayMinutes`)
- Test: `CloudSave.Api.Tests/SlotSyncTests.cs`

**Interfaces:**
- Produces: `GET /v1/saves` → three slots (empty or occupied) for the current user only
- Produces: `PUT /v1/saves/{slotId}` body `{ expectedRevision, towerName, schemaVersion, checksum, payloadBase64, deviceName, playMinutes, gameVersion, clientInstallId }`
  - `expectedRevision` 0 on first fill
  - match → persist, `revision++`, `200` `{ revision }`
  - mismatch → `409` `conflict` + metadata (timestamps, device names, playtime, tower name, stars/day if present in envelope)
- Produces: `GET /v1/saves/{slotId}` → payload + revision
- Produces: reject payload over 10 MiB compressed or 50 MiB decompressed; checksum mismatch → `400`
- Produces: cross-account GET/PUT → `404` (no leak)

- [ ] **Step 1: Failing isolation + revision tests**

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Implement transactional update `WHERE revision = expected`. Store gzip bytes, not Unity objects.**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: persist three cloud slots with revision preconditions`

---

### Task 6: Conflict recovery (30 days) + export + deletion + rate limits

**Files:**
- Create: `CloudSaveApi/src/CloudSave.Api/Saves/RevisionArchive.cs`
- Create: `CloudSaveApi/src/CloudSave.Api/Account/AccountEndpoints.cs`
- Test: `CloudSave.Api.Tests/RecoveryExportDeleteTests.cs`

**Interfaces:**
- Produces: on successful PUT that replaces a payload, archive previous blob with `KeepUntil = now+30d`
- Produces: `GET /v1/saves/{slotId}/revisions` list recoverable
- Produces: `POST /v1/saves/{slotId}/restore/{revisionId}` (authz owner only)
- Produces: `GET /v1/account/export` zip/json of account + current slots (no password hashes)
- Produces: `DELETE /v1/account` requires header `X-Confirm: DELETE` and a password or fresh access token issued in last 15 minutes
- Produces: rate limit auth (`5/min/IP` register/login) and PUT (`30/min/user`) — `429` `rate_limited`

- [ ] **Step 1: Failing tests** for archive, restore, export omits secrets, delete removes slots, cross-account restore 404

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Implement + ASP.NET rate limiter**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: add revision recovery export deletion and auth rate limits`

---

### Task 7: Unity AccountClient + CredentialStore

**Files:**
- Create: `Assets/Scripts/Persistence/CloudSaveContracts.cs`
- Create: `Assets/Scripts/Persistence/CredentialStore.cs`
- Create: `Assets/Scripts/Persistence/AccountClient.cs`
- Test: `Assets/Tests/EditMode/AccountClientTests.cs`

**Interfaces:**
- Consumes: HTTP JSON from Tasks 2–4
- Produces: `AccountClient.Register(email, password, inviteCode)` / `Login` / `Verify` / `Forgot` / `Reset` / `Logout` / `TryRefresh`
- Produces: `CredentialStore.SaveRefresh(accountId, refreshToken)` using Windows Credential Manager target `BuildATower/CloudSave/{accountId}`; EditMode tests inject `ICredentialStore` memory fake
- Produces: `GameSession.CurrentAccountId` set on login, cleared on logout; does not delete local files
- Produces: base URL from `CloudSaveConfig.ApiBaseUrl` default `https://api.escapeproductions.biz` (overridable in tests)

- [ ] **Step 1: Failing tests** with `HttpMessageHandler` stub — login stores refresh; logout clears session + credentials; 401 triggers refresh once

- [ ] **Step 2: Pipeline** `unity command run_tests -- --mode editor --filter AccountClientTests`

Expected: FAIL

- [ ] **Step 3: Implement. Never log tokens. Missing API → typed `CloudError.Offline`.**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: add Unity AccountClient and credential storage`

---

### Task 8: Unity SyncCoordinator + local-first Save

**Files:**
- Create: `Assets/Scripts/Persistence/SyncCoordinator.cs`
- Modify: `Assets/Scripts/Persistence/SaveCoordinator.cs`
- Modify: `Assets/Scripts/Persistence/LocalSaveRepository.cs` — `CreateDefault(string accountId = null)` uses `.../Saves/{accountId}/` when non-empty
- Modify: `Assets/Scripts/Persistence/SaveContracts.cs` + mapper — additive cloud fields
- Test: `Assets/Tests/EditMode/SyncCoordinatorTests.cs`

**Interfaces:**
- Consumes: `AccountClient`, `SaveCoordinator.SaveCurrent`
- Produces: `SyncStatus` enum `Synced, Pending, Offline, Conflict, Error`
- Produces: `EnqueueUploadAfterLocalSuccess(slotId, snapshot, lastSeenRevision)` — no-op if signed out / unverified
- Produces: `409` → `Conflict` with `CloudConflictInfo`; `TryResolveKeepLocal` / `TryResolveKeepCloud`
- Produces: network fail after local write → `Pending`, backoff 2s, 8s, 32s (cap); never mark sync success if local write failed

- [ ] **Step 1: Failing tests** — local fail skips upload; 409 sets Conflict; keep-local PUT with new expectedRevision; keep-cloud writes snapshot via `GameSession.PrepareLoad` path (or repository save) without claiming local write if download fails

- [ ] **Step 2: FAIL via Pipeline filter `SyncCoordinatorTests`**

- [ ] **Step 3: Implement**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat: sync cloud slots only after successful local save`

---

### Task 9: Pause + main menu Account and cloud slots

**Files:**
- Modify: `Assets/Scripts/UI/MainMenu.uxml` — `btn-account`, `panel-account` (email, password, invite, verify, forgot)
- Modify: `Assets/Scripts/UI/MainMenu.uss`
- Modify: `Assets/Scripts/UI/MainMenuController.cs`
- Modify: `Assets/Scripts/UI/LocalSaveMenuPresenter.cs` — list cloud slots when signed in
- Modify: `Assets/Scripts/UI/TowerHudController.cs` — Account button; show `SyncStatus`; Load lists cloud slots
- Test: `Assets/Tests/EditMode/CloudSaveMenuTests.cs`

**Interfaces:**
- Consumes: Task 7–8
- Produces: Account panel; Load shows local rows plus up to 3 cloud rows; confirm before replacing tower
- Produces: pause Save still local-first then enqueue
- Produces: main-menu Save stays disabled with existing tooltip (saves created while playing)

- [ ] **Step 1: Failing UI tests** (same style as `MainMenuLoadFlowTests`) — account panel opens; unverified shows verify prompt; cloud row present when `GET /v1/saves` stubbed

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Implement IMGUI/UIToolkit only; keep dollhouse menu look (no magenta chrome)

- [ ] **Step 4: PASS** `CloudSaveMenuTests|MainMenuLoadFlowTests`

- [ ] **Step 5: Commit** `feat: add account and cloud slots to pause and main menu`

---

### Task 10: Harvester VM, NPM notes, backups, README

**Files:**
- Create: `CloudSaveApi/deploy/harvester-vm.yaml` — VM name `cloud-save-api`, docker + compose plugin (copy `ensure-docker-compose` from `homelab/trading-agent/trading-agent-vm.yaml`), 2 CPU / 4Gi, **no** public NIC requirement if API is only reached via `edge-proxy` on LAN; attach the same LAN as `edge-proxy` can route to
- Create: `CloudSaveApi/deploy/npm-proxy.md` — NPM host `api.escapeproductions.biz` → `http://<cloud-save-api-lan>:8080`; websocket off; force HTTPS
- Create: `CloudSaveApi/deploy/backup.md` — `pg_dump` + WAL; VM snapshots are not backups; restore rehearsal checklist before `PublicRegistration: true`
- Modify: `README.md` — invite-only cloud save; pause Account; API outage does not block local Save

**Interfaces:**
- Produces: apply-able VM manifest and operator docs (no live apply required in CI)

- [ ] **Step 1: Write yaml + docs (no secrets)**

- [ ] **Step 2: Self-review: Postgres port not in NPM; health path `/health`**

- [ ] **Step 3: Commit** `docs: add cloud-save API VM proxy and backup runbooks`

---

### Task 11: Slice verification

**Files:** none required beyond README if copy is already correct

- [ ] **Step 1: `dotnet test CloudSaveApi/CloudSave.Api.sln`** — all PASS

- [ ] **Step 2: Pipeline EditMode** filters `AccountClientTests|SyncCoordinatorTests|CloudSaveMenuTests|MainMenuLoadFlowTests|GameSessionLoadTests`

Expected: all PASS

- [ ] **Step 3: Manual / ops checklist in commit message if not executed:** Compose on VM, NPM TLS, mint invite, register, verify (or capturing mail), save+load slot, force 409 and both resolutions, `PublicRegistration` still false

- [ ] **Step 4: Commit** only if README leftover: `docs: note invite-only live cloud save`

---

## Follow-on (not this plan)

- Marketing site on `edge-proxy`
- Public registration flip (ops: set `PublicRegistration: true` after the private check)
- Limited public beta marketing / rate-limit retune

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Dedicated VM + Compose + NPM hostname | 1, 10 |
| Invite codes + public flag | 3 |
| Email verify + reset | 4 |
| 3 slots + revision + isolation | 5 |
| Conflict + 30-day recovery + export/delete + rate limits | 6 |
| AccountClient + Credential Manager | 7 |
| Local-first sync + statuses | 8 |
| Pause/main Account + cloud Load | 9 |
| Offline local play | 1 health independent; 8 no-op when signed out |
| No marketing site | omitted |

No TBD placeholders. Email provider is SMTP settings / `IEmailSender`, not a branded vendor lock-in.
