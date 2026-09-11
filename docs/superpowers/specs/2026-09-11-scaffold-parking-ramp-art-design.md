# Scaffolding & Parking Ramp Structure Art Design

**Status:** Approved  
**Date:** 2026-09-11  
**Supersedes:** earlier draft of this doc that used 3×2 stairs-style multi-floor cutaways.

## 1. Goal

- **Parking ramp:** SimTower-style **3×1** room-like piece with per-floor art that stacks vertically into a continuous ramp to lobby.
- **Scaffolding:** repeating **1×1** wood stud/plank structure tiles.
- One look for all star tiers (no 01/03/05 variants).

## 2. Parking ramp — footprint & placement

### 2.1 Size

- Change `ParkingRamp` from **3×2** to **3×1**.
- One flight occupies a single basement floor strip.

### 2.2 Placement rules (`CanPlaceParkingRamp`)

Treated as a normal room occupant (owns cells). May clear scaffolding. Must not overlap elevators.

A candidate footprint is valid only if **either**:

1. **Lobby attach:** ramp origin floor is **B1** (`y == -1`), so the floor above is lobby (Floor G / `LobbyFloor`), **or**
2. **Exact stack:** a parking ramp already exists on floor `y + 1` with the **same `Origin.x`** and **width 3**.

Invalid examples:

- B2/B3 with no aligned ramp above
- Ramp whose X span does not exactly match the ramp above
- Any floor above lobby

### 2.3 Stack chain

Example: place 3×1 on B1, then another 3×1 on B2 with the same X span → continuous vertical connection visually and for accessibility up to lobby.

Demolishing a mid-chain ramp leaves deeper ramps **orphaned** until rebuilt; placement/ghost must reject new orphans the same way.

### 2.4 Accessibility

Update `ParkingStalls` / ramp floor expansion so 3×1 vertical chains (exact-X stacks reaching B1/lobby) remain the path that makes basement parking reachable.

## 3. Art

### 3.1 Parking ramp (3×1)

SimTower reference: curved drive band + landing/pillar on one floor height; hazard stripes; reads as two-lane circulation; stacks floor-by-floor without spanning two floors.

- Resource under `Resources/Art/Structure/` (e.g. `parking_ramp_3x1`).
- Paint as a per-instance sprite overlay (or structure sprite path) sized to 3×1 — **not** the old solid `Dollhouse/parking_ramp_3x2.png`.
- Magenta/transparent unused areas optional if they help stacking readability.

### 3.2 Scaffolding (1×1)

Light timber posts + planks on keyed background; tiles in bands on the structure layer instead of flat tan procedural color.

- Resource e.g. `scaffolding_stud` under `Resources/Art/Structure/`.

### 3.3 Fallbacks

Missing art → today’s procedural tiles.

## 4. Architecture

| Piece | Change |
|-------|--------|
| `RoomTypeSO` / `ParkingRamp.asset` | `size = (3, 1)` |
| `TowerGrid.CanPlaceParkingRamp` | Lobby-attach **or** exact-X ramp above |
| `StructureCutawayArt` | `TryRampSprite` (3×1), `TryScaffoldSprite` (1×1) |
| `TilemapTowerView` | Ramp sprite paint; scaffold structure sprite paint; clear overlays on demolish |
| Help text / HUD | Describe 3×1 stack-to-lobby rule |
| Tests | Placement chain, accessibility, art loaders |

Do **not** map ramp/scaffold into `RoomDollhouseArt`.

## 5. Out of scope

- Star-tier art variants
- Cars driving on ramps
- Placing ramps above lobby
- Changing underground parking lot / valet dollhouse art
- Soft X-overlap stacking (only exact span)

## 6. Testing

- B1 places; B2 exact-X under B1 places; B2 without B1 fails; misaligned X fails.
- Accessibility: lot on B2 reachable only with continuous ramp stack to lobby.
- Art loaders return sprites when assets exist.
- Play smoke: stacked B1/B2 looks like one continuous ramp; scaffold bands read as wood.

## 7. Success criteria

1. Parking ramp is 3×1 and only places on lobby-attach B1 or exact-X under another ramp.
2. Stacked ramps look and act like a SimTower garage shaft to lobby.
3. Scaffolding shows wood stud tiles.
4. Focused EditMode tests for placement + loaders pass.
