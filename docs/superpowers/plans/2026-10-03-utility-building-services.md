# Utility Building Services Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Mail Room, Recycling Center, and Loading Dock as Utility rooms with parking-style vehicle access for Recycling/Loading Dock, thin AmenitySystem hooks, HUD wiring, and matching dollhouse/menu art.

**Architecture:** Reuse `BuildFamily.Utility` and `RoomCategory.Service`. Generalize parking accessibility into a vehicle-access API consumed by placement and hooks. Extend `AmenitySystem` for Mail/Recycling stress relief and a Loading Dock shop/hotel demand nudge. Art follows magenta-sheet dollhouse + square menu icons.

**Tech Stack:** Unity 6 C#, EditMode NUnit, `RoomTypeSO` YAML assets, Resources dollhouse/menu PNGs.

**Spec:** `docs/superpowers/specs/2026-10-03-utility-building-services-design.md`

## Global Constraints

- Exactly three rooms: `service_mail`, `service_recycling`, `service_loading_dock`.
- `IncomeModel.None`; no visit spend / garbage-truck agents this pass.
- Recycling + Loading Dock require vehicle access; Mail does not.
- Loading Dock: basement only; may also access via edge-adjacent **accessible** parking.
- Amenity range ±2 floors / ≤12 cells; Loading Dock nudge +5% constant; no new star promotion criteria.
- Dollhouse: warm industrial Utility look; magenta is key plate only.

## File map

| File | Role |
|------|------|
| `Assets/Resources/Rooms/ServiceMail.asset` (etc.) | Three room blueprints |
| `Assets/Scripts/Economy/ParkingStalls.cs` (or sibling) | Vehicle-access API for Recycling / Loading Dock |
| `Assets/Scripts/Core/TowerGrid.cs` | `CanPlace` rejects inaccessible Recycling/Loading Dock |
| `Assets/Scripts/Economy/AmenitySystem.cs` | Mail/Recycling relief; dock eligibility helpers |
| `Assets/Scripts/Agents/AgentSystem.cs` / economy call sites | Wire dock demand nudge next to hotel amenity bonus |
| `Assets/Scripts/UI/TowerHudController.cs` | Add three Utility buttons |
| `Assets/Scripts/Rendering/RoomDollhouseArt.cs` | Leaf maps |
| `Assets/Resources/Art/Dollhouse/*`, `Art/Menu/*` | Art assets |
| `Assets/Tests/EditMode/*` | Catalog, access, amenity, art tests |

---

### Task 1: Catalog assets + EditMode tests

**Files:**
- Create: `Assets/Resources/Rooms/ServiceMail.asset`, `ServiceRecycling.asset`, `ServiceLoadingDock.asset` (+ metas)
- Create: `Assets/Tests/EditMode/BuildingServicesCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests** for ids, sizes, stars, costs, noise, basement/above flags, Utility family, `IncomeModel.None`.
- [ ] **Step 2: Run tests — expect fail** (missing assets).
- [ ] **Step 3: Author three `RoomTypeSO` assets per spec table.
- [ ] **Step 4: Run tests — expect pass.
- [ ] **Step 5: Commit** `feat: add Mail Recycling Loading Dock room catalog`

---

### Task 2: Vehicle-access API + placement validation

**Files:**
- Modify: `Assets/Scripts/Economy/ParkingStalls.cs` (preferred: add `RequiresVehicleAccess` / `IsVehicleAccessible` helpers) **or** create `VehicleAccess.cs` that reuses parking flood/adjacency
- Modify: `Assets/Scripts/Core/TowerGrid.cs` (`CanPlace` path)
- Create: `Assets/Tests/EditMode/BuildingServicesAccessTests.cs`

**Behavior:**
- Recycling / Loading Dock: accessible on B1, edge-touch lobby-reaching ramp, or same-floor adjacency chain among vehicle-access peers.
- Loading Dock additionally bridges through accessible parking lots.
- `CanPlace` false when post-place access would fail.

- [ ] **Step 1: Write failing access tests** (B1 OK; deep basement needs ramp; Loading Dock via parking bridge; Mail unrestricted).
- [ ] **Step 2: Run — expect fail.
- [ ] **Step 3: Implement access helpers + `CanPlace` gate.
- [ ] **Step 4: Run — expect pass.
- [ ] **Step 5: Commit** `feat: require vehicle access for recycling and loading dock`

---

### Task 3: AmenitySystem hooks + demand nudge

**Files:**
- Modify: `Assets/Scripts/Economy/AmenitySystem.cs`
- Modify: hotel/shop demand call sites that already use `HotelDemandBonus` (add Loading Dock +5% when in range and accessible)
- Create/extend: `Assets/Tests/EditMode/AmenitySystemTests.cs` or `BuildingServicesAmenityTests.cs`

- [ ] **Step 1: Write failing tests** — Mail −1 for condo/office; Recycling −2 when accessible; strongest-wins with leisure; dock nudge +5% only when accessible; inaccessible recycling grants 0.
- [ ] **Step 2: Run — expect fail.**
- [ ] **Step 3: Implement relief ids + Utility eligibility + dock bonus helper; wire call site.
- [ ] **Step 4: Run — expect pass.
- [ ] **Step 5: Commit** `feat: amenity hooks for mail recycling loading dock`

---

### Task 4: HUD Utility strip wiring

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Create: `Assets/Tests/EditMode/BuildingServicesHudTests.cs` (or extend existing Utility HUD tests if present)

- [ ] **Step 1: Failing test** that Utility catalog includes the three resource rooms.
- [ ] **Step 2: `TryAddRoomButton` for `Rooms/ServiceMail`, `ServiceRecycling`, `ServiceLoadingDock`.
- [ ] **Step 3: Tests pass; commit** `feat: wire building services into Utility build menu`

---

### Task 5: Dollhouse + menu art

**Files:**
- Modify: `Assets/Scripts/Rendering/RoomDollhouseArt.cs`
- Create: dollhouse + menu PNGs/bytes under Resources
- Create: `Assets/Tests/EditMode/BuildingServicesArtTests.cs`

Art order (human pick cadence): Mail → Recycling → Loading Dock.

- [ ] **Step 1: Failing art mapping tests.**
- [ ] **Step 2: Generate/crop magenta sheets; import Resources; map leaves.**
- [ ] **Step 3: Tests pass; commit** `art: add building services dollhouse and menu icons`

---

### Task 6: Play Mode smoke + README note

- [ ] **Step 1: Human Play Mode** — place Mail anywhere legal; Recycling/Loading Dock blocked without ramp/parking chain; accessible after ramp; Utility strip shows all three.
- [ ] **Step 2: README one-liner under recent systems if the project lists amenity packs.
- [ ] **Step 3: Commit** only if docs changed.

## Spec coverage

| Spec | Task |
|------|------|
| Catalog table | 1 |
| Vehicle access + placement | 2 |
| Amenity / dock nudge | 3 |
| HUD Utility strip | 4 |
| Art | 5 |
| Play verification | 6 |
