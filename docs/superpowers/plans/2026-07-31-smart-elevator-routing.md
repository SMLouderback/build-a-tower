# Smart Elevator & Stairs Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agents prefer stairs for ≤3 floors and otherwise pick the best elevator shaft by walk + wait + load, re-scoring while waiting so multiple shafts share traffic; car capacity becomes 10.

**Architecture:** `ElevatorRouting` (or helpers on `ElevatorSystem`) scores candidate shafts; `TransitRouter.TryPlanTrip` picks the lowest score instead of first-hit `FindServing`. `AgentSystem` periodically re-scores waiters and switches when an alternate beats a threshold with cooldown. Capacity constant bump only.

**Tech Stack:** Unity 6000.4.7f1, C#, existing `TransitRouter` / `ElevatorSystem` / `StairsPathfinder`, NUnit EditMode tests

## Global Constraints

- Stairs when `|Δfloor| ≤ 3` and stairs path exists (unchanged span)
- Balanced score: walk cells + weighted wait estimate + busy penalty
- Waiters re-score and may switch (≥25% better or absolute delta) with cooldown
- `ElevatorCar.Capacity = 10`
- No multi-car / express / research upgrades this slice
- Spec: `docs/superpowers/specs/2026-07-31-smart-elevator-routing-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Transit/ElevatorCar.cs` | Capacity 10 |
| `Assets/Scripts/Transit/ElevatorRouting.cs` | Score constants + score formula (pure helper) |
| `Assets/Scripts/Transit/ElevatorSystem.cs` | List serving shafts; queue depth / same-way passenger helpers |
| `Assets/Scripts/Transit/TransitRouter.cs` | Pick best shaft when planning elevator trips |
| `Assets/Scripts/Agents/Agent.cs` | Trip goal + switch cooldown fields |
| `Assets/Scripts/Agents/AgentSystem.cs` | Wait rescore / switch |
| `Assets/Tests/EditMode/ElevatorRoutingTests.cs` | New scoring / pick / switch tests |
| Update `ElevatorTests.cs` | Capacity 10 expectations |
| `README.md` | Dual-elevator + capacity note |

---

### Task 1: Capacity 10 + queue/load helpers + score function

**Files:**
- Modify: `Assets/Scripts/Transit/ElevatorCar.cs`
- Create: `Assets/Scripts/Transit/ElevatorRouting.cs`
- Modify: `Assets/Scripts/Transit/ElevatorSystem.cs`
- Test: `Assets/Tests/EditMode/ElevatorRoutingTests.cs`; update capacity assertions in `ElevatorTests.cs`

**Interfaces:**
- Produces: `ElevatorCar.Capacity = 10`
- Produces: `ElevatorSystem.GetServingShafts(int floorA, int floorB)` → list (non-maintenance)
- Produces: `int QueueLength(shaft, floor, direction)`; `int SameWayPassengerCount(shaft, direction)` (or passengers with dest in direction)
- Produces: `ElevatorRouting.EstimateWaitMinutes(shaft, entryFloor, direction)`; `ElevatorRouting.Score(walkCost, waitEstimate)`; constants `WaitWeight`, `BoardCycleMinutes`, `BusyPenalty`, `SwitchImproveRatio = 0.25f`, `RescoreIntervalGameMinutes`, `SwitchCooldownGameMinutes`

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Capacity_is_ten()
{
    Assert.AreEqual(10, ElevatorCar.Capacity);
}

[Test]
public void Score_penalizes_longer_queues()
{
    // same walkCost; larger waitEstimate → higher score
    Assert.Greater(
        ElevatorRouting.Score(walkCost: 10, waitEstimate: 5f),
        ElevatorRouting.Score(walkCost: 10, waitEstimate: 1f));
}

[Test]
public void GetServingShafts_returns_all_overlapping_non_maintenance()
{
    // two shafts both serve 0..4 → count 2; maintain one → count 1
}
```

- [ ] **Step 2: Run — expect fail / old capacity 8**

- [ ] **Step 3: Implement**

```csharp
// ElevatorCar
public const int Capacity = 10;

public static class ElevatorRouting
{
    public const float WaitWeight = 3f;
    public const float BoardCycleMinutes = 2f;
    public const float BusyPenaltyMinutes = 1.5f;
    public const float SwitchImproveRatio = 0.25f;
    public const float RescoreIntervalGameMinutes = 10f;
    public const float SwitchCooldownGameMinutes = 30f;

    public static float Score(int walkCost, float waitEstimate) =>
        walkCost + WaitWeight * waitEstimate;

    public static bool IsMeaningfullyBetter(float currentScore, float alternateScore)
    {
        if (alternateScore >= currentScore) return false;
        var improve = (currentScore - alternateScore) / Math.Max(1f, currentScore);
        return improve >= SwitchImproveRatio;
    }
}
```

Busy estimate helper can live on `ElevatorSystem` using car floor/direction vs entry.

Update `ElevatorTests` boarding loop that used `Capacity + 1` — still valid with 10.

- [ ] **Step 4: Tests PASS — Commit**

```bash
git commit -m "feat: elevator capacity 10 and routing score helpers"
```

---

### Task 2: TransitRouter picks best shaft

**Files:**
- Modify: `Assets/Scripts/Transit/TransitRouter.cs`
- Test: `ElevatorRoutingTests.cs` / extend `ElevatorTests.cs`

**Interfaces:**
- Consumes: serving list + score helpers + stairs pathfinder for walk costs
- Produces: `TryPlanTrip` elevator branch chooses min score (not first `FindServing`)

- [ ] **Step 1: Failing test — two shafts, crowded first, empty second**

Build lobby + tall structure + two elevator shafts at different X spanning floors 0–4+. Path from a start near shaft B toward a goal near shaft B. Enqueue many fake agents on shaft A at entry floor (or set queue lengths via public test hooks). Assert planned elevator leg uses shaft B’s X, not A.

Also keep: `|Δfloor| ≤ 3` with stairs → no Elevator leg.

- [ ] **Step 2: Implement in `TryPlanTrip`**

Replace:

```csharp
var shaft = _elevators.FindServing(start.y, goal.y);
```

with:

```csharp
ElevatorShaftRuntime best = null;
var bestScore = float.MaxValue;
var bestExitWalk = int.MaxValue;
var bestEntryWalk = int.MaxValue;
foreach (var shaft in _elevators.GetServingShafts(start.y, goal.y))
{
    var entry = new Vector2Int(shaft.X, start.y);
    var exit = new Vector2Int(shaft.X, goal.y);
    if (!_stairs.TryFindPath(start, entry, out var toShaft) || toShaft == null) continue;
    if (!_stairs.TryFindPath(exit, goal, out var fromShaft) || fromShaft == null) continue;
    var direction = goal.y >= start.y ? ElevatorDirection.Up : ElevatorDirection.Down;
    var walkCost = toShaft.Count + fromShaft.Count;
    var wait = _elevators.EstimateWaitMinutes(shaft, start.y, direction);
    var score = ElevatorRouting.Score(walkCost, wait);
    // tie-break: fromShaft.Count, then toShaft.Count, then shaft.X
    if (score < bestScore - 1e-3f || (≈score && better tie-break))
    {
        best = shaft; bestScore = score; /* keep toShaft/fromShaft */
    }
}
if (best == null) return false;
// build Walk → Elevator → Walk legs as today
```

Keep `FindServing` for callers that need “any” / exact-X lookup, but planning must not use first-hit alone.

- [ ] **Step 3: Tests PASS — Commit**

```bash
git commit -m "feat: TransitRouter picks lowest-cost elevator shaft"
```

---

### Task 3: Waiting re-score and shaft switch

**Files:**
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: `ElevatorRoutingTests.cs` or `CommercialVisitTests`-style agent wait test

**Interfaces:**
- Produces: `Agent.ElevatorTripGoalCell` (or reuse `GoalCell` / `ElevatorDestFloor`); `LastElevatorSwitchTotalMinutes`; `NextElevatorRescoreTotalMinutes`
- Produces: while `WaitingAtElevator`, after interval, if alternate meaningfully better → `RemoveFromQueues` + replan

- [ ] **Step 1: Failing test**

Agent waiting on crowded shaft A; shaft B empty and serving; after enough game minutes / forced rescore call, agent leaves A’s queue and plans toward B (or public `TryRescoreElevatorWait(agent, …)` returns true once).

Second case: tiny improvement → no switch (cooldown / threshold).

- [ ] **Step 2: Implement**

When entering elevator wait (existing enqueue path), stamp `NextElevatorRescoreTotalMinutes = clock.TotalMinutes + RescoreInterval`.

In tick for `WaitingAtElevator`:

```csharp
if (clock.TotalMinutes < agent.NextElevatorRescoreTotalMinutes) return;
agent.NextElevatorRescoreTotalMinutes = clock.TotalMinutes + ElevatorRouting.RescoreIntervalGameMinutes;
if (clock.TotalMinutes < agent.LastElevatorSwitchTotalMinutes + ElevatorRouting.SwitchCooldownGameMinutes)
    return;
// score current vs best alternate for (agent.Cell ≈ landing, goal)
if (!ElevatorRouting.IsMeaningfullyBetter(currentScore, bestScore)) return;
_elevators.RemoveFromQueues(agent.Id);
agent.LastElevatorSwitchTotalMinutes = clock.TotalMinutes;
ReplanTrip(agent, allowReplan: true);
```

Ensure `ReplanTrip` / `BeginTrip` still work when already on a landing.

- [ ] **Step 3: Tests PASS — Commit**

```bash
git commit -m "feat: elevator waiters switch to better shafts"
```

---

### Task 4: README + closeout

**Files:**
- Modify: `README.md`
- Spec coverage / plan checkboxes

- [ ] **Step 1: README** — note stairs ≤3; elevators chosen by walk+queue+load; capacity 10; waiters may change shafts

- [ ] **Step 2: Roslyn typecheck Scripts + EditMode**

- [ ] **Step 3: Commit**

```bash
git commit -m "docs: smart elevator routing play notes"
```

---

### Task 5: Closeout

- [ ] Final review against spec success criteria
- [ ] Push only when asked

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| Capacity 10 | 1 |
| Score walk+wait+load | 1–2 |
| Pick best shaft at plan time | 2 |
| Stairs ≤3 unchanged | 2 |
| Wait re-score + threshold switch | 3 |
| Cooldown / no thrash | 3 |
| README | 4 |
