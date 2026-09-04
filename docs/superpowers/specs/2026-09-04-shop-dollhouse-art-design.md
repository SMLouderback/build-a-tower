# Build-A-Tower — Shop Dollhouse Art

**Date:** 2026-09-04  
**Status:** Implemented  
**Depends on:** Shop catalog expansion (`2026-09-02-shop-catalog-expansion-design.md`), existing `RoomDollhouseArt` pipeline  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  

## 1. Goals

Give the six new commercial shops painterly dollhouse cutaways that match existing shop style and perspective, so they no longer show as flat placeholder colors in the tower cutaway.

### Success criteria

1. Each new shop type loads a dollhouse sprite via `RoomDollhouseArt` (no placeholder fill when mapped art exists).
2. Sprites match existing shop language: side cutaway, dark structural frame, warm interior lighting, readable at tower zoom.
3. Art is produced as **whole-room strips on a magenta contact sheet**, then cropped — **no panel stitching**.
4. Player reviews **one shop at a time** (2–3 variants per sheet) before crop/wire.
5. EditMode tests assert the six new type ids are mapped and Resources load.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Production | AI contact sheet (magenta chroma) → crop full rooms |
| Stitching | **None** — each strip is a complete room |
| Review | One shop per round; 2–3 variants on the sheet; pick winner |
| Style reference | Existing shops (`fast_food_16x1`, `restaurant_16x1`, `retail_16x1`, `fine_dining_4x1`) + office magenta-sheet framing |
| Normalize | Keep painterly aspect (~cell-proportional width × ~170–220px tall for 1-high); do **not** force office h=128 stitch pipeline |
| Order | Taco → Chicken → Mexican → Gag Gifts → Shoes → Department Store |
| Gameplay | Unchanged |

## 3. Pipeline

1. Write a style-locked prompt for the current shop (cutaway, dark frame, magenta `#FF00AA`-class background, N complete horizontal strips).
2. Generate one contact sheet with **2–3 full variants** of that shop at the target aspect ratio.
3. User picks a winner (or requests regenerates).
4. Crop strip(s) by magenta / bounds → `Assets/Resources/Art/Dollhouse/<leaf>.png` + matching `.bytes`.
5. Mirror Unity `.meta` conventions from an existing dollhouse PNG.
6. Map `type.id` → leaf in `RoomDollhouseArt.LeafByTypeId`.
7. Verify in Play Mode / EditMode resource tests; proceed to next shop.

## 4. Catalog mapping

| Room id | Display | Cells | Resource leaf |
|---------|---------|-------|---------------|
| `shop_food_taco` | Taco Counter | 4×1 | `taco_counter_12x1` |
| `shop_food_chicken` | Chicken Shack | 4×1 | `chicken_shack_12x1` |
| `shop_food_mexican` | Mexican Restaurant | 6×1 | `mexican_restaurant_16x1` |
| `shop_retail_gifts` | Gag Gifts | 4×1 | `gag_gifts_10x1` |
| `shop_retail_shoes` | Shoe Store | 4×1 | `shoe_store_12x1` |
| `shop_retail_department` | Department Store | 8×2 | `department_store_16x2` |

### Content briefs

| Shop | Mood / props | Window note |
|------|----------------|-------------|
| Taco Counter | Quick-service; counter, salsa, stools, menu boards; orange/green warmth | Optional small street window |
| Chicken Shack | Fried-chicken joint; warmers, buckets, stools; red/amber | Optional |
| Mexican Restaurant | Sit-down; booths/tables, cantina bar, pendants (restaurant rhythm) | Avoid big skyline |
| Gag Gifts | Novelty retail; crowded shelves, joke displays | Optional |
| Shoe Store | Specialty retail; wall racks, benches, fitting stools | Optional |
| Department Store | Two-floor cutaway in one sprite; atrium/escalator, multi-dept | Optional on upper band |

Shared: no painted-in agent people; dark outer frame; warm spot lighting; readable silhouettes.

## 5. Code / files

| Area | Change |
|------|--------|
| `Assets/Resources/Art/Dollhouse/` | Six PNG + `.bytes` (+ `.meta`) |
| `RoomDollhouseArt.cs` | Six `LeafByTypeId` entries |
| `RoomDollhouseArtTests` (or equivalent) | Mapped + load assertions for new ids |
| Crop helper (optional) | Small script under `.superpowers/sdd/` to chroma-crop sheets if useful |

No changes to visit rules, economy, HUD catalog, or room footprints.

## 6. Verification

- EditMode: each new id `IsMapped` and `Resources.Load` / dollhouse path resolves.
- Play Mode: place each shop — dollhouse overlay visible; department store fills 16×2.
- Visual: side-by-side with fast food / retail / restaurant at similar zoom — frame weight and lighting feel consistent.

## 7. Out of scope

- Replacing existing four shop sprites  
- Amenities / non-shop rooms  
- Changing `size` / costs / stars  
- Office h=128 panel-stitch pipeline for these shops  

## 8. Implementation notes

- Department Store last (only 2-high shop in this set).  
- Prefer generating at high resolution on the sheet, then crop — Unity scales via room footprint.  
- If a basement placement looks wrong with a street window, regenerate that shop with a solid back wall (per brief).
