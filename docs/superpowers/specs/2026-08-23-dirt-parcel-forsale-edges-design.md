# Build-A-Tower — Painted Dirt Parcel, Void Fill & For-Sale Edges

**Date:** 2026-08-23  
**Status:** Approved  
**Depends on:** `DirtBand`; `TilemapTowerView.PaintStarterGuides` / `PaintDirtCell`; `BuildController` starter guides; parallax ground line  
**Parent:** Visual polish → underground reads as owned land, not flat brown  
**Follow-ups:** Land purchase / expand parcel; move signs; optional dirt variants by biome

## 1. Goals

1. Underground dirt reads as **hand-painted cutaway earth** (strata + clear Floor G ground cut).  
2. Below Floor G, the view **never shows sky holes** — fill to the bottom of the screen.  
3. Hard land edges at today’s **`DirtBand` (−80…100)** marked with a **For Sale signpost** each side (no buy yet).  
4. Empty basement cells still restore to dirt after demolish.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Look | Painted strata + sharper Floor G ground cut |
| Fill | Hybrid: tiled painted dirt on the parcel + darker camera-following void behind |
| Parcel edges | Fixed at `DirtBand.MinX` / `MaxX` (−80 / 100) |
| Edge marker | One For Sale **signpost** sprite per side |
| Land buy | Out of scope |
| Architecture | Parcel plates + void fill + edge signs |
| Missing art | Fall back to flat `DirtBand.Color` (no pink) |

## 3. Runtime

**Layer stack (back → front):** sky → parallax → **void fill** → **parcel dirt** (structure tilemap) → tower → **For Sale signs**.

**Parcel dirt**
- Inside `DirtBand`: paint cells with Resources art.  
- `y == -1`: `dirt_crown` (sod/cut edge under Floor G).  
- `y <= -2`: `dirt_fill` (optional second variant hashed by cell).  
- Keep `DirtBand.Contains` / `ShouldRestore` / `PaintDirtCell` so demolish restores painted dirt.

**Void fill**
- `UndergroundVoidFill`: dark earth plane tracking the camera below Floor G; covers viewport bottom/edges behind parcel dirt.  
- Darker than parcel dirt so owned land still reads.

**Signs**
- Spawn at world `(MinX - 0.5, 0)` and `(MaxX + 0.5, 0)` (ground line); sorting above dirt.  
- No click / buy logic.

## 4. Art

| Asset | Notes |
|-------|--------|
| `dirt_fill` | Opaque soil/rock body, tile-friendly (~128×128 or strip), hand-painted |
| `dirt_crown` | Top dirt row under Floor G — cutaway crown / sod edge |
| `for_sale_sign` | Wooden post + FOR SALE board, transparent BG |
| Void | Solid dark umber / soft gradient (code tint OK) |

Delivery: `.png` + `.bytes` (+ metas) under `Assets/Resources/Art/…`.

## 5. Code touchpoints

| Piece | Role |
|-------|------|
| `DirtBand` | Limits unchanged; crown vs fill helpers |
| `TilemapTowerView` | Painted dirt in guides + `PaintDirtCell` |
| `UndergroundVoidFill` | Camera-following void behind dirt |
| `LandEdgeMarkers` (or BuildController spawn) | For Sale signs at band edges |
| `BuildController` | Wire guides + void + signs on start |

**Tests:** crown/fill by Y; `ShouldRestore` unchanged; sign positions at MinX/MaxX.

## 6. Success criteria

- Basement view shows painted earth and a clear ground cut at Floor G.  
- Panning/zooming never reveals sky under the dirt band.  
- For Sale signs sit at −80 and 100 ground line.  
- Demolish basement room → painted dirt returns.  
- Missing dirt art → flat brown fallback.
