# Build-A-Tower — Leisure Attractions (Casino, Nightclub, Chapel, Atrium)

**Date:** 2026-09-30  
**Status:** Approved (brainstorming)  
**Depends on:** Leisure family + `AmenitySystem`, traffic-venue visits (`ShopVisitRules` / `IncomeModel.TrafficVariable`), stairs pathfinder + lobby transfer floors  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Amenities/attractions catalog → star + wealth demand hooks → numeric rebalance → heatmaps → visual polish  
**Backlog (not this pass):** Entertainment cluster extras (Arcade / Rec Room); prestige cluster (Observation Deck / Sky Lounge)

## 1. Goals

Expand the **Leisure** catalog with four attractions. Casino / Nightclub / Chapel reuse the existing leisure visit + amenity-relief pipeline, with nightlife crime/noise hooks. **Atrium** is a 3-floor transit landmark with internal vertical pathing, stair-span reset on all atrium floors, wider proximity relief, and a non-stacking tower-wide marketing boost.

### Success criteria

In Play Mode a player can:

1. Place **Casino, Nightclub, Chapel, Atrium** from the Leisure family in the build HUD.
2. See agents visit Casino / Nightclub / Chapel using existing traffic-venue scheduling and spend.
3. Walk agents between the three atrium floors **without** external stairs or elevators.
4. Climb atrium floors with **no** stair wait / over-cap stress from atrium vertical edges.
5. Treat each atrium floor as a **transfer / stair-reset** floor (external stairs touching any atrium floor start a fresh comfort chain).
6. Get atrium proximity stress relief within **±6 floors / 24 cells** (other leisure stays ±2 / 12).
7. With ≥1 atrium, receive fixed marketing bonuses to hotel fill, condo demand, and street spawn; a second atrium does **not** increase the bonus.
8. EditMode tests cover catalog, atrium pathing/stress/transfer, amenity range, marketing non-stack, and crime load multipliers.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | Casino, Nightclub, Chapel, Atrium only |
| Approach | Reuse leisure visit + amenity pipeline; atrium adds transit hooks |
| Casino economy | No gambling minigame — visit spend + crime/noise/street weight only |
| Atrium income | None — not a traffic venue; proximity relief + marketing only |
| Atrium height | 3 floors; internal vertical walk edges (escalator fantasy) |
| Atrium transfer | **All three floors** reset stair comfort chain (with lobby / sky lobby) |
| Atrium proximity | **±6 floors, ≤24 cells** origin-to-origin (atrium only) |
| Marketing | Tower-wide if ≥1 atrium: hotel + condo + street spawn; **non-stacking** |
| Art | Placeholders OK until dollhouse PNGs exist |
| Subgroups | Flat Leisure list (no new HUD subgroups this pass) |

## 3. Catalog

All four use `buildFamily = Leisure`. Casino / Nightclub / Chapel use `IncomeModel.TrafficVariable`. Atrium uses `IncomeModel.None`.

| id | Display | ★ | Size | Slots (`maxOccupants`) | Pay cap | Build cost | Noise | Street wt | Hours | Relief | Notes |
|----|---------|---|------|------------------------|---------|------------|-------|-----------|-------|--------|-------|
| `leisure_casino` | Casino | 4 | 10×1 | 14 | $80 | $420,000 | 0.85 | 1.5 | 12:00–02:00 | 3 | Busy visitor crime load **×1.75** |
| `leisure_nightclub` | Nightclub | 3 | 8×1 | 12 | $55 | $295,000 | 0.90 | 1.75 | 20:00–04:00 | 2 | Busy visitor crime load **×1.5** |
| `leisure_chapel` | Chapel | 2 | 6×1 | 8 | $25 | $185,000 | 0.15 | 1.0 | 08:00–20:00 | 4 | Busy visitor crime load **×0.5** |
| `leisure_atrium` | Atrium | 3 | 8×3 | 0 | — | $450,000 | 0.20 | — | pathing always | 5 @ ±6/24 | `isAtrium`; marketing if ≥1 |

### Dwell minutes (`ShopVisitRules`)

| id | Dwell (min) |
|----|-------------|
| `leisure_casino` | 40–70 |
| `leisure_nightclub` | 50–90 |
| `leisure_chapel` | 20–40 |

Atrium is excluded from visit destination picks (not `TrafficVariable`).

### Asset files

Create under `Assets/Resources/Rooms/`:

- `LeisureCasino.asset`
- `LeisureNightclub.asset`
- `LeisureChapel.asset`
- `LeisureAtrium.asset`

Wire into `TowerHudController` room button list. Extend `LeisureCatalogTests` (and related tests) for the new assets.

## 4. Architecture

| Piece | Change |
|-------|--------|
| **RoomTypeSO** | Add `isAtrium` (bool). Optional: crime load multiplier field, or hardcode by id in `CrimeFloorLoads` |
| **StairsPathfinder** | Treat atrium cells like stairs for **vertical** edges (horizontal already walkable via occupied cells) |
| **AgentSystem / stair stress** | Skip stair wait and over-cap stress for vertical moves whose cells are inside an atrium |
| **TowerGrid / TransferFloorProvider** | Include every floor occupied by an atrium in transfer / stair-reset sets alongside lobby & sky lobby |
| **AmenitySystem** | Relief ids for four new rooms; atrium uses wider range constants (6 / 24); other leisure keep 2 / 12 |
| **AtriumMarketing** (new small helper) | `HasAtrium(grid)` → hotel fill +0.05, condo demand +0.05, street spawn ×1.15; boolean gate |
| **CrimeFloorLoads** | Scale busy visitor load for casino / nightclub / chapel as above |
| **Noise** | Author via `noiseOutput` on assets (heatmap already reads SO) |
| **Build HUD** | Add four `Resources.Load` entries with existing leisure |

## 5. Atrium transit rules

- Footprint **8×3**; place with normal multi-floor support rules.
- Every atrium cell is walkable; vertical neighbors **inside the same atrium** are path edges.
- Atrium vertical traversal does **not** apply `StairWaitStressPerMinute` or `StairsOverCapStressPerFloor`.
- Each of the three atrium floors is a transfer floor: `GetLobbyFloors` / `IsTransferLobbyFloor` (or equivalent) returns true for those Y levels when an atrium occupies them.
- External stairs that meet an atrium floor start a **fresh** 3-floor comfort chain from that floor (same effect as meeting a lobby).
- Atrium is **not** a sky lobby: no sky-lobby spacing/height rules; does not replace G lobby.

## 6. Marketing (non-stacking)

If the grid has ≥1 unbroken atrium (`!IsBroken`):

| Effect | Amount |
|--------|--------|
| Hotel fill / check-in demand | **+0.05** (additive, same units as local amenity +0.08) |
| Condo demand / fill chance | **+0.05** |
| Street visitor spawn chance | **×1.15** |

Two atriums ≡ one atrium. Zero atriums ≡ no bonus.

## 7. Amenity relief

| id | Relief |
|----|--------|
| `leisure_casino` | 3 |
| `leisure_nightclub` | 2 |
| `leisure_chapel` | 4 |
| `leisure_atrium` | 5 |

- Default leisure range: `MaxFloorDelta = 2`, `MaxHorizontalCells = 12` (unchanged).
- Atrium-only range when evaluating atrium relief: `MaxFloorDelta = 6`, `MaxHorizontalCells = 24`.
- `MaxReliefInRange` takes the max among in-range amenities (using each amenity’s own range rules).

## 8. Testing

### EditMode

- Catalog footprints, stars, noise, street weights; atrium `IncomeModel.None` / not traffic venue.
- Vertical path through atrium 3 floors without external stairs.
- Zero stair over-cap / wait stress on atrium vertical segments.
- Stair comfort chain resets on all three atrium floors.
- Atrium relief in range at ±6 / 24; out of range at ±7 or 25 cells; other leisure still ±2 / 12.
- Marketing: 0 / 1 / 2 atriums → bonus off / on / on (same magnitude).
- Crime load multipliers for casino / nightclub / chapel while busy.

### Play Mode (human)

Place all four; confirm agents traverse atrium; menu wiring; optional marketing feel check.

## 9. Out of scope

- Gambling / house-edge minigame
- Arcade, Observation Deck / Sky Lounge (backlog)
- Full dollhouse art polish if placeholders ship
- Numeric rebalance pass for the whole leisure economy
- Making atrium an elevator transfer lobby with lobby-walk semantics beyond walkable cells
