# Build-A-Tower — Elevator Passenger & Wait History

**Date:** 2026-08-03  
**Status:** Implemented  
**Depends on:** Elevator boarding / `ElevatorWaitMinutes`; Economy HUD unlock  
**Related:** Shop visit history (`VisitHistoryRing`)

## Goals

Show elevator **passengers/day** and **average wait** (at board time) for **yesterday** and a **rolling 7-day** window — **per shaft** (Selection) and **tower-wide** (Elev dropdown / selection context). Selection also shows **today’s** running totals.

## Locked decisions

| Decision | Choice |
|----------|--------|
| Scope | Both per shaft + tower |
| Window | Yesterday + 7-day avg (Selection also: today live) |
| Passenger | Count of board events (Waiting → Riding) |
| Wait | `ElevatorWaitMinutes` at board |
| 7d wait avg | Passenger-weighted: Σ wait / Σ passengers over recorded days |
| Empty day wait | 0 passengers → contribute 0 wait sum that day |

## Data

- Per `ElevatorShaftRuntime`: today passengers + wait sum; 7-day passenger ring; 7-day wait-sum ring  
- `ElevatorSystem`: tower rings; `ArchiveDay()` at midnight from `TowerSimulation.OnDayRolled`  
- Preserve shaft stats across `SyncFromGrid` when shaft reused by room id  

## UI

- Selection elevator: today passengers / avg wait; yesterday passengers / avg wait; 7d avg passengers / avg wait  
- Elev dropdown / selection context chips: `El yday N`, `El ~N/d`, `Wait yday Xm`, `Wait ~Xm`
