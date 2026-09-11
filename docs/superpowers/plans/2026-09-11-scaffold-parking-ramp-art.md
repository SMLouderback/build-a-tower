# Scaffolding & Parking Ramp Structure Art Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give parking ramps stairs-style transparent cutaway overlays and scaffolding repeating wood-stud structure tiles so both match the building cutaway look.

**Architecture:** Extend `StructureCutawayArt` with single-look ramp and scaffold loaders. `TilemapTowerView` paints ramps via SpriteRenderer overlays (mirror stairs) and scaffolding via structure-layer sprites instead of tan procedural tiles. New art lives under `Resources/Art/Structure/`.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode, existing structure art crop/meta patterns

## Global Constraints

- One look only (no star tiers).
- Ramps: transparent 3×2 cutaway overlays; stacking must compose like stairs.
- Scaffolding: 1×1 wood stud/plank structure tiles.
- Assets under `Resources/Art/Structure/` — not Dollhouse.
- Do not wire `Dollhouse/parking_ramp_3x2.png`.
- Missing art falls back to procedural tiles.
- Placement / parking accessibility / costs unchanged.
- Spec: `docs/superpowers/specs/2026-09-11-scaffold-parking-ramp-art-design.md`

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Resources/Art/Structure/parking_ramp_cutaway.png` (+ meta) | Transparent 3×2 ramp cutaway |
| `Assets/Resources/Art/Structure/scaffolding_stud.png` (+ meta) | 1×1 wood stud tile |
| `Assets/Scripts/Rendering/StructureCutawayArt.cs` | `TryRampSprite`, `TryScaffoldSprite` |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Ramp overlays + scaffold structure paint |
| `Assets/Tests/EditMode/StructureCutawayArtTests.cs` | Loader coverage |
| `Assets/Tests/EditMode/TilemapTowerViewTests.cs` or new `RampScaffoldArtTests.cs` | Paint-path coverage if EditMode-safe seams exist |

---

### Task 1: Create structure art assets

**Files:**
- Create: `Assets/Resources/Art/Structure/parking_ramp_cutaway.png` (+ `.meta`)
- Create: `Assets/Resources/Art/Structure/scaffolding_stud.png` (+ `.meta`)

**Interfaces:**
- Produces two PNG resources loadable via `Resources.Load<Texture2D>("Art/Structure/...")`

- [ ] **Step 1: Generate parking ramp cutaway**

Generate (or crop from a magenta contact sheet) a **3×2** industrial concrete parking ramp cutaway:
- BL→UR ascending run with chevrons/hazard edges allowed
- **Transparent / keyed corners** outside the run so stacked overlays compose
- Match structure cutaway style (point-friendly, readable at game scale)
- Save as `Assets/Resources/Art/Structure/parking_ramp_cutaway.png`
- Unity Sprite import meta: mesh type Full Rect, pivot bottom-left if authored as sprite; or leave as Texture2D for runtime Sprite.Create like stairs

- [ ] **Step 2: Generate scaffolding stud**

Generate a **1×1** light timber posts + planks tile on keyed/transparent background that tiles in bands without harsh seams.
- Save as `Assets/Resources/Art/Structure/scaffolding_stud.png`
- Same import conventions as other Structure tiles

- [ ] **Step 3: Visual sanity**

Open both assets; confirm transparency on ramp non-run areas and readable wood grain on scaffold at ~128–256 px.

- [ ] **Step 4: Commit**

```bash
git add Assets/Resources/Art/Structure/parking_ramp_cutaway.png Assets/Resources/Art/Structure/parking_ramp_cutaway.png.meta Assets/Resources/Art/Structure/scaffolding_stud.png Assets/Resources/Art/Structure/scaffolding_stud.png.meta
git commit -m "art: add parking ramp cutaway and scaffolding stud"
```

---

### Task 2: StructureCutawayArt loaders

**Files:**
- Modify: `Assets/Scripts/Rendering/StructureCutawayArt.cs`
- Modify: `Assets/Tests/EditMode/StructureCutawayArtTests.cs`

**Interfaces:**
- Consumes Task 1 resources
- Produces `TryRampSprite(out Sprite sprite)`, `TryScaffoldSprite(out Sprite sprite)`

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void TryRampSprite_loads_cutaway_when_present()
{
    Assert.IsTrue(StructureCutawayArt.TryRampSprite(out var sprite));
    Assert.IsNotNull(sprite);
    Assert.Greater(sprite.rect.width, 1f);
    Assert.Greater(sprite.rect.height, 1f);
}

[Test]
public void TryScaffoldSprite_loads_stud_when_present()
{
    Assert.IsTrue(StructureCutawayArt.TryScaffoldSprite(out var sprite));
    Assert.IsNotNull(sprite);
}
```

Reset any static caches between tests if the class caches sprites (follow stairs test patterns).

- [ ] **Step 2: Run red**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "StructureCutawayArtTests.TryRampSprite|StructureCutawayArtTests.TryScaffoldSprite" -testResults .superpowers/sdd/ramp-scaffold-task2-red.xml -logFile .superpowers/sdd/ramp-scaffold-task2-red.log
```

Expected: missing methods / failures.

- [ ] **Step 3: Implement loaders**

Mirror `LoadOrBuildStairs` simplified (no star tiers):

```csharp
public static bool TryRampSprite(out Sprite sprite)
{
    EnsureRamp();
    sprite = _rampSprite;
    return sprite != null;
}

public static bool TryScaffoldSprite(out Sprite sprite)
{
    EnsureScaffold();
    sprite = _scaffoldSprite;
    return sprite != null;
}
```

Load `Art/Structure/parking_ramp_cutaway` and `Art/Structure/scaffolding_stud` via existing Structure resource helpers. Crop keyed transparency; bottom-left pivot; PPU so ramp spans ~3×2 and scaffold ~1×1 world units (follow stairs PPU convention: `max(cw,ch) * factor` adjusted for footprint).

- [ ] **Step 4: Run green and commit**

Expected: new tests pass.

```bash
git add Assets/Scripts/Rendering/StructureCutawayArt.cs Assets/Tests/EditMode/StructureCutawayArtTests.cs
git commit -m "feat: load parking ramp and scaffolding structure sprites"
```

---

### Task 3: Paint ramps as overlays and scaffolds as structure sprites

**Files:**
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Create or modify: `Assets/Tests/EditMode/RampScaffoldArtTests.cs` (pure helpers if full view is hard to EditMode-test)

**Interfaces:**
- Consumes `TryRampSprite` / `TryScaffoldSprite`
- Produces ramp overlay dictionary + scaffold tile path; extend refresh/clear hooks

- [ ] **Step 1: Wire ramp branch in `PaintRoom` / `PaintCell`**

After the stairs branch:

```csharp
if (room.Type.isParkingRamp)
{
    PaintRampRoom(room, occupied, color);
    return;
}
```

Implement `PaintRampRoom` mirroring `PaintStairsRoom`:
- If `TryRampSprite` → `SetRampOverlay` and return (no rooms tiles)
- Else clear overlay and paint procedural rooms tiles as today

`SetRampOverlay`:
- Parent under `RampOverlays`
- `sortingOrder = StairsOverlaySort` (or `StairsOverlaySort + room.Origin.y` if needed for stack order)
- Position at `room.Origin`; scale with fit factor ~0.90 like stairs to meet the floor above

- [ ] **Step 2: Wire scaffolding paint**

In the structure paint path used when `UsesStructureMap` / scaffolding rooms paint cells:

```csharp
if (room.Type != null && room.Type.isScaffolding &&
    StructureCutawayArt.TryScaffoldSprite(out var scaffoldSprite))
{
    // Set structure tilemap cell using a Tile that references scaffoldSprite
    // OR assign via existing GetTile-from-sprite helper if present.
}
```

Prefer reusing any existing “sprite → Tile” helper used by elevator/structure art. If none, add a small cached `Tile` for the scaffold sprite.

- [ ] **Step 3: Clear/refresh**

- Clear ramp overlay on `ClearRoom` when demolishing a ramp.
- Extend `RefreshStairsOverlays` into `RefreshTransitOverlays` (or add `RefreshRampOverlays`) and call it from the same BuildController star/structure refresh site that refreshes stairs.

- [ ] **Step 4: Tests**

If EditMode can assert without a full scene:
- Static/helper test that ramp rooms are classified for overlay paint when sprite exists.
- Else document Play Mode smoke as the paint verification and keep loader tests as the automated gate.

Minimum automated gate: existing StructureCutawayArtTests + any new pure classification tests still pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Rendering/TilemapTowerView.cs Assets/Tests/EditMode/RampScaffoldArtTests.cs Assets/Tests/EditMode/RampScaffoldArtTests.cs.meta
git commit -m "feat: paint ramps and scaffolding with structure art"
```

---

### Task 4: Verification and spec completion

**Files:**
- Modify: `docs/superpowers/specs/2026-09-11-scaffold-parking-ramp-art-design.md`

- [ ] **Step 1: Focused EditMode**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "StructureCutawayArtTests|RampScaffoldArtTests" -testResults .superpowers/sdd/ramp-scaffold-final.xml -logFile .superpowers/sdd/ramp-scaffold-final.log
```

Expected: zero failures in these fixtures.

- [ ] **Step 2: Play Mode smoke**

1. Place scaffolding bands — wood stud tiles, not flat tan.
2. Place a parking ramp — cutaway overlay visible.
3. Stack a second ramp flight — both flights readable (transparent corners).
4. Demolish a ramp — overlay cleared; underlay restores.
5. Confirm underground parking lots / valet still use dollhouse art.

- [ ] **Step 3: Mark spec Implemented and commit**

```markdown
**Status:** Implemented
```

```bash
git add docs/superpowers/specs/2026-09-11-scaffold-parking-ramp-art-design.md
git commit -m "docs: mark scaffold and ramp structure art implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Transparent ramp cutaway art | 1 |
| Wood scaffold 1×1 art | 1 |
| Structure loaders | 2 |
| Ramp overlay paint like stairs | 3 |
| Scaffold structure sprite paint | 3 |
| Fallback procedural tiles | 3 |
| No dollhouse wiring of old ramp PNG | 1–3 |
| Tests + Implemented status | 2–4 |

No TBD placeholders remain for code tasks. Exact resource leaf names: `parking_ramp_cutaway`, `scaffolding_stud`.
