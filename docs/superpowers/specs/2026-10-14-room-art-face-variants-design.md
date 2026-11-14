# Build-A-Tower — Room Art Face Variants, Size Rebalance & Sky Windows

**Date:** 2026-10-14  
**Status:** Approved (design)  
**Depends on:** `RoomDollhouseArt`, `RoomExteriorFaces`, `DollhouseContextArt`, `TilemapTowerView`, sky/parallax layers  
**Supersedes (partial):** Runtime side-window mask + family accent bake in `2026-10-12-visual-polish-cutaway-design.md` (those techniques are removed; exterior *truth* goal remains)  
**Parent:** Visual polish — full room art reauthor after cutaway-truth slice

## 1. Goals

1. Mid-tower rooms never show fake exterior side windows or tan plaster edge blocks.  
2. Side truth comes from **four authored plates** per mapped room (both / left / right / neither).  
3. Catalog cell sizes are rebalanced so cutaways can use correct aspect (no stretch).  
4. No runtime family wall tint — family identity is authored furniture/trim only.  
5. Cutaways may read deeper (false depth / rooms behind); **back-wall windows use real alpha** so sky, clouds, and sky color show through.  
6. Every currently mapped dollhouse room ships all four face variants in one art pass (after sizes lock).

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Interior side bands | **Remove** `FillInteriorSideBand` — no runtime mask |
| Family accents | **Remove** `ApplyFamilyAccents` — no runtime tint |
| Face variants | Four full plates: `_ext`, `_L`, `_R`, `_int` |
| Rollout | All mapped dollhouse rooms (not living-only pilot) |
| Size vs art | **Rebalance catalog sizes first**, then redraw art to match |
| Approach | Pipeline-first → size table → four-leaf wire → author all rooms |
| Depth | Painted false depth OK; no second gameplay room row |
| Back windows | Transparent / alpha to sky layer |
| Side windows | Authored on exterior edges; may stay opaque/reflective glass |
| Magenta | `#FF00FF` key plate only |
| Missing leaf during pass | Fall back to current single plate, **still no mask/tint** |
| Lobby / stairs / elevators / ramp / legacy premiums | Out of dollhouse four-variant set (keep existing paths) |

## 3. Architecture

### 3.1 Bake (Phase 0)

`DollhouseContextArt.Bake` becomes:

1. Resolve source sprite (atrium mid leaf when needed; else face leaf / base).  
2. Optional key-crop only (existing load path).  
3. Cache — **no** side fill, **no** family lerp.

Cache key drops `family` once accents are gone: `(typeId, leftExterior, rightExterior, atriumRole)`.

### 3.2 Face leaf contract

Base stem uses **new** catalog `W×H` in the leaf name (example after rebalance):

| L exterior | R exterior | Suffix | Example |
|------------|------------|--------|---------|
| yes | yes | `_ext` | `mid_office_8x1_ext` |
| yes | no | `_L` | `mid_office_8x1_L` |
| no | yes | `_R` | `mid_office_8x1_R` |
| no | no | `_int` | `mid_office_8x1_int` |

`RoomExteriorFaces.Evaluate` unchanged.  
`RoomDollhouseArt` (or thin helper) maps `(typeId, left, right)` → resource path.  
Atrium: roof/solo vs `*_mid` stack role remains; **each** role that needs face truth gets the four suffixes (mid open-well plates use the same L/R window rules on the well edges).

### 3.3 Sorting / sky-through windows

- Sky / clouds / weather stay **behind** dollhouse overlays.  
- Back-wall window panes in PNGs are translucent (alpha &lt; 1) or fully clear — **never** painted sky.  
- Bake and key-crop must preserve window alpha (do not refill glass pixels).  
- Side exterior glass may remain opaque for neighbor seams.

### 3.4 Depth

Art may include rear bays, corridors, or furniture set farther back for depth. Still one `RoomTypeSO.size` footprint and one overlay sprite per instance.

## 4. Size rebalance table

**Rules:** monotonic tier ladders; multi-floor heights kept unless noted; scale `maxOccupants` / shop capacity roughly with width when width changes ≥ +2 cells; buildCost/income from prior numeric rebalance stay unless a note says otherwise (default: keep costs, bump occupants only).

### 4.1 Office

| typeId | Current | New | maxOccupants (new) |
|--------|---------|-----|--------------------:|
| office_micro | 2×1 | **3×1** | 1 |
| office_studio | 3×1 | **4×1** | 2 |
| office_base | 5×1 | **6×1** | 3 |
| office_mid_standard | 5×1 | **8×1** | 5 |
| office_mid_clinic | 6×1 | **9×1** | 6 |
| office_mid_team | 6×1 | **10×1** | 8 |
| office_upper_standard | 6×1 | **10×1** | 8 |
| office_upper_corner | 6×1 | **12×1** | 10 |
| office_upper_floor | 8×1 | **14×1** | 14 |
| office_premium | 9×1 | *(unchanged, no dollhouse)* | — |

### 4.2 Hotel

| typeId | Current | New | maxOccupants (new) |
|--------|---------|-----|--------------------:|
| hotel_base | 3×1 | **3×1** | 2 |
| hotel_accessible | 4×1 | **3×1** | 2 |
| hotel_mid_standard | 4×1 | **4×1** | 4 |
| hotel_mid_extended | 6×1 | **6×1** | 6 |
| hotel_studio | 6×1 | **5×1** | 3 |
| hotel_junior_suite | 6×1 | **5×1** | 4 |
| hotel_upper_standard | 5×1 | **5×1** | 4 |
| hotel_upper_king | 5×1 | **5×1** | 2 |
| hotel_upper_suite | 5×1 | **8×1** | 8 |
| hotel_premium | 4×1 | *(unchanged, no dollhouse)* | — |

### 4.3 Condo

| typeId | Current | New | maxOccupants (new) |
|--------|---------|-----|--------------------:|
| condo_studio | 4×1 | **4×1** | 1 |
| condo_alcove | 4×1 | **5×1** | 2 |
| condo_base | 5×1 | **8×1** | 3 |
| condo_mid_standard | 5×1 | **9×1** | 4 |
| condo_mid_loft | 5×1 | **10×1** | 3 |
| condo_mid_family | 6×1 | **12×1** | 5 |
| condo_upper_standard | 5×1 | **10×1** | 4 |
| condo_upper_corner | 6×1 | **12×1** | 5 |
| condo_upper_penthouse | 7×1 | **14×1** | 6 |
| condo_premium | 16×1 | *(unchanged, no dollhouse)* | — |

### 4.4 Shops

| typeId | Current | New | maxOccupants (new) |
|--------|---------|-----|--------------------:|
| shop_food_taco | 4×1 | **6×1** | 5 |
| shop_food_chicken | 4×1 | **6×1** | 6 |
| shop_food_fast | 6×1 | **8×1** | 6 |
| shop_food_restaurant | 7×1 | **10×1** | 8 |
| shop_food_mexican | 6×1 | **10×1** | 8 |
| shop_food_fine | 5×1 | **8×1** | 10 |
| shop_retail | 6×1 | **8×1** | 6 |
| shop_retail_gifts | 4×1 | **6×1** | 5 |
| shop_retail_shoes | 4×1 | **6×1** | 5 |
| shop_retail_department | 8×2 | **12×2** | 16 |

### 4.5 Service / parking

| typeId | Current | New | Notes |
|--------|---------|-----|-------|
| service_security | 3×1 | **2×1** | Match compact post art |
| service_housekeeping | 5×1 | **4×1** | |
| service_maintenance | 5×1 | **4×1** | |
| service_research | 6×1 | **8×1** | |
| service_mail | 4×1 | **4×1** | |
| service_recycling | 6×1 | **6×1** | |
| service_loading_dock | 8×1 | **8×1** | |
| service_conference | 5×1 | **8×1** | eventCapacity stay 40 unless playtest says otherwise |
| service_event_hall | 9×2 | **12×2** | eventCapacity stay 120 |
| parking_underground | 7×1 | **6×1** | stalls stay 6 |
| service_valet | 3×1 | **3×1** | |

### 4.6 Leisure / metro (keep footprint; reauthor faces + alpha)

| typeId | Size |
|--------|------|
| leisure_gym, leisure_spa, leisure_chapel | 6×1 |
| leisure_nightclub | 8×1 |
| leisure_bowling, leisure_casino | 10×1 |
| leisure_pool, leisure_theater | 8×2 |
| leisure_cathedral | 10×2 |
| leisure_atrium | 8×3 (+ mid plate) |
| metro_station | 8×3 |

Stairs, elevator, parking_ramp, lobby: **no size change** in this slice.

## 5. Art look

- Warm architectural / industrial dollhouse (muted wood, concrete, glass); not toy-pink.  
- Correct aspect for new `W×H`; furniture readable at tower zoom.  
- `_ext` / `_L` / `_R` / `_int` differ **only** in side-wall window treatment (plus any trim that belongs on that edge).  
- Family readable via beds vs desks vs counters — not a color wash.  
- Magenta key plate; window glass alpha for back openings.

## 6. Delivery phases

| Phase | Deliverable |
|-------|-------------|
| 0 | Strip mask + family accent bake; EditMode update; Play Mode: no tan bands, no green/purple wall wash |
| 1 | Apply size table to `RoomTypeSO` assets + catalog tests |
| 2 | Four-leaf resolution API + tests; missing leaf → single plate fallback |
| 3 | Author four variants for every mapped room (Office → Hotel → Condo → Shops → Service → Leisure/Metro); retire obsolete stems |
| 4 | Play Mode cohesion checklist (below) |

## 7. Verification

### EditMode

- Bake does not call side fill or family accent.  
- Face leaf picker returns correct suffix for all four face pairs.  
- Size table: each listed typeId reports new `size`.  
- When Phase 3 closes: Resources exist for all four leaves per mapped id (plus atrium mid set).  
- Transparent-window regression: sample pixel in a back-window region has alpha &lt; 1 on at least one authored plate (spot-check types).

### Play Mode

- Mid-tower: solid interior sides, no tan columns.  
- Tower edge rooms: real side windows on true exterior.  
- No family-colored wall wash by band.  
- No stretched furniture.  
- Day/dusk/cloud motion visible through back windows.  
- Stacked atrium still open-well mid / skylight top.

## 8. Out of scope

Demand/climate graphs; full economy retune; new room types; menu icon redo (unless size rename requires it); gameplay double-depth rooms; runtime accent channel; shared facade endcap kit.

## 9. Follow-ups

- Menu icons refresh if cutaway read changes enough.  
- Legacy premium dollhouse plates.  
- Land expansion / value heatmap (separate).
