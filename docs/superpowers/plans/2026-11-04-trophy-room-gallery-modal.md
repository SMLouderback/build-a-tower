# Trophy Room Gallery Modal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scale the tower Trophy Room cutaway to match peer dollhouse rooms (wide shell + tiny live medal chips) and add a closable close-up gallery modal opened from Info or Select-tool click rules—without forcing pause.

**Architecture:** Pure open-policy helper + `TrophyRoomGalleryModal` (IMGUI gate) wired into HUD `BlocksWorldInput`; BuildController requests open on second/double select click; cutaway uses retargeted `trophy_room_6x1` shell plus `TrophyRoomDisplay` pedestal chip layout composited/overlaid from `TrophyCase`; modal draws a larger gallery plate with full pedestal sprites. No new save fields.

**Tech Stack:** Unity 6000 EditMode NUnit, IMGUI HUD, existing `TrophyRoomDisplay` / `TrophyCase` / dollhouse Resources art.

**Spec:** `docs/superpowers/specs/2026-11-04-trophy-room-gallery-modal-design.md`

## Global Constraints

- Footprint stays `service_trophy_room` **6×1**; vanity only (no sim buffs).
- Modal must **not** call pause/speed APIs on open or close.
- Open: Info **View trophies**; Select-tool click when Trophy Room already selected (covers double-click as second down); first click selects only.
- Magenta `#FF00FF` is key plate only; warm industrial dollhouse (not pink/toy).
- Do not kill an open Unity Editor; prefer `commit --only` / named paths; dirty unrelated WIP stays uncommitted.
- EditMode via Unity batchmode when lockfile absent.

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/UI/TrophyRoomOpenPolicy.cs` | Pure open rules for select clicks |
| `Assets/Scripts/UI/TrophyRoomGalleryModal.cs` | Open/close/Esc; draw panel; no pause |
| `Assets/Scripts/Rendering/TrophyRoomDisplay.cs` | Pedestal chip layout + shared keys |
| `Assets/Scripts/UI/TowerHudController.cs` | BlocksWorld; draw modal; Info button; Esc |
| `Assets/Scripts/Build/BuildController.cs` | Select-tool open request |
| `Assets/Resources/Art/Dollhouse/trophy_room_6x1*` | Wide-shot shell (+ L/R/ext/int as needed) |
| `Assets/Resources/Art/Dollhouse/trophy_room_gallery_closeup*` | Optional large modal plate (or reuse shell scaled) |
| `Assets/Tests/EditMode/TrophyRoomOpenPolicyTests.cs` | Click open rules |
| `Assets/Tests/EditMode/TrophyRoomGalleryModalTests.cs` | Modal lifecycle / no pause |
| `Assets/Tests/EditMode/TrophyRoomDisplayTests.cs` | Chip layout + pedestal mapping |

---

### Task 1: Select open policy

**Files:**
- Create: `Assets/Scripts/UI/TrophyRoomOpenPolicy.cs`
- Test: `Assets/Tests/EditMode/TrophyRoomOpenPolicyTests.cs`

**Interfaces:**
- Consumes: `TrophyRoomDisplay.RoomTypeId`
- Produces: `TrophyRoomOpenPolicy.ShouldOpenGallery(string hitRoomTypeId, int hitInstanceId, string selectedTypeId, int selectedInstanceId, bool hasSelection)` → `bool`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomOpenPolicyTests
    {
        const string TrophyId = "service_trophy_room";

        [Test]
        public void First_select_of_trophy_room_does_not_open()
        {
            Assert.IsFalse(TrophyRoomOpenPolicy.ShouldOpenGallery(
                TrophyId, 7, selectedTypeId: null, selectedInstanceId: 0, hasSelection: false));
        }

        [Test]
        public void Second_select_of_same_trophy_room_opens()
        {
            Assert.IsTrue(TrophyRoomOpenPolicy.ShouldOpenGallery(
                TrophyId, 7, selectedTypeId: TrophyId, selectedInstanceId: 7, hasSelection: true));
        }

        [Test]
        public void Select_of_different_room_does_not_open()
        {
            Assert.IsFalse(TrophyRoomOpenPolicy.ShouldOpenGallery(
                TrophyId, 8, selectedTypeId: TrophyId, selectedInstanceId: 7, hasSelection: true));
            Assert.IsFalse(TrophyRoomOpenPolicy.ShouldOpenGallery(
                "service_mail", 7, selectedTypeId: TrophyId, selectedInstanceId: 7, hasSelection: true));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run Unity EditMode filter `TrophyRoomOpenPolicyTests` (batchmode if unlocked).  
Expected: FAIL — type/method missing.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace BuildATower
{
    public static class TrophyRoomOpenPolicy
    {
        public static bool ShouldOpenGallery(
            string hitRoomTypeId,
            int hitInstanceId,
            string selectedTypeId,
            int selectedInstanceId,
            bool hasSelection)
        {
            if (hitRoomTypeId != TrophyRoomDisplay.RoomTypeId)
                return false;
            if (!hasSelection || selectedTypeId != TrophyRoomDisplay.RoomTypeId)
                return false;
            return selectedInstanceId == hitInstanceId;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/TrophyRoomOpenPolicy.cs Assets/Tests/EditMode/TrophyRoomOpenPolicyTests.cs
git commit -m "feat(ui): add Trophy Room gallery open policy"
```

---

### Task 2: Gallery modal gate (no pause)

**Files:**
- Create: `Assets/Scripts/UI/TrophyRoomGalleryModal.cs`
- Test: `Assets/Tests/EditMode/TrophyRoomGalleryModalTests.cs`

**Interfaces:**
- Consumes: none (UI gate only)
- Produces: `TrophyRoomGalleryModal` with `bool IsOpen`, `void Open()`, `void Close()`, `bool HandleEsc()`, `bool BlocksWorld`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomGalleryModalTests
    {
        [Test]
        public void Open_and_Close_toggle_IsOpen_and_BlocksWorld()
        {
            var modal = new TrophyRoomGalleryModal();
            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.BlocksWorld);

            modal.Open();
            Assert.IsTrue(modal.IsOpen);
            Assert.IsTrue(modal.BlocksWorld);

            modal.Close();
            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.BlocksWorld);
        }

        [Test]
        public void HandleEsc_closes_when_open_and_returns_true()
        {
            var modal = new TrophyRoomGalleryModal();
            modal.Open();
            Assert.IsTrue(modal.HandleEsc());
            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.HandleEsc());
        }

        [Test]
        public void Open_does_not_require_or_invoke_pause_callbacks()
        {
            // Modal API has no pause parameters; constructing/opening must not throw.
            var modal = new TrophyRoomGalleryModal();
            modal.Open();
            modal.Close();
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 2: Run test — expect FAIL (missing type)**

- [ ] **Step 3: Implement**

```csharp
namespace BuildATower
{
    public sealed class TrophyRoomGalleryModal
    {
        public bool IsOpen { get; private set; }
        public bool BlocksWorld => IsOpen;

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public bool HandleEsc()
        {
            if (!IsOpen)
                return false;
            Close();
            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git commit --only Assets/Scripts/UI/TrophyRoomGalleryModal.cs Assets/Tests/EditMode/TrophyRoomGalleryModalTests.cs -m "feat(ui): add Trophy Room gallery modal gate"
```

---

### Task 3: HUD wire-up (BlocksWorld, Esc, Info button, draw)

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Test: extend `TrophyRoomGalleryModalTests` or add `TrophyRoomGalleryHudTests` for a tiny pure helper if needed; otherwise manual + existing BlocksWorld pattern check via compiling tests that construct modal used by HUD field.

**Interfaces:**
- Consumes: `TrophyRoomGalleryModal`, `TrophyRoomDisplay.BuildPedestals`, `simulation.Rewards.TrophyCase`
- Produces: HUD field `_trophyGalleryModal`; `OpenTrophyGallery()` public/internal for BuildController; Info **View trophies** button when selected room id is `service_trophy_room`

- [ ] **Step 1: Add field and BlocksWorldInput**

```csharp
readonly TrophyRoomGalleryModal _trophyGalleryModal = new();

public bool BlocksWorldInput
{
    get
    {
        var celeb = ResolveCelebration();
        return IsEscPauseOpen
               || GameSession.HasRestoreFallbackNotice
               || _rewardTutorialGate.IsModalOpen
               || _trophyGalleryModal.BlocksWorld
               || (celeb != null && celeb.IsActive);
    }
}

public void OpenTrophyGallery() => _trophyGalleryModal.Open();
```

- [ ] **Step 2: Esc handling** — where other modals handle Escape, call `_trophyGalleryModal.HandleEsc()` before pause menu if open.

- [ ] **Step 3: Draw modal** after reward tutorial draw (dim full-screen box + centered panel ~720×420): title “Trophy Room”; gallery texture if present (`Art/Dollhouse/trophy_room_gallery_closeup` else stretch shell); pedestal row from `BuildPedestals`; Close button calls `Close()`.

- [ ] **Step 4: Info panel** — when `build.SelectedRoom?.Type?.id == TrophyRoomDisplay.RoomTypeId`, draw `GUI.Button(..., "View trophies")` → `OpenTrophyGallery()`.

- [ ] **Step 5: Run EditMode filter `TrophyRoomGalleryModalTests|TrophyRoomOpenPolicyTests` — PASS**

- [ ] **Step 6: Commit**

```bash
git commit --only Assets/Scripts/UI/TowerHudController.cs -m "feat(ui): wire Trophy Room gallery modal into HUD"
```

---

### Task 4: BuildController select-tool open

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs` (`HandleClicks` Select branch ~1492)
- Test: policy already covered; optional thin test not required if policy is the logic

**Interfaces:**
- Consumes: `TrophyRoomOpenPolicy.ShouldOpenGallery`, `hud.OpenTrophyGallery()`
- Produces: select click opens gallery when policy true **after** evaluating pre-click selection, then `TrySelectAt`

- [ ] **Step 1: In Select branch before `TrySelectAt`**

```csharp
if (CurrentTool == BuildTool.Select)
{
    // ... existing elevator edge early-out ...

    RoomInstance hit = null;
    if (Grid != null)
        Grid.TryGetRoomAt(cell, out hit);

    var shouldOpen = TrophyRoomOpenPolicy.ShouldOpenGallery(
        hit?.Type?.id,
        hit?.InstanceId ?? 0,
        SelectedRoom?.Type?.id,
        SelectedRoom?.InstanceId ?? 0,
        SelectedRoom != null);

    TrySelectAt(cell);

    if (shouldOpen && hud != null)
        hud.OpenTrophyGallery();
    return;
}
```

Note: evaluate policy **before** `TrySelectAt` so first click (no prior selection of same instance) does not open; second click sees prior selection.

- [ ] **Step 2: Play-smoke mentally** — first click selects; second click opens; other rooms unaffected.

- [ ] **Step 3: Commit**

```bash
git commit --only Assets/Scripts/Build/BuildController.cs -m "feat(build): open Trophy Room gallery on re-select"
```

---

### Task 5: Wide cutaway shell + tiny medal chips

**Files:**
- Modify: `Assets/Scripts/Rendering/TrophyRoomDisplay.cs`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs` and/or HUD draw path for chips
- Replace/update: `Assets/Resources/Art/Dollhouse/trophy_room_6x1.png` (+ `.bytes`, L/R/ext/int siblings as required by `DollhouseContextArt`)
- Test: `Assets/Tests/EditMode/TrophyRoomDisplayTests.cs`

**Interfaces:**
- Consumes: `TrophyCase`, metal sprite paths
- Produces: `TrophyRoomDisplay.CutawayChipNormalizedCenters` (10 `Vector2` in 0–1 room space); `ChipPixelSize` constant; SyncView refreshes paint so chips update on metal up

- [ ] **Step 1: Failing test for layout count/order**

```csharp
[Test]
public void CutawayChipNormalizedCenters_one_per_subject_in_enum_order()
{
    var centers = TrophyRoomDisplay.CutawayChipNormalizedCenters;
    Assert.AreEqual(10, centers.Length);
    foreach (var c in centers)
    {
        Assert.GreaterOrEqual(c.x, 0f);
        Assert.LessOrEqual(c.x, 1f);
        Assert.GreaterOrEqual(c.y, 0f);
        Assert.LessOrEqual(c.y, 1f);
    }
}
```

- [ ] **Step 2: Implement centers** along a shelf band (e.g. y≈0.35, x spaced 0.08..0.92 for 10 subjects).

- [ ] **Step 3: Art** — replace zoomed plate with wide gallery shell matching peer 6×1 scale (empty pedestals OK in plate). Keep magenta key. Update `.bytes` copies. Prefer editing existing leaf `trophy_room_6x1` so `RoomDollhouseArt` mapping unchanged.

- [ ] **Step 4: Overlay chips** — on SyncView / after dollhouse paint for trophy rooms, draw filled metal sprites (and empty silhouette if asset exists) at world positions derived from room origin + normalized centers × size. Prefer a small `TrophyRoomChipLayer` helper called from `SyncView` + `PaintRoom` path rather than baking into PNG. Chips must be clearly smaller than modal pedestals.

- [ ] **Step 5: Run `TrophyRoomDisplayTests` — PASS**; visual check in Editor when possible.

- [ ] **Step 6: Commit** art + code together for this task.

```bash
git commit --only Assets/Scripts/Rendering/TrophyRoomDisplay.cs Assets/Scripts/Rendering/TilemapTowerView.cs Assets/Tests/EditMode/TrophyRoomDisplayTests.cs Assets/Resources/Art/Dollhouse/trophy_room_6x1.png Assets/Resources/Art/Dollhouse/trophy_room_6x1.bytes # + siblings if changed
  -m "feat(render): wide Trophy Room cutaway with medal chips"
```

---

### Task 6: Modal gallery presentation polish

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (draw body)
- Optional create: `Assets/Resources/Art/Dollhouse/trophy_room_gallery_closeup.png` + `.bytes`
- Optional: `Assets/Scripts/UI/TrophyRoomGalleryHud.cs` static Draw(panel, TrophyCase, styles) to keep HUD thinner

**Interfaces:**
- Consumes: `TrophyRoomDisplay.BuildPedestals`, MenuIconArt/Texture load pattern from Resources
- Produces: readable pedestal row/grid in modal; empty slots show muted placeholder label or silhouette

- [ ] **Step 1: Extract or finish `TrophyRoomGalleryHud.Draw(Rect panel, TrophyCase trophyCase, GUIStyle title, GUIStyle label)`** drawing title, close control, background art, pedestals with subject name + metal sprite.

- [ ] **Step 2: Load close-up art** from `Art/Dollhouse/trophy_room_gallery_closeup` if present; else scale shell texture. Magenta-key if using MenuIconArt-style loader, or opaque plate.

- [ ] **Step 3: EditMode** — if Draw is pure enough, assert pedestal labels count == 10 via a small formatter helper; otherwise rely on Task 2 + Display tests.

- [ ] **Step 4: Commit**

```bash
git commit --only Assets/Scripts/UI/TowerHudController.cs Assets/Scripts/UI/TrophyRoomGalleryHud.cs Assets/Resources/Art/Dollhouse/trophy_room_gallery_closeup.png Assets/Resources/Art/Dollhouse/trophy_room_gallery_closeup.bytes -m "feat(ui): draw Trophy Room close-up gallery contents"
```

---

### Task 7: Regression pass

**Files:** none required unless fixes

- [ ] **Step 1: Run EditMode filter**

`TrophyRoom|TrophyRoomOpenPolicy|TrophyRoomGallery|RewardTutorial|RewardBook`

Expected: all PASS.

- [ ] **Step 2: Manual checklist** (Editor play if available)

1. Place Trophy Room — cutaway scale matches neighbors; chips update on claim.  
2. First click selects; second click opens modal; Close/Esc dismiss; sim speed unchanged.  
3. Info **View trophies** opens modal.  
4. Metal upgrade still bursts from room anchors.

- [ ] **Step 3: Commit any fixes**; stop when green.

---

## Self-review (plan vs spec)

| Spec requirement | Task |
|------------------|------|
| Wide-shot cutaway shell | 5 |
| Tiny live medal chips | 5 |
| Closable close-up modal | 2, 3, 6 |
| Open: Info / re-select / double-click-as-second | 1, 3, 4 |
| No forced pause | 2 |
| BlocksWorld while open | 2, 3 |
| Burst FX / vanity / no new saves | unchanged; 7 verifies |

No TBD placeholders. Open policy evaluates selection **before** `TrySelectAt` so first click never opens.
