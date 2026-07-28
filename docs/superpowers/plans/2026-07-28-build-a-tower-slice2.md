# Build-A-Tower Slice #2 Implementation Plan

**Status:** Done (2026-07-28)

**Goal:** Placeable stairs, day clock, office/hotel agents pathing via stairs (≤3 floors), stress stub.

**Architecture:** `TowerGrid` occupancy + `StairsPathfinder` graph + `AgentSystem` driven by `GameClock`; agents rendered as colored dots.

**Tech Stack:** Unity 6000.4.x, C#, existing Tilemaps + IMGUI HUD, NUnit EditMode/PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-07-28-build-a-tower-slice2-design.md`

## Shipped (vs original task list)

- Stairs `(2, 2)` with BL→UR run, stack rules, punch-through lobby/rooms, rooms buildable behind stairs
- Floor G = lobby at `y == 0` (ground / 1st floor); basement access via stairs origin at `−1`
- `GameClock`, `StairsPathfinder` (|Δfloor| ≤ 3), `AgentSystem` (office / hotel / condo)
- HUD: stairs tool, clock, agents/stress; README play steps; EditMode + PlayMode smoke
- tower-together reference mirrored for Slice #3 elevators

## Deferred

- Elevators → Slice #3
- Retail / restaurant / happy-hour schedules → Slice #4
- Economy payouts, subway/parking arrivals
