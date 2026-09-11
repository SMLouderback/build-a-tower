# Scaffolding & Parking Ramp Structure Art Design

**Status:** Approved  
**Date:** 2026-09-11

## 1. Goal

Replace flat procedural tiles for scaffolding and parking ramps with structure cutaway art that matches the rest of the building.

- **Parking ramps:** stairs-style transparent 3×2 cutaway overlays so stacked flights layer correctly.
- **Scaffolding:** repeating 1×1 wood stud/plank tiles for temporary structure fill.
- **One look** for all star tiers (no 01/03/05 variants).
- Visuals only — placement, cost, and accessibility rules unchanged.

## 2. Why not dollhouse

`RoomDollhouseArt` paints opaque whole-room overlays. Stacked parking ramps need transparent corners so flights compose like stairs. Both assets therefore use the **structure cutaway** path (`StructureCutawayArt` + `TilemapTowerView` overlays/tiles), not dollhouse.

The existing solid `Assets/Resources/Art/Dollhouse/parking_ramp_3x2.png` is **not** wired for this feature (keep on disk unused, or archive later).

## 3. Architecture

### 3.1 Art loaders — `StructureCutawayArt`

- `TryRampSprite(out Sprite)` — load/build a single 3×2 transparent cutaway (keyed magenta/alpha), bottom-left pivot, PPU sized so the sprite spans the 3×2 footprint (same approach as stairs).
- `TryScaffoldSprite(out Sprite)` — load/build a single 1×1 wood stud/plank sprite for structure-layer paint.

Assets under `Resources/Art/Structure/` (names finalized in the plan, e.g. `parking_ramp_cutaway`, `scaffolding_stud`).

### 3.2 Paint — `TilemapTowerView`

**Ramps (mirror stairs):**

1. When ramp sprite exists, do **not** paint opaque rooms-layer tiles for that footprint.
2. One `SpriteRenderer` overlay per ramp `InstanceId` (parent e.g. `RampOverlays`).
3. Sorting order in the stairs band (~20); if stacked overlays Z-fight, bias by `Origin.y`.
4. Scale/position to room origin and size so the upper tread meets the floor above, matching stairs fit conventions.
5. Refresh overlays on place, demolish, and existing structure refresh hooks (extend stairs refresh into a shared transit overlay refresh if cleaner).

**Scaffolding:**

1. When scaffold sprite exists, paint structure-layer cells with that sprite instead of `TowerLookPalette.Scaffold` procedural color.
2. Keep scaffolding on the structure map ownership path (`UsesStructureMap`).

### 3.3 Fallbacks

If art is missing, keep today’s procedural colored tiles so builds never break.

## 4. Art content

| Asset | Size | Look |
|-------|------|------|
| Parking ramp cutaway | 3×2 | Industrial concrete garage ramp, optional yellow chevrons/hazard edges; **transparent non-run corners** for stacking |
| Scaffolding stud | 1×1 | Light timber posts + planks on keyed background; tiles cleanly in bands |

Generation: AI contact sheet → chroma-key crop / meta, same pipeline as other structure art.

## 5. Out of scope

- Star-tier variants for ramp or scaffold
- Cars driving on ramps / ramp agent visuals
- Changing ramp stack or role-conflict rules
- Scaffold “finished corridor kit”
- Re-arting underground parking lots or valet (already dollhouse)
- Wiring the old solid dollhouse ramp PNG

## 6. Testing

- EditMode: ramp and scaffold structure loaders return non-null sprites when assets exist.
- EditMode: ramp paint path uses overlay when sprite exists (no opaque rooms tiles for that room).
- EditMode: scaffold paint path uses structure sprite when present.
- Play Mode / visual: two stacked ramp flights compose without fully occluding each other; scaffold bands read as wood fill.

## 7. Success criteria

1. Parking ramps show cutaway art and stacked flights layer like stairs.
2. Scaffolding shows wood stud tiles instead of flat tan squares.
3. Missing art falls back safely to procedural tiles.
4. Placement and parking accessibility behavior unchanged.
