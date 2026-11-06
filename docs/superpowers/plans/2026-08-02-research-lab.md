# Research Lab Tech Tree Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a 5×3 research tech tree with one tower-wide project, pooled lab researchers, climate-scaled idle/active daily burn, ETA/$ estimates, pause decay, and buffs into shops/elevators/crime/HK/maint.

**Architecture:** Pure `ResearchSystem` owns tree progress, active project, pause/decay, and completion. `ResearchCatalog` defines branches/levels/work/buffs. `ResearchEffects` exposes multipliers. `EconomySystem` charges lab idle/active + research wages. `TowerSimulation` ticks progress and wires climate. HUD Selection drives Start/Pause.

**Tech Stack:** Unity 6000.x, C#, NUnit EditMode, existing EconomySystem / MarketClimate / BuildController / TowerHudController

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-02-research-lab-design.md`
- 5 branches × 3 levels; II needs I, III needs II (same branch)
- One active project; researcher pool = sum staff on non-broken `service_research`
- Base work I/II/III = 1440 / 4320 / 10080 game minutes
- `ResearcherSpeedBonus = 0.35` per researcher beyond the first
- Idle **$500**/non-broken lab/day; Active **$2000**/day while running; wage **$350**/researcher/day
- Climate mult = `MarketClimate.SpendMultiplier`
- Pause decay = **5%** of active project `BaseWorkMinutes` per day
- Auto-pause: broke at midnight charge, or pool == 0
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/ResearchCatalog.cs` | Branch/level defs, work minutes, buff magnitudes |
| `Assets/Scripts/Economy/ResearchSystem.cs` | State, start/pause, progress, decay, complete |
| `Assets/Scripts/Economy/ResearchEffects.cs` | Query multipliers from completed levels |
| `Assets/Scripts/Economy/EconomySystem.cs` | Research wages + idle/active burn |
| `Assets/Scripts/Build/BuildController.cs` | Auto-hire + staffed research |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Own/tick ResearchSystem |
| Buff hooks | Shop spend, ElevatorCar/Routing, CrimeSystem, RoomConditionRules |
| `Assets/Scripts/UI/TowerHudController.cs` | Research Selection UI |
| Tests | `ResearchSystemTests.cs`, wage/burn tests, effect tests |

---

### Task 1: ResearchCatalog + ResearchSystem core

**Files:**
- Create: `Assets/Scripts/Economy/ResearchCatalog.cs`
- Create: `Assets/Scripts/Economy/ResearchSystem.cs`
- Test: `Assets/Tests/EditMode/ResearchSystemTests.cs`

**Interfaces:**
```csharp
public enum ResearchBranch { Marketing, Elevator, Security, Housekeeping, Maintenance }

public static class ResearchCatalog
{
    public const int MaxLevel = 3;
    public const float ResearcherSpeedBonus = 0.35f;
    public const int IdlePerLabPerDay = 500;
    public const int ActivePerDay = 2000;
    public const float DecayFractionPerDay = 0.05f;
    public static int BaseWorkMinutes(int level); // 1440/4320/10080
    public static string BranchDisplayName(ResearchBranch b);
}

public sealed class ResearchSystem
{
    public ResearchBranch? ActiveBranch { get; }
    public int ActiveLevel { get; } // 1–3 when active
    public float ActiveProgress { get; } // work minutes done
    public bool IsPaused { get; }
    public bool IsComplete(ResearchBranch branch, int level);
    public int HighestCompleted(ResearchBranch branch); // 0–3
    public bool CanStart(ResearchBranch branch, int level);
    public bool TryStart(ResearchBranch branch, int level);
    public void Pause();
    public void TickProgress(float deltaGameMinutes, int researcherPool);
    public void TickDayDecay(); // call on day roll when paused
    public float WorkPerGameMinute(int researcherPool);
    public float EstimateEtaMinutes(int researcherPool);
    public int EstimateRemainingCost(int researcherPool, int nonBrokenLabs, float climateMult);
}
```

- [ ] **Step 1: Failing tests** — CanStart I yes / II without I no; TryStart I; TickProgress with 1 vs 4 researchers fills faster; TickDayDecay while paused reduces progress; complete unlocks II; 0 pool pauses.

- [ ] **Step 2: Implement catalog + system**

```csharp
// Speed
public static float WorkPerGameMinute(int n) =>
    n <= 0 ? 0f : 1f + (n - 1) * ResearcherSpeedBonus;

// On TickProgress: if paused or pool<=0 → set paused, return
// else add work; if >= BaseWork → complete, clear active

// On TickDayDecay: if paused && active → progress = max(0, progress - DecayFractionPerDay * BaseWork)
```

Store per-node progress in `Dictionary<(ResearchBranch,int), float>` and completed set.

- [ ] **Step 3: Tests PASS + Commit** `feat: research tech tree progress and pause decay`

---

### Task 2: Staff Research Lab + wages + idle/active burn

**Files:**
- Modify: `BuildController.cs` (`ApplyAutoHireOnPlace`, `IsStaffedServiceRoom` add `service_research`)
- Modify: `EconomySystem.cs` — `ResearchWagePerDay = 350`, wage switch; midnight research burn API
- Test: extend `EconomySystemTests` / `BuildCatalogTests`

**Interfaces:**
```csharp
// EconomySystem
public const int ResearchWagePerDay = 350;
public const string ResearchId = "service_research";
public int LastResearchBurn { get; private set; }

// In OnNewDay, after wages:
// burn = ResearchCatalog.IdlePerLabPerDay * nonBrokenLabCount
// if research.IsRunning && !research.IsPaused: burn += ActivePerDay
// burn = Round(burn * climate.SpendMultiplier)
// if wallet cannot pay full burn: pay what we can or pay 0 and research.Pause(); else Subtract
```

Prefer: try `wallet.TrySpend(burn)`; on failure call `research.Pause()` and skip burn (or charge partial — **spec: auto-pause**; charge **0** that day if cannot afford full burn).

Also need helper:
```csharp
public static int CountNonBrokenResearchLabs(TowerGrid grid);
public static int CountResearcherPool(TowerGrid grid);
```

- [ ] **Step 1: Failing tests** — auto-hire; wage; idle-only burn; idle+active; Recession cheaper than Boom; broke pauses.

- [ ] **Step 2: Implement + Commit** `feat: research lab staffing wages and daily burn`

---

### Task 3: Wire ResearchSystem into TowerSimulation

**Files:**
- Modify: `TowerSimulation.cs`

**Interfaces:**
- `public ResearchSystem Research => _research;`
- Awake: `new ResearchSystem()`
- Each Update after agents: `_research.TickProgress(dt, CountResearcherPool(grid))` and auto-pause if pool==0
- OnDayRolled: after economy (or before): if paused, `_research.TickDayDecay()`; economy needs research reference for burn — pass `_research` into `OnNewDay` or charge from simulation after economy

Recommended `OnNewDay` signature extension:
```csharp
economy.OnNewDay(grid, agents, wallet, stars, climateOffset, research, climateSpendMult);
```

- [ ] **Step 1: Wire + Commit** `feat: tick ResearchSystem from TowerSimulation`

---

### Task 4: ResearchEffects + buff hooks

**Files:**
- Create: `Assets/Scripts/Economy/ResearchEffects.cs`
- Modify: shop spend path, `ElevatorCar` / routing, `CrimeSystem`, `RoomConditionRules` / repair
- Test: `ResearchEffectsTests.cs`

**Interfaces:**
```csharp
public static class ResearchEffects
{
    public static float ShopSpendMultiplier(ResearchSystem r);
    public static float ElevatorSpeedMultiplier(ResearchSystem r);
    public static float ElevatorRoutingWaitWeightScale(ResearchSystem r); // II/III
    public static float CrimeSuppressionMultiplier(ResearchSystem r);
    public static float CleanMinutesMultiplier(ResearchSystem r);
    public static float RepairMinutesMultiplier(ResearchSystem r);
    public static float RepairChunkMultiplier(ResearchSystem r);
}
```

Cumulative from highest completed level per branch (spec table). Wire:

- Shop: multiply spend when recording visit spend
- Elevator: multiply `MinutesPerFloor` effective (divide time by speed mult)
- Crime: multiply baseline/patrol decay rates by suppression mult
- Clean/Repair: multiply minutes / chunk via RoomConditionRules taking optional ResearchSystem or static current effects set by simulation

Avoid global static mutable if possible: pass `ResearchEffects` snapshots or `ResearchSystem` into systems that already get climate. Minimal: `TowerSimulation` sets `ResearchEffects.Active = _research` each frame (pragmatic) **or** pass through Tick — prefer pass-through where easy; for RoomConditionRules static helpers, accept optional multipliers as parameters from AgentSystem.

- [ ] **Step 1: Tests for multipliers at levels 0/1/2/3**

- [ ] **Step 2: Hook + Commit** `feat: apply research buffs to economy transit crime ops`

---

### Task 5: Research Selection HUD

**Files:**
- Modify: `TowerHudController.cs` Selection when `service_research`
- Modify: `BuildController.GetSelectionSummary` optional lines

**UI (IMGUI):**
- Staff stepper (already if staffed)
- `Researchers in pool: N`
- For each branch: three buttons/labels I/II/III — locked / % / ✓
- Start / Pause on selected node
- ETA, est. $, idle/day, active/day, climate

Use `ResearchSystem` + `ResearchCatalog` estimate helpers.

Keep Goals fixed-height behavior.

- [ ] **Step 1: Implement UI**

- [ ] **Step 2: Play Mode smoke** (manual checklist in report)

- [ ] **Step 3: Commit** `feat: research lab selection UI for tech tree`

- [ ] **Step 4: Mark spec Implemented** when Play Mode acceptance done

---

## Self-review (plan vs spec)

| Spec | Task |
|------|------|
| Catalog / tree / progress / decay | 1 |
| Staff, wages, idle/active, climate | 2 |
| Sim tick / day | 3 |
| Buffs | 4 |
| Selection UI | 5 |
| Parallel projects / scientist agents | Out of scope |

No TBD in task contracts. Elevator II/III = speed + WaitWeight scale (not full dispatcher rewrite).
