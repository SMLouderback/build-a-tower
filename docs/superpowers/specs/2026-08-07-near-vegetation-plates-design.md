# Build-A-Tower — Hand-Painted Near Vegetation Plates

**Date:** 2026-08-07  
**Status:** Implemented  
**Depends on:** `2026-08-07-parallax-25d-design.md`; `ParallaxBackdrop`  
**Parent:** Visual polish → parallax depth readable against lobby/skyline  
**Follow-ups:** Wind sway; alternate grass/tree variants; mid-roof silhouette trim

## 1. Goals

1. Replace procedural blob grass/trees with **hand-painted AI plates** that match lobby and city style.  
2. Split vegetation into **two layers** (opaque grass + transparent trees) for real depth.  
3. Keep procedural vegetation as **fallback** if plates fail to load.  
4. Drop the far skyline plate (mismatched castles); mid roofs + near veg only.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Art source | Generate new hand-painted plates (not procedural-first) |
| Layout | **Two plates**: grass + trees |
| Tree gaps | Transparent so city and grass show through |
| Grass plate | Opaque |
| Runtime | Two Resources strips (`near_grass`, `near_trees`) |
| Far city | **Removed** (asset + layer deleted) |
| Out of scope | Multiple veg variants; wind; regenerating mid roofs |

## 3. Runtime — vegetation

**Stack (back → front):** mid roofs → **near_grass** → **near_trees** → tower/dirt

| Layer | Resource | Sorting | Lag | Seat |
|-------|----------|---------|-----|------|
| Grass | `Art/Parallax/near_grass` | −145 | ~0.97 | `groundY` |
| Trees | `Art/Parallax/near_trees` | −140 | ~0.985 | `groundY` |

- Trees slightly closer (higher lag) for subtle depth.  
- If a plate is missing, fall back to procedural grass-only or trees-only for that layer.  
- Daylight tint applies to both (existing veg tint path, split across both renderer arrays).

## 4. Art — vegetation

| Asset | Target size | Notes |
|-------|-------------|-------|
| `near_grass` | ~1024×128 | Opaque; tileable L↔R; soil→grass; **no trees** |
| `near_trees` | ~1024×256 | Transparent sky; trunks, canopies, shrubs; tileable L↔R; feet on grass line |

**Look:** Orthographic 2.5D, hand-painted, warm greens matching skyline trees / lobby plants — not flat circle stamps. Mixed deciduous masses, soft leaf clusters, visible trunks, varied heights.

**Pipeline:** generate → key white/black/sky plates on trees → edge-fade for tiling → `.png` + `.bytes` under `Assets/Resources/Art/Parallax/`.

## 5. Far city layer — removed

The far skyline plate (`far_city`) was dropped; it read as mismatched “castle” silhouettes behind mid roofs. Parallax city depth is **mid roofs only**, plus near grass/trees.

## 6. Code — `ParallaxBackdrop`

- Replace single `_veg` with `_grass` + `_trees` (transforms, offsets, renderers, tile widths).  
- Serialize `grassLag`, `treeLag`, `grassMaxHeight`, `treeMaxHeight` (retire single `vegLag` / `vegMaxHeight` or map them).  
- Branch load path:
  - **City plates:** keep sky/ground flood cleanup + opaque fill.  
  - **Grass:** opaque; edge fade; no sky flood that eats blades.  
  - **Trees:** key plates; **preserve alpha**; edge fade; do **not** force opaque on foliage.  
- Split procedural `BuildVegetationStrip` into grass-only / trees-only fallbacks (or draw one and mask).  

## 7. Success criteria

- Grass reads as painted turf, not a flat green bar.  
- Trees read as painted canopies with city visible through gaps.  
- No far-city / castle silhouettes behind mid roofs.  
- Missing veg assets → procedural fallback, no pink/errors.  
- Play Mode after cache/domain reload shows new plates.
