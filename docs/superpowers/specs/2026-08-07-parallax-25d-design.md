# Build-A-Tower — 2.5D Parallax Backdrop

**Date:** 2026-08-07  
**Status:** Implemented  
**Depends on:** Cutaway camera pan; structure cutaway art  

## Goals
1. Read the orthographic tower as **2.5D** via layered depth, not a flat blue void.  
2. Far / mid / near plates drift slower than the camera (`ParallaxBackdrop`).  
3. Prefer hand-painted AI plates; procedural skyline fallback if missing.

## Layers
| Layer | Resource | Parallax factor |
|-------|----------|-----------------|
| Mid roofs | `Art/Parallax/mid_roofs` | mid lag |
| Near grass | `Art/Parallax/near_grass` | ~0.97 |
| Near trees | `Art/Parallax/near_trees` (alpha) | ~0.985 |

## Wiring
`BuildController` / `TowerSimulation` call `ParallaxBackdrop.EnsureInScene()` on boot.

## Runtime notes
- Layers follow **camera X** but seat on a fixed **groundY** (lobby/dirt contact).
- Skyline sky/ground-bar cleared by **border flood-fill** only (no hole-punching mid-tone keys).
- Small disconnected blobs (floating specks) removed; L/R edges faded so tiled half-buildings disappear.
- **Near grass / trees** painted plates (`near_grass`, `near_trees`); trees keep alpha so the city shows through.
- Far skyline layer removed (mid roofs + near veg only).
- Prefer `.bytes` TextAssets under `Resources`.
