# Build-A-Tower — Slice #3 Design

**Date:** 2026-07-28  
**Status:** Approved (implement)  
**Depends on:** Slice #2 (stairs, agents, clock, stress stub, Floor G lobby)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Reference:** `docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md`  

## 1. Goals

Unlock tall towers: **normal elevators** that carry agents beyond the stairs floor limit, with place/extend placement, queues, and routing that still prefers stairs for short hops.

### Slice #3 success criteria

In Play mode a player can:

1. Place a **normal elevator** shaft (**1×2** starter) that punch-throughs lobby/rooms like stairs.
2. **Extend** that shaft up and/or down along the same column up to **30** floors total.
3. See a **car** move in the shaft and agents **queue / board / ride / exit**.
4. Agents with `|Δfloor| ≤ 3` and a stairs chain still use **stairs**.
5. Agents with longer trips use the elevator when the shaft serves both floors.
6. Tall trips with **no** elevator (and no legal stairs path) raise **stress** (no floating).
7. Funds decrease on place/extend; demolish restores underlay; stairs and shafts **never** share cells.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | **Normal elevators only** |
| Shaft width | **1** cell |
| Initial place | Height **2** (bottom origin + one floor above) |
| Extend | Anytime along same X; grow top and/or bottom; span **≤ 30** floors |
| Placement vs rooms | **Punch-through** (underlay bookmark like stairs) |
| Stairs ↔ elevator | **Hard ban** — neither may overlap the other’s cells |
| Cars per shaft | **1** |
| Passenger capacity | **8** (tunable constant) |
| Routing | Stairs if `\|Δfloor\| ≤ 3` and stairs chain exists; else elevator if shaft serves both floors; else fail |
| Express elevators (2-wide, lobby↔lobby) | Later slice |
| Sky lobbies every 15+ floors | Later slice |
| Multi-car / service / express modes | Out of scope |
| Exact SimTower daypart / binary parity | Out of scope |

## 3. Architecture

```
GameClock ──► TowerSimulation
                 ├── ElevatorSystem (shafts, cars, queues)
                 ├── TransitRouter (leg choice: walk / stairs / elevator)
                 ├── StairsPathfinder (short vertical only)
                 └── AgentSystem ──► AgentView
BuildController ──► TowerGrid / TilemapTowerView
```

- `TowerGrid` remains occupancy source of truth; elevator shafts are rooms (`isElevatorShaft`).
- `ElevatorSystem` owns runtime cars/queues; rebuilt or updated when shafts are placed/extended/demolished.
- `TransitRouter` replaces “stairs-only trip” entry from `AgentSystem`: plan a trip as walk segments + at most one elevator ride for MVP (no multi-shaft transfers required if one shaft covers both floors).

## 4. Placement

### 4.1 Room type

| Field | Value |
|-------|--------|
| `id` | `elevator_normal` |
| `isElevatorShaft` | true |
| `size` (asset default) | `(1, 2)` — width 1, height 2 |
| `allowAboveGround` / `allowBasement` | true |
| `buildCost` | per-cell cost (place = 2 × cell cost; extend = added cells × cell cost) |

### 4.2 Rules

- Requires lobby; X within lobby bounds.
- Punch-through: may overlap lobby, tenant rooms, scaffolding; bookmarks underlay; stairs **rejected** on any cell.
- Empty cells in the footprint need structural support equivalent to stairs (adjacent level or footprint continuity).
- Extend: same instance (or replace with taller instance) on same `origin.x`; new minY/maxY must keep contiguous span; `maxY - minY + 1 ≤ 30`.
- Demolish: restore underlay; clear car/queues for that shaft.

### 4.3 UX

- HUD **Elevator** tool; ghost shows 1×2 then extend preview when stretching ends.
- Help: “Elevators for trips beyond 3 floors; stairs for short hops. Shafts are 1 wide; extend up to 30 floors. Cannot overlap stairs.”

## 5. Runtime model

### 5.1 `ElevatorShaft`

- `InstanceId` / grid room link  
- `X`, `MinFloor`, `MaxFloor`  
- Per-floor wait queues: **Up** and **Down** lists of agent ids  
- One `ElevatorCar`

### 5.2 `ElevatorCar`

| Field | Notes |
|-------|--------|
| `Floor` | Current floor (int) |
| `Direction` | Up / Down / None |
| `State` | `Idle`, `Moving`, `DoorsOpen` |
| `Passengers` | Up to capacity 8 |
| Motion | **1 floor per 2 game minutes** default (tunable); doors dwell **1 game minute** |

**Dispatch (simplified):**

1. If passengers want off at current floor → open doors, alight.  
2. Else if queue at current floor in a useful direction → open doors, board until full.  
3. Else if demand above/below (queues or passenger destinations) → move one floor that way.  
4. Else → Idle; optional home: prefer **Floor G** if served, else **MinFloor**.

Tick from `TowerSimulation` alongside agents (game clock, not physics).

## 6. Routing & agents

### 6.1 `TransitRouter.TryPlanTrip(start, goal)`

1. Same cell → trivial.  
2. Same floor → walk path on occupied cells (existing horizontal connectivity).  
3. If `\|Δfloor\| ≤ StairsPathfinder.MaxStairsFloorSpan` **and** `StairsPathfinder` finds a path → stairs path.  
4. Else find a normal shaft with `MinFloor ≤ start.y, goal.y ≤ MaxFloor` (and walk access to landing cells on those floors). Plan: walk → shaft at start floor → ride → shaft at goal floor → walk.  
5. Else fail.

Landing cell for a shaft on floor `y` is `(shaft.X, y)` (the shaft cell itself is walkable for boarding).

### 6.2 Agent phases (additions)

Reuse existing phases where possible; add:

- `WaitingAtElevator` — enqueued; stress rises with wait time  
- `Riding` — locked to car floor until alight  

`Moving` remains for walking/stairs cell paths.

### 6.3 Stress

- No route → stress bump (Slice #2 behavior).  
- Waiting longer than a threshold (e.g. **10 game minutes**) → stress rises each tick while waiting.  
- Full car that never serves → eventually stress (same wait path).

Condo-never-hotel and Slice #2 schedules unchanged.

## 7. View

- Shaft cells: distinct placeholder color on rooms (or structure) layer; keep punch-through paint like stairs (shaft visible on top).  
- Car: simple marker (rect/sprite) at `(X + 0.5, Floor + 0.5)` updated each tick.  
- Agents waiting: stay on landing cell; riding: follow car position.

## 8. Testing

**EditMode**

- Place 1×2 shaft; reject overlap with stairs; extend to 30; reject 31.  
- Trip `|Δfloor| = 4` fails without shaft; succeeds with shaft serving both floors.  
- Car boards from queue and alights at destination floor.

**PlayMode**

- Lobby + offices stacked >3 floors + shaft → workers reach upper floors.  
- Short hop still uses stairs when both exist.

## 9. Out of scope

- Express elevators (2-wide, lobby↔lobby)  
- Sky lobbies / lobbies every 15+ floors  
- Multi-car shafts, service elevators, escalators  
- Multi-shaft transfer itineraries (agent needs one shaft covering both floors in MVP)  
- Exact tower-together schedule tables / binary parity  

## 10. Suggested files

```
Assets/Scripts/Transit/
  StairsPathfinder.cs      (existing — keep short-hop BFS)
  TransitRouter.cs
  ElevatorShaft.cs
  ElevatorCar.cs
  ElevatorSystem.cs
Assets/Scripts/Data/RoomTypeSO.cs   (+ isElevatorShaft)
Assets/ScriptableObjects/Rooms/ElevatorNormal.asset
```

Wire `ElevatorSystem` + `TransitRouter` from `TowerSimulation`.

## Spec self-review

- No TBDs left for MVP behavior; capacity/motion/dwell are concrete tunables.  
- Stairs ban and punch-through are consistent with placement and routing landings.  
- Scope is one slice: normal shaft + one car + router; express/sky lobbies explicitly deferred.  
- Multi-shaft transfers deferred explicitly so routing stays one elevator leg.
