# Build-A-Tower — Elevator Shaft Floor Labels

**Date:** 2026-09-05  
**Status:** Approved — implementation plan ready  
**Depends on:** `ElevatorSystem` / `ElevatorShaftRuntime.Serves`, lobby + sky-lobby floors on `TowerGrid`  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  

## 1. Goals

Make floor identity and Express stop rules obvious on the tower cutaway using world-space labels on elevator shafts — **no new AI art**.

### Success criteria

1. Player can read the floor on any elevator shaft cell at normal zoom.
2. Express shafts show non-served floors as muted (won’t stop) while still showing the floor id.
3. Labels refresh when shafts or lobby/sky-lobby set change.
4. EditMode tests cover formatter + mute rule (`Serves`).
5. No gameplay / routing changes.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Priority vs parking/scaffolding art | Shaft labels **first**; Parking Ramp + Scaffolding art later |
| Label content | Every shaft floor cell labeled |
| Express | Mute when `!Serves(floor)` (lobby + sky-lobby stops only today) |
| Service | Mute only via `Serves` (today = all floors in span active) |
| Naming | `L` / `Bn` / `n` / `SLn` |
| Implementation | World-space text overlays (pooled), not baked tiles / HUD pins |
| AI art | None for this feature |

## 3. Naming

| Floor | Label |
|-------|--------|
| `TowerGrid.LobbyFloor` (`0`) | `L` |
| `floor < 0` | `B{n}` with `n = -floor` (`-1` → `B1`) |
| Sky lobby (`TryGetSkyLobbyOnFloor`) | `SL{n}` e.g. `SL15` |
| Other `floor > 0` | decimal (`1`, `2`, …) |

Sky lobby check wins over plain positive numbering when a sky lobby occupies that Y.

## 4. Visuals

- **Placement:** One label per floor in the shaft’s `[MinFloor, MaxFloor]`, centered on the shaft footprint width at that Y (Express 2-wide: single centered label, not two).
- **Sort:** Above shaft / transit occlusion art so text stays readable.
- **Active** (`Serves`): high-contrast light text.
- **Muted** (`!Serves`): ~35–45% opacity, cooler gray; still legible as floor id.
- **Pool:** Reuse text objects; clear when shaft removed.

## 5. Architecture

### Pure helpers

- `FloorLabels.Format(int floor, TowerGrid grid) → string`
- Uses `grid.TryGetSkyLobbyOnFloor` for `SLn`

### View sync

- `ElevatorShaftFloorLabels` (standalone helper or `TilemapTowerView` methods):
  - `Sync(TowerGrid grid, ElevatorSystem elevators)`
  - For each `ElevatorShaftRuntime`: labels for each Y in span; active iff `shaft.Serves(y)`
- Call `Sync` after `ElevatorSystem.SyncFromGrid` and elevator repaint paths (`RepaintTransitOnTop` / shaft paint)

### Non-goals

- No change to Express/Service routing
- No Parking Ramp / Scaffolding dollhouse art in this spec
- No room footprint / dollhouse regen

## 6. Testing

| Case | Expect |
|------|--------|
| Format `0` | `L` |
| Format `-2` | `B2` |
| Format `3` (no SL) | `3` |
| Format sky lobby Y | `SL{Y}` |
| Express, floor not in `StopFloors` | muted |
| Normal shaft floor in span | active |
| Sync 1×N shaft | N labels (optional count assert) |

## 7. Follow-ups (out of scope)

1. Parking Ramp dollhouse art  
2. Scaffolding dollhouse art  
3. Optional later: regenerate art if we widen rooms again  

## 8. Self-review

- [x] No unresolved placeholders  
- [x] Naming + mute rules consistent with `Serves` / lobby APIs  
- [x] Scope excludes parking/scaffolding art and routing changes  
- [x] Success criteria testable in EditMode + Play Mode smoke  
