# 3★ Stars, Ops Rooms & Service Workers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cap stars at 3★ with Security+Housekeeping+Maintenance facilities; add condition/dirty/broken rooms; hire visible maid and handyman agents; unlock Fine Dining, Conference, Security, Research at 3★ and HK/Maint at 2★.

**Architecture:** Extend `StarSystem` for 3★ gates. `RoomInstance` holds Condition/Dirty/Broken/StaffedWorkers. Midnight decay and wage debit in `EconomySystem`. Hotel checkout marks Dirty; `AgentSystem` skips Dirty/Broken for fills. Maid/Handyman roles pathfind jobs via existing transit. New room SOs + HUD Utility nesting.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode, existing StarSystem / AgentSystem / EconomySystem / BuildCatalog

## Global Constraints

- Combined slice: 3★ + unlock pack + condition + visible service agents
- 3★: pop ≥ 60, stress ≤ 20, lobby + elevator + Security + Housekeeping + Maintenance (Broken facilities do not count)
- HK/Maint unlock **2★**, auto-hire **1** on place, staff **0–4**; Security/Research/Conference/Fine Dining **3★**
- Wages: maid **$200/day**, handyman **$300/day** per hired worker
- Clean: basic hotel **15** min, premium **30** min; repair **+10** per **60** game minutes
- Condition 0–100, −1/day; &lt;70 stress/eval; &lt;40 income pause; 0 broken → bulldoze only
- Lobby / elevators / stairs do not degrade
- Service agents excluded from star population
- Spec: `docs/superpowers/specs/2026-07-31-stars3-ops-services-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/StarSystem.cs` | MaxStars 3; 3★ criteria; goals |
| `Assets/Scripts/Economy/RoomConditionRules.cs` | Degrade? pause income? clean minutes; thresholds |
| `Assets/Scripts/Core/RoomInstance.cs` | Condition, Dirty, Broken, StaffedWorkers |
| `Assets/Scripts/Economy/EconomySystem.cs` | Decay, income pause, wages |
| `Assets/Scripts/Agents/AgentEnums.cs` + `Agent.cs` + `AgentSystem.cs` | Maid/Handyman jobs; dirty on checkout; sync skips |
| `Assets/Scripts/Agents/AgentView.cs` | Service colors |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Dirty/broken tints |
| `Assets/Scripts/Data/BuildFamily.cs` / catalog / HUD | Utility rooms + Fine Dining |
| New Resources room assets | Six SOs |
| Tests + README | Coverage + play notes |

---

### Task 1: StarSystem 3★

**Files:**
- Modify: `Assets/Scripts/Economy/StarSystem.cs`
- Test: `Assets/Tests/EditMode/StarSystemTests.cs`

**Interfaces:**
- Produces: `MaxStars = 3`; `ThreeStarPopulation = 60`; `ThreeStarMaxStress = 20f`
- Produces: `MeetsCriteria(3)` requires elevator + non-broken Security + Housekeeping + Maintenance (match by `id` prefix `service_` or category/flags)
- Produces: `FormatNextStarGoal` lines for 3★ facilities

Identify facilities via room type id: `service_security`, `service_housekeeping`, `service_maintenance` (Broken rooms excluded: `room.IsBroken` or `Condition == 0`).

- [ ] **Step 1: Failing tests** — MaxStars 3; cannot promote to 3 without all three facilities; Broken HK fails gate; pop/stress thresholds.

- [ ] **Step 2: Implement** (temporary: detect facilities by id string; rooms land in Task 3).

- [ ] **Step 3: Commit** `feat: unlock 3-star criteria in StarSystem`

---

### Task 2: Condition / Dirty / Broken on RoomInstance

**Files:**
- Create: `Assets/Scripts/Economy/RoomConditionRules.cs`
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Test: `Assets/Tests/EditMode/RoomConditionTests.cs`

**Interfaces:**
- Produces: `Condition` (default 100), `Dirty`, `IsBroken => Condition <= 0`, `StaffedWorkers` (0–4)
- Produces: `RoomConditionRules.CanDegrade(RoomTypeSO)`, `IncomePaused(room)`, `CleanMinutes(hotelType)`, `ApplyMidnightDecay(room)`, `ApplyRepairTick(+10)`
- Produces: `MarkDirty()`, `ClearDirty()`, `SetStaffedWorkers(int)` clamped 0–4

```csharp
public static class RoomConditionRules
{
    public const int PauseBelow = 40;
    public const int StressBelow = 70;
    public const int RepairChunk = 10;
    public const float RepairMinutesPerChunk = 60f;
    public const float CleanBasicMinutes = 15f;
    public const float CleanPremiumMinutes = 30f;
    public static bool CanDegrade(RoomTypeSO t) =>
        t != null && !t.isLobby && !t.isElevatorShaft && !t.isStairs;
}
```

- [ ] **Step 1: Failing tests** for defaults, decay, broken, clean minutes basic vs premium, staff clamp.

- [ ] **Step 2: Implement + Commit** `feat: room condition dirty and broken state`

---

### Task 3: New room assets + catalog + auto-hire on place

**Files:**
- Create Resources (+ ScriptableObjects mirrors if project pattern requires):  
  `Housekeeping.asset`, `Maintenance.asset`, `SecurityPost.asset`, `ResearchLab.asset`, `Conference.asset`, `ShopFineDining.asset`
- Modify: `BuildCatalog.cs` / `RoomTypeSO.ResolvedBuildFamily` if needed for Utility
- Modify: `TowerHudController.cs` — load/add buttons
- Modify: `BuildController.cs` — on place HK/Maint set `StaffedWorkers = 1`
- Test: catalog / place auto-hire EditMode test

**Suggested SO fields:**

| Id | requiredStars | size | buildCost | notes |
|----|---------------|------|-----------|-------|
| service_housekeeping | 2 | 3×1 | 40000 | Utility |
| service_maintenance | 2 | 3×1 | 45000 | Utility |
| service_security | 3 | 2×1 | 35000 | Utility |
| service_research | 3 | 4×1 | 80000 | Utility |
| service_conference | 3 | 4×1 | 60000 | Utility |
| shop_food_fine | 3 | 4×1 | 200000 | TrafficVariable baseIncome 200 |

- [ ] **Step 1: Assets + failing catalog/place test for auto-hire 1**

- [ ] **Step 2: Wire HUD; Commit** `feat: 2-star ops rooms and 3-star unlock pack`

---

### Task 4: Economy decay, income pause, wages

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Test: `EconomySystemTests` / new ops economy tests

**Interfaces:**
- OnNewDay before/after income: apply decay to degradable rooms; if Condition hits 0, broken
- Skip recurring / shop earnings when `IncomePaused` or Broken
- Add expense: sum StaffedWorkers on HK rooms × 200 + Maint × 300
- Expose last wage expense if useful for HUD

- [ ] **Step 1: Failing tests** — decay −1; paused room pays 0; wages debit.

- [ ] **Step 2: Implement + Commit** `feat: condition decay income pause and service wages`

---

### Task 5: Dirty checkout + SyncHomes skips

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (hotel checkout path, SyncHomes)
- Test: EditMode hotel dirty / broken skip tests

**Interfaces:**
- On checkout: `homeRoom.MarkDirty()`
- SyncHomes: skip hotel if Dirty or Broken; skip office/condo if Broken
- Stress bump when home Condition &lt; 70 (simple +stress/day or on schedule tick)

- [ ] **Step 1: Failing tests**

- [ ] **Step 2: Implement + Commit** `feat: hotel dirty blocks check-in until cleaned`

---

### Task 6: Maid and Handyman agents

**Files:**
- Modify: `AgentEnums.cs`, `Agent.cs`, `AgentSystem.cs`, `AgentView.cs`
- Possibly `TowerSimulation` population count excludes service roles
- Test: service job EditMode tests (can drive with forced minutes / public hooks)

**Interfaces:**
- Roles: `Maid`, `Handyman`
- Sync staff agents to each HK/Maint room’s `StaffedWorkers`
- Maid: claim oldest Dirty hotel → trip → work CleanMinutes → ClearDirty
- Handyman: claim lowest Condition 1–99 degradable → trip → work 60 min → +10 Condition
- Idle at home when no jobs; excluded from Population

- [ ] **Step 1: Failing tests** — maid clears dirty after work time; handyman +10; ignores Broken/0.

- [ ] **Step 2: Implement pathing via existing BeginTrip/Replan patterns.

- [ ] **Step 3: Commit** `feat: visible maid and handyman service agents`

---

### Task 7: Visuals + Selection HUD

**Files:**
- Modify: `TilemapTowerView.cs` — tint Dirty (e.g. brownish overlay) and Broken (dark desaturated)
- Modify: `TowerHudController.cs` / selection help — Condition, Dirty/Broken, staff stepper calling `BuildController.TrySetStaffedWorkers`
- Modify: `BuildController.cs` — staff setter + repaint
- Test: optional format tests

- [ ] **Step 1: Implement UI + paint hooks when Condition/Dirty changes**

- [ ] **Step 2: Commit** `feat: condition dirty broken visuals and staff UI`

---

### Task 8: README + closeout

**Files:**
- Modify: `README.md`

- [ ] Document 3★ goals, HK/Maint at 2★, condition/dirty/broken, maid/handyman timings, wages
- [ ] Roslyn typecheck Scripts + EditMode
- [ ] Commit `docs: 3-star ops services play notes`

---

### Task 9: Closeout

- [ ] Spec success checklist
- [ ] Push only when asked

## Spec coverage

| Spec requirement | Task |
|------------------|------|
| MaxStars 3 + criteria | 1 |
| Condition/Dirty/Broken | 2, 4, 5 |
| Room pack + auto-hire | 3 |
| Decay, pause, wages | 4 |
| Dirty checkout / sync | 5 |
| Visible maid/handyman | 6 |
| Visuals + staff UI | 7 |
| README | 8 |
