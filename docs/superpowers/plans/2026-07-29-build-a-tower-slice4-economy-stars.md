# Slice #4 Economy + Stars + Sandbox Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add daily rent/upkeep economy, condo sale events, 0→1→2 star progression with unlock gating, and sandbox time-speed + scrollbar camera controls for testing long-term play.

**Architecture:** Lightweight `EconomySystem` and `StarSystem` pure C# services owned by `TowerSimulation`, driven off `GameClock` day/quarter boundaries. Placement gates read `RoomTypeSO.requiredStars` vs `StarSystem.CurrentStars`. Sandbox HUD presets mutate clock speed; `CutawayCamera` gains IMGUI scrollbars that pan the same orthographic camera.

**Tech Stack:** Unity 6000.4.x, C#, NUnit EditMode/PlayMode, existing IMGUI `TowerHudController`, `FundsWallet`, `GameClock`, `AgentSystem`.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-29-build-a-tower-slice4-design.md`
- Stars MVP range: **0–2** only; 3–5 roadmap only
- Recurring income: **midnight**; condo sale: **event**; star check: every **90** days
- Elevator `requiredStars = 1`; start sandbox at **0★**
- Time presets: Pause · 1x · 2x · 5x · 10x · 60x (1x = 1 game minute / real second)
- Do not implement retail traffic income, multi-car, express, research, or full ledgers
- Prefer TDD EditMode tests; if Unity batch is locked by open Editor, note it and verify via Test Runner
- Commit after each task; do not edit the plan file's checkbox semantics beyond marking progress in commits/messages if needed

---

### Task 1: Mutable GameClock speed

**Files:**
- Modify: `Assets/Scripts/Time/GameClock.cs`
- Modify: `Assets/Tests/EditMode/GameClockTests.cs`

**Interfaces:**
- Produces: `float MinutesPerRealSecond { get; set; }`, existing `Paused`, `Tick`, `DayIndex`, `DayRolled`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void SetSpeed_changes_minutes_advanced_per_real_second()
{
    var clock = new GameClock(1f);
    clock.MinutesPerRealSecond = 5f;
    clock.Tick(1f);
    Assert.AreEqual(5f, clock.LastTickGameMinutes);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode filter `GameClockTests.SetSpeed_changes_minutes_advanced_per_real_second` (Editor Test Runner or batch if unlocked).
Expected: FAIL — property missing or speed stuck at constructor value.

- [ ] **Step 3: Write minimal implementation**

Replace readonly `_minutesPerRealSecond` with a settable property clamped to `>= 0.01f` when not relying on Pause for zero; Pause remains the stop switch (presets set `Paused` separately).

```csharp
float _minutesPerRealSecond;

public float MinutesPerRealSecond
{
    get => _minutesPerRealSecond;
    set => _minutesPerRealSecond = Mathf.Max(0.01f, value);
}
```

Constructor assigns via the property. `Tick` continues to use `_minutesPerRealSecond`.

- [ ] **Step 4: Run tests — expect PASS** (all `GameClockTests`)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Time/GameClock.cs Assets/Tests/EditMode/GameClockTests.cs
git commit -m "feat: allow GameClock speed changes at runtime"
```

---

### Task 2: EconomySystem (midnight rent, upkeep, condo sale)

**Files:**
- Create: `Assets/Scripts/Economy/EconomySystem.cs` (+ `.meta`)
- Create: `Assets/Tests/EditMode/EconomySystemTests.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Core/RoomInstance.cs` — add `public bool CondoSold { get; set; }`

**Interfaces:**
- Consumes: `FundsWallet`, `TowerGrid`, `IReadOnlyList<Agent>` or agent home lookup, `GameClock.DayIndex`
- Produces:
  - `void OnNewDay(TowerGrid grid, IReadOnlyList<Agent> agents, FundsWallet wallet)`
  - `bool TrySellCondo(RoomInstance room, FundsWallet wallet)` — pays once if `UpfrontSale` and not sold
  - `int LastIncome`, `int LastExpense`, `int LastNet`
  - `const int ElevatorDailyUpkeep = 10_000`

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void Midnight_pays_daily_rent_for_occupied_office()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
    Assert.IsTrue(grid.TryPlace(Office(baseIncome: 5000), new Vector2Int(0, 1), out var office));
    var agents = new List<Agent> { new Agent(1, AgentRole.OfficeWorker, office, office.Origin) };
    var wallet = new FundsWallet(100_000);
    var economy = new EconomySystem();
    economy.OnNewDay(grid, agents, wallet);
    Assert.AreEqual(105_000, wallet.Balance);
    Assert.AreEqual(5000, economy.LastIncome);
}

[Test]
public void Midnight_charges_elevator_upkeep()
{
    // lobby + elevator shaft, no income rooms → balance decreases by ElevatorDailyUpkeep
}

[Test]
public void Condo_sale_pays_once()
{
    var condo = /* UpfrontSale baseIncome 200000 */;
    var wallet = new FundsWallet(0);
    var economy = new EconomySystem();
    Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
    Assert.IsFalse(economy.TrySellCondo(condoRoom, wallet));
    Assert.AreEqual(200_000, wallet.Balance);
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement `EconomySystem`**

Midnight logic:
- Income: for each grid room with `QuarterlyRent` or `NightlyRate` and `baseIncome > 0`, if any agent `HomeRoom` matches → add `baseIncome`.
- Expense: for each `isElevatorShaft` room → add `ElevatorDailyUpkeep` to expense and `wallet` spend via balance subtract (use a small `TrySpend` or allow `FundsWallet` to go unchanged if you add `void Charge(int)` — prefer extending wallet with `void Subtract(int amount)` that floors at 0 **or** allows debt; spec: debit upkeep — implement `Subtract` that reduces balance without clamping negative for honesty, or clamp at 0; **clamp at 0** for MVP).

Condo: if `incomeModel == UpfrontSale` && !`CondoSold` → Add baseIncome, set `CondoSold = true`.

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Economy Assets/Scripts/Core/RoomInstance.cs Assets/Tests/EditMode/EconomySystemTests.cs
git commit -m "feat: add daily economy sweep and condo sale"
```

---

### Task 3: StarSystem (quarterly 0–2)

**Files:**
- Create: `Assets/Scripts/Economy/StarSystem.cs` (+ `.meta`)
- Create: `Assets/Tests/EditMode/StarSystemTests.cs` (+ `.meta`)

**Interfaces:**
- Produces:
  - `int CurrentStars` (0–2)
  - `void Evaluate(TowerGrid grid, float averageStress, int population)`
  - Constants: `QuarterDays = 90`, pop/stress thresholds from spec
  - `bool CanBuild(RoomTypeSO type)` → `type == null || CurrentStars >= type.requiredStars`
  - `string LastResult` optional for HUD

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Evaluate_grants_one_star_when_thresholds_met()
{
    var stars = new StarSystem();
    var grid = /* lobby + enough fake population via Evaluate args */;
    stars.Evaluate(grid, averageStress: 10f, population: 10);
    Assert.AreEqual(1, stars.CurrentStars);
}

[Test]
public void Evaluate_demotes_when_stress_too_high()
{
    var stars = new StarSystem();
    // force to 1, then evaluate with stress 50, pop 10 → demote to 0
}

[Test]
public void CanBuild_blocks_elevator_at_zero_stars()
{
    var stars = new StarSystem();
    var elev = ElevatorType(); // requiredStars = 1
    Assert.IsFalse(stars.CanBuild(elev));
    stars.DebugSetStars(1); // or Evaluate to 1
    Assert.IsTrue(stars.CanBuild(elev));
}
```

Prefer a package-visible `internal` test hook or just Evaluate twice rather than DebugSetStars.

- [ ] **Step 2: FAIL then implement**

Demote current tier if fail; then try promote once. Cap at 2.

1★: pop ≥ 10, stress ≤ 40, `grid.HasLobby`  
2★: pop ≥ 30, stress ≤ 25, HasLobby, ≥1 elevator shaft in rooms

- [ ] **Step 3: PASS + Commit**

```bash
git add Assets/Scripts/Economy/StarSystem.cs Assets/Tests/EditMode/StarSystemTests.cs
git commit -m "feat: add quarterly star progression 0-2"
```

---

### Task 4: `requiredStars` on room types + asset gates

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs` — `public int requiredStars;`
- Modify: `Assets/ScriptableObjects/Rooms/ElevatorNormal.asset` and `Assets/Resources/Rooms/ElevatorNormal.asset` → `requiredStars: 1`
- Create (or duplicate): premium office/hotel/condo assets with `requiredStars: 2`, higher `buildCost`/`baseIncome`; register in Resources if needed
- Set condo `baseIncome` sale price on `Condo.asset` (e.g. 250000) if still 0
- Set hotel `baseIncome` daily if still 0 (e.g. 2000) so midnight pays

- [ ] **Step 1: Test that SO field defaults to 0 and elevator asset reads as 1** (EditMode load Resources)

- [ ] **Step 2: Apply YAML/asset edits**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Data/RoomTypeSO.cs Assets/ScriptableObjects/Rooms Assets/Resources/Rooms
git commit -m "feat: gate elevators and premium rooms by requiredStars"
```

---

### Task 5: Wire Economy + Stars into TowerSimulation + AgentSystem + BuildController

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` — after creating condo agent, call sale hook
- Modify: `Assets/Scripts/Build/BuildController.cs` — gate `TryPlaceSelected` / elevator extend/place by stars

**Interfaces:**
- `TowerSimulation` exposes `Economy`, `Stars`
- On clock day change: `economy.OnNewDay`; if `DayIndex % 90 == 0 && DayIndex > 0`: `stars.Evaluate(...)`
- Subscribe `DayRolled` or detect day index change in `Update`

- [ ] **Step 1: Failing integration-style EditMode test** (construct systems without MonoBehaviour where possible) proving day 90 evaluation path via direct calls already covered; add BuildController gate test if extractable — otherwise test `StarSystem.CanBuild` + document manual. Prefer:

```csharp
[Test]
public void Placement_helper_respects_stars()
{
    // If BuildController is hard to new(), test a static/helper:
    // StarSystem.CanBuild is enough; BuildController calls it.
}
```

In `BuildController.TryPlaceSelected` / `TryExtendElevator` / `TryPlaceSelected` for elevators:

```csharp
var sim = GetComponent<TowerSimulation>();
if (sim?.Stars != null && !sim.Stars.CanBuild(SelectedRoomType)) return false;
```

- [ ] **Step 2: Implement wiring**

```csharp
// TowerSimulation fields
EconomySystem _economy;
StarSystem _stars;
int _lastDayIndex;

public EconomySystem Economy => _economy;
public StarSystem Stars => _stars;

// Update after clock.Tick:
if (_clock.DayIndex != _lastDayIndex)
{
    // for each day crossed, run OnNewDay; for quarters crossed, Evaluate
    ...
    _lastDayIndex = _clock.DayIndex;
}
```

Handle multi-day jumps from 60x speed: loop from `_lastDayIndex+1` to `DayIndex`.

Condo sale in `SyncHomes` when adding condo resident:

```csharp
economy?.TrySellCondo(room, wallet);
```

Pass economy/wallet into SyncHomes or callback from simulation `OnGridChanged` after SyncHomes by scanning new condo agents — cleanest: `AgentSystem.SyncHomes` accepts optional `Action<RoomInstance> onNewCondoResident`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Simulation Assets/Scripts/Agents Assets/Scripts/Build
git commit -m "feat: wire economy and stars into simulation and build gates"
```

---

### Task 6: HUD — stars, pop, stress, net, time presets

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs` — `SetSpeedPreset(float multiplier, bool paused)` helper if useful

- [ ] **Step 1: Add HUD rows**

- Stars: `Stars: {n}/2`
- Pop / stress
- Last net from economy
- Buttons: `||` `1x` `2x` `5x` `10x` `60x` calling:

```csharp
simulation.Clock.Paused = paused;
if (!paused) simulation.Clock.MinutesPerRealSecond = multiplier;
```

Highlight active preset.

Grey locked room/tool buttons when `!stars.CanBuild(room)`.

- [ ] **Step 2: Manual Play Mode check** (or skip if Editor busy — note in commit)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/TowerHudController.cs
git commit -m "feat: HUD stars economy summary and time speed presets"
```

---

### Task 7: Camera scrollbars

**Files:**
- Modify: `Assets/Scripts/Camera/CutawayCamera.cs`
- Optionally modify: `TowerHudController` only if scrollbars live there — prefer `CutawayCamera.OnGUI` for scrollbars so pan works without build reference, with optional `BuildController`/`TowerGrid` bounds via serialized refs or `FindAnyObjectByType`

**Behavior:**
- Bottom horizontal scrollbar → camera X
- Right vertical scrollbar → camera Y
- Bounds: from grid MinX/MaxX and min/max room Y if grid available; else fallback `-5..40` X, `-5..30` Y
- Padding ±5 cells; account for `orthographicSize` so scrollbar maps view center

```csharp
void OnGUI()
{
    // HorizontalTrack bottom; VerticalTrack right
    // GUI.HorizontalScrollbar / VerticalScrollbar
}
```

Exclude scrollbar interaction from build clicks if needed (expand `IsPointerOverHud` or camera consumes GUI).

- [ ] **Step 1: Implement scrollbars + keep RMB pan / scroll zoom**

- [ ] **Step 2: Manual verify pan to tall/wide extents**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Camera/CutawayCamera.cs
git commit -m "feat: add camera pan scrollbars for tall wide towers"
```

---

### Task 8: README, PlayMode smoke, spec status Done

**Files:**
- Modify: `README.md` — Slice 4 play steps (stars, midnight income, time buttons, scrollbars)
- Modify: `Assets/Tests/PlayMode/TowerSandboxBuildSmokeTests.cs` — optional assert funds move after forced `Economy.OnNewDay`
- Modify: `docs/superpowers/specs/2026-07-29-build-a-tower-slice4-design.md` — Status → Done when shipping

- [ ] **Step 1: Extend smoke or EditMode coverage for day rollover if easy**

- [ ] **Step 2: README steps 17+**

- [ ] **Step 3: Commit**

```bash
git add README.md Assets/Tests docs/superpowers/specs/2026-07-29-build-a-tower-slice4-design.md
git commit -m "docs: Slice 4 play steps and closeout"
```

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| Midnight rent | 2, 5 |
| Elevator upkeep | 2 |
| Condo sale once | 2, 5 |
| Stars 0–2 quarterly | 3, 5 |
| Demotion | 3 |
| Elevator requiredStars=1 | 4, 5 |
| Premium 2★ variants | 4 |
| HUD ★/pop/stress/net | 6 |
| Time presets | 1, 6 |
| Camera scrollbars | 7 |
| README / Done | 8 |
| Stars 3–5 content | Non-goal |

## Plan self-review

- No TBDs left for MVP behavior; thresholds copied from spec.
- `GameClock.MinutesPerRealSecond` naming consistent across tasks 1 and 6.
- Multi-day 60x jumps handled in Task 5 day loop.
- CutawayCamera already pans/zooms — Task 7 extends, does not replace.
