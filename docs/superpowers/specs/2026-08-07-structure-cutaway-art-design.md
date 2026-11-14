# Build-A-Tower — Structure Cutaway Art (Lobby / Elevator / Stairs)

**Date:** 2026-08-07  
**Status:** Implemented  
**Depends on:** Atmosphere polish (`2026-08-07-visual-polish-atmosphere-design.md`); `TilemapTowerView` structure/rooms layers  
**Parent:** Visual polish → room art kits (structure first)  
**Follow-ups:** Office / hotel / condo / commercial / parking cutaway kits; elevator car art pass; day-night multiply on sprites

## 1. Goals

1. Replace flat-color lobby, elevator shafts, and stairs with **hand-authored cutaway art** (AI first pass, swappable PNGs).  
2. Match the polished tower cutaway mood for structure/transit only.  
3. Support **variable lobby width** and **variable elevator height** without obvious tiling; stairs stay a **single static 2×2** overlay.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Scope | Lobby + elevator + stairs only |
| Fidelity | Full cutaway panels (not cell-tint only) |
| Art source | Generate PNGs in-repo; replaceable by filename |
| Lobby | Seamless tileable strips; enough mid variants to reduce repetition |
| Elevator | Top (gears) + bottom (springs) + scalable mid (rails / dark) |
| Stairs | One static image; BL↔TR stair run; place repeatedly as 2×2 rooms |
| Pixel size | **128×128 per cell** (stairs overlay **256×256** for 2×2; star-tier swaps) |

## 3. Art inventory

Path: hand-painted AI PNG bytes at `Assets/Resources/Art/Structure/*.bytes` (and optional `.png`), decoded at runtime via `LoadImage` → `SetPixels` (same path as procedural room tiles — avoids pink). Parallax plates at `Assets/Resources/Art/Parallax/`.

| Asset | Size | Notes |
|-------|------|--------|
| `lobby_mid_01` … `lobby_mid_06` | 64×64 | Seamless L/R; floor G strip |
| `lobby_cap_left`, `lobby_cap_right` | 64×64 | Optional end caps |
| `elevator_top` | 64×64 | Gears / machine (flat 2D side) |
| `elevator_mid` | 64×64 | Rails + dark shaft (tile vertically) |
| `elevator_bottom` | 64×64 | Safety springs / pit |
| `stairs_star_01` | 256×256 | 0–1★ basic / utility stairs |
| `stairs_star_03` | 256×256 | 2–3★ mid hotel stairs |
| `stairs_star_05` | 256×256 | 4–5★ luxury stairs |
| `elevator_car` | crop-to-opaque | Nearly fills shaft cell (0.98×0.94) |

Style: **orthographic 2.5D side cutaway**. Lobby structural bands are copied from the **first loaded mid tile** onto every variant (no forced white-crown overwrite). Stairs swap by `StarSystem.CurrentStars`.

### Presentation rules (no new AI generations)
- Prefer existing files under the Cursor `assets/` cache and `Resources/Art/` — redeploy / crop / tint / scale only.
- Parallax: crop empty sky, preserve aspect, seat building feet on `groundY` (lobby/dirt), daylight tint from `DayNightSky`.

## 4. Painting rules

### Lobby
- For each lobby cell at `(x, 0)`, pick mid variant by stable hash of `x` (and optional caps at min/max X).  
- **Seamless strip rule (locked):** every `lobby_mid_*` tile shares the **same** ceiling slab, crown molding, baseboard, and floor bands (identical thickness, style, and color across the full width). Left/right **edge columns** are generic cream wall only (mirrored so L↔R match). Variant art (door, bench, plant, etc.) stays in the **center** only and never touches those bands or edges.  
- Draw on **structure** tilemap (existing lobby path).

### Elevator
- Shaft cells sorted by Y within the room instance:  
  - max Y → `elevator_top`  
  - min Y → `elevator_bottom`  
  - else → `elevator_mid`  
- Draw on **rooms** layer as a **fully opaque** shaft (no sky bleed-through). Underlay rooms do not paint through elevator cells.  
- Car / doors: keep existing `ElevatorView` for this slice.

### Stairs
- On place/repaint, stamp **`stairs_2x2`** once at room origin covering the 2×2 footprint (sprite overlay).  
- Art: **one continuous flight** BL→TR; **upper-left and lower-right empty (transparent)**; no room background — stairs float over underlay rooms.  
- Same art every placement. Underlay rooms still paint into stairs cells; elevators/ramps remain opaque transit that blocks underlay paint.

### Fallback
- If a sprite is missing, keep current `TowerLookPalette` flat paint.

## 5. Non-goals

- Other room families  
- Agent / furniture density of the full reference image  
- Rewriting placement rules or stair/elevator gameplay  
- Commit unless asked  

## 6. Acceptance

1. Extended lobby shows seamless segments with visible variant variety.  
2. Tall elevator shows distinct top, repeating mid, distinct bottom.  
3. Multiple stairs placements show the same continuous BL→TR 2×2 overlay with empty UL/LR over rooms behind.  
4. Missing art falls back to palette colors without errors.  
5. Spec + art files live under `docs/superpowers` and `Assets/Resources/Art/Structure/`.
