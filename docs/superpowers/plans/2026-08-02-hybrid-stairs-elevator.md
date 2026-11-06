# Hybrid Stairs + Elevator Pathing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agents ride elevators to the closest served floor toward their destination, finish with stairs (and the reverse when starting off-shaft); soft-cap scoring + over-cap stress make long stairs a last resort, not a hard pathfinding wall.

**Architecture:** `StairsPathfinder` allows long paths when asked; `ElevatorRouting` owns comfort/over-cap score + stress affordance helpers; `TransitRouter` scores full-elevator vs closest-exit hybrid candidates; `AgentSystem` applies over-cap stair stress while walking `Stairs` legs and passes current stress into planning.

**Tech Stack:** Unity 6000.x, C#, NUnit EditMode, existing `TransitRouter` / `StairsPathfinder` / `ElevatorSystem` / `AgentSystem`

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-02-hybrid-stairs-elevator-design.md`
- Comfort band: `StairsPathfinder.MaxStairsFloorSpan = 3`
- Soft-cap penalty: `StairsOverCapPenaltyPerFloor = 40` (score units)
- Over-cap stress: `StairsOverCapStressPerFloor = 25`; refuse next over-cap floor at stress 100
- Prefer pure stairs when `|Δfloor| ≤ 3` and path exists
- One elevator shaft per planned trip; no multi-shaft transfers
- Conference / event revenue: out of scope
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Work on branch `feature/hybrid-stairs-elevator` (create from current HEAD if missing)

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Transit/StairsPathfinder.cs` | Optional max-span / allow-long paths; keep comfort const = 3 |
| `Assets/Scripts/Transit/ElevatorRouting.cs` | Over-cap score penalty + stress affordance helpers |
| `Assets/Scripts/Transit/ElevatorSystem.cs` | Shafts serving one floor; closest floor clamp helper |
| `Assets/Scripts/Transit/TransitRouter.cs` | Hybrid candidates + scoring + optional stress gate |
| `Assets/Scripts/Agents/Agent.cs` | Stair-effort floor counter for over-cap stress |
| `Assets/Scripts/Agents/AgentSystem.cs` | Apply stress on stair floor changes; plan with stress; refuse/replan |
| `Assets/Tests/EditMode/ElevatorRoutingTests.cs` | Hybrid + soft-cap preference tests |
| `Assets/Tests/EditMode/ElevatorTests.cs` | Update any obsolete hard-reject assumptions |
| `Assets/Tests/EditMode/StairsStressTests.cs` | Affordance + refuse math (pure helpers / small harness) |
| `README.md` | One-line play note if needed |
| Spec status | Mark Implemented when slice done |

---

### Task 1: StairsPathfinder long paths + ElevatorRouting soft-cap helpers

**Files:**
- Modify: `Assets/Scripts/Transit/StairsPathfinder.cs`
- Modify: `Assets/Scripts/Transit/ElevatorRouting.cs`
- Test: `Assets/Tests/EditMode/StairsStressTests.cs` (create; helpers + pathfinder span)

**Interfaces:**
- Produces:
```csharp
// StairsPathfinder
public const int MaxStairsFloorSpan = 3; // comfort band
public bool TryFindPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path);
// existing — keep behavior: reject when |Δfloor| > MaxStairsFloorSpan

public bool TryFindPath(
    Vector2Int start,
    Vector2Int goal,
    int maxFloorSpan,
    out List<Vector2Int> path);
// maxFloorSpan < 0 means unlimited floor span; otherwise reject when |Δfloor| > maxFloorSpan
// Existing 3-arg TryFindPath MUST call TryFindPath(start, goal, MaxStairsFloorSpan, out path)

// ElevatorRouting
public const int StairsComfortFloorSpan = 3; // mirror MaxStairsFloorSpan
public const float StairsOverCapPenaltyPerFloor = 40f;
public const float StairsOverCapStressPerFloor = 25f;

public static float StairsOverCapPenalty(int stairFloorSpan);
// max(0, stairFloorSpan - StairsComfortFloorSpan) * StairsOverCapPenaltyPerFloor

public static int MaxAffordableOverCapFloors(float currentStress);
// floors where currentStress + n * StairsOverCapStressPerFloor <= 100 (before clamp refusal)
// If currentStress >= 100 → 0
```

- [ ] **Step 1: Write failing tests** in `Assets/Tests/EditMode/StairsStressTests.cs`

```csharp
[Test]
public void StairsOverCapPenalty_zero_within_comfort()
{
    Assert.AreEqual(0f, ElevatorRouting.StairsOverCapPenalty(3));
    Assert.AreEqual(40f, ElevatorRouting.StairsOverCapPenalty(4));
    Assert.AreEqual(80f, ElevatorRouting.StairsOverCapPenalty(5));
}

[Test]
public void MaxAffordableOverCapFloors_respects_stress()
{
    Assert.AreEqual(4, ElevatorRouting.MaxAffordableOverCapFloors(0f)); // 0,25,50,75 → 4th lands at 100
    Assert.AreEqual(0, ElevatorRouting.MaxAffordableOverCapFloors(100f));
    Assert.AreEqual(1, ElevatorRouting.MaxAffordableOverCapFloors(90f)); // 90+25=115 would exceed — only 0 free? 
    // Spec: reject if applying next floor pushes past refusal. Affordable n where stress + n*25 <= 100.
    // 90 + 25 = 115 > 100 → 0 floors. Fix expectation to 0 for 90.
}

// Correct expectations:
// stress 0: n where 25n <= 100 → n <= 4
// stress 75: 25n <= 25 → n <= 1
// stress 76: 25n <= 24 → n = 0
// stress 100: 0
```

Also add a pathfinder test: stacked stairs spanning 4 floors — 3-arg `TryFindPath` fails; 4-arg with `maxFloorSpan: -1` succeeds. Reuse Lobby/Stairs helpers from `ElevatorTests` patterns (2×2 stairs stacked).

- [ ] **Step 2: Run tests — expect FAIL** (missing APIs)

Prefer: compile/test via existing project habit (Unity EditMode if Editor free; else Roslyn typecheck Scripts+EditMode). Do not invent parallel-cli.

- [ ] **Step 3: Implement helpers + pathfinder overload**

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Transit/StairsPathfinder.cs Assets/Scripts/Transit/ElevatorRouting.cs Assets/Tests/EditMode/StairsStressTests.cs
git commit -m "feat: allow long stair paths and soft-cap scoring helpers"
```

---

### Task 2: ElevatorSystem single-floor helpers

**Files:**
- Modify: `Assets/Scripts/Transit/ElevatorSystem.cs`
- Test: extend `Assets/Tests/EditMode/ElevatorRoutingTests.cs`

**Interfaces:**
- Consumes: existing `ElevatorShaftRuntime.Serves(int floor)`, `MinFloor`, `MaxFloor`
- Produces:
```csharp
public IReadOnlyList<ElevatorShaftRuntime> GetShaftsServingFloor(int floor);
// non-maintenance shafts where Serves(floor)

public static int ClosestFloorOnShaft(ElevatorShaftRuntime shaft, int targetFloor);
// Mathf.Clamp(targetFloor, shaft.MinFloor, shaft.MaxFloor)
```

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void GetShaftsServingFloor_returns_shafts_covering_floor()
{
    // Place elevator 0–4 at x=0; query floor 2 → contains shaft; floor 9 → empty
}

[Test]
public void ClosestFloorOnShaft_clamps_to_min_max()
{
    // Min=0 Max=10; target 11 → 10; target -1 → 0; target 5 → 5
}
```

- [ ] **Step 2: Implement**

- [ ] **Step 3: Tests PASS; Commit**

```bash
git commit -m "feat: query elevator shafts by single floor and closest stop"
```

---

### Task 3: TransitRouter hybrid planning + soft scoring

**Files:**
- Modify: `Assets/Scripts/Transit/TransitRouter.cs`
- Modify: `Assets/Tests/EditMode/ElevatorRoutingTests.cs`
- Modify: `Assets/Tests/EditMode/ElevatorTests.cs` only if an assertion becomes wrong

**Interfaces:**
- Consumes: `StairsPathfinder.TryFindPath(..., maxFloorSpan)`, `ElevatorRouting.StairsOverCapPenalty`, `ElevatorRouting.MaxAffordableOverCapFloors`, `ElevatorSystem.GetShaftsServingFloor`, `ClosestFloorOnShaft`
- Produces:
```csharp
public bool TryPlanTrip(Vector2Int start, Vector2Int goal, out List<TransitLeg> legs);
// delegates to overload with agentStress: 0f

public bool TryPlanTrip(
    Vector2Int start,
    Vector2Int goal,
    float agentStress,
    out List<TransitLeg> legs);
```

**Algorithm (replace current elevator-only block after the ≤3 stairs fast path):**

1. Keep same-floor walk; keep `|Δfloor| ≤ 3` stairs fast path using 3-arg `TryFindPath` (comfort).
2. Collect candidates, each with `score` and `legs`:
   - **Full elevator:** for each shaft in `GetServingShafts(start.y, goal.y)`, same as today (`TryShaftWalkPaths`), score = `ElevatorRouting.Score(walkCost, wait) + StairsOverCapPenalty(0)`.
   - **Hybrid (start on shaft):** for each shaft in `GetShaftsServingFloor(start.y)` where `ClosestFloorOnShaft(shaft, goal.y) != goal.y`:
     - entry = start.y, exit = ClosestFloorOnShaft(shaft, goal.y)
     - walk start→(shaft.X, entry); elevator entry→exit; stairs (shaft.X, exit)→goal with `TryFindPath(..., maxFloorSpan: -1)`
     - `S = |exit - goal.y|`; if `S - 3 > MaxAffordableOverCapFloors(agentStress)` skip
     - score = `Score(walkCells + stairsCells, wait) + StairsOverCapPenalty(S)`
   - **Hybrid reverse (start off shaft):** for each non-maintenance shaft, entry = ClosestFloorOnShaft(shaft, start.y); if entry == start.y skip (covered above); require stairs start→(shaft.X, entry) with unlimited span; exit = ClosestFloorOnShaft(shaft, goal.y); elevator; if exit != goal stairs exit→goal; apply affordance on **total** stair floor span `S = |start.y - entry| + |exit - goal.y|` (sum of vertical stair spans); soft-cap penalty on that `S`.
   - **Optional over-cap pure stairs:** if no full-elevator candidate exists, also try `TryFindPath(start, goal, -1)` as a stairs-only candidate with soft-cap penalty + affordance (so towers with only stairs still work for 4+ floors when connected).
3. Pick lowest score (tie-break like today: prefer shorter exit walk, then entry walk, then lower shaft X).
4. If none, return false.

**Leg shapes:**
- Full: Walk, Elevator, Walk (walks may be empty-ish single cell — keep existing style)
- Hybrid: Walk, Elevator, Stairs (and/or leading Stairs instead of Walk when start off shaft — use `TransitLegKind.Stairs` when the path changes floors, else Walk)

- [ ] **Step 1: Failing tests** in `ElevatorRoutingTests.cs`

```csharp
[Test]
public void TryPlanTrip_hybrid_elevator_then_stairs_above_shaft()
{
    // Lobby + offices/floors 1–11; elevator x=0 spans 0–10; stairs stacked so 10↔11 connected
    // Plan (lobby cell) → cell on floor 11
    // Expect: contains Elevator with ExitFloor=10 and a Stairs leg afterward
}

[Test]
public void TryPlanTrip_hybrid_stairs_then_elevator_below_start()
{
    // Same tower; plan floor 11 → lobby; Expect Stairs then Elevator EntryFloor=10
}

[Test]
public void TryPlanTrip_prefers_full_elevator_over_overcap_stairs_when_both_exist()
{
    // Elevator 0–4; ALSO continuous stairs 0–4 if buildable; normal wait
    // Span 4: if stairs path exists, score must still pick Elevator under empty queues
}

[Test]
public void TryPlanTrip_uses_stairs_when_span_le_3_even_with_elevators()
{
    // Keep existing test green
}
```

Build helper carefully: stairs are 2×2; stack flights sharing landings (see existing stairs placement tests / BuildController notes). Elevator extend 0→10.

- [ ] **Step 2: Implement `TransitRouter`**

- [ ] **Step 3: Tests PASS** (also run existing `ElevatorTests` router cases)

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: score hybrid elevator+stairs trips with soft cap"
```

---

### Task 4: Agent stair over-cap stress + plan with stress

**Files:**
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: extend `Assets/Tests/EditMode/StairsStressTests.cs`

**Interfaces:**
- Consumes: `TransitRouter.TryPlanTrip(..., agentStress)`, `ElevatorRouting.StairsOverCapStressPerFloor`
- Produces on `Agent`:
```csharp
/// <summary>Floors already crossed on the current Stairs leg (for comfort/over-cap).</summary>
public int StairsFloorsCrossedThisLeg { get; set; }
```

**Behavior:**
1. `BeginTrip` / `ReplanTrip` call `_router.TryPlanTrip(agent.Cell, to, agent.Stress, out legs)`.
2. `StartLeg` for non-elevator: if `leg.Kind == Stairs`, reset `StairsFloorsCrossedThisLeg = 0`.
3. In `StepMovement`, when advancing to a new cell where `target.y != previousCell.y` and current leg is Stairs:
   - `StairsFloorsCrossedThisLeg++`
   - If `StairsFloorsCrossedThisLeg > ElevatorRouting.StairsComfortFloorSpan`:
     - If `agent.Stress >= 100f`: refuse — do not commit to that cell; clear path; `ReplanTrip` (or Stall if replan fails).
     - Else: `agent.Stress = Min(100, Stress + StairsOverCapStressPerFloor)`.
4. Pure unit test for affordance already in Task 1; add a small static helper if easier to test refuse gate without full movement simulation:

```csharp
public static bool TryApplyStairFloorCrossing(
    Agent agent,
    int floorsCrossedAfterStep,
    out bool refused);
// mutates stress / returns refused — used by StepMovement
```

- [ ] **Step 1: Failing tests** for `TryApplyStairFloorCrossing`

```csharp
[Test]
public void Stair_crossing_within_comfort_adds_no_overcap_stress()
{
    var a = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
    a.Stress = 10f;
    Assert.IsTrue(AgentSystem.TryApplyStairFloorCrossing(a, floorsCrossedAfterStep: 3, out var refused));
    Assert.IsFalse(refused);
    Assert.AreEqual(10f, a.Stress);
}

[Test]
public void Stair_crossing_over_cap_adds_stress_and_refuses_at_100()
{
    var a = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
    a.Stress = 90f;
    Assert.IsTrue(AgentSystem.TryApplyStairFloorCrossing(a, 4, out var refused));
    Assert.IsFalse(refused);
    Assert.AreEqual(100f, a.Stress); // 90+25 capped
    Assert.IsFalse(AgentSystem.TryApplyStairFloorCrossing(a, 5, out refused));
    Assert.IsTrue(refused);
}
```

Note: Policy — when stress is 90, first over-cap floor applies +25 → 100 and **allows** that floor; next over-cap refuses because stress >= 100. Align `MaxAffordableOverCapFloors` with the same rule: affordable floors where after each apply stress stays ≤100, counting floors that land exactly on 100 as allowed.

Recalibrate Task 1 affordance if needed so plan-time and runtime match:
- `MaxAffordableOverCapFloors`: largest `n` such that for each `i=1..n`, `stress + i * 25` conceptually — actually runtime applies one floor at a time and clamps. Affordable while `stress < 100` before the step (can still take a floor that clamps to 100). So `n =` count of steps until stress would start a step already at 100.
- stress 0 → can take floors until after 4th over-cap stress=100; 5th refused → n=4
- stress 90 → 1st OK (→100), 2nd refused → n=1
- stress 100 → n=0

Update Task 1 test expectations to match if not already.

- [ ] **Step 2: Implement Agent + AgentSystem wiring**

- [ ] **Step 3: Tests PASS; Commit**

```bash
git commit -m "feat: apply over-cap stair stress and refuse at 100"
```

---

### Task 5: Docs + spec status

**Files:**
- Modify: `docs/superpowers/specs/2026-08-02-hybrid-stairs-elevator-design.md` — Status: Implemented
- Modify: `README.md` — brief note that elevators hand off to stairs above/below shaft range (≤3 comfort; longer costs stress)

- [ ] **Step 1: Update docs**
- [ ] **Step 2: Commit**

```bash
git commit -m "docs: mark hybrid stairs pathing implemented"
```

---

## Spec coverage check

| Spec item | Task |
|-----------|------|
| ≤3 stairs preference | Task 3 (fast path) |
| Full elevator candidates | Task 3 |
| Closest-exit hybrid + reverse | Task 3 |
| Soft-cap score penalty | Task 1 + 3 |
| Long stair pathfinding | Task 1 |
| Stress + refuse at 100 | Task 4 |
| Plan-time affordance | Task 3 + 4 |
| Elevator single-floor helpers | Task 2 |
| Tests listed in spec §7 | Tasks 3–4 |
| Non-goals (multi-shaft, conference) | Not implemented |

## Execution

User requested implementation immediately. Use **Subagent-Driven Development** (do not ask inline vs SDD). Create `feature/hybrid-stairs-elevator` before Task 1 if not on it.
