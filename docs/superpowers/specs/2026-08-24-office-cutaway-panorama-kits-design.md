# Build-A-Tower — Office Cutaway Panorama Kits

**Date:** 2026-08-24  
**Status:** Implemented — plan `docs/superpowers/plans/2026-08-24-office-cutaway-panorama-kits.md`; art commit `099b128`  
**Depends on:** Office luxury catalog (`luxuryBand` Base/Mid/Upper); `TilemapTowerView.PaintRoom`; lobby pan slice pipeline (`StructureCutawayArt` / 128×128 cells); `RoomInstance` / `TowerGrid.TryPlace`  
**Parent:** Visual polish → room cutaway kits (offices first)  
**Follow-ups:** Hotel / condo / commercial panoramas; soft day-night multiply on room sprites; star-tier elevator shafts

## 1. Goals

1. Replace flat office tiles with **full-room cutaway panoramas** sized exactly to each office type’s footprint.  
2. Visual quality by luxury band: **Base = humble**, **Mid = nicer**, **Upper = extravagant / high-tech**.  
3. **Two variants per office type** (18 images for the 9-type catalog) to break monotony.  
4. Variant chosen **at place time**, sticky on that room instance.  
5. Broken offices: **grey darken + “Condemned” caution-tape overlay** (no separate damaged interiors).

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Scope | Offices only (9 catalog types) |
| Art model | Full-room panoramas per type×variant — **not** modular lobby-style mid tiles shared across widths |
| Engine paint | Slice each panorama into 128×128 cell tiles on the **rooms** tilemap (stairs `skipCell` still works) |
| Variants | 2 per type; random at place; sticky on instance |
| Broken | Grey wash + Condemned caution overlay; clear on repair |
| Missing art | Flat `TowerLookPalette` paint (no pink) |
| Ghosts | Flat palette (panoramas for placed rooms only) |
| Day-night multiply on panoramas | Optional / follow-up — do not block this slice |

## 3. Catalog → art

| ID | Display | Band | Size | Look | Variants |
|----|---------|------|------|------|----------|
| `office_micro` | Micro Office | Base | 3×1 | Humble | `_v01`, `_v02` |
| `office_studio` | Studio Office | Base | 4×1 | Humble | `_v01`, `_v02` |
| `office_base` | Small Office | Base | 6×1 | Humble | `_v01`, `_v02` |
| `office_mid_standard` | Mid Office | Mid | 9×1 | Nicer | `_v01`, `_v02` |
| `office_mid_clinic` | Professional Suite | Mid | 10×1 | Nicer | `_v01`, `_v02` |
| `office_mid_team` | Team Bay | Mid | 12×1 | Nicer | `_v01`, `_v02` |
| `office_upper_standard` | Upper Office | Upper | 12×1 | High-end / high-tech | `_v01`, `_v02` |
| `office_upper_corner` | Corner Suite | Upper | 14×1 | High-end / high-tech | `_v01`, `_v02` |
| `office_upper_floor` | Corporate Floor | Upper | 18×1 | High-end / high-tech | `_v01`, `_v02` |

Legacy `office` / `office_premium` are migrated catalog ids — if still placeable, map to `office_base` / `office_mid_standard` art or keep palette fallback.

## 4. Art format

- Path: `Assets/Resources/Art/Offices/`  
- Naming: `{typeId}_v01` / `{typeId}_v02` (e.g. `office_micro_v01`)  
- Pixel size: **`(widthCells × 128) × 128`** — Micro `384×128`, Mid Office `1152×128`, Corporate Floor `2304×128`  
- Opaque orthographic 2.5D side cutaway; no generator plate  
- Ship `.png` + `.bytes` (bytes-first load like lobby/dirt)  
- Variants share footprint and band quality; different furniture layout / props

**Look notes**
- **Base:** worn desks, cheap lamps, clutter, plain walls  
- **Mid:** tidy desks, better lighting, plants/art, cleaner finishes  
- **Upper:** glass, screens, sleek furniture, premium materials

## 5. Runtime

**Variant sticky**
- On successful office place, set `RoomInstance.ArtVariant` to `1` or `2` at random.  
- Repaint / rebuild always uses stored value; `0` / unset → treat as `1`.

**Paint**
- `OfficeCutawayArt` (or equivalent) loads `{typeId}_v{NN}`, slices left→right into cell tiles, caches by type+variant.  
- `TilemapTowerView.PaintRoom`: if office category and art available → paint sliced tiles; else palette path.  
- Honor `skipCell` for stairs punch-through.

**Broken**
- Keep panorama tiles.  
- Apply grey darken over room cells.  
- Stamp one Condemned caution-tape overlay across the footprint (readable, footprint-local).  
- Clear overlay when no longer broken.

## 6. Acceptance

1. Each of the 9 offices shows a full cohesive interior matching its size.  
2. Two placements of the same type can show different variants; each stays sticky after repaint.  
3. Base / Mid / Upper read humble / nicer / high-end.  
4. Broken → grey + Condemned tape; repaired → clean again.  
5. Stairs through an office still punch correctly.  
6. Missing art → flat palette, no errors.

## 7. Non-goals

- Hotel / condo / shop panoramas  
- Star threshold retune  
- Elevator shaft star tiers  
- Separate damaged interior art sets  
- Modular shared mid tiles across different office widths

## Implementation notes

- Tasks 1–5 on `feature/conference-event-halls`: `RoomInstance.ArtVariant`, `TowerGrid` place assignment, `OfficeCutawayArt`, `TilemapTowerView` paint path, `OfficeCondemnedOverlay`, 18 panoramas under `Assets/Resources/Art/Offices/`.
- Play Mode visual verification recommended after domain reload.
