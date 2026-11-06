# Star-Tier Elevator Cars + Star Celebration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Elevator cars show exact 0★–5★ art, and each live promote/demote step queues a pause modal (fireworks on promote only) that restores prior sim speed on Continue.

**Architecture:** `StarSystem` records per-step `StarChangeEvent`s. `StarCelebrationController` drains a FIFO queue, pauses/restores via `TowerSimulation.SetSpeedPreset`, draws IMGUI modals, and spawns procedural world fireworks on promotes. `ElevatorView` loads `elevator_car_s{SS}` and refreshes when stars sync.

**Tech Stack:** Unity Built-in RP, IMGUI, `ParticleSystem`, Resources `.bytes` / `LoadImage`, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-08-21-star-tier-cars-celebration-design.md`

## Global Constraints

- Car naming: `elevator_car_s{star:00}` (e.g. `elevator_car_s03`); current car becomes **5★**.
- Exact star 0–5 for cars (not the stairs 3-bucket map).
- Promote: fireworks + modal + pause; **one queue entry per star gained**.
- Demote: quieter modal + pause; **no fireworks**; queue if multiple.
- Continue restores snapshotted `MinutesPerRealSecond` + `Paused`.
- Esc during celebration: Continue-only (no Esc pause menu / no skip).
- `ForceStars` / load: **no** celebration events.
- Structure art (lobby/stairs/cars) jumps to **final** star immediately; celebration is presentation only.
- Shaft tile art out of scope; no celebration SFX in v1.
- Do not commit `.superpowers/sdd/*`, `_Recovery/`, or `*.wip`.
- Shell is Windows PowerShell — use `;` not `&&`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/StarSystem.cs` | Step event list on promote/demote/`ForceStars` |
| `Assets/Scripts/Economy/StarChangeEvent.cs` | Event kind + star value |
| `Assets/Scripts/UI/StarCelebrationController.cs` | Queue, pause gate, IMGUI modal |
| `Assets/Scripts/Rendering/StarFireworks.cs` | Procedural particle bursts behind tower |
| `Assets/Scripts/Transit/ElevatorView.cs` | Star-tier car sprites |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Drain events → celebration + sync cars |
| `Assets/Scripts/UI/TowerHudController.cs` | OR celebration into `BlocksWorldInput`; block Esc pause while celebrating |
| `Assets/Tests/EditMode/StarSystemTests.cs` | Promote/demote event tests |
| `Assets/Tests/EditMode/ElevatorCarArtTests.cs` | Resource naming tests |
| `Assets/Resources/Art/Structure/elevator_car_s{SS}.*` | Car art 0–5 |

---

### Task 1: Star change events

**Files:**
- Create: `Assets/Scripts/Economy/StarChangeEvent.cs`
- Modify: `Assets/Scripts/Economy/StarSystem.cs`
- Modify: `Assets/Tests/EditMode/StarSystemTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum StarChangeKind { Promoted, Demoted }

public readonly struct StarChangeEvent
{
    public StarChangeKind Kind { get; }
    public int Stars { get; }
    public StarChangeEvent(StarChangeKind kind, int stars) { Kind = kind; Stars = stars; }
}
```
- Produces on `StarSystem`:
  - `public IReadOnlyList<StarChangeEvent> PendingChanges { get; }`
  - `public void ClearPendingChanges()` — empties the list
  - Each `CurrentStars++` in `TryPromote` appends `new StarChangeEvent(Promoted, CurrentStars)`
  - Demote in `EvaluateQuarterly` appends `new StarChangeEvent(Demoted, CurrentStars)` after decrement
  - `ForceStars` calls `ClearPendingChanges()` and does **not** append

- [ ] **Step 1: Write failing tests** in `StarSystemTests.cs`:

```csharp
[Test]
public void TryPromote_cascades_enqueue_one_event_per_star()
{
    var stars = new StarSystem();
    Assert.IsTrue(stars.TryPromote(GridReadyForTwoStars(), averageStress: 10f, population: 30));
    Assert.AreEqual(2, stars.CurrentStars);
    Assert.AreEqual(2, stars.PendingChanges.Count);
    Assert.AreEqual(StarChangeKind.Promoted, stars.PendingChanges[0].Kind);
    Assert.AreEqual(1, stars.PendingChanges[0].Stars);
    Assert.AreEqual(StarChangeKind.Promoted, stars.PendingChanges[1].Kind);
    Assert.AreEqual(2, stars.PendingChanges[1].Stars);
}

[Test]
public void EvaluateQuarterly_demote_enqueues_demoted_event()
{
    var stars = new StarSystem();
    stars.ForceStars(1);
    stars.ClearPendingChanges();
    // Lobby-only grid fails 1★ population/stress — use stress/pop that fail MeetsCriteria(1)
    stars.EvaluateQuarterly(GridWithLobby(), averageStress: 90f, population: 1);
    Assert.AreEqual(0, stars.CurrentStars);
    Assert.AreEqual(1, stars.PendingChanges.Count);
    Assert.AreEqual(StarChangeKind.Demoted, stars.PendingChanges[0].Kind);
    Assert.AreEqual(0, stars.PendingChanges[0].Stars);
}

[Test]
public void ForceStars_does_not_enqueue_and_clears_pending()
{
    var stars = new StarSystem();
    stars.TryPromote(GridReadyForTwoStars(), averageStress: 10f, population: 30);
    Assert.IsTrue(stars.PendingChanges.Count > 0);
    stars.ForceStars(5);
    Assert.AreEqual(0, stars.PendingChanges.Count);
    Assert.AreEqual(5, stars.CurrentStars);
}

[Test]
public void ClearPendingChanges_empties_list()
{
    var stars = new StarSystem();
    stars.TryPromote(GridWithLobby(), averageStress: 10f, population: 10);
    stars.ClearPendingChanges();
    Assert.AreEqual(0, stars.PendingChanges.Count);
}
```

Adapt `GridReadyForTwoStars` / `GridWithLobby` to the helpers already in the test file. If demote criteria differ, use whatever setup existing demote tests use (search for `EvaluateQuarterly` in the file); if none exist, construct a state that fails `MeetsCriteria(CurrentStars)` after `ForceStars(1)`.

- [ ] **Step 2: Run EditMode tests for these cases — expect FAIL** (types/APIs missing). If Unity Editor lock blocks the runner, use the repo's net8/Roslyn harness pattern under `.superpowers/sdd/` and report which route.

- [ ] **Step 3: Implement** `StarChangeEvent.cs` and the pending list on `StarSystem` as specified. Keep `TryPromote` / `EvaluateQuarterly` return behavior unchanged for existing tests.

- [ ] **Step 4: Run tests — expect PASS (new + existing `StarSystemTests`).

- [ ] **Step 5: Commit** only the event + `StarSystem` + test files.

```
feat: emit per-step star promote and demote events
```

---

### Task 2: Star-tier elevator car loading

**Files:**
- Modify: `Assets/Scripts/Transit/ElevatorView.cs`
- Create: `Assets/Tests/EditMode/ElevatorCarArtTests.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs` (call `elevatorView.SetStarRating` from `SyncStructureArtToStars`)

**Interfaces:**
- Produces:
```csharp
public static string ElevatorCarResource(int star) =>
    $"elevator_car_s{Mathf.Clamp(star, 0, 5):00}";

public void SetStarRating(int stars); // reloads sprite; applies to all car renderers
```
- Fallback load order for one star: exact `elevator_car_s{SS}` → nearest lower → nearest higher → legacy `elevator_car` → `BuildFallbackCar()`.
- Reuse existing black-plate keying / crop path from `LoadCarSprite`.

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void ElevatorCarResource_UsesZeroPaddedStar()
{
    Assert.AreEqual("elevator_car_s00", ElevatorView.ElevatorCarResource(0));
    Assert.AreEqual("elevator_car_s03", ElevatorView.ElevatorCarResource(3));
    Assert.AreEqual("elevator_car_s05", ElevatorView.ElevatorCarResource(5));
    Assert.AreEqual("elevator_car_s05", ElevatorView.ElevatorCarResource(9));
}
```

- [ ] **Step 2: Run — expect FAIL.**

- [ ] **Step 3: Implement** `ElevatorCarResource`, star-aware load with fallback chain, `SetStarRating` that clears `_carSprite` and forces reload, and `SyncStructureArtToStars`:

```csharp
void SyncStructureArtToStars()
{
    var stars = _stars?.CurrentStars ?? 0;
    var structureChanged = StructureCutawayArt.SetStarRating(stars);
    elevatorView?.SetStarRating(stars);
    if (structureChanged)
        build?.RefreshStarStructureArt();
}
```

Preserve today's behavior: if only cars need refresh, still call `SetStarRating` on the view even when structure art returns false. If `RefreshStarStructureArt` should only run when structure changes, keep that gate; always push stars to `elevatorView`.

- [ ] **Step 4: Run tests — PASS.**

- [ ] **Step 5: Commit**

```
feat: load elevator car sprites by exact star rating
```

---

### Task 3: Celebration controller + fireworks + HUD gating

**Files:**
- Create: `Assets/Scripts/UI/StarCelebrationController.cs`
- Create: `Assets/Scripts/Rendering/StarFireworks.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Create: `Assets/Tests/EditMode/StarCelebrationPauseTests.cs` (pure pause-gate helper if extracted)

**Interfaces:**
- Produces on `StarCelebrationController`:
```csharp
public bool IsActive { get; }           // modal open or queue non-empty while waiting on Esc pause
public void Enqueue(IReadOnlyList<StarChangeEvent> events);
public void ClearQueue();               // optional safety on scene unload
```
- `StarFireworks.Play(Transform parent, Vector3 towerTopWorld)` / `Stop()` — procedural `ParticleSystem` bursts; sorting order below cars (cars use 30; use ~5–10) and above sky.
- Pause gate (can be private methods on the controller mirroring HUD):
  - Snapshot `simulation.Clock.MinutesPerRealSecond` + `Paused`
  - `SetSpeedPreset(speed, paused: true)` while modal open
  - On Continue: restore snapshot exactly like `TowerHudController.ResumeFromPause`

**Wire-up**
- After `TryPromote` / `EvaluateQuarterly` in `TowerSimulation`, if `PendingChanges.Count > 0`: `celebration.Enqueue(copy); stars.ClearPendingChanges();` then `SyncStructureArtToStars()`.
- Ensure a `StarCelebrationController` exists (serialized or runtime `AddComponent` near HUD/sim).
- `TowerHudController.BlocksWorldInput` → true if Esc-paused **or** `celebration.IsActive`.
- Esc `Update` handler: if `celebration.IsActive`, return immediately (no pause menu).
- If Esc pause already open when events arrive: controller waits until `!hud.IsEscPaused` (expose a bool, or check `BlocksWorldInput` carefully — prefer an explicit `IsEscPauseOpen` on HUD) before starting the next modal.

**IMGUI modal (OnGUI on controller)**
- Promote: warm tint, title `N★`, body “Your tower earned another star!”, button Continue.
- Demote: muted tint, title `Demoted to N★`, body “Quarterly review…”, Continue.
- Full-screen dim; Continue stops fireworks (promote) and advances queue.

- [ ] **Step 1: Write pause-restore unit test** for a small extracted helper if you pull snapshot/restore into a testable static/service; otherwise verify compile + a focused EditMode test that `Enqueue` of one Promoted leaves `IsActive` true after a tick method, and Continue clears it. Prefer:

```csharp
// If extracting:
public readonly struct SpeedSnapshot { public float MinutesPerRealSecond; public bool Paused; }
public static SpeedSnapshot Capture(GameClock clock);
public static void Apply(TowerSimulation sim, SpeedSnapshot snap);
```

Test Capture/Apply with a fake or by testing the struct round-trip through a thin wrapper. Keep this minimal — do not overbuild.

- [ ] **Step 2: Implement controller + fireworks + HUD/sim wiring.**

- [ ] **Step 3: Verify compile** (Unity EditMode or net8 typecheck). Manually note Play Mode checklist in the commit body / report: promote cascade shows two modals; demote no fireworks; Continue restores speed.

- [ ] **Step 4: Commit**

```
feat: queue star celebrate/demote modals with fireworks and pause restore
```

---

### Task 4: Car art assets

**Files:** `Assets/Resources/Art/Structure/elevator_car_s{SS}.{png,bytes}` (+ `.meta`)

- [ ] **Step 1: Rename** `elevator_car` → `elevator_car_s05` (png, bytes, metas). Keep a copy or fallback alias only if needed — prefer rename; code already falls back to legacy name then procedural.
- [ ] **Step 2: Generate 0★–4★ cars** per the mood table in the spec (same silhouette family, transparent background, no black plate).
- [ ] **Step 3: Normalize** (key black plate if present, crop to content), write `.png` + `.bytes`, metas from template.
- [ ] **Step 4: Commit**

```
feat: add star-tier elevator car art 0-5
```

---

### Task 5: Docs closeout

- [ ] Mark spec **Implemented**.
- [ ] Note follow-up on structure-cutaway / lobby star specs if useful (optional one-liner).
- [ ] Commit

```
docs: mark star-tier cars and celebration implemented
```

## Spec coverage

| Requirement | Task |
|-------------|------|
| Per-step promote/demote events | 1 |
| ForceStars silent | 1 |
| Car resource naming + fallback | 2 |
| Sync cars on star change | 2 |
| Queue + pause + Continue restore | 3 |
| Fireworks on promote only | 3 |
| Esc blocked during celebration | 3 |
| Wait if Esc pause already open | 3 |
| Car art 0–5 | 4 |
| Spec Implemented | 5 |
