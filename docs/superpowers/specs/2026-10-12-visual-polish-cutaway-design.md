# Build-A-Tower — Visual Polish (Cutaway Truth, Atrium Stack, Family Accents)

**Date:** 2026-10-12  
**Status:** Approved design  
**Depends on:** `RoomDollhouseArt` / `TilemapTowerView` dollhouse overlays; `BuildingShellEnvelope`; atrium `isAtrium` + fatigue-free climb; build-menu `FamilyColor`; metro/sky/weather WIP  
**Parent:** Preferred feature order — visual polish after economy + heatmaps  
**Follow-ups:** Value heatmap toggles; land expansion; tutorial tips; casino/bowling minigames; full room art reauthor

## 1. Goals

1. Mid-tower rooms no longer look like every face is an exterior facade (fake side windows).  
2. Stacked atriums read as an open vertical well with a skylight only on the top volume.  
3. Living/commercial families stay readable at tower zoom via subtle material accents (not full-room tints).  
4. Cohesion pass: land ready sky/cloud, weather precip, metro train WIP; light interior lighting + agent contrast.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Scope | Broader polish: exterior truth + atrium + accents + cohesion |
| Interior sides | Solid interior walls (no outdoor windows) |
| Exterior sides | Keep existing dollhouse windows |
| Exterior definition | No adjacent **room** on that L/R edge (shell filler alone ≠ room; stairs/elevators count as occupied) |
| Neighbor updates | Place/remove beside a room rebuilds context art for both |
| Atrium fatigue | Keep fatigue-free / reduced climb — valid vertical traffic strategy |
| Atrium art | Roof/solo = skylight plate; mid-stack = open-well plate |
| Atrium light gameplay | Marketing/amenity stay non-stacking; only roof atrium is “daylit”; **no upkeep change** this slice |
| Family color | Material accents only; shared palette with build-menu `FamilyColor` |
| Accent strength | ~25–40% blend on matched accent pixels |
| Full-room tint | Forbidden for family identity (dirty/broken tints unchanged) |
| Architecture | Context-aware paint + accent remap + atrium leaf variants |
| Agents / sky / weather | Cohesion: ship WIP + contrast/lighting tighten, not full redesign |

## 3. Architecture

| Module | Role |
|--------|------|
| `RoomExteriorFaces` | Pure: L/R exterior booleans for a `RoomInstance` on `TowerGrid` |
| `AtriumStackRoles` | Pure: for each atrium, `Roof` / `Mid` / `Solo` from same-footprint contiguous stacks |
| `DollhouseContextArt` | Cache context sprites: window mask + accent remap + atrium leaf |
| `RoomDollhouseArt` | Leaf resolution (incl. `atrium_8x3_mid`); family accent source hues |
| `BuildFamilyColors` (or shared helper) | Single palette used by HUD + dollhouse accents |
| `TilemapTowerView` | Request context sprite on paint; rebuild neighbors on edge changes |
| Sky / weather / metro / agents | Wire existing WIP; `InteriorLighting` + agent contrast pass |

**Paint flow:** place/remove → exterior faces (+ atrium role) → context sprite bake → existing dollhouse `SpriteRenderer` overlay.

**Cache key:** `(typeId, leftExterior, rightExterior, atriumRole, family)` (+ art revision if needed).

## 4. Exterior windows & interior walls

- **Left exterior** iff no other room occupies any cell immediately left of the room’s left edge cells (same for right).  
- Interior L/R: replace vertical window band (~outer 8–12% of sprite width per side, tuned to 128 PPU) with solid wall sampled from nearby non-window wall pixels.  
- Low mask confidence → leave windows (editor log once); do not ship broken half-masks.  
- Cutaway-facing “front” windows stay; this slice targets side facade windows only.

## 5. Atrium roof vs mid-stack

- Stack = same origin X + width, contiguous in Y; gap breaks stack.  
- Uppermost / solo → roof skylight art (`atrium_8x3`).  
- Below another atrium in stack → mid open-well (`atrium_8x3_mid`, new).  
- Fatigue-free atrium climb unchanged.  
- Daylit flavor applies only to roof role; no mid-well upkeep bump this slice.

## 6. Family material accents & cohesion

**Accents:** Office blue / Hotel purple / Condo green / Shops amber / Leisure teal / Utility cool teal / Transit olive — match `TowerHudController.FamilyColor` values extracted to a shared helper. Remap carpets, textiles, desk pads, soft trim only.

**Cohesion:**

- Sky cloud layer (day / storm / sunrise-sunset), weather precip ground clip, metro train + dwell.  
- Interior lighting: weaker exterior sky influence for mid-tower / basement.  
- Agents: silhouette/contrast at play zoom only.

## 7. Out of scope

- Value heatmap UI; land purchase; tutorial tips; casino/bowling minigames  
- Full reauthor of every dollhouse plate  
- Atrium stacking bans or fatigue return on mid climbs  
- Above-ground parking priority work  
- Hard floor bans / deep transport↔economy beyond existing hooks  

## 8. Verification

### EditMode

1. Isolated room → both L/R exterior true.  
2. Neighbor on left → left becomes interior; rebuild flips windows → wall.  
3. Remove neighbor → exterior windows return.  
4. Two stacked atriums same footprint → lower Mid, upper Roof.  
5. Accent bake changes family accent pixels without multiplying whole sprite to family color.  

### Play Mode

1. Mid-tower offices/hotels/condos read by accents at zoom-out; not neon-washed.  
2. Atrium column: skylight only on top; mid open well.  
3. Climbing atrium stack still avoids stair fatigue.  
4. Clouds / rain / metro train do not regress.  

## 9. Risks

| Risk | Mitigation |
|------|------------|
| Window bands not strip-aligned on some art | Confidence gate + per-type override later |
| Accent remap hits skin/food wrongly | Tight hue gates; family-specific source ranges |
| Context cache churn on mass place | Invalidate only edge-touched rooms |
| Mid atrium art missing | Ship placeholder open-well plate before polish pass complete |
