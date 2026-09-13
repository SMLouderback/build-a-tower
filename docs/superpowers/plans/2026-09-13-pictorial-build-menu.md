# Pictorial Build Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove legacy Office/Hotel/Condo from the build catalog and ship a SimTower-like pictorial IMGUI menu (tools + families + variants) with dedicated square icons.

**Architecture:** Stop feeding `placeableRooms` legacy types into `_roomButtons`. Add `MenuIconArt` to load `Art/Menu/{id}` textures. Rewrite `TowerHudController` build-panel drawing as a 2-column tools/families strip plus a variants grid. Generate menu PNGs (+ `.bytes` twins if that matches Structure/Dollhouse conventions) for tools, families, subgroups, and every catalog room.

**Tech Stack:** Unity 6000.x, C#, IMGUI HUD, NUnit EditMode, Resources art under `Art/Menu/`

**Spec:** `docs/superpowers/specs/2026-09-13-pictorial-build-menu-design.md`

## Global Constraints

- Remove legacy menu entries: displayName/id **Office**, **Hotel**, **Condo** (not the luxury `office_*` / `hotel_*` / `condo_*` sets).
- Dedicated square icons; no English text baked into icon images.
- Missing icon → color fallback; HUD must not throw.
- `BuildCatalog` data model stays; presentation changes.
- Do not regenerate dollhouse tower-view art in this plan.

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/UI/TowerHudController.cs` | Clear legacy placeableRooms path; new 2-col layout + texture buttons |
| `Assets/Scripts/UI/MenuIconArt.cs` (new) | Load/cache menu textures by id |
| `Assets/Scripts/UI/BuildCatalog.cs` | Unchanged grouping (verify only) |
| `Assets/Resources/Art/Menu/*` | Icon PNGs (+ bytes/meta as needed) |
| `Assets/Tests/EditMode/MenuCatalogTests.cs` (new) | Legacy absent; catalog counts |
| `Assets/Tests/EditMode/MenuIconArtTests.cs` (new) | Loader present/missing |

---

### Task 1: Drop legacy Office / Hotel / Condo from catalog (TDD)

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Create: `Assets/Tests/EditMode/MenuCatalogTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Catalog_excludes_legacy_office_hotel_condo()
{
    // Build catalog the same way HUD does (or expose a test helper / static builder).
    // Assert no room with id in { "office", "hotel", "condo" } (and/or displayName exact match).
    // Assert Office family has 9 rooms, Hotel 9, Condo 9.
}
```

Use actual Resources-loaded RoomTypeSO ids from assets if helpers exist; otherwise construct a filtered list mirroring `EnsureElevatorAndCatalog` rules.

- [ ] **Step 2: Implement**

In `EnsureElevatorAndCatalog`, either:
- Stop iterating `placeableRooms`, **or**
- Skip entries whose id/displayName is legacy Office/Hotel/Condo (and clear scene list if serialized),

Prefer: filter `placeableRooms` with an explicit deny-list so other intentional scene entries (if any) can remain, **and** clear known legacy refs from the deny-list. Document deny-list ids: `office`, `hotel`, `condo` (confirm asset ids).

- [ ] **Step 3: Green + commit**

```bash
git commit -m "fix: remove legacy Office Hotel Condo from build menu"
```

---

### Task 2: MenuIconArt loader (TDD)

**Files:**
- Create: `Assets/Scripts/UI/MenuIconArt.cs`
- Create: `Assets/Tests/EditMode/MenuIconArtTests.cs`

- [ ] **Step 1: Failing tests** for `TryGetTexture(string id, out Texture2D)` — false when missing; after a stub asset (or skip present-assert until Task 3), true when present.

Minimal API:

```csharp
public static class MenuIconArt
{
    public const string ResourcesRoot = "Art/Menu/";
    public static bool TryGetTexture(string id, out Texture2D tex);
    public static void ResetCache();
}
```

Load via `.bytes` TextAsset then `LoadImage` if that matches Dollhouse/Structure conventions; else `Resources.Load<Texture2D>`.

- [ ] **Step 2: Implement + green + commit**

```bash
git commit -m "feat: add MenuIconArt loader for build menu icons"
```

---

### Task 3: Generate and install menu icons

**Files:**
- Create: `Assets/Resources/Art/Menu/*.png` (+ `.bytes` / `.meta` per project convention)

Icon id list (stable):

**Tools:** `tool_select`, `tool_lobby`, `tool_sky_lobby`, `tool_scaffold`, `tool_bulldoze`  
**Families:** `family_office`, `family_hotel`, `family_condo`, `family_shops`, `family_utility`, `family_transit`  
**Shop subgroups:** `subgroup_food`, `subgroup_retail`  
**Rooms:** one icon per catalog `room.id` (e.g. `office_micro`, `hotel_base`, `condo_studio`, `shop_fast_food`, `stairs`, `elevator_normal`, …)

Style: warm dollhouse mini-scenes / clear symbols; square; no English on image; magenta key OK.

- [ ] Generate in batches; install under `Art/Menu/`; commit.

```bash
git commit -m "art: add pictorial build menu icons"
```

(May be multiple commits by batch if needed.)

---

### Task 4: Wire pictorial 2-column HUD layout

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`

- [ ] Replace glyph-based family/room/tool drawing with `MenuIconArt.TryGetTexture` + `GUI.DrawTexture` inside `DrawIconButton` (or a new `DrawPictureButton`).
- [ ] Restructure build panel: tools 2-col → families 2-col → variants grid (shops: subgroup row then rooms).
- [ ] Fallback: existing placeholder color fill if texture missing.
- [ ] Keep tooltips and selection outlines.
- [ ] Bump `IconSize` slightly if needed for readability (e.g. 40–48) without breaking panel width.

- [ ] Play-smoke checklist (manual / report): no legacy trio; icons visible; select room still builds.

```bash
git commit -m "feat: pictorial SimTower-like build menu layout"
```

---

### Task 5: Verification + spec Implemented

- [ ] EditMode: `MenuCatalogTests` + `MenuIconArtTests` green.
- [ ] Mark spec **Implemented**.
- [ ] Commit:

```bash
git commit -m "docs: mark pictorial build menu implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Remove legacy Office/Hotel/Condo | 1 |
| Dedicated square icons | 3 |
| 2-col strip + variants | 4 |
| MenuIconArt + fallback | 2, 4 |
| Tests | 1, 2, 5 |
| Spec Implemented | 5 |
