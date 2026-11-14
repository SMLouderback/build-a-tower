# Hotel Cutaway Panorama Kits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Paint hotel rooms as full-room cutaway panoramas shared by footprint × luxury band, with dirty tint and broken caution overlay (both readable when combined).

**Architecture:** `HotelCutawayArt` resolves art from `(widthCells, luxuryBand)` → one of 6 keys under `Art/Hotels/`, slices into 128×128 tiles, and `TilemapTowerView` paints them on the rooms layer (palette fallback). Dirty applies a warm tile tint (+ optional cleaning marker); broken applies grey wash + caution-tape overlay (reuse/generalize office condemned pattern). Dirty+broken keep both cues.

**Tech Stack:** Unity Built-in RP, rooms Tilemap, Resources `.bytes`, NUnit EditMode.

**Spec:** `docs/superpowers/specs/2026-08-26-hotel-cutaway-panorama-kits-design.md`

## Global Constraints

- Hotels on the build menu (9 types) via **6 shared pans** — do not remove menu types.
- Art sharing: same **size × luxury band** → same art key; 1 variant per key for v1.
- Full-room panoramas sized `(widthCells × 128) × 128`; **no non-uniform stretch**.
- Naming: `hotel_{W}_{band}` under `Assets/Resources/Art/Hotels/` (e.g. `hotel_3_base`).
- Quality bar: consistent orthographic perspective; finished ends; no mini-rooms stuck together; multi-zone only with real walls/doors; floor/ceiling in painted art.
- Dirty: clean pan + warm tint (optional cleaning marker); no separate dirty interiors.
- Broken: grey wash + caution overlay; clear on repair.
- Dirty + broken: both readable.
- Missing art → `TowerLookPalette` flat paint (no pink).
- Honor stairs `skipCell` punch-through.
- Do not refactor offices into a shared `RoomCutawayArt` in this slice.
- Do not commit `.superpowers/sdd/*`, `_Recovery/`, `*.wip`.
- PowerShell: use `;` not `&&`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Rendering/HotelCutawayArt.cs` | IsHotel, resolve art key, load/slice/cache, TryHotelTile |
| `Assets/Scripts/Rendering/HotelRoomOverlays.cs` (or extend office overlay) | Broken caution overlay + optional dirty marker for hotels |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Paint hotel pans; dirty/broken tint + overlay sync/clear |
| `Assets/Scripts/Rendering/OfficeCondemnedOverlay.cs` | Reuse caution sprite builder if generalized; else leave offices alone |
| `Assets/Tests/EditMode/HotelCutawayArtTests.cs` | Key resolution, sizes, IsHotel, cache poison guard |
| `Assets/Resources/Art/Hotels/…` | 6 panoramas + `.bytes` + metas |

**Art keys**

| Key | Cells | Px | Types |
|-----|-------|-----|-------|
| `hotel_3_base` | 3 | 384×128 | base, accessible |
| `hotel_4_mid` | 4 | 512×128 | mid_standard (+ legacy premium → mid) |
| `hotel_5_mid` | 5 | 640×128 | studio, junior_suite |
| `hotel_6_mid` | 6 | 768×128 | mid_extended |
| `hotel_5_upper` | 5 | 640×128 | upper_standard, upper_king |
| `hotel_8_upper` | 8 | 1024×128 | upper_suite |

---

### Task 1: HotelCutawayArt — IsHotel, art-key resolve, expected sizes

**Files:**
- Create: `Assets/Scripts/Rendering/HotelCutawayArt.cs`
- Create: `Assets/Tests/EditMode/HotelCutawayArtTests.cs`

**Interfaces:**
```csharp
public static class HotelCutawayArt
{
    public const int CellPixels = 128;
    public static bool IsHotel(RoomTypeSO type); // category == Hotel (or Living hotel ids)
    public static string ResolveArtKey(RoomTypeSO type); // hotel_3_base, …
    public static string ResourcePath(string artKey); // Art/Hotels/{artKey}
    public static Vector2Int ExpectedPixelSize(string artKey);
    public static void ResetForTests();
}
```

Resolve from `type.size.x` + `type.luxuryBand` (and legacy `hotel_premium` → `hotel_4_mid`). Unknown/mismatched → empty key / paint fallback later.

- [ ] **Step 1: Failing tests** — all 9 menu ids map to the 6 keys; sizes match table; non-hotel false.
- [ ] **Step 2: Implement → PASS → commit**

```
feat: add hotel cutaway art key resolver
```

---

### Task 2: Load, slice, cache, and paint hotel panoramas

**Files:**
- Modify: `Assets/Scripts/Rendering/HotelCutawayArt.cs`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Extend: `Assets/Tests/EditMode/HotelCutawayArtTests.cs` (cache / width mismatch)

**Behavior:**
- Bytes-first `Resources.Load<TextAsset>` → `LoadImage` → slice 128×128 → `Tile[]` cache keyed by art key.
- **Do not cache null** on failed load / width mismatch (office poison lesson).
- `PaintRoom` / `PaintCell`: after office branch, if hotel → try panorama; else palette.
- Honor `skipCell`; no ghosts.

- [ ] **Step 1: Failing test** — width mismatch does not permanently poison cache for correct width.
- [ ] **Step 2: Implement load/slice/paint with palette fallback → PASS.**
- [ ] **Step 3: Commit**

```
feat: paint hotel rooms from cutaway panoramas
```

---

### Task 3: Dirty tint + broken caution overlay (both readable)

**Files:**
- Create or modify: `Assets/Scripts/Rendering/HotelRoomOverlays.cs` and/or reuse `OfficeCondemnedOverlay`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Tests for tint helpers / overlay clear if extracted

**Behavior:**
- Dirty hotel cutaway: warm brownish tile tint (align with existing dirty `RoomPaintColor` intent); optional light cleaning marker.
- Broken hotel cutaway: grey wash + caution-tape footprint overlay; clear on repair / `ClearRoom`.
- Dirty + broken: broken wash + caution overlay **and** dirty cue still visible.
- Offices’ condemned behavior must not regress.

- [ ] **Step 1: Implement dirty/broken visual paths for hotel pans.**
- [ ] **Step 2: Commit**

```
feat: hotel dirty tint and broken caution overlay
```

---

### Task 4: Generate and commit 6 hotel panorama arts

**Files:** `Assets/Resources/Art/Hotels/{artKey}.{png,bytes}` (+ metas)

- Author Base → Mid → Upper pans at exact pixel sizes.
- Enforce quality bar: no stretch, no letterbox, finished ends, coherent multi-zone only with walls/doors.
- Normalize pipeline (crop plate/frame, uniform height 128, stitch same-scale zones if needed).
- Verify png ≡ bytes and exact dimensions.

- [ ] **Step 1: Generate + normalize all 6 keys.**
- [ ] **Step 2: Play Mode smoke (optional) — place each menu hotel; check dirty + broken.**
- [ ] **Step 3: Commit**

```
feat: add hotel cutaway panorama art
```

---

### Task 5: Docs closeout

- [ ] Mark spec **Implemented** with plan path / art commit.
- [ ] Commit

```
docs: mark hotel cutaway panorama kits implemented
```

## Spec coverage

| Requirement | Task |
|-------------|------|
| 6 shared keys / menu types | 1 |
| Paint + stairs skip + palette fallback | 2 |
| Dirty + broken (+ both) | 3 |
| 6 panoramas + quality bar | 4 |
| Docs | 5 |
