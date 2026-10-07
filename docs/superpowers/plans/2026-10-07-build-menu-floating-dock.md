# Floating Build Dock & Info Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the tall left scrolling build panel with a right-default floating 2-column dock, family side popout, and separately draggable upper-left info panel.

**Architecture:** Keep IMGUI in the existing HUD stack. Extract pure layout helpers (`HudFloatingPanel`, popout side math) plus focused draw helpers (`BuildDockGui`, `FamilyPopoutGui`, `BuildInfoGui`). `TowerHudController` owns state, wires intents to `BuildController`, updates hit-testing, and exposes Reset layout from pause UI.

**Tech Stack:** Unity 6 C#, IMGUI, PlayerPrefs, EditMode NUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-build-menu-floating-dock-design.md`

## Global Constraints

- Approach 1 only — IMGUI refactor; no uGUI/UI Toolkit migration.
- Main dock: tools + families only; rooms live in the side popout.
- Popout: subgroup chips then room icons; prefer right of dock, flip left if clipped.
- Default dock: right edge; default info: upper-left.
- Drag via grip/title bar only; soft-clamp (slight overhang OK, never fully off-screen).
- Persist dock + info rects in PlayerPrefs; Reset on dock + pause menu.
- Icons primary; English names via tooltips; warm industrial dollhouse chrome; magenta = key plate only.
- No catalog data-model, economy, or placement-rule changes.
- Reuse existing `MenuStripColumns` (2), `MenuIconSize`, `DrawPictureButton`, catalog builders, selection line builders.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/UI/HudFloatingPanel.cs` | Soft-clamp, grip-drag state helpers, PlayerPrefs load/save/reset for named rects |
| `Assets/Scripts/UI/BuildMenuPopoutLayout.cs` | Pure popout rect + side (Right/Left) given dock + screen + popout size |
| `Assets/Scripts/UI/BuildDockGui.cs` | Draw dock chrome + 2-col tools/families (or stay as methods if extraction proves noisier — prefer separate static helper class) |
| `Assets/Scripts/UI/FamilyPopoutGui.cs` | Draw subgroup chips + room icons into a popout rect |
| `Assets/Scripts/UI/BuildInfoGui.cs` | Draw catalog vs placed selection content into info rect |
| `Assets/Scripts/UI/TowerHudController.cs` | Replace left scroll panel with dock+popout+info; hit-test; pause Reset |
| `Assets/Tests/EditMode/HudFloatingPanelTests.cs` | Soft-clamp + prefs round-trip/reset |
| `Assets/Tests/EditMode/BuildMenuPopoutLayoutTests.cs` | Right-prefer / left-flip cases |
| `Assets/Tests/EditMode/MenuLayoutTests.cs` | Keep existing strip layout tests green |

Unity EditMode test runner (from `Build-A-Tower` root; adjust Unity path if needed):

```text
& "C:\Program Files\Unity\Hub\Editor\6000.2.14f1\Editor\Unity.exe" -batchmode -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" -runTests -testPlatform EditMode -testFilter "HudFloatingPanelTests|BuildMenuPopoutLayoutTests|MenuLayoutTests" -testResults "TestResults\build-menu.xml" -logFile "unity-build-menu-tests.log" -nographics
```

---

### Task 1: HudFloatingPanel soft-clamp + PlayerPrefs

**Files:**
- Create: `Assets/Scripts/UI/HudFloatingPanel.cs`
- Create: `Assets/Tests/EditMode/HudFloatingPanelTests.cs`

**Interfaces:**
- `public static class HudFloatingPanel`
- `public const float MinVisibleGrip = 28f;` — minimum on-screen strip of the panel (grip remnant)
- `public static Rect SoftClamp(Rect panel, float screenWidth, float screenHeight, float minVisible = MinVisibleGrip)`
- `public static void SaveRect(string keyPrefix, Rect rect)` — stores `x,y,w,h` floats under `{keyPrefix}.x` etc.
- `public static bool TryLoadRect(string keyPrefix, out Rect rect)` — false if any key missing
- `public static void ClearRect(string keyPrefix)`
- Key prefixes used later: `"bat.buildDock"`, `"bat.buildInfo"`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HudFloatingPanelTests
    {
        [Test]
        public void SoftClamp_keeps_min_visible_when_fully_off_right()
        {
            var panel = new Rect(2000f, 100f, 120f, 200f);
            var clamped = HudFloatingPanel.SoftClamp(panel, 800f, 600f, minVisible: 28f);
            Assert.LessOrEqual(clamped.x, 800f - 28f);
            Assert.GreaterOrEqual(clamped.xMax, 28f);
        }

        [Test]
        public void SoftClamp_allows_slight_overhang_when_grip_still_visible()
        {
            // 40px overhang, 80px still on-screen (≥ 28) → do not pull back
            var panel = new Rect(720f, 100f, 120f, 200f);
            var clamped = HudFloatingPanel.SoftClamp(panel, 800f, 600f, minVisible: 28f);
            Assert.AreEqual(720f, clamped.x, 0.01f);
        }

        [Test]
        public void SoftClamp_keeps_min_visible_when_fully_off_top_left()
        {
            var panel = new Rect(-500f, -400f, 100f, 80f);
            var clamped = HudFloatingPanel.SoftClamp(panel, 800f, 600f, 28f);
            Assert.GreaterOrEqual(clamped.xMax, 28f);
            Assert.GreaterOrEqual(clamped.yMax, 28f);
        }

        [Test]
        public void Prefs_round_trip_and_clear()
        {
            const string key = "bat.test.hudPanel";
            HudFloatingPanel.ClearRect(key);
            Assert.IsFalse(HudFloatingPanel.TryLoadRect(key, out _));

            var original = new Rect(12f, 34f, 100f, 200f);
            HudFloatingPanel.SaveRect(key, original);
            Assert.IsTrue(HudFloatingPanel.TryLoadRect(key, out var loaded));
            Assert.AreEqual(original.x, loaded.x, 0.01f);
            Assert.AreEqual(original.y, loaded.y, 0.01f);
            Assert.AreEqual(original.width, loaded.width, 0.01f);
            Assert.AreEqual(original.height, loaded.height, 0.01f);

            HudFloatingPanel.ClearRect(key);
            Assert.IsFalse(HudFloatingPanel.TryLoadRect(key, out _));
        }
    }
}
```

- [ ] **Step 2: Run EditMode filter `HudFloatingPanelTests` — expect fail (type missing).**
- [ ] **Step 3: Implement `HudFloatingPanel`**

```csharp
using UnityEngine;

namespace BuildATower
{
    public static class HudFloatingPanel
    {
        public const float MinVisibleGrip = 28f;

        public static Rect SoftClamp(Rect panel, float screenWidth, float screenHeight, float minVisible = MinVisibleGrip)
        {
            var minV = Mathf.Max(1f, minVisible);
            // Keep at least minV pixels of the panel inside [0, screen]
            if (panel.xMax < minV) panel.x = minV - panel.width;
            if (panel.x > screenWidth - minV) panel.x = screenWidth - minV;
            if (panel.yMax < minV) panel.y = minV - panel.height;
            if (panel.y > screenHeight - minV) panel.y = screenHeight - minV;
            return panel;
        }

        public static void SaveRect(string keyPrefix, Rect rect)
        {
            PlayerPrefs.SetFloat(keyPrefix + ".x", rect.x);
            PlayerPrefs.SetFloat(keyPrefix + ".y", rect.y);
            PlayerPrefs.SetFloat(keyPrefix + ".w", rect.width);
            PlayerPrefs.SetFloat(keyPrefix + ".h", rect.height);
            PlayerPrefs.Save();
        }

        public static bool TryLoadRect(string keyPrefix, out Rect rect)
        {
            rect = default;
            var kx = keyPrefix + ".x";
            if (!PlayerPrefs.HasKey(kx)) return false;
            if (!PlayerPrefs.HasKey(keyPrefix + ".y")) return false;
            if (!PlayerPrefs.HasKey(keyPrefix + ".w")) return false;
            if (!PlayerPrefs.HasKey(keyPrefix + ".h")) return false;
            rect = new Rect(
                PlayerPrefs.GetFloat(kx),
                PlayerPrefs.GetFloat(keyPrefix + ".y"),
                PlayerPrefs.GetFloat(keyPrefix + ".w"),
                PlayerPrefs.GetFloat(keyPrefix + ".h"));
            return true;
        }

        public static void ClearRect(string keyPrefix)
        {
            PlayerPrefs.DeleteKey(keyPrefix + ".x");
            PlayerPrefs.DeleteKey(keyPrefix + ".y");
            PlayerPrefs.DeleteKey(keyPrefix + ".w");
            PlayerPrefs.DeleteKey(keyPrefix + ".h");
            PlayerPrefs.Save();
        }
    }
}
```

Also add a small drag helper usable by IMGUI (optional in this task, required by Task 3):

```csharp
public static bool DragGrip(Rect gripRect, ref Rect panelRect, float screenW, float screenH, ref bool dragging, ref Vector2 dragOffset)
{
    var e = Event.current;
    var id = GUIUtility.GetControlID(FocusType.Passive);
    switch (e.GetTypeForControl(id))
    {
        case EventType.MouseDown:
            if (gripRect.Contains(e.mousePosition) && e.button == 0)
            {
                GUIUtility.hotControl = id;
                dragging = true;
                dragOffset = e.mousePosition - new Vector2(panelRect.x, panelRect.y);
                e.Use();
                return true;
            }
            break;
        case EventType.MouseDrag:
            if (GUIUtility.hotControl == id && dragging)
            {
                panelRect.x = e.mousePosition.x - dragOffset.x;
                panelRect.y = e.mousePosition.y - dragOffset.y;
                panelRect = SoftClamp(panelRect, screenW, screenH);
                e.Use();
                return true;
            }
            break;
        case EventType.MouseUp:
            if (GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                dragging = false;
                e.Use();
                return true;
            }
            break;
    }
    return false;
}
```

- [ ] **Step 4: Run tests — expect pass.**
- [ ] **Step 5: Commit** `feat: add HudFloatingPanel soft-clamp and prefs`

---

### Task 2: Popout side layout math

**Files:**
- Create: `Assets/Scripts/UI/BuildMenuPopoutLayout.cs`
- Create: `Assets/Tests/EditMode/BuildMenuPopoutLayoutTests.cs`

**Interfaces:**
- `public enum BuildMenuPopoutSide { Right, Left }`
- `public static class BuildMenuPopoutLayout`
- `public static BuildMenuPopoutSide ChooseSide(Rect dock, Vector2 popoutSize, float screenWidth, float gap = 4f)`
  - Prefer Right if `dock.xMax + gap + popoutSize.x <= screenWidth`; else Left.
- `public static Rect Place(Rect dock, Vector2 popoutSize, BuildMenuPopoutSide side, float gap = 4f)`
  - Right: `x = dock.xMax + gap`, `y = dock.y`
  - Left: `x = dock.x - gap - popoutSize.x`, `y = dock.y`

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void ChooseSide_prefers_right_when_space()
{
    var dock = new Rect(500f, 100f, 100f, 300f);
    var side = BuildMenuPopoutLayout.ChooseSide(dock, new Vector2(160f, 280f), screenWidth: 800f);
    Assert.AreEqual(BuildMenuPopoutSide.Right, side);
}

[Test]
public void ChooseSide_flips_left_when_right_clips()
{
    var dock = new Rect(700f, 100f, 100f, 300f);
    var side = BuildMenuPopoutLayout.ChooseSide(dock, new Vector2(160f, 280f), screenWidth: 800f);
    Assert.AreEqual(BuildMenuPopoutSide.Left, side);
}

[Test]
public void Place_right_and_left_rects()
{
    var dock = new Rect(600f, 80f, 100f, 200f);
    var size = new Vector2(150f, 180f);
    var right = BuildMenuPopoutLayout.Place(dock, size, BuildMenuPopoutSide.Right, gap: 4f);
    Assert.AreEqual(704f, right.x, 0.01f);
    Assert.AreEqual(80f, right.y, 0.01f);
    Assert.AreEqual(150f, right.width, 0.01f);

    var left = BuildMenuPopoutLayout.Place(dock, size, BuildMenuPopoutSide.Left, gap: 4f);
    Assert.AreEqual(600f - 4f - 150f, left.x, 0.01f);
}
```

- [ ] **Step 2: Run — expect fail.**
- [ ] **Step 3: Implement `BuildMenuPopoutLayout`.**
- [ ] **Step 4: Run — expect pass.**
- [ ] **Step 5: Commit** `feat: add build menu popout side layout`

---

### Task 3: Wire floating dock + popout + info into TowerHudController

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (replace the left `GUI.BeginScrollView` build panel block ~lines 667–800 with floating regions; keep top bar / pause / maps / news)
- Optionally create helper classes `BuildDockGui.cs` / `FamilyPopoutGui.cs` / `BuildInfoGui.cs` if methods would make `TowerHudController` worse; otherwise private methods are OK **as long as** the left scroll monolith is removed.

**State / prefs:**
- `_dockRect`, `_infoRect` fields; load via `HudFloatingPanel.TryLoadRect` in `Awake`/`OnEnable`, else defaults:
  - Dock default: width ≈ `2 * MenuIconSize + gaps + padding` (~120–140), height fit tools+families, `x = Screen.width - width - gap`, `y ≈ mid`.
  - Info default: `x = gap`, `y = gap + news + topBar + 6`, width ~260, height content-driven or min ~160.
- Persist on mouse-up after drag (and optionally on disable).
- `_expandedFamily` already exists — keep; popout visibility = `_expandedFamily.HasValue`.
- `_popoutRect` computed each frame via `BuildMenuPopoutLayout`.
- Drag: grip bars only via `HudFloatingPanel.DragGrip`.
- Tiny **Reset** button on dock grip → `ResetBuildMenuLayout()`.
- Public `void ResetBuildMenuLayout()` clears prefs, restores defaults, closes popout.

**Draw behavior:**
1. Draw dock box + grip (“Build”) + reset + 2-col `DrawToolIcons` then family icons (extract family row from `DrawIconCatalog` without inline rooms).
2. If family open: draw popout — for Shops show subgroup chips then rooms; for other families show rooms (and any existing subgroup pattern). Reuse `DrawPictureButton` / `DrawRoomIconGrid` logic.
3. Draw info panel grip + content:
   - If `build.SelectedRoom != null` → existing Selection block content (summary, economy lines, price/staff/research/elevator controls).
   - Else if tool/room armed → help text + economy lines for `SelectedRoomType` / tool blurb (former Catalog header content).
   - Else idle label.
4. Remove the old single left scroll panel and `_scroll` for that panel.
5. Update `ContainsGuiPoint` to union `_dockRect`, `_infoRect`, and `_popoutRect` (when open) instead of (or in addition to clearing) old `_panelRect`. Prefer setting `_panelRect` unused/zero or make `_panelRect` the dock for any code that used `xMax` for maps/legend offset — **update maps/legend anchoring** to use dock or screen-right safely so they don’t assume left panel.

**Pause Reset:**
- In pause root (`DrawPauseOverlay` main buttons) and/or `DrawPauseOptions`, add button **Reset layout** calling `ResetBuildMenuLayout()` (spec: main/pause menu — pause Esc menu counts; also add to Options).

- [ ] **Step 1: Implement wiring; keep `MenuLayoutTests` green.**
- [ ] **Step 2: Run EditMode filters `MenuLayoutTests|HudFloatingPanelTests|BuildMenuPopoutLayoutTests` — expect pass.**
- [ ] **Step 3: Manual Play Mode smoke** (document in commit body if automated UI tests unavailable): dock right, popout flip, drag clamp, info swap, reset.
- [ ] **Step 4: Commit** `feat: floating build dock, family popout, and info panel`

---

### Task 4: Pause-menu Reset layout polish + hit-test regression

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` — ensure Reset is reachable from Esc pause main list (not only Options); ensure `ContainsGuiPoint` covers dock+popout+info.
- Create or extend: `Assets/Tests/EditMode/BuildMenuLayoutResetTests.cs` — test `ResetBuildMenuLayout` clears prefs and restores default dock x near right (instantiate controller via new GameObject + `AddComponent` if feasible without full scene; if MonoBehaviour Awake needs simulation, test a packageable static `BuildMenuLayoutDefaults.DockRect(screenW, screenH)` / `InfoRect(...)` instead and call those from Reset).

**Preferred extract for testability:**

```csharp
public static class BuildMenuLayoutDefaults
{
    public static Rect DefaultDock(float screenW, float screenH, float gap = 8f) { /* right-aligned */ }
    public static Rect DefaultInfo(float screenW, float screenH, float topY, float gap = 8f) { /* UL */ }
}
```

- [ ] **Step 1: Failing test** that `DefaultDock` places `xMax` within `gap` of `screenW`, and Reset clears prefs keys `bat.buildDock.*` / `bat.buildInfo.*`.
- [ ] **Step 2: Implement defaults helper + pause button + ContainsGuiPoint union.**
- [ ] **Step 3: Tests pass; commit** `feat: reset build menu layout from pause menu`

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| 2-col dock tools+families | 3 |
| Family side popout + subgroup chips | 3 |
| Prefer right / flip left | 2, 3 |
| Default dock right / info UL | 3, 4 |
| Grip-only drag + soft clamp | 1, 3 |
| Info catalog ↔ selection | 3 |
| PlayerPrefs + Reset dock + pause | 1, 3, 4 |
| Hit-test blocking | 3, 4 |
| No economy/placement changes | all |

## Self-review notes

- No TBD placeholders.
- Soft-clamp definition matches “slight overhang OK” (Task 1 tests).
- Maps/legend left-panel offset must be updated in Task 3 — do not leave `_panelRect` as a ghost left panel.
