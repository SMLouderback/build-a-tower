# Build-A-Tower — Parking Ramp

**Date:** 2026-08-04  
**Status:** Implemented  
**Depends on:** Underground Parking + Valet (`2026-08-03-five-star-parking-valet-design.md`)  
**Parent:** 5★ parking arrivals foundation

## Goals

1. Add a **Parking Ramp** room that visually connects Lobby/B1 to deeper basement parking (car stairs).  
2. **B2+ parking** only counts for stalls / arrivals when a continuous ramp chain reaches **B1 or Lobby**.  
3. **B1 parking** stays accessible without a ramp (street/lobby access level).

## Locked decisions

| Decision | Choice |
|----------|--------|
| Scope | Visual + access gate for deep parking (option 2) |
| Size | **3×2** (two floors) |
| Family | Transit |
| Unlock | **4★** |
| Cost / upkeep | **$25,000** / **$200/day** |
| B1 parking | Always eligible (with Valet) |
| B2+ parking | Needs ramp chain to B1 or Lobby |
| Agent pathing | People do **not** use ramps; spawn-at-stall arrivals unchanged when eligible |

## Room

- id: `parking_ramp`  
- `buildFamily = Transit`  
- `allowBasement = true`, `allowAboveGround = false` except upper landing may sit on **Lobby floor** (same pattern as stairs from G→B1)  
- Not stairs/elevator for people pathfinding (`isStairs` / `isElevatorShaft` false)  
- Flag or id check: `ParkingStalls.IsRamp`  
- Stack: consecutive flights share columns like stairs (origin step −1 floor)

## Access helpers

```
IsParkingFloorAccessible(grid, floor):
  if floor >= 0: false (no above-ground parking)
  if floor == -1: true
  else: exists ramp chain from floor up to -1 or 0
```

Ramp connects floors `origin.y` and `origin.y + 1` (size.y = 2).  
Chain: walk upward via overlapping/stacked ramps until B1 (−1) or Lobby (0).

`ParkingStalls.TotalStalls` / claim: only count stalls on accessible floors (and not broken).

## Non-goals

- Visible car agents on ramps  
- SimTower coverage/demand  
- Changing Valet requirement for 5★
