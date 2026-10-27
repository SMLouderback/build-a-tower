# Build-A-Tower Live Cloud Save API Design

**Date:** 2026-09-26  
**Status:** Approved (brainstorming)  
**Extends:** `2026-09-17-cloud-save-accounts-design.md` (slices 3–5, now scheduled for live deploy)  
**Does not replace:** local `.batsave` pause/main-menu Save/Load already on the current game branch

## Goal

Stand up a live, invite-then-public cloud-save service at `https://api.escapeproductions.biz` so a signed-in player with a verified email can persist up to three named towers, load them on another session, and resolve conflicts explicitly — while the Windows game stays fully playable offline with local saves.

## Product decisions (this pass)

- **Full product surface** in one implementation plan: accounts, invite codes, email verification and password reset, three-slot sync, conflict UI, 30-day revision recovery, export, and account deletion.
- **Invite-only first.** You mint invite codes. Anyone with a valid unused (or remaining-use) code can register. After a short private check, flip a server config flag to public registration (codes no longer required).
- **Invite code + verified email** before any cloud sync. Password reset also goes through email.
- **Hosting:** new Harvester VM running Docker Compose (ASP.NET Core Identity API + PostgreSQL). Nginx Proxy Manager on the existing `edge-proxy` VM is the only public face (`https://api.escapeproductions.biz`). Postgres and admin stay LAN/Tailscale-only.
- **Marketing site** (`escapeproductions.biz` static rebuild) is **out of this pass**.
- Safety work is **not** skipped: rate limits, account isolation, checksum/schema validation, backups, and conflict recovery ship before the public-registration flip. The invite window *is* the limited external beta.

## Live surface and hosting

Players keep saving locally as they do today. Once signed in with a verified email, pause and main-menu **Save** also sync one of three named cloud slots; **Load** can pick a cloud slot (confirm before replacing the active tower).

Public hostname is only `https://api.escapeproductions.biz` via NPM on `edge-proxy`. A new Harvester VM runs Compose. Local play never depends on the API being up. API/home outage must not block local save.

## Accounts, invites, and email

Email/password accounts (no social login, no MFA).

During invite-only, registration requires a valid invite code you mint. Codes may be single-use or limited-use. After the public flip, codes are not required.

Cloud sync requires a verified email. Verification and password-reset mail use an external transactional provider configured for the API; missing email config must not block local play (it may block *cloud* register/reset with a clear error).

Tokens: access ~15 minutes + rotating refresh ~30 days; only refresh hashes stored server-side. Client stores refresh tokens in Windows Credential Manager.

Sign-out stops sync and does not delete local files. Account deletion requires recent authentication and explicit confirmation.

No public administration portal. Mint codes via a LAN/Tailscale-only admin path or CLI on the API VM.

## Slots, sync, and conflicts

Three named cloud slots per verified account. Local towers work with no account; attach to a free slot after verification. Local save directories are separated by game account so accounts cannot open each other’s files by accident.

**Save:** write local first; if signed in and online, upload only after local success, supplying last-seen revision.

- Match → store and bump revision in one transaction.
- Mismatch → reject with conflict metadata (timestamps, device names, playtime, tower name, key progress).
- Player explicitly chooses local or cloud. Rejected cloud revision recoverable for 30 days.

UI status: `Synced` | `Pending` | `Offline` | `Conflict` | `Error`.

Network failure: local OK, mark Pending, bounded exponential backoff. Auth failure: keep local, offer login/recovery. Corrupt/unsupported local: preserve file, expose recovery copies. Local write failure: do not claim sync success. Server validation failure: keep local, show support-safe error id.

Agents, elevator cars/queues, and other transient transit runtime still regenerate after load (same middle-ground snapshot as local saves).

Envelope fields already specified in the parent spec apply to cloud payloads: optional account ID, slot ID, tower name, schema version, cloud revision, timestamps, client installation ID / device name, game version, checksum, compressed snapshot.

## Security, ops, and rollout

- HTTPS-only at the public hostname.
- Rate limits on auth and save endpoints.
- Compressed ~10 MiB and decompressed ~50 MiB payload limits; checksum + schema validation.
- Authorization on every account/slot/revision operation.
- Structured audit without passwords, tokens, or save payloads.
- Encrypted off-cluster backups and Postgres WAL archives. VM snapshots are not backups.
- Health endpoint for NPM / uptime checks.
- Immutable images with rollback; deploy/migrate/rollback documented in the implementation plan.

**Rollout**

1. Deploy private API + mint first invite codes.
2. Invite window: register, verify, sync, conflict, recovery, export, deletion on real hardware.
3. Flip `PublicRegistration` (or equivalent) to on.
4. Existing local towers are preserved throughout.

Public self-service registration stays off until that private check — not until a later project.

## Client architecture (unchanged boundaries)

| Boundary | Responsibility |
|----------|----------------|
| Snapshot / LocalSaveRepository / SaveCoordinator | Already shipped for local `.batsave` |
| **AccountClient** | Register (with invite code), verify, login, password reset, logout; Credential Manager |
| **SyncCoordinator** | Revision compare, upload/download, offline queue, status for UI |

**Pause:** Resume · Save · Load · Options · Account · Main Menu  
**Main menu:** Account entry; Load lists local saves and cloud slots when signed in.

## Out of scope

- WebGL / downloadable-build hosting
- Social login, MFA
- Automatic semantic merge of conflicting saves
- Public administration portal
- Putting `escapeproductions.biz` / `www` on `edge-proxy` in this pass
- Persisting full agent/elevator runtime continuity

## Testing

**Unity:** AccountClient against a test host or recorded contracts; Credential Manager isolation in EditMode where possible; SyncCoordinator queue + both conflict choices; pause/main-menu Account + cloud slot rows; dirty quit still works offline.

**API:** Real temporary PostgreSQL — Identity lifecycle, invite consume, email-verify gate on sync, cross-account isolation, revision preconditions, size/checksum limits, three-slot enforcement, 30-day recovery, export/deletion, public-registration flag.

**Ops:** Compose up on the API VM; NPM proxy + TLS; backup restore rehearsal before the public flip.

## Error handling

Server responses use stable machine-readable codes and correlation IDs. Client mapping matches the parent spec table.
