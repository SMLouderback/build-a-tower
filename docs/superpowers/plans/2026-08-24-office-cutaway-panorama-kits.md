# Office Cutaway Panorama Kits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Paint each office type as a full-room cutaway panorama (2 sticky variants per type), with grey + Condemned overlay when broken.

**Architecture:** Store `ArtVariant` (1–2) on `RoomInstance` at place. `OfficeCutawayArt` loads `{typeId}_v0N` from `Art/Offices/`, slices into 128×128 tiles, and `TilemapTowerView` paints them on the rooms layer (palette fallback). Broken state adds a grey wash plus a footprint Condemned caution overlay cleared on repair.

**Tech Stack:** Unity Built-in RP, rooms Tilemap, Resources `.bytes`, NUnit EditMode.

**Spec:** `docs/superpowers/specs/2026-08-24-office-cutaway-panorama-kits-design.md`

## Global Constraints

- Offices only — 9 catalog types × 2 variants = 18 panoramas.
- Full-room panoramas sized `(widthCells × 128) × 128`; not modular shared mids across widths.
- Naming: `{typeId}_v01` / `{typeId}_v02` under `Assets/Resources/Art/Offices/`.
- Variant random at place, sticky on instance; unset/`0` → treat as `1`.
- Broken: grey darken + Condemned caution overlay; no separate damaged interiors.
- Missing art → `TowerLookPalette` flat paint (no pink).
- Honor stairs `skipCell` punch-through.
- Do not commit `.superpowers/sdd/*`, `_Recovery/`, `*.wip`.
- PowerShell: use `;` not `&&`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Core/RoomInstance.cs` | `ArtVariant` property |
| `Assets/Scripts/Core/TowerGrid.cs` | Assign random variant on successful office place |
| `Assets/Scripts/Rendering/OfficeCutawayArt.cs` | Resource names, load, slice cache |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Paint office panoramas + broken wash |
| `Assets/Scripts/Rendering/OfficeCondemnedOverlay.cs` | Caution-tape overlay spawn/clear |
| `Assets/Tests/EditMode/OfficeCutawayArtTests.cs` | Naming, sizes, variant sticky, fallback |
| `Assets/Resources/Art/Offices/…` | 18 panoramas + metas |

---

### Task 1: Sticky ArtVariant on RoomInstance + place assignment

**Files:**
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Modify: `Assets/Scripts/Core/TowerGrid.cs` (or place path that constructs rooms)
- Create: `Assets/Tests/EditMode/OfficeCutawayArtTests.cs` (variant sticky section)

**Interfaces:**
```csharp
// RoomInstance
public int ArtVariant { get; set; } // 1 or 2 when set; 0 = unset → treat as 1 at paint

// Helper (static ok)
public static int ClampArtVariant(int v) => v == 2 ? 2 : 1;
public static int RollArtVariant() => Random.Range(0, 2) == 0 ? 1 : 2; // or System.Random in tests
```

On successful place of an office (`category == Office` / `RoomCategory.Office`), set `ArtVariant = RollArtVariant()` if still `0`.

- [ ] **Step 1: Failing tests** — place office gets 1 or 2; repaint path reads same; non-office unchanged.
- [ ] **Step 2: Implement → PASS → commit**

```
feat: sticky art variant on placed offices
```

---

### Task 2: OfficeCutawayArt resource naming + expected pixel size

**Files:**
- Create: `Assets/Scripts/Rendering/OfficeCutawayArt.cs`
- Extend: `Assets/Tests/EditMode/OfficeCutawayArtTests.cs`

**Interfaces:**
```csharp
public static class OfficeCutawayArt
{
    public const int CellPixels = 128;
    public static bool IsOffice(RoomTypeSO type);
    public static string ResourceLeaf(string typeId, int variant); // office_micro_v01
    public static string ResourcePath(string typeId, int variant); // Art/Offices/...
    public static Vector2Int ExpectedPixelSize(Vector2Int cellSize); // (w*128, 128)
}
```

Map known ids from the catalog table; unknown office id still builds path from `type.id`.

- [ ] **Step 1: Tests for all 9 ids × sizes + ClampArtVariant leaf names.**
- [ ] **Step 2: Implement → PASS → commit**

```
feat: add office cutaway art resource helpers
```

---

### Task 3: Load, slice, and paint office panoramas

**Files:**
- Modify: `Assets/Scripts/Rendering/OfficeCutawayArt.cs`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`

**Behavior:**
- Bytes-first `Resources.Load<TextAsset>` → `LoadImage` → slice columns of 128×128 → `Tile` cache keyed by `(typeId, variant, cellIndex)`.
- `PaintRoom`: after lobby/elevator special cases, if office → try panorama for cells not skipped; on any load failure fall back to existing flat paint for that room.
- Do not paint ghosts with panoramas.

- [ ] **Step 1: Implement load/slice/paint with palette fallback.**
- [ ] **Step 2: Compile check (Unity or Roslyn).**
- [ ] **Step 3: Commit**

```
feat: paint office rooms from cutaway panoramas
```

---

### Task 4: Condemned overlay + grey wash when broken

**Files:**
- Create: `Assets/Scripts/Rendering/OfficeCondemnedOverlay.cs` (optional; may live on view)
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Extend tests if pure helpers extracted

**Behavior:**
- When painting broken office: grey darken tiles (multiply/lerp like existing broken wash or dedicated grey).
- Spawn/update a caution-tape “Condemned” sprite overlay spanning the room footprint; sorting above rooms, below elevator cars (~25).
- Clear overlay when room not broken or demolished (`ClearRoom`).

- [ ] **Step 1: Implement wash + overlay lifecycle.**
- [ ] **Step 2: Commit**

```
feat: condemned overlay for broken offices
```

---

### Task 5: Generate and commit office panorama art

**Files:** `Assets/Resources/Art/Offices/{typeId}_v01|_v02` (+ png/bytes/metas)

- Generate 18 hand-painted panoramas matching band look and exact pixel sizes.
- Normalize, strip plates, write Resources + metas.
- Prefer generating in band batches (Base → Mid → Upper) if tooling needs staging; one commit when complete is OK.

- [ ] **Step 1: Generate Base (6), Mid (6), Upper (6).**
- [ ] **Step 2: Normalize + metas.**
- [ ] **Step 3: Commit**

```
feat: add office cutaway panorama art
```

---

### Task 6: Docs closeout

- [ ] Mark spec **Implemented**.
- [ ] Commit

```
docs: mark office cutaway panorama kits implemented
```

## Spec coverage

| Requirement | Task |
|-------------|------|
| Sticky variant 1–2 | 1 |
| Resource names / sizes | 2 |
| Full-room paint + stairs skip | 3 |
| Broken grey + Condemned | 4 |
| 18 panoramas | 5 |
| Docs | 6 |
