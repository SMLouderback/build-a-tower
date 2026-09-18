# Build-A-Tower Cloud Save Accounts Design

**Date:** 2026-09-17  
**Status:** Approved (brainstorming)  
**Supersedes for this effort:** `2026-09-07-cloud-save-accounts-design.md` on `feature/cloud-save-accounts` (reuse intent; this doc records current-branch decisions)

## Goal

Players can save and load from the in-game pause menu and the main menu, play fully offline, and—once signed in—synchronize up to three named towers to a homelab ASP.NET Core + PostgreSQL API at `https://api.escapeproductions.biz`, with explicit conflict handling, thirty-day revision recovery, and account export/deletion.

The game remains fully playable without an account or network connection. Windows desktop is the first target.

## Delivery approach

Sequential product slices on the **current game branch** (port local-save work from `feature/cloud-save-accounts`; do not abandon current tip):

1. Port local `.batsave` foundation onto the current game branch  
2. Pause + main-menu Save/Load (manual saves; local recovery copies)  
3. ASP.NET Core + PostgreSQL Compose API on Harvester (account lifecycle)  
4. Authenticated three-slot cloud synchronization  
5. Conflict UI, revision recovery, data export, and account deletion  

Public self-service registration stays disabled until backup restoration, account isolation, rate limiting, and conflict recovery have all passed verification.

## Snapshot scope (middle ground)

Persist enough state for a materially continuous tower:

- Tower structure: cells, room instances, stable `RoomTypeSO.id` references, room evaluation/condition and related room fields  
- Funds, difficulty, stars, simulation clock (day/minute/speed/paused)  
- Economy / progression / research unlocks required to resume the same build options and income rules  

**Regenerate on load (not persisted in this release):** live agents, pedestrian routes, elevator car positions/queues, and other transient transit runtime. Demand pools and ephemeral news/events may be rebuilt from persisted structural/economy state where that is already how systems boot.

Schema version starts at `1` and migrates forward explicitly. Corrupt, unsupported, or failed-migration saves are preserved and reported; never silently overwritten.

## Out of scope

- WebGL / downloadable-build hosting  
- Social login, MFA  
- Automatic semantic merge of conflicting saves  
- Public administration portal  
- Persisting full agent/elevator runtime continuity (separate follow-up if needed)

## Client architecture

Five boundaries in the Windows game client:

| Boundary | Responsibility |
|----------|----------------|
| **Snapshot layer** | Versioned DTOs + mappers; plain data only (no Unity object refs) |
| **LocalSaveRepository** | Atomic writes under `Application.persistentDataPath/Saves/`; current file + three recovery copies |
| **SaveCoordinator** | Capture/restore; dirty tracking; pending-load handoff into `TowerSandbox` via `GameSession` |
| **AccountClient** | Register, verify, login, password reset, logout; refresh tokens in Windows Credential Manager |
| **SyncCoordinator** | Revision compare, upload/download, offline queue, sync status for UI |

Port base: Persistence scripts and pause/main wiring from `feature/cloud-save-accounts`, adapted to current systems (leisure, shops, amenities, etc.), then extend snapshot fields for the middle-ground scope above.

### Menus

**Pause (`TowerHudController` IMGUI):** Resume · **Save** · **Load** · Options · Account (when relevant) · Main Menu  

- Saves in this release are **manual** (no timed autosave requirement)  
- **Save** writes local immediately; if signed in and online, sync only after local success  
- **Load** lists local saves (and cloud slots when signed in); confirm before replacing the active tower  
- Quit-to-main-menu warns when the tower is dirty / unsaved  

**Main menu (UIToolkit):** Replace Save/Load stubs with real flows; Account entry for login/register; Load can launch `TowerSandbox` from a selected save via pending-load handoff.

### Envelope

Every local and cloud save envelope includes: optional account ID, slot ID, player-visible tower name, schema version, cloud revision (when attached), timestamps, client installation ID / device name, game version, checksum, and compressed snapshot payload.

### Local durability

Write complete temp → flush → atomic replace. Cloud sync never blocks local play; network failure leaves a successful local save marked Pending.

## API, sync, conflicts, and ops

**Backend (new in-repo):** Stateless ASP.NET Core Identity API + PostgreSQL via Docker Compose on Harvester. Nginx Proxy Manager exposes only `https://api.escapeproductions.biz`. Postgres and admin remain LAN/Tailscale-only. Marketing site stays independently deployed.

**Accounts:** Email/password registration, verification, login, password reset, session revocation, account deletion. Cloud sync requires verified email. Access tokens (~15 min) + rotating refresh tokens (~30 days); only refresh hashes stored server-side. Transactional email (verification / reset) uses an external provider configured for the API; provider choice is an ops decision and must not block local play.

**Slots:** Three named cloud slots per verified account. Local towers work without an account; attach to a free slot after verification. Local save directories are separated by game account so accounts cannot open each other’s files by accident.

**Sync:** Upload supplies last-seen revision. Match → store and bump in one transaction. Mismatch → reject with conflict metadata (timestamps, device names, playtime, tower name, key progress). UI status: `Synced` | `Pending` | `Offline` | `Conflict` | `Error`. Sign-out stops sync without deleting local files.

**Conflicts & recovery:** Player explicitly chooses local or cloud. Rejected cloud revision recoverable for 30 days. Players can export account + saves; deletion requires recent authentication and explicit confirmation.

**Security:** HTTPS-only; endpoint rate limits; compressed (~10 MiB) and decompressed (~50 MiB) payload limits; checksum + schema validation; authorization on every account/slot/revision operation; structured audit without passwords, tokens, or save payloads.

**Ops:** Encrypted off-cluster backups, Postgres WAL archives, restore rehearsals, health/metrics/alerts, immutable images with rollback. VM snapshots are not backups. API/home outage must not block local play or local saving.

## Error handling

| Failure | Client behavior |
|---------|-----------------|
| Auth failure | Keep local; offer login/recovery |
| Network failure | Local OK; Pending; bounded exponential backoff |
| Revision mismatch | Conflict UI |
| Corrupt / unsupported local | Preserve file; expose recovery copies |
| Local write failure | Do not claim sync success |
| Server validation failure | Keep local; show support-safe error id |

Server responses use stable machine-readable codes and correlation IDs.

## Testing

**Unity:** Mapper round-trips; repository atomicity/recovery; pause and main-menu Save/Load; dirty quit warning; sync queue and both conflict choices; schema migration fixtures as the schema grows.

**API:** Real temporary PostgreSQL — Identity lifecycle, cross-account isolation, revision preconditions, size/checksum limits, three-slot enforcement, 30-day recovery, export/deletion.

**Ops:** Deploy/migrate/rollback; backup restore rehearsal before enabling public registration.

## Rollout gates

1. Local save/load playable on current branch  
2. Private API + account lifecycle verified  
3. Authenticated three-slot sync  
4. Conflict, recovery, export, deletion  
5. Limited external beta  
6. Public registration (only after ops verification)

Existing local towers are preserved throughout migration and rollout.
