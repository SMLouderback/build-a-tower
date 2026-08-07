# Lobby Panorama Segments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do not commit unless the user asks (or this plan’s commit steps are explicitly approved via “go for it” on the full feature).

**Goal:** Load six 640×128 lobby panoramas, slice each into five 128×128 tiles, and paint lobby cells by world-X segment so building left→right reveals a continuous hotel lobby.

**Architecture:** `StructureCutawayArt` prefers `lobby_pan_0N` wide textures; after plate-strip and shared shell lock, caches `Tile[6][5]`. `TryLobbyTile(cellX)` maps `floor_div(cellX,5)%6` → pan and `positive_mod(cellX,5)` → slice. Falls back to existing `lobby_mid_*` if pans missing. `TilemapTowerView` paint path unchanged.

**Tech Stack:** Unity Built-in RP, `Resources.Load` + `.bytes`/`LoadImage`, `UnityEngine.Tilemaps.Tile`, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-08-07-lobby-panorama-segments-design.md`

## Global Constraints

- Pan size **640×128**; cell **128×128**; **6** pans; **5** cells per pan.
- Segment: `floor_div(cellX, 5)`; pan: `positive_mod(segment, 6)`; slice: `positive_mod(cellX, 5)`.
- Shared crown/floor bands locked across all pans; no white/grey generator plates.
- Primary assets: `lobby_pan_01` … `lobby_pan_06` under `Assets/Resources/Art/Structure/`.
- Fallback: `lobby_mid_*` / procedural shell.
- Out of scope: double-height lobby, stairs/elevator, sprite-overlay masking.

## File map

| File | Role |
|------|------|
| `Assets/Resources/Art/Structure/lobby_pan_0N.png` + `.bytes` | Wide lobby source art |
| `Assets/Scripts/Rendering/StructureCutawayArt.cs` | Load, slice, index, `TryLobbyTile` |
| `Assets/Tests/EditMode/StructureCutawayArtTests.cs` | Segment/slice + load tests |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | No logic change (already uses `TryLobbyTile`) |

---

### Task 1: Segment / slice math + failing tests

**Files:**
- Modify: `Assets/Scripts/Rendering/StructureCutawayArt.cs`
- Modify: `Assets/Tests/EditMode/StructureCutawayArtTests.cs`

**Interfaces:**
- Produces: `public const int LobbyPanCount = 6`, `LobbyPanCells = 5`
- Produces: `public static int LobbyPanIndex(int cellX)`, `public static int LobbySliceIndex(int cellX)`

- [ ] **Step 1: Write failing tests**

Replace mid-variant adjacency tests with pan math tests in `StructureCutawayArtTests.cs`:

```csharp
[Test]
public void LobbyPanIndex_CyclesEveryFiveCells()
{
    Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(0));
    Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(4));
    Assert.AreEqual(1, StructureCutawayArt.LobbyPanIndex(5));
    Assert.AreEqual(1, StructureCutawayArt.LobbyPanIndex(9));
    Assert.AreEqual(2, StructureCutawayArt.LobbyPanIndex(10));
    Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(30)); // 6*5
}

[Test]
public void LobbySliceIndex_IsColumnWithinPan()
{
    Assert.AreEqual(0, StructureCutawayArt.LobbySliceIndex(0));
    Assert.AreEqual(4, StructureCutawayArt.LobbySliceIndex(4));
    Assert.AreEqual(0, StructureCutawayArt.LobbySliceIndex(5));
    Assert.AreEqual(3, StructureCutawayArt.LobbySliceIndex(-2)); // -2 mod 5 = 3
}

[Test]
public void LobbyPanIndex_HandlesNegativeX()
{
    // cells -5..-1 → segment -1 → pan 5
    Assert.AreEqual(5, StructureCutawayArt.LobbyPanIndex(-1));
    Assert.AreEqual(5, StructureCutawayArt.LobbyPanIndex(-5));
    Assert.AreEqual(4, StructureCutawayArt.LobbyPanIndex(-6));
}
```

Keep `TryLobbyTile_RuntimeArt_DoesNotThrow`. Remove or stop asserting on `LobbyVariantIndex` adjacent uniqueness (no longer primary).

- [ ] **Step 2: Add minimal pan index helpers (make tests compile/pass)**

In `StructureCutawayArt.cs` near lobby constants:

```csharp
public const int LobbyPanCount = 6;
public const int LobbyPanCells = 5;
public const int LobbyPanWidthPixels = CellPixels * LobbyPanCells; // 640

public static int FloorDiv(int a, int b)
{
    var q = a / b;
    var r = a % b;
    if (r != 0 && ((r > 0) != (b > 0))) q--;
    return q;
}

public static int PositiveMod(int a, int b)
{
    var m = a % b;
    return m < 0 ? m + b : m;
}

public static int LobbyPanIndex(int cellX) =>
    PositiveMod(FloorDiv(cellX, LobbyPanCells), LobbyPanCount);

public static int LobbySliceIndex(int cellX) =>
    PositiveMod(cellX, LobbyPanCells);
```

- [ ] **Step 3: Run EditMode tests**

Run Unity EditMode tests for `StructureCutawayArtTests` (or compile-check if batch unavailable). Expected: pan/slice tests PASS.

---

### Task 2: Load panoramas, slice to cached tiles, wire `TryLobbyTile`

**Files:**
- Modify: `Assets/Scripts/Rendering/StructureCutawayArt.cs`

**Interfaces:**
- Consumes: `LobbyPanIndex`, `LobbySliceIndex`, existing plate-strip / shell lock helpers
- Produces: `TryLobbyTile(int cellX, out Tile tile)` returns `_lobbyPanTiles[pan][slice]` when pans loaded

- [ ] **Step 1: Add pan tile cache fields**

```csharp
static Tile[][] _lobbyPanTiles; // [LobbyPanCount][LobbyPanCells]
```

Clear in `ResetCache()`.

- [ ] **Step 2: Implement `TryLoadLobbyPanPixels` → 640×128 Color[]**

Load `lobby_pan_{n:00}` via `.bytes` / PNG like `TryLoadLobbyCellPixels`, but resize with a new `ResizeLobbyToPan(Texture2D src)` that:
1. Uses `FindLobbyContentRect` (plate strip)
2. Samples into `LobbyPanWidthPixels * CellPixels` (640×128) with bilinear
3. Runs `FillLobbyWhiteEdgeBars` adapted for width **or** row-wise plate fill on pan width
4. Returns flat `Color[]` row-major, length `640*128`

- [ ] **Step 3: Slice pan into five cell tiles with shared shell**

In `EnsureLoaded`, **before** mid fallback loop:

```csharp
_lobbyPanTiles = new Tile[LobbyPanCount][];
var anyPan = false;
for (var p = 0; p < LobbyPanCount; p++)
{
    var name = $"lobby_pan_{p + 1:00}";
    var panPx = TryLoadLobbyPanPixels(name);
    if (panPx == null)
    {
        _lobbyPanTiles[p] = null;
        continue;
    }
    anyPan = true;
    if (_lobbyShell == null)
    {
        // Shell from leftmost cell of first pan
        _lobbyShell = ExtractCellFromPan(panPx, 0);
        FillLobbyWhiteEdgeBars(_lobbyShell);
    }
    _lobbyPanTiles[p] = new Tile[LobbyPanCells];
    for (var s = 0; s < LobbyPanCells; s++)
    {
        var cell = ExtractCellFromPan(panPx, s);
        LockLobbyStructure(cell);
        FillLobbyWhiteEdgeBars(cell);
        ForceOpaque(cell);
        _lobbyPanTiles[p][s] = MakeTile($"{name}_s{s}", cell, FilterMode.Bilinear);
    }
}
```

`ExtractCellFromPan(Color[] panPx, int slice)` copies columns `[slice*128, (slice+1)*128)` into a `128×128` buffer.

Optional: 1–2px horizontal lerp at slice edges using neighboring slice columns when `s` is 0 or 4 against adjacent pan — YAGNI for v1; skip unless seams look harsh.

- [ ] **Step 4: Keep mid load as fallback only when `!anyPan`**

If no pans loaded, keep existing `_lobbyMids` loop. If pans loaded, skip mid primary path (mids unused).

- [ ] **Step 5: Wire `TryLobbyTile`**

```csharp
public static bool TryLobbyTile(int cellX, out Tile tile)
{
    EnsureLoaded();
    tile = null;
    if (_lobbyPanTiles != null)
    {
        var p = LobbyPanIndex(cellX);
        var s = LobbySliceIndex(cellX);
        if (p >= 0 && p < _lobbyPanTiles.Length &&
            _lobbyPanTiles[p] != null &&
            s >= 0 && s < _lobbyPanTiles[p].Length &&
            _lobbyPanTiles[p][s] != null)
        {
            tile = _lobbyPanTiles[p][s];
            return true;
        }
    }
    // fallback mids
    if (_lobbyMids == null || _lobbyMids.Length == 0) return false;
    var i = LobbyVariantIndex(cellX);
    if (i < 0 || i >= _lobbyMids.Length || _lobbyMids[i] == null) return false;
    tile = _lobbyMids[i];
    return true;
}
```

- [ ] **Step 6: Widen `FillLobbyWhiteEdgeBars` / row helpers for pan width OR only run on extracted cells**

Prefer plate-strip on full pan, then `FillLobbyWhiteEdgeBars` only on 128×128 extracts (existing function). Do not rewrite edge fill for 640 unless needed.

---

### Task 3: Generate and deploy six lobby panoramas

**Files:**
- Create: `Assets/Resources/Art/Structure/lobby_pan_01.png` … `lobby_pan_06.png`
- Create: matching `.bytes` (copy of PNG bytes) + `.meta` as needed

**Art brief (each image):** Orthographic 2.5D hotel lobby side cutaway, cream marble walls, dark walnut floor (~15% bottom), white crown (~10% top), columns; opaque; no checkerboard/white plate; continuous left→right scene; one floor tall.

| # | Beat |
|---|------|
| 01 | Arched street windows + planters |
| 02 | Reception desk + concierge |
| 03 | Revolving / main doors |
| 04 | Seating lounge + lamps |
| 05 | Grand columns / chandelier hint |
| 06 | Side corridor / elevator lobby mouth |

- [ ] **Step 1: Generate six wide images** (tool aspect 16:9 or widest available)

Use `GenerateImage` with the brief above; filenames `lobby_pan_0N_src.png`.

- [ ] **Step 2: Normalize each to 640×128**

With System.Drawing or similar: content-aware crop (drop white/grey plate rows), resize to 640×128, save PNG. Copy file bytes to `lobby_pan_0N.bytes`.

- [ ] **Step 3: Verify in Play Mode**

Exit/re-enter Play Mode (static cache). Build/extend lobby: cells 0–4 = pan 01 continuous; 5–9 = pan 02; no five-wide stamp of one photo; crown/floor continuous.

---

### Task 4: Spec status + README note

**Files:**
- Modify: `docs/superpowers/specs/2026-08-07-lobby-panorama-segments-design.md` — Status → Implemented
- Modify: `README.md` only if it documents lobby art paths (one-line note about `lobby_pan_*`)

- [ ] **Step 1: Mark spec Implemented**
- [ ] **Step 2: Manual checklist** — 5-cell continuous pan; next pan at cell 5; partial reveal; no white bars

---

## Spec coverage

| Spec requirement | Task |
|------------------|------|
| 5-cell blocks, 1 tall, 6 pans | 1–3 |
| World-X segment/slice math | 1 |
| Slice-into-tiles runtime | 2 |
| New 640×128 art, classic look | 3 |
| Fallback mids | 2 |
| Shared shell lock | 2 |
| Tests | 1 |
| Out of scope respected | all |

## Self-review

- No TBD placeholders.
- `LobbyPanIndex` / `LobbySliceIndex` names consistent across tasks.
- `FillLobbyWhiteEdgeBars` stays cell-sized; pans sliced first.
