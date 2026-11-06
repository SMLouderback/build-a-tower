# Star-Tier Lobby Sets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Six lobby panorama sets (0★–5★) that swap with `StarSystem.CurrentStars`, so lobby art shows tower progression.

**Architecture:** `StructureCutawayArt` gains a lobby star index separate from the stairs tier, loads `lobby_s{SS}_pan_{PP}` for the active star (with fallback to nearest star, then legacy names), and rebuilds its 5×5 tile cache on star change. `BuildController` repaints lobby cells alongside stairs overlays.

**Tech Stack:** Unity Built-in RP, Resources `.bytes` + `LoadImage` → `SetPixels`, `UnityEngine.Tilemaps.Tile`, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-08-07-star-tier-lobby-sets-design.md`

## Global Constraints

- Pans: **5 per star**, 5 cells each, **640×128**, opaque, no plate bars.
- Stars: **0–5**, one set each; selection uses exact `CurrentStars` clamped to 0–5.
- Naming: `lobby_s{star:00}_pan_{pan:00}` (e.g. `lobby_s00_pan_01`).
- Existing pans become the **5★** set (renamed `lobby_s05_pan_01`…`05`).
- Fallback order: active star → nearest lower star → nearest higher star → `lobby_mid_*` / procedural.
- Unset lobby star defaults to **0★** (fresh tower), not 5★.
- Do **not** run `LockLobbyStructure` on pan slices (it stamps pillars through the art).
- Do not commit `.superpowers/sdd/*` or `_Recovery/`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Rendering/StructureCutawayArt.cs` | Lobby star index, tier resource names, cache rebuild |
| `Assets/Scripts/Build/BuildController.cs` | Refresh lobby + stairs on star change |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Repaint lobby rooms helper |
| `Assets/Tests/EditMode/StructureCutawayArtTests.cs` | Star index + resource name tests |
| `Assets/Resources/Art/Structure/lobby_s{SS}_pan_{PP}.{png,bytes}` | Art for six tiers |

---

### Task 1: Star-aware lobby loading

**Files:**
- Modify: `Assets/Scripts/Rendering/StructureCutawayArt.cs`
- Modify: `Assets/Tests/EditMode/StructureCutawayArtTests.cs`

**Interfaces:**
- Produces: `public const int LobbyStarSets = 6;`
- Produces: `public static int LobbyStarIndex(int stars)` → clamp 0–5
- Produces: `public static string LobbyPanResource(int star, int pan)` → `lobby_s{star:00}_pan_{pan+1:00}`

- [x] **Step 1: Write failing tests**

```csharp
[Test]
public void LobbyStarIndex_ClampsToSetRange()
{
    Assert.AreEqual(0, StructureCutawayArt.LobbyStarIndex(-3));
    Assert.AreEqual(0, StructureCutawayArt.LobbyStarIndex(0));
    Assert.AreEqual(3, StructureCutawayArt.LobbyStarIndex(3));
    Assert.AreEqual(5, StructureCutawayArt.LobbyStarIndex(5));
    Assert.AreEqual(5, StructureCutawayArt.LobbyStarIndex(9));
}

[Test]
public void LobbyPanResource_UsesStarAndPanNumbering()
{
    Assert.AreEqual("lobby_s00_pan_01", StructureCutawayArt.LobbyPanResource(0, 0));
    Assert.AreEqual("lobby_s03_pan_02", StructureCutawayArt.LobbyPanResource(3, 1));
    Assert.AreEqual("lobby_s05_pan_05", StructureCutawayArt.LobbyPanResource(5, 4));
}
```

- [x] **Step 2: Run EditMode tests, expect failures** (methods missing).

- [x] **Step 3: Implement selection + load**

```csharp
public const int LobbyStarSets = 6;
static int _lobbyStar = -1;

public static int LobbyStarIndex(int stars) => Mathf.Clamp(stars, 0, LobbyStarSets - 1);

public static string LobbyPanResource(int star, int pan) =>
    $"lobby_s{star:00}_pan_{pan + 1:00}";
```

Add a loader that walks the fallback chain for one pan index:

```csharp
static Color[] LoadLobbyPanForStar(int star, int pan)
{
    var px = TryLoadLobbyPanPixels(LobbyPanResource(star, pan));
    if (px != null) return px;
    for (var s = star - 1; s >= 0 && px == null; s--)
        px = TryLoadLobbyPanPixels(LobbyPanResource(s, pan));
    for (var s = star + 1; s < LobbyStarSets && px == null; s++)
        px = TryLoadLobbyPanPixels(LobbyPanResource(s, pan));
    return px;
}
```

- [x] **Step 4: Rebuild cache on star change**

In `EnsureLoaded`, set `if (_lobbyStar < 0) _lobbyStar = 0;` and build pans via `LoadLobbyPanForStar(_lobbyStar, p)`. Extract the pan-building loop into `static void BuildLobbyPanTiles()` so it can rerun.

In `SetStarRating(int stars)`:

```csharp
var tier = StarTierIndex(stars);
var lobbyStar = LobbyStarIndex(stars);
var changed = false;
if (tier != _stairsStarTier) { _stairsStarTier = tier; changed = true; if (_attempted) _stairsSprite = LoadOrBuildStairs(); }
if (lobbyStar != _lobbyStar) { _lobbyStar = lobbyStar; changed = true; if (_attempted) BuildLobbyPanTiles(); }
return changed || !_attempted;
```

Clear `_lobbyStar = -1;` in `ResetCache()`.

- [x] **Step 5: Run tests, expect PASS.**

### Task 2: Repaint lobby on star change

**Files:**
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs`

- [x] **Step 1: Add a lobby repaint entry point** on `TilemapTowerView` that repaints all lobby rooms using the existing lobby paint path (reuse `RepaintAllRooms` if it already covers structure tiles; otherwise iterate rooms where `room.Type.isLobby` and call the existing lobby paint method).
- [x] **Step 2: Call it from `BuildController.RefreshStairsArt`** so a star change refreshes stairs overlays **and** lobby tiles. Keep the existing method name working (rename with the old name delegating, or update the single `TowerSimulation` call site).
- [x] **Step 3: Verify compile** (no other callers broken).

### Task 3: Art — rename 5★ set, generate 0★–4★

**Files:** `Assets/Resources/Art/Structure/lobby_s{SS}_pan_{PP}.{png,bytes}` (+ `.meta`)

- [x] **Step 1: Rename** `lobby_pan_01..05` → `lobby_s05_pan_01..05` (png, bytes, metas).
- [x] **Step 2: Generate 5 wide images per tier** for 0★–4★ using the moods in the spec table.
- [x] **Step 3: Normalize** each to 640×128 (plate/letterbox crop, 5:1 center crop, force opaque), write `.png` + `.bytes`, create metas from an existing template.
- [ ] **Step 4: Play Mode check** — set stars low/high and confirm the lobby swaps, no white bars, no pink.

### Task 4: Docs

- [x] Mark spec **Implemented**; note star-tier lobby art in the lobby panorama spec follow-ups.

## Spec coverage

| Requirement | Task |
|-------------|------|
| One 5-pan set per star | 1, 3 |
| Exact-star selection | 1 |
| Cache rebuild + repaint on star change | 1, 2 |
| Fallback chain | 1 |
| Tier-flavored art | 3 |
| Tests | 1 |
