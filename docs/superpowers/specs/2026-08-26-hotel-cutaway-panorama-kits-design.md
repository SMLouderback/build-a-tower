# Build-A-Tower — Hotel Cutaway Panorama Kits

**Date:** 2026-08-26  
**Status:** Spec approved (brainstorm) — plan next  
**Depends on:** Hotel luxury catalog (`luxuryBand` Base/Mid/Upper); `TilemapTowerView.PaintRoom`; office cutaway pipeline (`OfficeCutawayArt` / 128×128 cells); `RoomInstance.Dirty` / `IsBroken`; office condemned overlay pattern  
**Parent:** Visual polish → room cutaway kits (hotels after offices)  
**Follow-ups:** Second art variants; condo / commercial panoramas; soft day-night multiply on room sprites

## 1. Goals

1. Replace flat hotel tiles with **full-room cutaway panoramas** sized exactly to each shared footprint.  
2. Visual quality by luxury band: **Base = modest**, **Mid = nicer**, **Upper = premium**.  
3. Keep all current hotel **build-menu types**; **share art by footprint × luxury band** (not one unique pan per type id).  
4. **One variant** per shared pan for now (second variants later).  
5. **Dirty** (needs maid) and **broken/damaged** (low maintenance) are readable on top of clean pans — **no separate dirty/damaged interior art**.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Scope | Hotels on the build menu (9 types) via 6 shared pans |
| Approach | Mirror offices: dedicated `HotelCutawayArt` + `Art/Hotels/` (do not refactor offices into a shared loader in this slice) |
| Art model | Full-room panoramas per **width × band** — not modular mid-tiles stretched across widths |
| Engine paint | Slice each panorama into 128×128 cell tiles on the **rooms** tilemap (stairs `skipCell` still works) |
| Art sharing | Same size + same luxury band → same art key |
| Variants | 1 per art key for v1 |
| Dirty | Keep clean pan; warm dirty tile tint; optional light cleaning marker; clear when cleaned |
| Broken | Grey wash tile tint + caution-tape footprint overlay (office condemned pattern); clear on repair |
| Dirty + broken | Both readable: broken wash + caution overlay, plus a dirty cue so maid-need is not invisible |
| Missing art | Flat `TowerLookPalette` paint (no pink) |
| Ghosts | Flat palette (panoramas for placed rooms only) |
| Day-night multiply on panoramas | Optional / follow-up — do not block this slice |
| Catalog consolidation | **Do not** remove menu types (unlike Mid Office cleanup); sharing is art-only |

## 3. Catalog → art keys

| Art key | Pixel size | Band | Menu types that use it |
|---------|------------|------|-------------------------|
| `hotel_3_base` | 384×128 | Base | `hotel_base` (Base Hotel), `hotel_accessible` (Accessible Hotel) |
| `hotel_4_mid` | 512×128 | Mid | `hotel_mid_standard` (Mid Standard Hotel) |
| `hotel_5_mid` | 640×128 | Mid | `hotel_studio` (Studio Hotel), `hotel_junior_suite` (Junior Suite) |
| `hotel_6_mid` | 768×128 | Mid | `hotel_mid_extended` (Mid Extended Hotel) |
| `hotel_5_upper` | 640×128 | Upper | `hotel_upper_standard` (Upper Standard), `hotel_upper_king` (Upper King) |
| `hotel_8_upper` | 1024×128 | Upper | `hotel_upper_suite` (Upper Suite) |

**Resolver rule:** `HotelCutawayArt` maps `(size.x, luxuryBand)` → art key. Type id is irrelevant except for `IsHotel` / category checks. Legacy `hotel_premium` (4×1, if still loadable) maps to `hotel_4_mid` when band is Mid or unset-as-Mid.

## 4. Art format & quality bar

- Path: `Assets/Resources/Art/Hotels/`  
- Naming: `{artKey}` (e.g. `hotel_3_base`) — ship `.png` + identical `.bytes` (bytes-first load like offices/lobby)  
- Pixel size: **`(widthCells × 128) × 128`**  
- Opaque orthographic 2.5D side cutaway; no generator plate / letterbox  

**Quality bar (non-negotiable)**
- Same orthographic perspective and furniture scale across all hotel pans (match the settled office camera feel).  
- **No horizontal stretch** to fill width — art authored for that exact width.  
- Finished left/right ends — no furniture cut off mid-object.  
- Must **not** read as smaller rooms stuck together.  
- Larger rooms **may** show living / bedroom / bath zones **only** when separated by believable walls/doors.  
- Floor/ceiling framing lives **in the painted art** (not runtime procedural bars).

**Look notes**
- **Base:** modest motel/budget — simple bed, basic lamp, plain walls.  
- **Mid:** nicer hotel — better bedding, desk/TV, cleaner finishes; 6×1 may be living + sleeping with a real partition.  
- **Upper:** premium materials, larger bed / suite feel; 8×1 suite may be living + bedroom (+ bath nook) with proper partitions.

## 5. Runtime

**Load / paint**
- `HotelCutawayArt.TryHotelTile(room, cellX, out Tile)` loads the resolved pan, slices left→right, caches by art key (and width).  
- Do not cache failed builds in a way that poisons later correct loads (lesson from office alias cache).  
- `TilemapTowerView.PaintRoom` / `PaintCell`: if hotel category and art available → paint sliced tiles; else palette path.  
- Honor `skipCell` for stairs punch-through.

**Dirty**
- When `room.Dirty` (or clean-work remaining, if that is how the view already decides “needs maid”): apply warm brownish tile tint on hotel cutaway cells (aligned with existing `RoomPaintColor` dirty lerp intent).  
- Optional: a light footprint “needs cleaning” marker if tint alone is too subtle in Play Mode; not a second interior art set.  
- Clear tint/marker when dirty is cleared.

**Broken / damaged**
- When `room.IsBroken`: grey wash on hotel cutaway tiles + caution-tape footprint overlay (reuse or lightly generalize the office condemned overlay).  
- Clear overlay and restore clean tint path on repair.

**Both dirty and broken**
- Show broken grey wash + caution overlay, **and** keep a dirty cue (stronger brown in the wash and/or stacked cleaning marker under/with the caution overlay) so maid-need remains visible.

## 6. Acceptance

1. All 9 menu hotels show a full cohesive cutaway via the 6 shared keys.  
2. Base / Mid / Upper read as distinct luxury tiers.  
3. Dirty → dirty tint (and marker if any); cleaned → clean pan again.  
4. Broken → grey wash + caution overlay; repaired → clean again.  
5. Dirty + broken both readable at once.  
6. Stairs through a hotel still punch correctly.  
7. Missing art → flat palette, no errors.

## 7. Non-goals

- Second art variants  
- Removing or merging hotel menu types  
- Separate dirty/damaged interior panoramas  
- Condo / shop / office changes (offices stay as settled)  
- Generalizing office+hotel into one `RoomCutawayArt` loader  
- Star threshold retune / economy changes  

## Implementation notes

- Prefer parallel `HotelCutawayArt` (+ hotel overlay helper or shared caution overlay used by hotels) wired beside the office branch in `TilemapTowerView`.  
- Art pipeline may reuse office normalize lessons: crop letterbox/frames, uniform scale to height 128, stitch same-scale zones with walls/doors for wide pans — never non-uniform stretch.
