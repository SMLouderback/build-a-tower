# Slice #3 checklist — Elevators (from tower-together → Build-A-Tower)

**Goal:** Standard elevator shafts that move agents farther than stairs allow, faster, without stair fatigue — without requiring full SimTower parity on day one.

**Upstream oracles:**  
- [specs/ELEVATORS.md](specs/ELEVATORS.md)  
- [specs/ROUTING.md](specs/ROUTING.md)  
- [specs/PEOPLE.md](specs/PEOPLE.md)  
- [AGENTS.md](AGENTS.md)  

**Our stack today:** `TowerGrid` · `StairsPathfinder` · `AgentSystem` · `GameClock` · Tilemap placeholders  

---

## 0. Scope lock (Slice #3 MVP)

| In | Out (later) |
|----|-------------|
| Placeable **standard** elevator shaft (SimTower-like width; we use cell units) | Express / service carriers |
| 1–N cars per shaft (start with **1 car**) | Full 8-car shafts |
| Served contiguous floor range | Sky-lobby express mapping |
| Agents queue, board, ride, exit | Housekeeping-only routes |
| Routing: stairs if \|Δfloor\| ≤ 3 **else** elevator | Escalators |
| Stress when waiting too long / no route | Exact tick-parity with EXE |
| HUD tool + cost | Full daypart schedule tables UI |

Parity with tower-together’s binary traces is a **stretch**, not a Slice #3 exit criterion.

---

## 1. Concept map (their terms → ours)

| tower-together | Build-A-Tower |
|----------------|---------------|
| Carrier (shaft + queues + served floors) | `ElevatorShaft` / `ElevatorCarrier` on grid + transit layer |
| Car (moving cab) | `ElevatorCar` (floor position, door/dwell state) |
| Route leg (stairs / elevator / transfer) | Extend pathfinder + `Agent` trip planner |
| Family dispatcher | `ScheduleDirector` / agent role policies |
| Floor 0 = lobby (clone) | Our floor **G** = lobby (`y == 0`); floors above are `1+`, basements `−1` and below |
| `resolve_sim_route_between_floors` | `TransitRouter.TryPlanLeg(from, to)` |
| Stair cost high + height limit | Keep \|Δfloor\| ≤ 3 for stairs-only; elevators for longer |
| Queue ring per floor | Per-shaft up/down wait lists at landing cells |

---

## 2. Implementation checklist

### A. Data / placement

- [x] `RoomTypeSO` (or `TransitTypeSO`): `isElevatorShaft`, size e.g. **(4 × H)** or place shaft then drag height
- [x] Placement rules: punch-through like stairs (overlap lobby/rooms) **or** dedicated shaft columns — pick one and document
- [x] Served range = contiguous floors the shaft occupies (cap later at ~30 floors like standard carriers)
- [x] Cost from funds wallet; demolish restores underlay (same pattern as stairs)
- [x] HUD: Elevator tool

### B. Runtime model

- [x] `ElevatorShaft`: id, minFloor, maxFloor, landings, wait queues (up/down)
- [x] `ElevatorCar`: currentFloor, direction, state (`Idle` / `Moving` / `DoorsOpen` / `Boarding`), passenger list, capacity (~17 for standard MVP)
- [x] Tick from `GameClock` / `TowerSimulation` (not Unity physics)

### C. Motion (simplified vs ELEVATORS.md)

- [x] Move **1 floor per N game seconds** (tune to feel faster than walking stairs)
- [x] Stop when passengers want off or queued demand in direction
- [x] Dwell / door open time stub (fixed, ignore daypart tables for MVP)
- [ ] Idle: return to home floor (lobby / mid) optional stub (deferred; car idles in place)

### D. Routing integration

- [x] Replace “stairs-only BFS” with **leg planner**:
  1. Same floor → walk
  2. \|Δfloor\| ≤ 3 and stairs chain exists → stairs leg
  3. Else if a shaft serves both floors (or transfer) → elevator leg
  4. Else → no route → stress
- [x] Agent phases: `Walking` / `WaitingAtElevator` / `Riding` / `Arrived`
- [x] Waiting: enqueue on shaft at current floor + direction; stress rises with wait time
- [x] Keep condo-never-hotel and other cross-rules from Slice #2

### E. Agents / schedules

- [x] Office / hotel trips longer than 3 floors use elevators when available
- [x] No path / full elevator → stress (reuse Slice #2 stress stub)
- [x] Do not block Slice #4 economy on elevator fidelity

### F. View / UX

- [x] Placeholder shaft tiles + moving car marker (colored rect on shaft column)
- [x] Ghost placement for shaft height
- [x] Help text: “Elevators for trips beyond 3 floors; stairs for short hops”

### G. Tests

- [x] EditMode: place shaft; car moves; queue boards/alights
- [x] EditMode: trip Δfloor=4 fails without elevator, succeeds with
- [x] PlayMode: lobby + tall stack + shaft → workers reach upper floors

---

## 3. Suggested file layout (Unity)

```
Assets/Scripts/Transit/
  StairsPathfinder.cs      (existing)
  TransitRouter.cs          (leg selection)
  ElevatorShaft.cs
  ElevatorCar.cs
  ElevatorSystem.cs         (tick all shafts)
```

Wire `ElevatorSystem` from `TowerSimulation` next to `AgentSystem`.

---

## 4. What to read first in upstream specs

1. `ROUTING.md` — one-leg-at-a-time model (adopt this idea)  
2. `ELEVATORS.md` — carrier vs car; standard mode defaults; capacity  
3. `PEOPLE.md` — in-transit vs idle state bands (simplify to our `AgentPhase`)  
4. Facility docs only as needed for later schedule fidelity  

---

## 5. Explicit non-goals for Slice #3

- Express elevators / sky lobbies every 15 floors  
- Service elevators / housekeeping routing  
- Exact schedule_index daypart tables  
- Escalators  
- Binary trace parity  
- Multiplayer / Cloudflare worker architecture  

---

## 6. Exit criteria

In Play mode:

1. Place a standard elevator shaft spanning more than 3 floors.  
2. Agents with destinations beyond stairs limit ride the elevator.  
3. Short trips still prefer stairs when available.  
4. Missing elevator on a tall trip → stress, no floating.  
5. Funds decrease on shaft build; demolish restores underlay.
