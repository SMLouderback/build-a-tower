# Build-A-Tower — Lobby Panorama Segments (5-cell reveal)

**Date:** 2026-08-07  
**Status:** Approved  
**Depends on:** Structure cutaway art (`2026-08-07-structure-cutaway-art-design.md`); `StructureCutawayArt` / `TilemapTowerView` lobby paint path  
**Parent:** Visual polish → cohesive hotel lobby  
**Follow-ups:** Double-height lobby; soft pan-boundary lighting; retire `lobby_mid_*` entirely once pans prove out

## 1. Goals

1. Replace per-cell repeating lobby “photos” with **wide panoramas** that read as one hotel lobby scene.  
2. **Reveal** each panorama left→right as lobby cells are built; start the next panorama when a block is exhausted.  
3. Keep classic cutaway look (marble, columns, arches, desk, doors) at **one cell tall**.  
4. Preserve shared crown/floor continuity across pans so strip seams stay calm.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Art source | **Generate new** wide images (classic cutaway reference look) |
| Block width | **Always 5 cells** |
| Block height | **1 cell** (current lobby grid) |
| Panorama count | **6** distinct pans, then cycle |
| Runtime approach | **Slice into tiles** from each wide texture (keep tilemap path) |
| Segment index | World X: `floor_div(cellX, 5)` then `% 6` |
| Slice index | `positive_mod(cellX, 5)` → columns `0..4` |
| Primary assets | `lobby_pan_01` … `lobby_pan_06` |
| Fallback | Existing `lobby_mid_*` / procedural shell if a pan fails to load |
| Out of scope | Double-height lobby; stairs/elevator; sprite-overlay masking; baking 30 separate cell files as source of truth |

## 3. Runtime reveal

```
segment = floor_div(cellX, LobbyPanCells)   // 5
pan     = positive_mod(segment, LobbyPanCount) // 6
slice   = positive_mod(cellX, LobbyPanCells)   // 0..4
```

- Only **built** lobby cells receive a tile; unbuilt cells stay empty, so a partial block shows only the left portion of that pan.  
- The next world-X cell after a full block starts the next pan at slice `0`.  
- `TilemapTowerView.TryPaintLobbyArt` keeps calling `TryLobbyTile(cellX)`; no overlay/mask layer.

Negative `cellX` uses the same floor-division / positive-modulo rules so strips left of origin stay stable.

## 4. Art

| Asset | Size | Notes |
|-------|------|--------|
| `lobby_pan_01` … `lobby_pan_06` | **640×128** | Opaque; no white/grey generator plate |
| Delivery | `.bytes` + optional `.png` | `Assets/Resources/Art/Structure/` |

**Shared language (all six):** cream/marble walls, dark walnut floor band, white crown molding, columns/pilasters. Prompt-lock **identical floor/crown band heights** so shell locking works.

**Scene beats (distinct, not crops of one photo):**

1. Arched street windows + planters  
2. Reception desk + concierge  
3. Revolving / main doors  
4. Seating lounge + lamps  
5. Grand columns / chandelier hint  
6. Side corridor / elevator-lobby mouth  

Within each pan, left→right must read as **one continuous room** (no full room reset every 128px).

**Pipeline:** generate wide → plate-strip / content crop → normalize to 640×128 → runtime slice into five 128×128 tiles per pan (cached). Soft **1–2px** horizontal blend only at pan boundaries (optional light touch).

## 5. Code

**`StructureCutawayArt`**

- Constants: `LobbyPanCount = 6`, `LobbyPanCells = 5`, pan pixels `640×128`, cell `128×128`.  
- Cache: `Tile[LobbyPanCount][LobbyPanCells]` (or flat equivalent).  
- `LobbySegmentIndex` / `LobbySliceIndex` (or fold into `TryLobbyTile`).  
- Replace per-cell `LobbyVariantIndex` mid hashing as the **primary** lobby path.  
- Load pans first; on failure fall back to `lobby_mid_*` / procedural.  
- On load: plate-strip → lock shared shell bands from first successful pan → slice → `ForceOpaque` → cache tiles.

**Tests (`StructureCutawayArtTests`)**

- Segment/slice math stable for negative and positive X.  
- Slices `0..4` differ within a segment.  
- Segment boundaries advance pan (`…4` then `…5` → next pan slice `0`).  
- `TryLobbyTile` does not throw when pans (or fallbacks) are present.

## 6. Success criteria

- A 5-cell lobby run shows **one** continuous panorama, not five identical stamps.  
- Extending past 5 cells starts a **different** pan without breaking crown/floor bands.  
- Partial builds only reveal the built slices.  
- Play Mode after cache reset shows new art without pink / white plate bars.
