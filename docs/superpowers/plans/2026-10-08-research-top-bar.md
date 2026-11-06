# Research Top-Bar Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a top-bar Research dropdown with the full project controls, and slim the selected-lab info panel to staff/status plus a pointer note.

**Architecture:** Extract `ResearchHudPanel` from `TowerHudController.DrawResearchSelection`. Wire a Research button/dropdown in the top bar (Goals/Maps pattern). Lab visibility via `EconomySystem.CountResearchLabs` (includes broken).

**Tech Stack:** Unity 6 C#, IMGUI HUD, EditMode NUnit.

**Spec:** `docs/superpowers/specs/2026-10-08-research-top-bar-design.md`

## Global Constraints

- Show Research when ≥1 lab exists (broken included).
- Caption: Idle / `{Branch} L{n}` / Paused.
- Dropdown = full Start/Pause/grid/ETA UI; room info = no project grid + note to use top bar.
- No research economy/rule changes.
- Dropdown in `ContainsGuiPoint`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/UI/ResearchHudPanel.cs` | Shared drawer + status caption helper |
| `Assets/Scripts/Economy/EconomySystem.cs` | `CountResearchLabs` |
| `Assets/Scripts/UI/TowerHudController.cs` | Top-bar button, dropdown, slim lab note |
| `Assets/Tests/EditMode/ResearchHudPanelTests.cs` | Caption + count tests |

---

### Task 1: CountResearchLabs + status caption (TDD)

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` — add `CountResearchLabs(TowerGrid grid)` counting all `ResearchId` rooms (broken included). Keep `CountNonBrokenResearchLabs` as-is.
- Create: `Assets/Scripts/UI/ResearchHudPanel.cs` (+ meta) with:
```csharp
public static class ResearchHudPanel
{
    public static string StatusCaption(ResearchSystem research);
    // Idle → "Research · Idle"
    // Running && Paused → "Research · Paused"
    // Running → "Research · {BranchDisplayName} L{level}"
}
```
- Create: `Assets/Tests/EditMode/ResearchHudPanelTests.cs` (+ meta)

- [ ] **Step 1: Failing tests** for count (0 / 1 healthy / 1 broken / mixed) and caption cases.
- [ ] **Step 2: Implement.**
- [ ] **Step 3: Pass; commit** `feat: add research lab count and top-bar status caption`

---

### Task 2: Extract ResearchHudPanel.Draw from DrawResearchSelection

**Files:**
- Modify: `ResearchHudPanel.cs` — move body of `DrawResearchSelection` into `Draw(rect/cx/cy/inner/..., ResearchSystem, TowerGrid, MarketClimate?, ref pickBranch, ref pickLevel, styles)` returning next `cy` or used height.
- Modify: `TowerHudController` — temporarily call the shared drawer from the old selection site OR delete selection grid in Task 3; prefer extract first then Task 3 wires top bar and slims selection.

Keep pick state on HUD (`_researchPickBranch` / `_researchPickLevel`) passed by ref into the drawer. Move `SyncResearchPick` / caption helpers used by the drawer into `ResearchHudPanel` if they are pure.

- [ ] **Step 1: Extract without behavior change; existing research EditMode tests still pass if any.**
- [ ] **Step 2: Commit** `refactor: extract ResearchHudPanel drawer`

---

### Task 3: Top-bar Research button + dropdown + slim lab info

**Files:**
- Modify: `TowerHudController.cs`
  - Fields: `_researchOpen`, `_researchDropdownRect`
  - In `DrawTopInfoBar` right cluster: if `CountResearchLabs >= 1`, draw button with `ResearchHudPanel.StatusCaption(simulation.Research)`; toggle `_researchOpen`; close Goals/Maps/`_infoPanel` when opening Research (and close Research when those open).
  - Draw dropdown under bar calling `ResearchHudPanel.Draw`.
  - `ContainsGuiPoint`: include `_researchOpen && _researchDropdownRect.Contains`.
  - Replace `DrawResearchSelection` call in selected-room info with staff/status + note: `Use Research in the top bar to start or pause projects.`

- [ ] **Step 1: Implement UI wiring.**
- [ ] **Step 2: Manual smoke or EditMode helper test for mutual exclusivity if extractable.**
- [ ] **Step 3: Commit** `feat: add Research top-bar dropdown dashboard`

## Spec coverage

| Spec | Task |
|------|------|
| Count labs incl. broken | 1 |
| Status caption | 1 |
| Shared full drawer | 2 |
| Top bar + hit-test + slim room note | 3 |
