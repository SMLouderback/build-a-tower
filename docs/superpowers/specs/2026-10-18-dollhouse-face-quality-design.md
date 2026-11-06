# Build-A-Tower — Dollhouse Face Quality, Finished Walls & Class Accents

**Date:** 2026-10-18  
**Status:** Approved (design)  
**Depends on:** `RoomDollhouseArt`, `RoomExteriorFaces`, `DollhouseContextArt`, `DollhouseInfrastructureArt`, face leaf contract in `2026-10-14-room-art-face-variants-design.md`  
**Extends / corrects:** `2026-10-14-room-art-face-variants-design.md` — face suffixes and sky-back windows remain; **runtime side seal is demoted**; quality and accent rules are locked here  
**Parent:** Visual polish — restore concept-tower dollhouse look

## 1. Goals

1. Rooms read as **real dollhouse interiors** (concept tower / best existing plates), not hollow cutouts or grey paint-overs.  
2. Adjacency uses **authored face plates** with finished plaster/paint on interior sides — decor and furniture may sit on or against that wall.  
3. Four faces per mapped room stay: windows back-only / L+back / R+back / all three.  
4. Family/class identity uses **interior accents** (furniture, art, rugs, bedding, lamps, one feature panel) — never a blanket wall tint.  
5. New art **matches or improves** fidelity — no cheaper, softer, or more pixelated look than the best current plates.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Shared-side finish | **Finished room plaster/paint** matching that room (not bare concrete as the visible wall) |
| Decor on interior sides | Allowed — art, plants, furniture against the solid side wall |
| Face variants | `_ext` / `_L` / `_R` / `_int` (unchanged mapping) |
| Primary adjacency mechanism | **Select the correct face leaf** |
| `SealInteriorSides` | **Missing-leaf fallback only**; must sample that plate’s plaster, never flat generic grey |
| Visible party-wall cement | Thin **underlay** for seams only (`DollhouseInfrastructureArt`); must not read as the room’s side finish |
| Back windows | Alpha → sky / clouds |
| Side exterior windows | Authored glass with alpha (or clear keyed panes) on exterior faces |
| Modular generator frames | Strip — no thick picture-frame shells between stacked rooms |
| Magenta `#FF00FF` | Key plate only — never scene color |
| Family color | Accents in art, not runtime multiply/lerp on walls |
| Quality | Match or beat best current plate for that stem; reject softer/flatter/artifacted results |
| Rollout order | Offices first → hotel → condo → remaining mapped dollhouse types |

## 3. Face contract (reminder)

| L exterior | R exterior | Suffix | Windows | Interior sides |
|------------|------------|--------|---------|----------------|
| yes | yes | `_ext` | back + left + right | — |
| yes | no | `_L` | back + left | right = finished plaster + optional decor |
| no | yes | `_R` | back + right | left = finished plaster + optional decor |
| no | no | `_int` | back only | both sides finished plaster + optional decor |

`RoomExteriorFaces.Evaluate` remains source of truth. Leaf names keep catalog `W×H` (e.g. `micro_office_3x1_L`).

## 4. Color accents (not blanket tints)

Map to original solid-fill class identity without washing the whole sprite:

| Family | Accent vocabulary | Avoid |
|--------|-------------------|--------|
| Office | Cool blue-grey plaster, steel, cool lamps; micro/lower may keep warmer tan plaster as base identity | Full-room blue wash |
| Hotel | Warm mauve / blush feature wall or bedding, amber lamps | Hot magenta key leftovers; room-wide pink tint |
| Condo | Soft green trim, plants, warmer wood | Full green overlay |
| Shop / leisure | Per-type props, signage, lighting | Category flood fill |

Accents must stay readable at tower zoom and survive face-variant swaps (same accent language on `_ext` through `_int`).

## 5. Quality bar

| Rule | Requirement |
|------|-------------|
| Resolution | Catalog canvas = `cells × 128` PPU; no downscale-then-upscale loops |
| Detail | Preserve furniture edges, mullions, plants, wall art |
| Processing | Surgical key/fill only; prefer highest-res contact sheet as source |
| Anti-pixelation | No nearest-neighbor stretch; no aggressive posterize |
| Anti-cheap | No flat grey paint-over as the product look; no moth-eaten alpha; no thick modular frames; no blanket family tint |
| Compare gate | Before shipping a stem: side-by-side vs prior best plate + in-game crop; **reject** if softer, flatter, or more artifacted |

Processing order for rebuilds:

1. Author / keep solid finished walls on interior sides (from contact `_L`/`_R`/`_int` bands where those are correct).  
2. Key true glass / plate magenta to alpha only.  
3. Clear painted sky-blue pane fills to alpha when they are real windows.  
4. Light fringe scrub on outer ring only.  
5. Fit to cell canvas **once**; re-apply thin window frames after resize if needed.  
6. Do **not** “fill everything grey then punch holes” as the main wall strategy.

## 6. Runtime architecture

### 6.1 Context bake

`DollhouseContextArt.Bake`:

1. Resolve face leaf via `RoomDollhouseArt.ResourcePathForFaces` (atrium mid when needed).  
2. If the correct face leaf **exists** → use it as-is (key-crop already done on load). **Do not** call `SealInteriorSides`.  
3. If the face leaf is **missing** → fall back to legacy / `_ext` / base sprite, then `SealInteriorSides` as last resort, sampling plaster from the source plate (skip furniture-like pixels).  
4. Cache key: `(typeId, leftExterior, rightExterior, atriumRole)`.

### 6.2 Infrastructure underlay

`DollhouseInfrastructureArt` keeps floor / ceiling / thin party bands for seam structure. Party bands must stay **behind** the dollhouse overlay and narrow enough that authored side walls (and their decor) remain the visible finish.

### 6.3 Tests

- Face path resolution for all four suffixes.  
- When a face leaf is present, bake pixels on the interior side band match the source plate (no grey seal rewrite).  
- Window alpha preserved on exterior / back panes.  
- Missing-leaf fallback still seals without crashing.

## 7. Art pipeline

1. Prefer existing v2/v3 **face contact sheets** at full resolution.  
2. Rebuild stems with a dedicated tool (evolve `Tools/fix_office_face_plates.py` / crop pipeline) that respects §5.  
3. After writing PNG+`.bytes`, force Unity reimport of `Assets/Resources/Art/Dollhouse`.  
4. Visual-verify scenarios: solo exterior (sky through glass); horizontal adjacency (finished solid wall, not grey glass); stacked rooms (no double frame / sky seam).  
5. Offices land first; hotel/condo reuse the same contract.

## 8. Non-goals

- Replacing dollhouse with solid category color blocks.  
- Second gameplay depth row.  
- Re-authoring lobby / stairs / elevators / ramp as four-face dollhouse in this pass.  
- Runtime family wall tint (`ApplyFamilyAccents`).

## 9. Success criteria

1. Placing a neighbor swaps to `_L`/`_R`/`_int` and the shared side reads as **finished wall + decor**, not grey-filled former windows.  
2. Solo edge rooms show **sky through** exterior and back glass.  
3. Class readable from accents at a glance without a multiply tint.  
4. Side-by-side vs prior best plate: equal or sharper detail, no new pixelation.  
5. Magenta gate on visual-verify stays within scenario limits; no hot `#FF00FF` in-scene.
