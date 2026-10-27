# 5★ Parking & Valet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Earnable 5★ requiring Valet + ≥6 parking stalls; placeable Underground Parking and Valet; ~25% arrivals via parking; restore basement dirt after demolish.

**Architecture:** Extend `StarSystem` for 5★. New room SOs under Utility. Small `ParkingSystem` (or helpers on `AgentSystem`) tracks stall claims and arrival flags. `TilemapTowerView` restores dirt cells; `BuildController.TryDemolishAt` calls it after clearing structure tiles.

**Tech Stack:** Unity IMGUI / existing TowerGrid, RoomTypeSO, AgentSystem, StarSystem. No new packages.

**Spec:** `docs/superpowers/specs/2026-08-03-five-star-parking-valet-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Parking `maxOccupants` = stall capacity only — never SyncHomes living agents into parking
- Prior star gates (2–4★) unchanged
- Metro / Recycling / Theater / Cathedral out of scope

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Economy/StarSystem.cs` | MaxStars 5; five-star constants; Valet + stall gate; Goals text |
| `Assets/Scripts/Economy/ParkingStalls.cs` (new) | Count stalls / free stalls; claim/release helpers |
| `Assets/Scripts/Agents/Agent.cs` | `ArrivedViaParking`, stall claim fields |
| `Assets/Scripts/Agents/AgentSystem.cs` | 25% parking arrival path; release on leave |
| `Assets/Scripts/Economy/EconomySystem.cs` | Daily upkeep for parking/valet if not covered by room type |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | `PaintDirtCell` / restore band helper |
| `Assets/Scripts/Build/BuildController.cs` | Call dirt restore after basement demolish clear |
| `Assets/Resources/Rooms/ParkingUnderground.asset` | New SO |
| `Assets/Resources/Rooms/Valet.asset` | New SO |
| `Assets/Scripts/UI/TowerHudController.cs` | Add room buttons |
| `Assets/Tests/EditMode/StarSystemTests.cs` | 5★ criteria |
| `Assets/Tests/EditMode/ParkingDirtTests.cs` (new) | Stall count + dirt restore unit tests |
| `README.md` | 5★ + parking/valet + dirt note |

---

### Task 1: Dirt restore

**Why first:** Independent bugfix; validates demolish path before new basement rooms.

- [x] **Step 1: Failing test** — Helper or view API: clearing a basement structure cell then restore yields dirt tile present (or pure function `ShouldRestoreDirt(cell)` + paint called). Prefer testing `ShouldRestoreDirt` / restore decision if Tilemap hard to assert in EditMode.
- [x] **Step 2: Implement** `TilemapTowerView.PaintDirtCell(Vector2Int cell)` using same color as `PaintStarterGuides`. `BuildController.TryDemolishAt`: after dual ClearCell, if cell is basement within dirt band and `!Grid.TryGetRoomAt`, paint dirt.
- [x] **Step 3: Verify** EditMode + manual Play demolish basement office/service.

### Task 2: Room assets + catalog

- [x] **Step 1: Create** `ParkingUnderground.asset` and `Valet.asset` per spec (ids `parking_underground`, `service_valet`).
- [x] **Step 2: Wire** `TowerHudController` `AddRoomButton` for both under Utility.
- [x] **Step 3: Ensure** SyncHomes skips parking (no Office/Condo/Hotel category — Service with maxOccupants must not get RoleFor living). Confirm `RoleFor` / living filter excludes Service.

### Task 3: StarSystem 5★

- [x] **Step 1: Failing tests** — MaxStars 5; cannot promote to 5 without Valet; without 6 stalls; with Valet+6 stalls+prior gates+pop/stress succeeds; FormatNextStarGoal mentions Valet and stalls.
- [x] **Step 2: Implement** constants, `MeetsCriteria`, `FormatNextStarGoal`, `RequiredPopulation`/`AllowedStress`.
- [x] **Step 3: Verify** tests pass.

### Task 4: Stall accounting + upkeep

- [x] **Step 1: Implement** `ParkingStalls` (or static helpers): `TotalStalls(grid)`, `ClaimedCount`, `TryClaim`, `Release`.
- [x] **Step 2: Economy** midnight debit $500 per parking room + $1000 per valet (match existing elevator/service upkeep pattern).
- [x] **Step 3: Tests** for stall totals and claim/release capacity.

### Task 5: Arrival routing

- [x] **Step 1: Agent fields** `ArrivedViaParking`, parking room/slot refs; clear on release.
- [x] **Step 2: Hook** office/hotel/street/condo-buyer entry: 25% roll → claim stall → begin from parking cell toward goal; else lobby.
- [x] **Step 3: On Outside leave** release stall if claimed; prefer parking exit cell when `ArrivedViaParking`.
- [x] **Step 4: Test** claim fails when full → lobby fallback; release restores capacity.

### Task 6: Docs

- [x] **Step 1: README** — 5★ requirements; Parking/Valet at 4★; dirt restore; arrivals note.
- [x] **Step 2: Mark spec** Status: **Implemented**.
- [x] **Step 3: Link** spec from README index.

---

## Self-review checklist

- [x] No Metro/Recycling/Theater/Cathedral in tasks  
- [x] Dirt fix included  
- [x] 5★ numbers match spec (150 / 12 / Valet / 6 stalls)  
- [x] Commits only if user asks  
