# Build-A-Tower — Slice #1 Design

**Date:** 2026-07-27  
**Status:** Draft for review  
**Homage:** SimTower-style 2D side-cutaway skyscraper simulation  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  

## 1. Goals

Build the foundation for a mobile-capable tower sim by proving **grid placement + data-driven room definitions** in the Unity Editor before agents, elevators, or economy loops.

### Slice #1 success criteria

In Play mode a player can:

1. Place a **Lobby on floor 1** (sets tower width).
2. Select room blueprints from a toolbar backed by **ScriptableObjects**.
3. Place rooms as multi-cell blocks with ghost preview (valid/invalid).
4. Demolish rooms and free cells.
5. Pan (and optionally zoom) an orthographic cutaway camera.
6. See funds decrease by `buildCost` on place (no income simulation yet).

No overlapping rooms; no placement outside lobby width.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Project path | `Escape/Build-A-Tower` |
| First platform | Desktop / Unity Editor; mobile later |
| Slice #1 scope | Grid + ScriptableObject rooms only |
| Visual style | Classic side cutaway; **placeholder colored cells + labels** |
| World tech | Unity `Grid` + layered Tilemaps + `TowerGrid` occupancy |
| Later slices | Agents → transit/elevators → economy/stars → UI overlays → ECS scale |

### Visual reference

SimTower cutaway screenshots live in `docs/reference/simtower/`. Gameplay reference: [SimTower YouTube](https://www.youtube.com/watch?v=pzV6m56JOHw).

Key visual/mechanical cues from references:

- Horizontal floor bands; sky above, brown “dirt” for basements
- Floor 1 lobby; tower width capped by lobby
- Modular rooms (hotels, offices, retail, parking)
- Elevator shafts as vertical black columns (behavior later)
- Escalators often B1↔lobby; short stairs between nearby floors
- Build toolbar with tool + room icons

## 3. Architecture overview

```
RoomTypeSO (assets)
       │
       ▼
BuildController ──validate──► TowerGrid (cell → RoomInstance)
       │                            │
       ▼                            ▼
Ghost preview              Tilemap Layer 2 (rooms)
Funds HUD                  Tilemap Layer 0/1 (bg/structure)
Orthographic Camera
```

**Approach:** Tilemap for rendering/placeholders; `TowerGrid` as source of truth for occupancy and room instances.

## 4. Coordinate system

- Cell key: `Vector2Int(x, floor)`
- `floor >= 1` → above ground; `floor <= -1` → basement; **floor `0` unused**
- World cell size: 1×1 Unity units (width × floor height)
- Room footprint: `origin` (bottom-left / min corner) + `size` (`Vector2Int` width × height in floors)

## 5. Room data model

### 5.1 `RoomTypeSO` (ScriptableObject)

| Field | Type | Notes |
|-------|------|--------|
| `id` | string | Stable key |
| `displayName` | string | UI label |
| `category` | enum | Structure, Office, Condo, Hotel, Commercial, Transit, Parking, Service |
| `size` | Vector2Int | cells wide × floors high |
| `buildCost` | int | Deducted on place |
| `placeholderColor` | Color | Cutaway tint |
| `incomeModel` | enum | None, QuarterlyRent, NightlyRate, UpfrontSale, TrafficVariable |
| `baseIncome` | int | Stored for later economy |
| `noiseOutput` | float 0–1 | Future evaluation |
| `noiseSensitivity` | float 0–1 | Future evaluation |
| `requiresHousekeeping` | bool | Hotels |
| `activeHoursStart` / `End` | int? | e.g. offices 9–17; null = always |
| `allowAboveGround` | bool | Placement rule |
| `allowBasement` | bool | Placement rule |

### 5.2 `RoomInstance` (runtime)

- `instanceId` (int, monotonic)
- `type` → `RoomTypeSO`
- `origin` (`Vector2Int`)
- Covered cells = all `(origin.x + dx, origin.y + dy)` for size
- `evaluation` stub (0–100), unused in slice #1

### 5.3 Starter catalog (placeholders; SimTower-inspired widths)

Sizes are design targets for data assets; exact balance can tune later.

| Blueprint | Size (W×H) | Category | Notes |
|-----------|------------|----------|--------|
| Lobby | stretchable width × 1 | Structure | Required first on floor 1; height 1 only in slice #1 |
| Office | 9×1 | Office | Quarterly rent later |
| Condo | 16×1 | Condo | Upfront sale later |
| Hotel Single | 4×1 | Hotel | Nightly + housekeeping later |
| Retail / Fast Food | 16×1 | Commercial | Basement-friendly |

Income and noise fields are authored now but **not simulated** in slice #1.

**Not in slice #1 toolbar:** Stairs, elevators, escalators, parking ramps/spaces.

## 6. Grid layers & placement rules

### 6.1 Layers

| Layer | Name | Slice #1 |
|-------|------|----------|
| 0 | Background | Sky, dirt, empty slots, ground line |
| 1 | Structure | Lobby shell / exterior frame |
| 2 | Rooms | Painted from `RoomInstance`s |
| 3 | Transit | Reserved empty |
| 4 | Agents | Reserved empty |

### 6.2 Placement rules

1. First structure: **Lobby on floor 1**; defines `minX`/`maxX` for the tower.
2. No room may extend past lobby horizontal bounds.
3. All cells in the footprint must be free on the Rooms layer (and Transit when used).
4. Respect `allowAboveGround` / `allowBasement` on the room type.
5. Ghost preview: green if valid, red if invalid; click commits.
6. Demolish removes `RoomInstance` and clears tiles for those cells.
7. Lobby demolition is **forbidden** in slice #1.

## 7. Editor build flow (Play mode)

1. Start with empty scene + **$2,000,000** starting funds.
2. Place lobby on floor 1 via **click-drag** to set width (height fixed at **1** floor for slice #1). Lobby `buildCost` scales with width (per-cell cost on the Lobby `RoomTypeSO`).
3. Select tool/room from toolbar listing `RoomTypeSO` assets.
4. Move ghost on grid; click to place; funds − `buildCost` (reject place if insufficient funds).
5. Bulldoze tool removes non-lobby rooms; **lobby cannot be demolished** in slice #1.
6. Camera: orthographic; middle-mouse or right-drag pan; scroll zoom.

### Minimal HUD

- Funds
- Selected tool / room name
- Cursor floor / cell readout

UI: **UIToolkit** for toolbar + funds HUD.

## 8. Out of scope (slice #1)

- Agents, pathfinding, stress/anger
- Elevator / escalator / stairs **behavior** (scheduling, capacity, waiting)
- Day/night cycles, quarterly/nightly payouts, star progression
- Evaluation view / traffic heatmaps
- ECS / `DrawMeshInstanced` agent rendering
- Mobile touch controls
- Final pixel or vector art packs
- Weather, VIP events, security, recycling, etc.

## 9. Future slices (roadmap, not implemented now)

1. **Slice #2:** Agents + stairs-only pathing + stress stub  
2. **Slice #3:** Standard elevator shaft (wait, capacity, stress)  
3. **Slice #4:** Time + economy + star unlocks  
4. **Slice #5:** Evaluation/traffic overlays + polished HUD  
5. **Slice #6:** Agent scale optimization (data-oriented / ECS) + mobile input  

Long-term transit rules (from original tech spec / SimTower): stairs ≤3 floors; escalators 1 floor + noise; standard elevators capacity-limited; express elevators stop at sky lobbies every 15 floors.

## 10. Unity project layout (planned)

```
Build-A-Tower/
  docs/
    reference/simtower/
    superpowers/specs/
  Assets/   (created when Unity project is generated)
    ScriptableObjects/Rooms/
    Scripts/
      Core/          TowerGrid, RoomInstance, enums
      Data/          RoomTypeSO
      Build/         BuildController, GhostPreview
      Rendering/     TilemapPainter / layer helpers
      Camera/        CutawayCamera
      UI/            BuildToolbar, FundsHUD
    Scenes/
      TowerSandbox.unity
    Tiles/           placeholder colored tiles
```

Unity project generation is an **implementation** step after this spec is approved (not part of this document’s commit requirements beyond docs).

## 11. Verification (slice #1)

Manual Play Mode checklist:

- [ ] Cannot place Office before Lobby
- [ ] Cannot place outside lobby X bounds
- [ ] Cannot overlap rooms
- [ ] Insufficient funds blocks placement
- [ ] Demolish frees cells and allows re-place
- [ ] Lobby demolish is blocked
- [ ] Camera pan/zoom remains usable with a 20+ floor placeholder stack

Optional Edit Mode tests later: pure C# unit tests for `TowerGrid.CanPlace` / `Place` / `Demolish` without Scene view.

## 12. References

- Original technical specification provided in project kickoff (architecture, rooms, transit, economy, UI, ECS notes)
- [SimTower gameplay video](https://www.youtube.com/watch?v=pzV6m56JOHw)
- Local screenshots: `docs/reference/simtower/`
