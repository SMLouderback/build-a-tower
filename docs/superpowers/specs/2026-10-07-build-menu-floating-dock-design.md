# Build-A-Tower — Floating Build Dock & Info Panel

**Date:** 2026-10-07  
**Status:** Approved (brainstorming)  
**Depends on:** Existing IMGUI build strip in `TowerHudController`; catalog tools/families/subgroups/rooms; `BuildController` selection APIs; main/pause menu  
**Engine target:** Unity (2D), desktop/Editor-first  
**Related (not this):** Numeric rebalance, demand/heatmap UI, uGUI/UI Toolkit migration, room economy changes

## 1. Goals

Replace the tall left scrolling build panel with a SimTower-like floating dock and a separate info panel so players get more tower screen and less scrolling.

### Success criteria

In Play Mode a player can:

1. See a compact **2-column** build dock defaulted to the **right** edge.
2. Click a **family** to open a side **popout** with subgroup chips and room icons; tools live on the main dock.
3. See the popout open to the **right** of the dock, flipping **left** only when it would clip off-screen.
4. Drag dock and info panel via **grip/title bars** only; soft-clamp keeps a usable grip on-screen.
5. See the **info panel** show the armed catalog pick while building, and switch to placed-room details/actions when a tower unit is selected.
6. Persist panel positions across sessions; recover via on-dock reset and main/pause **Reset layout**.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Architecture | Approach 1 — refactor IMGUI panels / helpers; no uGUI rebuild |
| Main strip contents | Tools + families only; rooms in side popout |
| Popout contents | Subgroup chips/tabs at top, then room icons |
| Popout side | Prefer right of dock; flip left if clipped |
| Default dock position | Right edge (SimTower-style) |
| Info content | Catalog pick while building; placed selection when a tower unit is selected; idle otherwise |
| Info placement | Default upper-left; separately draggable |
| Drag affordance | Dedicated grip / title bar only |
| Dock density | Compact 2-column icon grid |
| Clamp | Soft clamp — slight overhang OK; never fully off-screen |
| Persistence | PlayerPrefs for dock + info rects (+ optional last open family) |
| Reset | Small control on dock + **Reset layout** in main/pause menu |
| Labels | Icons primary; English names via tooltips |
| Look | Warm industrial dollhouse HUD; no toy/pink palette; magenta = key plate only |

## 3. Layout & panels

Three floating IMGUI regions (not one tall scroll):

### 3.1 BuildDock

- Default: right edge, mid-height.
- Chrome: title/grip bar + tiny reset control.
- Body: compact 2-column icon grid — **tools first**, then **family** icons.
- Click tool → select tool, close family popout.
- Click family → open/toggle `FamilyPopout` for that family.

### 3.2 FamilyPopout

- Anchored to the dock; not independently persisted as its own free-floating window.
- Prefer opening to the **right** of the dock; if the popout rect would leave the screen, open to the **left**.
- Top: subgroup chips/tabs for the active family.
- Body: room variant icons for the active subgroup (tooltips for English names).
- Selecting a room arms build mode and updates the info panel.
- Active family / selected room / armed tool use a clear selected border; unaffordable rooms stay greyed as today.

### 3.3 InfoPanel

- Default: upper-left of the screen; own grip; separately draggable and persisted.
- Content priority:
  1. If a tower unit is selected → placed-room details + actions (sell/repair/etc.; reuse existing selection summary builders).
  2. Else if a catalog room/tool is armed → cost/size/income or tool blurb.
  3. Else → compact idle (“Select a tool or room”).

## 4. Interaction & state

- **Drag:** only via each panel’s grip/title bar; icon clicks never start a drag.
- **Soft-clamp:** after each move, keep a usable strip of the grip on-screen (slight overhang allowed).
- **Family toggle:** click active family again → close popout; click another family → switch content (keep open).
- **Popout close:** choosing a non-family tool closes the popout. Escape / empty-world clear may clear tool selection and close the popout (match existing clear-selection behavior where present).
- **Input blocking:** mouse over dock, popout, or info does not paint/place on the tower (extend existing HUD hit tests).
- **Persistence keys:** dock rect, info rect, optional last-open family id. Load on HUD init; ignore corrupt/missing values and fall back to defaults.
- **Reset layout:** clears those prefs and snaps dock to default right, info to default UL, popout closed. Available from dock control and main/pause menu.

## 5. Code structure

Keep rendering in the existing HUD stack; extract focused helpers so `TowerHudController` does not grow another monolithic blob.

| Unit | Responsibility |
|------|----------------|
| `HudFloatingPanel` | Grip drag, soft-clamp, PlayerPrefs load/save/reset for a `Rect` |
| `BuildDockGui` | Draw 2-col tools + families; emit click intents |
| `FamilyPopoutGui` | Subgroup chips + room icons; compute right/left placement |
| `BuildInfoGui` | Catalog summary vs placed selection UI (reuse existing line builders) |
| `TowerHudController` | Own open-family / subgroup / rect state; wire intents to `BuildController`; hit-rects; call Reset from pause/main menu path |

No catalog data-model change — same tools/families/subgroups/rooms, new chrome only.

Suggested location: `Assets/Scripts/UI/` alongside `TowerHudController.cs` (and EditMode tests under `Assets/Tests/EditMode/`).

## 6. Visual polish

- Same IMGUI skin language as today’s build strip: tighter padding, warmer panel chrome consistent with dollhouse HUD.
- Icons primary; English on tooltips only.
- Selected/armed affordances clear; unaffordable greyed as today.
- Magenta remains key-plate only, not scene/UI color.

## 7. Out of scope

- uGUI / UI Toolkit rebuild
- Numeric rebalance, demand graphs, heatmaps
- Changes to placement rules, costs, or room economy formulas
- Redesigning non-build HUD (clock, money, season tile) except adding Reset layout to the existing main/pause menu

## 8. Verification

### Manual (Play Mode)

1. Fresh layout: dock right, info UL, popout closed.
2. Drag both panels; soft-clamp; reset from dock and from main/pause menu.
3. Family → subgroup → room → place; tool select closes popout.
4. Near right edge: popout flips left; near left (after drag): prefers right when space allows.
5. Info shows catalog while building; switches to placed selection when picking a tower unit.
6. Mouse over panels does not place rooms.

### Automated (EditMode)

- Soft-clamp math (fully off-screen input → clamped with grip remnant).
- Popout side choice (fits right → right; otherwise left).
- PlayerPrefs round-trip and Reset clears keys / returns defaults.

## 9. Non-goals for follow-ups

Above-ground parking, rebalance, and heatmap UI remain lower priority than this polish pass completing and any later numeric/visual work already queued separately.
