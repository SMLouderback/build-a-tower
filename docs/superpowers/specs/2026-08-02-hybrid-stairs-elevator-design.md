# Build-A-Tower — Hybrid Stairs + Elevator Pathing

**Date:** 2026-08-02  
**Status:** Implemented  
**Depends on:** `TransitRouter`, `StairsPathfinder`, `ElevatorSystem`, agent trip execution + stress  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → more transit → evaluation/heatmaps → polish  
**Related (later):** Conference / event-hall revenue (separate slice)  
**Supersedes (behavior):** Hard reject of stair journeys with `|Δfloor| > MaxStairsFloorSpan` as the only long-haul option when elevators stop short of the goal

## 1. Goals

Make **stairs useful at higher elevations**: agents ride elevators to the **closest served floor** toward their destination, then use stairs for the remaining gap (and the reverse when starting above/below a shaft). Keep **short stair hops (≤3 floors)** preferred for nearby shops/amenities. Long stair climbs remain possible with **soft score penalties** and **over-cap stress**, not a hard pathfinding wall.

### Success criteria

In Play Mode / EditMode a player (or test) can confirm:

1. Elevator serves floors through **10**, stairs connect **10↔11** → agents going lobby→11 ride to 10 then take stairs (and reverse going down).
2. Agents on floor **8** or **10** visiting amenities on floor **9** use stairs when a path exists (≤3), without requiring an elevator stop on 9.
3. When an elevator serves **both** start and goal, agents prefer the elevator under normal waits; long stairs (4+ floors) only win under **extreme wait** or when **no shaft reaches the goal**.
4. Climbing stairs past 3 floors adds stress per extra floor; at **100 stress** the agent **refuses** another over-cap floor and replans / fails as today if no alternate.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Approach | **Closest-exit hybrid + scored alternatives** |
| Short hops | Prefer pure stairs when `\|Δfloor\| ≤ 3` and a path exists (even if elevators exist) |
| Full elevator | Candidate when a shaft serves **both** start and goal floors |
| Hybrid shape | Elevator to **closest shaft floor to goal**, then stairs; reverse = stairs to shaft then elevator (then stairs only if goal still outside shaft) |
| Candidate pick | **Score** full-elevator vs hybrid (and cheap stairs); take best |
| Stairs > 3 vs elevator | **Soft cap** — steep per-floor score penalty; 4+ stairs mainly when no elevator to goal or wait is extreme |
| Stairs > 3 traversability | **Allowed** with stress; not a hard pathfinder reject for hybrid / long stairs |
| Comfort constant | `StairsPathfinder.MaxStairsFloorSpan = 3` (rename/docs as comfort band; still the soft-cap threshold) |
| Multi-shaft transfers | **Out of scope** (one elevator shaft per planned trip) |
| Conference / event revenue | **Out of scope** (separate slice) |

## 3. Routing rules

Implemented primarily in `TransitRouter.TryPlanTrip`.

1. **Same floor** — walk only (unchanged).
2. **Comfortable stairs** — if `|startFloor − goalFloor| ≤ 3` and `StairsPathfinder` finds a path, return a single `Stairs` (or walk) leg. Unchanged short-hop preference.
3. **Full elevator candidates** — for each shaft with `Serves(startFloor) && Serves(goalFloor)`, build walk → elevator(start→goal) → walk, score with existing wait estimates.
4. **Hybrid candidates** — for each shaft that can participate:
   - **Start on shaft:** entry = start floor; exit = `Clamp(goalFloor, MinFloor, MaxFloor)`; if exit == goal, this is full elevator (covered above); if exit ≠ goal, elevator then stairs exit→goal.
   - **Start off shaft (above/below):** stairs/walk from start to a valid **entry floor** on the shaft (closest shaft floor toward start / reachable within pathfinding), then elevator toward goal (exit = clamp goal), then stairs if needed.
5. **Score all valid candidates**; return the best as `List<TransitLeg>` (`Walk` / `Elevator` / `Stairs`).
6. **No valid candidate** — return false (agent stuck / stress as today).

### Closest exit

For a shaft and goal floor `G`:

`exitFloor = Clamp(G, shaft.MinFloor, shaft.MaxFloor)`.

Stairs remaining span `S = |exitFloor − G|`. Hybrid is only useful when `S > 0` and a stairs path exists from `(shaft.X, exitFloor)` (or exit cell) to the goal.

## 4. Soft-cap scoring

Reuse `ElevatorRouting.Score(walkCost, waitEstimate, waitWeightScale)` as the base.

**Additions:**

| Constant | Proposed default | Role |
|----------|------------------|------|
| `StairsComfortFloorSpan` | `3` (= `MaxStairsFloorSpan`) | Comfort band |
| `StairsOverCapPenaltyPerFloor` | `40` | Added to trip score per stair floor past comfort |

For a candidate whose **stair portion** has floor span `S`:

- If `S ≤ 3`: no over-cap penalty.
- If `S > 3`: `score += (S - 3) * StairsOverCapPenaltyPerFloor`.

Pure-stairs trips with `S > 3` are only considered when competing against elevators (or when no elevator exists); under normal waits they lose to full elevator. Extreme wait can still beat **comfortable** stairs (`S ≤ 3`); beating over-cap stairs requires very large waits or no elevator to the goal.

Tune defaults in Play Mode if elevators feel too sticky or stairs too attractive.

## 5. Runtime stair stress

In `AgentSystem`, while consuming a `Stairs` leg:

| Rule | Behavior |
|------|----------|
| Floors 1–3 of continuous stair effort | No *extra* over-cap stress from this rule |
| Each floor beyond 3 | `Stress += StairsOverCapStressPerFloor` (proposed **25**), clamped to 100 |
| Next over-cap floor would hit refusal | If stress is already 100, or applying the next floor’s stress is refused by policy: **do not enter that floor**; cancel remaining stair progress; **replan**. If still only reachable via more over-cap stairs, fail/stuck as today |

**Plan-time gate:** reject a candidate if the agent’s current stress cannot absorb the required over-cap floors before refusal (e.g. stress 90 and two over-cap floors at 25 each → only one floor affordable → candidate invalid if `S - 3 > affordable`).

Elevator-wait stress and other stress sources remain unchanged.

## 6. Components

| Component | Change |
|-----------|--------|
| `TransitRouter` | Candidate generation (full + hybrid); soft-cap scoring; keep ≤3 pure-stairs fast path |
| `StairsPathfinder` | Allow paths with `|Δfloor| > 3` when requested; keep `MaxStairsFloorSpan = 3` as comfort constant for router/agents |
| `ElevatorSystem` | Helpers: shafts serving a single floor; closest floor on shaft to target (clamp) |
| `AgentSystem` | Over-cap stair stress + refuse/replan |
| `ElevatorRouting` (optional) | Shared helpers for over-cap penalty / constants |

**Unchanged:** car movement, queues, wait estimate formulas, research buffs, crime.

## 7. Tests

EditMode (extend `ElevatorRoutingTests` / `ElevatorTests` / agent stress tests as needed):

1. Shaft max 10 + stairs 10–11 → trip lobby→11 uses elevator then stairs; reverse uses stairs then elevator.
2. Floors 8↔9 with stairs path → stairs preferred even if elevators exist.
3. Shaft serves start and goal, normal wait → full elevator beats over-cap pure stairs.
4. No shaft to goal, hybrid gap ≤3 → hybrid wins when stairs connect.
5. Over-cap stress: after enough over-cap floors, agent at 100 refuses further climb / candidate rejected at plan time.

## 8. Non-goals

- Transferring across **two elevator shafts** in one trip.
- Changing stair **build** footprint or stacking rules.
- Conference / Comic-con event halls and population×star revenue.
- New HUD chrome for transit mode (optional later).

## 9. Rollout

1. Spec approved → implementation plan → Subagent-Driven Development on a feature branch.
2. EditMode tests first where practical; Play Mode checklist for lobby→above-shaft floors and local amenity hops.
3. Tune `StairsOverCapPenaltyPerFloor` / `StairsOverCapStressPerFloor` after one Play Mode pass.
