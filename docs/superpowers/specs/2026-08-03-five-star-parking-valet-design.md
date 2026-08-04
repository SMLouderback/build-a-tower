# Build-A-Tower — 5★, Underground Parking & Valet

**Date:** 2026-08-03  
**Status:** Implemented  
**Depends on:** 4★ Security gate; basement placement; agent Outside↔lobby trips; tilemap demolish path  
**Parent roadmap:** Deeper economy → higher stars → **5★ parking arrivals** → Metro/Recycling/Theater/Cathedral later  
**Follow-ups (tabled):** Population/staff stats HUD; Metro; Recycling; Movie Theater; Cathedral/evaluation

## 1. Goals

1. Raise earnable stars to **5★** (fill the existing HUD fifth slot).  
2. Add **Underground Parking** and **Valet** so towers can grow a basement arrivals path.  
3. Route a minority of arrivals through parking when Valet + free stalls exist.  
4. Fix basement demolish so vacated cells show **dirt** again, not sky.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Slice scope | Approach **A/1**: 5★ gate + Parking + Valet + dirt restore; other endgame rooms later |
| `MaxStars` | **5** |
| 5★ population | **150** |
| 5★ max stress | **≤12** |
| 5★ facilities | **Valet** placed & not broken + **≥6** parking stalls (`Σ maxOccupants` of parking rooms) |
| Prior gates | Unchanged (elevator / HK+Maint / staffed Security) |
| Parking unlock | **4★** (build before earning 5★) |
| Valet unlock | **4★** |
| Parking size | **6×1**, basement-only, `maxOccupants = 6` |
| Valet size | **3×1**, basement-only (any basement floor) |
| Parking upkeep | **$500/day** per parking room |
| Valet upkeep | **$1,000/day** |
| Valet staff | Optional UI later; **not required** for 5★ this slice |
| Arrival share | **~25%** of new office / hotel / street / condo-buyer trips use parking when Valet + free stall |
| Star Population | Parking/valet occupants do **not** count toward star Population |
| Dirt fix | After demolish clears structure+rooms tiles, restore dirt on empty basement cells in the starter dirt band |

## 3. Stars

- `StarSystem.MaxStars = 5`.  
- Constants: `FiveStarPopulation = 150`, `FiveStarMaxStress = 12f`.  
- `MeetsCriteria(5)`: lobby + pop + stress + elevator + HK + Maint + staffed Security + **HasOperationalValet** + **ParkingStallCount ≥ 6**.  
- `FormatNextStarGoal` adds Valet ✓/✗ and `Parking stalls N/6`.  
- `RequiredPopulation` / `AllowedStress` extend for target ≥ 5.

## 4. Rooms

### Underground Parking (`parking_underground`)

- Category: Service → Utility family.  
- `allowBasement = true`, `allowAboveGround = false`.  
- `requiredStars = 4`.  
- `size = (6,1)`, `maxOccupants = 6` (stall count).  
- `buildCost` ~$80,000; income model upkeep/expense **$500/d** (mirror elevator-style daily debit or existing service upkeep path).  
- Does not spawn home agents via SyncHomes (`maxOccupants` used as stall capacity only — exclude from living SyncHomes roles).

### Valet (`service_valet`)

- Category: Service → Utility.  
- Basement-only; `requiredStars = 4`; `size = (3,1)`; `maxOccupants = 0`.  
- `buildCost` ~$60,000; **$1,000/d** upkeep.  
- Gate: room exists and not Broken (staff not required this slice).

## 5. Arrival routing (light)

When Valet operational and free stalls > 0, with probability **0.25** for eligible new arrivals:

1. Claim a stall on a parking room (track occupancy count or agent→stall).  
2. Spawn/enter at parking cell (or Outside→parking cell).  
3. Trip parking → lobby/goal as today after “arrival”.  
4. On leave, if `ArrivedViaParking`, prefer exit via parking then Outside; release stall.

Eligible: office morning commute start, hotel check-in start, street visitor spawn, condo buyer first trip.  
If claim fails or path fails → fall back to lobby Outside entry.

## 6. Dirt restore

Root cause: `BuildController.TryDemolishAt` clears **both** rooms and structure tilemaps for vacated cells, wiping starter dirt on `structureTilemap`.

Fix: after clear / before or after repaint, for each vacated cell with `y < LobbyFloor` inside `DirtMinX…DirtMaxX` and depth, if no grid room remains (or only need empty structure), call `TilemapTowerView.PaintDirtCell` (or restore helper). Scaffolding still paints over dirt when needed above-ground; basement scaffolding rare — if basement scaffold, keep scaffold paint.

## 7. Non-goals

- Metro, Recycling, Theater, Cathedral  
- Parking ramps / coverage / SimTower demand tables  
- Valet staff agents / AI  
- Population & staff statistics HUD  
- Changing 1–4★ thresholds  

## 8. Success criteria

1. Can earn 5★ when pop/stress/facilities + Valet + ≥6 stalls met; Goals text accurate.  
2. Parking and Valet placeable at 4★ in basement; blocked above ground.  
3. With Valet + stalls, some arrivals visibly use parking; stalls release on leave.  
4. Demolishing a basement room restores brown dirt (no sky hole) within the dirt band.  
5. EditMode tests cover 5★ criteria, stall counting, dirt restore helper.
