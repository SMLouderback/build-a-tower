# Dirt Parcel, Void Fill & For-Sale Edges Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Painted parcel dirt with a Floor G crown cut, a camera-following dark void so the screen never shows sky under ground, and For Sale signposts at DirtBand ± edges (no land buy).

**Architecture:** Keep `DirtBand` limits (−80…100). `TilemapTowerView` paints `dirt_crown` / `dirt_fill` tiles instead of flat brown. New `UndergroundVoidFill` tracks the camera below Floor G behind the structure layer. `LandEdgeMarkers` places non-interactive For Sale sprites at MinX/MaxX.

**Tech Stack:** Unity Built-in RP, structure Tilemap, SpriteRenderer, Resources `.bytes`, NUnit EditMode.

**Spec:** `docs/superpowers/specs/2026-08-23-dirt-parcel-forsale-edges-design.md`

## Global Constraints

- Parcel edges fixed: `DirtBand.MinX = -80`, `DirtBand.MaxX = 100`, Depth ≥ 10.
- Crown at `y == -1`; fill at `y <= -2`.
- Void fill darker than parcel; never covers above Floor G (`y >= 0`).
- For Sale signs at ground line beside MinX / MaxX; **no buy logic**.
- Missing dirt art → flat `DirtBand.Color` fallback.
- Do not commit `.superpowers/sdd/*`, `_Recovery/`, `*.wip`.
- PowerShell: use `;` not `&&`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Rendering/DirtBand.cs` | Crown/fill helpers |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Painted dirt paint path |
| `Assets/Scripts/Rendering/UndergroundVoidFill.cs` | Camera void plane |
| `Assets/Scripts/Rendering/LandEdgeMarkers.cs` | For Sale signs |
| `Assets/Scripts/Build/BuildController.cs` | Wire void + signs |
| `Assets/Tests/EditMode/DirtBandTests.cs` | Crown/fill + restore tests |
| `Assets/Resources/Art/Dirt/…` | Art assets |

---

### Task 1: DirtBand crown/fill selection + tests

**Files:**
- Modify: `Assets/Scripts/Rendering/DirtBand.cs`
- Create: `Assets/Tests/EditMode/DirtBandTests.cs`

**Interfaces:**
```csharp
public static bool IsCrownRow(int cellY) => cellY == -1;
public static bool IsFillRow(int cellY) => cellY <= -2 && cellY >= -Depth;
// Resource name helpers for later paint:
public static string DirtTileResource(int cellY, int cellX) =>
    IsCrownRow(cellY) ? "dirt_crown" :
    (HashFillVariant(cellX, cellY) == 0 ? "dirt_fill" : "dirt_fill"); // single fill OK; second variant optional
```

Keep `Contains` / `ShouldRestore` / `Color` / MinX/MaxX/Depth.

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void IsCrownRow_OnlyNegativeOne()
{
    Assert.IsTrue(DirtBand.IsCrownRow(-1));
    Assert.IsFalse(DirtBand.IsCrownRow(-2));
    Assert.IsFalse(DirtBand.IsCrownRow(0));
}

[Test]
public void ShouldRestore_EmptyBasementInsideBand()
{
    var grid = new TowerGrid();
    Assert.IsTrue(DirtBand.ShouldRestore(new Vector2Int(0, -1), grid));
    Assert.IsFalse(DirtBand.ShouldRestore(new Vector2Int(0, 0), grid));
}

[Test]
public void DirtTileResource_CrownVsFill()
{
    Assert.AreEqual("dirt_crown", DirtBand.DirtTileResource(-1, 0));
    Assert.AreEqual("dirt_fill", DirtBand.DirtTileResource(-3, 0));
}
```

- [ ] **Step 2: Expect FAIL → implement → PASS → commit**

```
feat: distinguish dirt crown vs fill cell selection
```

---

### Task 2: Paint painted dirt tiles in TilemapTowerView

**Files:**
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`

**Behavior:**
- Load dirt tiles from Resources (`Art/Dirt/dirt_crown`, `Art/Dirt/dirt_fill` or `Art/Structure/…` — pick one folder and use it consistently; prefer `Art/Dirt/`).
- Prefer `.bytes` TextAsset → `LoadImage` → Tile (mirror structure-art load style if present); else Texture2D; else fall back to `GetTile(DirtBand.Color, …)`.
- `PaintStarterGuides` and `PaintDirtCell` use crown/fill selection via `DirtBand.DirtTileResource`.

- [ ] **Step 1: Implement load + paint with flat-color fallback.**
- [ ] **Step 2: Compile check (Unity or Roslyn).**
- [ ] **Step 3: Commit**

```
feat: paint parcel dirt from crown and fill art tiles
```

---

### Task 3: Underground void fill

**Files:**
- Create: `Assets/Scripts/Rendering/UndergroundVoidFill.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs` (ensure component + camera ref)

**Behavior:**
```csharp
public sealed class UndergroundVoidFill : MonoBehaviour
{
    public void Bind(Camera cam);
    // LateUpdate: position/scale a SpriteRenderer (or Quad) so it covers the
    // viewport below y=0; sortingOrder behind structure dirt (e.g. -20);
    // color darker than DirtBand.Color.
}
```

- [ ] **Step 1: Implement + bind from BuildController Awake/Start near guide paint.**
- [ ] **Step 2: Commit**

```
feat: add camera-following underground void fill
```

---

### Task 4: For Sale edge markers

**Files:**
- Create: `Assets/Scripts/Rendering/LandEdgeMarkers.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Create/extend: `Assets/Tests/EditMode/LandEdgeMarkersTests.cs` (pure position math if extracted)

**Behavior:**
- Load `for_sale_sign` from Resources; fallback simple procedural post if missing.
- Place at `(DirtBand.MinX - 0.5f, 0f)` and `(DirtBand.MaxX + 0.5f, 0f)`; flip X scale on one side optional.
- sortingOrder above dirt (~15), below elevator cars (30).

```csharp
public static Vector3 LeftSignPosition() =>
    new(DirtBand.MinX - 0.5f, 0f, 0f);
public static Vector3 RightSignPosition() =>
    new(DirtBand.MaxX + 0.5f, 0f, 0f);
```

- [ ] **Step 1: Test positions → implement spawn → commit**

```
feat: place For Sale signs at dirt parcel edges
```

---

### Task 5: Art assets

**Files:** `Assets/Resources/Art/Dirt/dirt_fill`, `dirt_crown`, `for_sale_sign` (+ png/bytes/metas)

- [ ] Generate hand-painted dirt fill + crown + For Sale signpost.
- [ ] Normalize, key backgrounds, write Resources + metas.
- [ ] Commit

```
feat: add dirt parcel and for-sale sign art
```

---

### Task 6: Docs

- [ ] Mark spec **Implemented**.
- [ ] Commit

```
docs: mark dirt parcel and for-sale edges implemented
```

## Spec coverage

| Requirement | Task |
|-------------|------|
| Crown vs fill | 1, 2 |
| Painted parcel dirt + fallback | 2, 5 |
| Void to screen bottom | 3 |
| Signs at −80 / 100 | 4, 5 |
| Demolish restore | 1, 2 (existing ShouldRestore path) |
| Docs | 6 |
