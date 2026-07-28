# Build-A-Tower — Slice #2 Design

**Date:** 2026-07-28  
**Status:** Done  
**Depends on:** Slice #1 (grid, rooms, lobby, scaffolding, funds)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Next:** Slice #3 — elevators (`docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md`) 

## 1. Goals

Make the tower feel lived-in: **placeable stairs**, a **simple day clock**, and **agents** that commute via stairs-only pathing, with a **stress stub** when paths fail.

### Slice #2 success criteria

In Play mode a player can:

1. Place **Stairs** (2×2 cells) like other rooms.
2. See a **game clock** (time + day) on the HUD.
3. See **office workers** arrive (6–9am stagger), stay ~8 hours, leave via lobby.
4. See **hotel guests** check in from 4pm via lobby, check out by 11am next day.
5. See **condo residents** present at home (no outing schedules yet).
6. Agents path horizontally on occupied floors and vertically on stairs; trips with **|Δfloor| > 3** fail.
7. Failed/stuck pathing raises **stress**; HUD shows agent count + average stress.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Elevators | Slice #3 |
| Schedules | Lite: office commute + hotel check-in/out only |
| Stairs placement | Segment size `(2, 2)`; stair run BL→UR; stack with connecting-floor share |
| Stairs travel limit | `|destFloor - startFloor| ≤ 3` via contiguous stairs chain |
| Condo | Present at home; never uses hotels (enforced) |
| Retail / restaurants / happy hour | Slice #4 |
| Subway / parking arrivals | Later |
| Agent rendering | Colored dots (Agents layer), not ECS |
| Economy payouts | Not in Slice #2 |

## 3. Architecture

```
GameClock ──► ScheduleDirector ──► AgentSystem ──► StairsPathfinder ──► TowerGrid
                                      │
                                      ▼
                                  AgentView (dots)
BuildController ──► TowerGrid / TilemapTowerView
```

- `TowerGrid` remains occupancy source of truth.
- Dirty flag / rebuild transit graph when rooms/stairs change.

## 4. Stairs

| Field | Value |
|-------|--------|
| `id` | `stairs` |
| `category` | `Transit` |
| `isStairs` | `true` |
| `size` | `(2, 2)` — width 2, spans two floors; stair run BL→UR |
| Stairs stacking | Connecting floor may share; corner roles 1 and 4 must not occupy the same cell; roles 2 and 3 may |
| Lobby floor | **Floor G = `y == 0`** (ground / 1st floor). Floors above `1+`; basements `−1` and below. Stairs from G reach B1 (origin `y = −1`) or floor 1 (origin `y = 0`). |
| Placement | Existing `CanPlace` + support rules |
| Demolish | Same scaffolding fill as other rooms |

**Travel (pathfinder):** A journey is legal only if the absolute floor span ≤ 3 and stairs segments form a contiguous vertical chain connecting those floors at reachable X.

## 5. Day clock

- Accelerated sim time (default ~1 real second = 1 game minute; tunable).
- Tracks minutes-of-day and day index (weekday name for HUD).
- No income / star logic.

## 6. Agents (schedules lite)

| Role | Binding | Behavior |
|------|---------|----------|
| Office worker | Office room (`maxOccupants`, default 2) | Arrive lobby→office 6–9am; 8h at desk; leave via lobby; ~5–10% overtime stub |
| Hotel guest | Hotel room (1) | From outside via Floor G lobby from 4pm; overnight; out by 11am |
| Condo resident | Condo | Spawn/stay at home; no trips in S2 |
| Retail | — | No customers in S2 |

- Outside = spawn/despawn at lobby edge cells.
- Condo agents must not path to hotels.

## 7. Pathfinding

- Walkable: occupied cells (rooms, lobby, scaffolding, stairs).
- Horizontal: adjacent cells same floor.
- Vertical: stairs footprint links floor `y` ↔ `y+1` for each X in the stairs width.
- BFS from start to goal; reject if floor span of journey > 3.

## 8. Stress stub

- Per-agent stress `0–100`.
- Increases when no path / stuck on a required trip.
- Decays slowly while idle or pathing successfully.
- HUD: `Agents: N | Stress: avg`.

## 9. Out of scope

Elevators; retail/restaurant curves; happy hour; condo F&B; subway/parking; economy/stars; evaluation overlays; ECS; polished art / 2.5D.

## 10. Verification

- EditMode: stairs place; pathfinder ≤3 floors; no path when disconnected / too tall.
- PlayMode: lobby + office + stairs → workers move on clock; hotel check-in/out; HUD clock/agents/stress.
- Manual: demolish under stairs leaves scaffolding; rebuild over studs.

## Spec self-review

- No placeholders or TBDs in success criteria.
- Elevators explicitly deferred (no contradiction with stairs ≤3).
- Schedules lite matches locked decisions; full American-day curves documented as Slice #4.
- Scope fits one slice: transit graph + agents + clock, not economy.
