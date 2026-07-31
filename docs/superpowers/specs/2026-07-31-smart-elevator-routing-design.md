# Build-A-Tower — Smart Elevator & Stairs Routing

**Date:** 2026-07-31  
**Status:** Approved  
**Depends on:** Slice #3 elevators + TransitRouter; commercial visits optional  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → **more transit** → evaluation/heatmaps → polish

## 1. Goals

Stop agents from all piling onto the first elevator shaft. Routes should prefer stairs for short climbs and, for longer trips, the **most efficient serving shaft** given walk distance, queue length, and car load — so a second (or third) shaft actually gets used.

### Success criteria

In Play Mode a player can:

1. Place two elevators serving overlapping floors and see both cars move / both landing queues used under load (not one idle forever).
2. See trips of **≤ 3 floors** still prefer **stairs** when a stairs path exists.
3. See agents waiting at a crowded shaft eventually peel off to a quieter shaft when it is clearly better.
4. Observe car boarding capped at **10** passengers; extras wait for the next stop.
5. Feel pressure to build more shafts as traffic grows (foundation for later multi-car upgrades).

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Stairs short trips | Keep `|Δfloor| ≤ 3` + valid stairs path → stairs only |
| Shaft pick | **Balanced score**: walk distance + estimated wait + load |
| While waiting | **Periodically re-score**; switch if another shaft wins by a threshold |
| Car capacity | **10** passengers (`ElevatorCar.Capacity`; was 8) |
| Multi-car / express / research upgrades | **Out of scope** this slice |
| Architecture | Cost-scored pick in `TransitRouter` + wait replan in `AgentSystem` |

## 3. Current bug (why shafts sit idle)

`ElevatorSystem.FindServing(floorA, floorB)` returns the **first** non-maintenance shaft that spans both floors. `TransitRouter.TryPlanTrip` uses that single result. Agents never compare alternatives, so later shafts stay idle while the first accumulates queues.

## 4. Route planning

### 4.1 Stairs preference (unchanged rule)

If `abs(goal.y - start.y) ≤ StairsPathfinder.MaxStairsFloorSpan` (3) **and** `StairsPathfinder.TryFindPath` succeeds → plan a single stairs/walk leg. Do **not** use elevators for that trip.

### 4.2 Elevator candidate set

A shaft is a candidate when:

- Not `InMaintenance`
- `Serves(start.y)` and `Serves(goal.y)`
- Walk path exists from `start` → landing cell `(shaft.X, start.y)`
- Walk path exists from `(shaft.X, goal.y)` → `goal`

### 4.3 Score (lower is better)

For each candidate:

```
walkCost = cells(start → entry) + cells(exit → goal)
waitEstimate = (queueAheadAtEntry + sameWayPassengers) / Capacity * BoardCycleMinutes
             + busyPenalty   // car far from entry or moving opposite direction
score = walkCost + WaitWeight * waitEstimate
```

Suggested MVP constants (tunable):

| Constant | Suggested | Role |
|----------|-----------|------|
| `ElevatorCar.Capacity` | **10** | Boarding hard cap |
| `WaitWeight` | ~2–4 | How much wait dominates walk cells |
| `BoardCycleMinutes` | ~DoorDwell + ~2–4 floors of travel | Rough “minutes per full load cycle” |
| `BusyPenalty` | small fixed minutes if car is far / wrong way | Prefer idle/near cars |
| `SwitchThreshold` | ≥ 25% better score **or** absolute delta ≥ N | Avoid thrashing |
| `RescoreIntervalGameMinutes` | ~5–15 | How often waiters re-evaluate |
| `SwitchCooldownGameMinutes` | ~30 | After a switch, don’t switch again immediately |

**Tie-break:** lower walk-from-exit (closer to destination), then lower walk-to-entry, then stable shaft id / X.

### 4.4 Plan output

Unchanged leg shape: Walk → Elevator → Walk, with `ElevatorX` / entry / exit floors set to the **winning** shaft.

## 5. Waiting replan

While `AgentPhase.WaitingAtElevator`:

1. On an interval (`RescoreIntervalGameMinutes`), recompute scores for the same `start`≈current landing and original `goal` (agent should retain destination floor / goal cell).
2. If best alternate score beats current shaft score by `SwitchThreshold` **and** cooldown elapsed:
   - Dequeue from current shaft (`RemoveFromAllQueues` / existing remove API)
   - Clear elevator wait fields
   - `ReplanTrip` (or plan specifically to the winning shaft)
3. Do **not** switch for tiny improvements (thrash guard).

Maintenance / span loss already triggers replan today — keep that path.

## 6. Capacity

- Set `ElevatorCar.Capacity = 10`.
- Existing board loop already stops when `PassengerIds.Count >= Capacity`; leftover queue waits.
- No UI change required beyond behavior; optional selection text later (“Capacity 10”).

## 7. Systems / files (expected)

| Area | Change |
|------|--------|
| `ElevatorCar.cs` | Capacity 8 → 10 |
| `ElevatorSystem.cs` | Enumerate all serving shafts; queue depth / load helpers for scoring |
| `TransitRouter.cs` | Score candidates; pick best instead of `FindServing` first-hit |
| `AgentSystem.cs` | Periodic wait re-score + switch with threshold/cooldown |
| `Agent.cs` | Optional: last switch time / cached trip goal for rescoring |
| Tests | Two shafts → not always first; stairs ≤3 still stairs; capacity 10; switch when alternate much better |
| README | Note smarter elevator choice + capacity 10 |

## 8. Out of scope

- Multiple cars per shaft  
- Express / service / freight elevators  
- Research-driven speed or capacity upgrades  
- Continuous global traffic simulation beyond agent trips  
- Changing the stairs floor span (stays 3)

## 9. Verification

- EditMode: with two overlapping shafts and asymmetric queues, planner selects the lighter/closer shaft, not always index 0.  
- EditMode: `|Δfloor| ≤ 3` with stairs still returns stairs-only plan.  
- EditMode: boarding never exceeds 10.  
- EditMode: waiter switches when alternate score beats threshold; does not oscillate every tick.  
- Play Mode: dual elevators both move under morning office load; crowded shaft sheds waiters to the other.

## 10. Roadmap note

This is a **more transit** / pathing slice that also reinforces deeper-economy pressure (build more shafts). Later: multi-car upgrades and express modes.
