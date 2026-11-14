# Build-A-Tower — Parking Ramp

**Date:** 2026-08-04  
**Status:** Implemented  
**Depends on:** Underground Parking + Valet (`2026-08-03-five-star-parking-valet-design.md`)  
**Parent:** 5★ parking arrivals foundation

## Goals

1. Add a **Parking Ramp** room that visually connects Lobby/B1 to deeper basement parking (car stairs).  
2. **B2+ parking** only counts for stalls / arrivals when a continuous ramp chain reaches **B1 or Lobby** and the lot is in a **contiguous garage run** touching that ramp.  
3. **B1 parking** stays accessible without a ramp (street/lobby access level).  
4. Same-floor lots can extend access by abutting other accessible lots (no gaps).

## Locked decisions

| Decision | Choice |
|----------|--------|
| Scope | Visual + access gate for deep parking (option 2) |
| Size | **3×2** (two floors) |
| Family | Transit |
| Unlock | **4★** |
| Cost / upkeep | **$25,000** / **$200/day** |
| B1 parking | Always eligible (with Valet) |
| B2+ parking | Needs ramp chain to B1 or Lobby **and** a contiguous parking link (lot touches that ramp, or touches parking that does) |
| Agent pathing | People do **not** use ramps; spawn-at-stall arrivals unchanged when eligible |
| Lobby extend | Parking ramps on Floor G are **lobby-overlapping transit** (like stairs/elevators) — lobby may still widen past a ramp entrance |

## Room

- id: `parking_ramp`  
- `buildFamily = Transit`  
- `allowBasement = true`, `allowAboveGround = false` except upper landing may sit on **Lobby floor** (same pattern as stairs from G→B1)  
- Not stairs/elevator for people pathfinding (`isStairs` / `isElevatorShaft` false)  
- Flag or id check: `ParkingStalls.IsRamp`  
- Stack: consecutive flights share columns like stairs (origin step −1 floor)

## Access helpers

```
IsParkingAccessible(grid, parking):
  Seed lots:
    - any B1 (−1) parking, or
    - any parking that edge-touches a ramp whose floor chain reaches B1 or Lobby
  Expand: same-floor edge-adjacent parking lots (gap-free garage run)
  Lot counts iff it is in the reachable seed component
```

Ramp connects floors `origin.y` and `origin.y + 1` (size.y = 2).  
Ramp chains still walk via overlapping/stacked ramps until B1 (−1) or Lobby (0).  
Deeper floors still need their own ramp link — stacked parking alone does not bridge floors.

Same-floor lots with a **gap** (empty cells between) do **not** share access — bridge them with contiguous parking.

`ParkingStalls.TotalStalls` / claim: only count stalls on accessible lots (and not broken).

`IsParkingFloorAccessible` remains as a floor-level ramp-chain helper (used by tests / tooling); stall eligibility uses per-lot `IsParkingAccessible`.

## Non-goals

- Visible car agents on ramps  
- SimTower coverage/demand  
- Changing Valet requirement for 5★
