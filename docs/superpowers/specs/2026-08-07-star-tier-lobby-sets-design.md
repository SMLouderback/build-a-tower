# Build-A-Tower — Star-Tier Lobby Panorama Sets (0★–5★)

**Date:** 2026-08-07  
**Status:** Implemented  
**Depends on:** `2026-08-07-lobby-panorama-segments-design.md`; `StructureCutawayArt`; `StarSystem`; `TilemapTowerView`  
**Parent:** Visual polish → lobby progression readable at a glance  
**Follow-ups:** Star-tier elevator/corridor art; double-height lobby; tier-flavored room kits

## 1. Goals

1. Show tower **progression** through lobby art: dingy at 0★ → glamorous at 5★.  
2. One **distinct 5-pan set per star** (0,1,2,3,4,5) — six sets total.  
3. Reuse the existing 5-cell panorama reveal (no new tiling math).  
4. Swap lobby art immediately when `StarSystem.CurrentStars` changes.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Sets | **One per star**, 0★ through 5★ |
| Pans per set | **5** (same as current trimmed set; a bad beat can be dropped/replaced per tier) |
| Scene variation | **Tier-flavored** scenes, not recolors of the 5★ set |
| Current art | Becomes the **5★** set (renamed) |
| Naming | `lobby_s{SS}_pan_{PP}` (e.g. `lobby_s00_pan_01`, `lobby_s05_pan_05`) |
| Selection | Exact `CurrentStars` (clamped 0–5) — **not** the stairs 3-bucket map |
| Cache | Active star only; rebuilt on star change |
| Fallback | Nearest lower star → nearest higher star → `lobby_mid_*` / procedural |
| Out of scope | Stairs tier remap; double-height lobby; regenerating 5★ art |

## 3. Runtime

**Selection**

```
lobbyStar = clamp(CurrentStars, 0, 5)
segment   = floor_div(cellX, 5)
pan       = positive_mod(segment, 5)   // 5 pans per set
slice     = positive_mod(cellX, 5)
resource  = $"lobby_s{lobbyStar:00}_pan_{pan+1:00}"
```

**Swap**
- `SetStarRating(stars)` tracks a lobby star separately from the stairs tier. When the lobby star changes, the lobby tile cache is rebuilt (and stairs reload on their own tier change as today).
- Callers refresh structure art: stairs overlays **and** a lobby repaint, so built cells pick up the new tier without a scene reload.

**Reveal** — unchanged: only built cells get tiles; a partial block reveals only the left slices; the next block advances the pan.

## 4. Art

**Format (every tier):** 640×128, opaque, no white/grey generator plate, orthographic 2.5D cutaway, one cell tall, continuous left→right within a pan.

| Stars | Mood | Beat flavor |
|-------|------|-------------|
| 5★ | Current glam | Existing set: arched windows, grand desk, revolving doors, lounge, elevator corridor |
| 4★ | Nice, less opulent | Soft marble, simpler desk, modest chandelier, tidy seating, clean corridor |
| 3★ | Average mid hotel | Laminate desk, tired carpet, fluorescent wash, generic wall art |
| 2★ | Budget motel | Plastic plants, scuffed walls, vending / ice machine, thin rugs |
| 1★ | Rough inn | Peeling paint, mismatched furniture, buzzing lights, stained floor |
| 0★ | Dingy / near abandoned | Boarded window, trash bags, water stains, broken chair, bare bulb |

Within a tier, keep floor/crown palette consistent so 5-cell strips read continuous. Across tiers, color and clutter must jump enough that a star-up is obvious without UI.

**Pipeline:** generate wide → plate-strip / content crop → normalize to 640×128 → `.png` + `.bytes` (+ metas) under `Assets/Resources/Art/Structure/`.

## 5. Code

**`StructureCutawayArt`**
- `public const int LobbyStarSets = 6;` (0–5) and keep `LobbyPanCount = 5`, `LobbyPanCells = 5`.
- `static int _lobbyStar = -1;` distinct from `_stairsStarTier`.
- `public static int LobbyStarIndex(int stars) => Mathf.Clamp(stars, 0, 5);`
- `static string LobbyPanResource(int star, int pan) => $"lobby_s{star:00}_pan_{pan + 1:00}";`
- Pan load tries the active star, then nearest lower, then nearest higher (then `lobby_mid_*` / procedural if no pan resolves).
- Unset `_lobbyStar` defaults to **0★** in `EnsureLoaded`.
- `SetStarRating(int stars)`: update `_lobbyStar` and `_stairsStarTier`; when loaded, reload whichever changed; return `true` if anything changed.
- Slicing / shell handling unchanged (no `LockLobbyStructure` on pan slices).

**Call sites**
- `BuildController.RefreshStarStructureArt` refreshes stairs overlays and repaints lobby rooms.
- `TowerSimulation` star-change path calls that refresh.

**Tests (`StructureCutawayArtTests`)**
- `LobbyStarIndex` clamps below 0 and above 5.
- `LobbyPanResource` formats `lobby_s03_pan_02` style names.
- Existing pan/slice math tests unchanged.

## 6. Success criteria

- 0★ lobby reads run-down; 5★ reads luxurious; each step between is visibly different.
- Gaining/losing a star swaps built lobby art without restarting play.
- Missing tier art falls back without pink or empty cells.
- 5-cell blocks still read as one continuous room per pan.
