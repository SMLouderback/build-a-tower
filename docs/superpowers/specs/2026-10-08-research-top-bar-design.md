# Build-A-Tower — Research Top-Bar Dashboard

**Date:** 2026-10-08  
**Status:** Approved (brainstorming)  
**Depends on:** `ResearchSystem`, `EconomySystem` research lab counting, top-bar Info/Goals/Maps dropdown pattern in `TowerHudController`, floating info panel (selection)  
**Engine target:** Unity (2D) IMGUI HUD  

## 1. Goals

Move research project management out of the selected-lab info panel into a dedicated top-bar **Research** dropdown so players never need to hunt a lab to start/pause work.

### Success criteria

1. With ≥1 Research lab placed, a **Research** button appears in the top bar with status caption (Idle / active branch+level / Paused).
2. Opening it shows the full research controls (branch×level grid, Start/Pause, effect, ETA, costs, pool/labs/climate).
3. Selecting a Research lab no longer shows Start/Pause grid; shows staff/status plus a note to use the top-bar Research control.
4. Dropdown is included in HUD hit-testing (no world deselect/place while clicking controls).
5. No changes to research economy rules or catalog effects.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Architecture | Approach 1 — extract shared drawer; top-bar toggle like Goals/Maps |
| Visibility | Show when ≥1 Research lab exists (any condition, including broken) |
| Button caption | Status: Idle / `{Branch} L{n}` when running / Paused |
| Dropdown content | Full controls (same as today’s `DrawResearchSelection`) |
| Selected lab info | Staff/status only + note pointing to top-bar Research |
| Sim rules | Unchanged |

## 3. Top bar & dropdown

- Button in the right cluster near Maps/Goals.
- Visible iff `CountResearchLabs(grid) >= 1` where count includes broken labs (`Type.id == EconomySystem.ResearchId`).
- Caption helper examples:
  - Idle: `Research · Idle`
  - Running: `Research · {BranchDisplayName} L{level}`
  - Paused: `Research · Paused`
- Toggle `_researchOpen`; opening Research closes Goals/Maps/`_infoPanel` dropdowns (mutual exclusivity with other overlays, same spirit as existing top-bar panels).
- Dropdown rect under the bar; large enough for the full drawer; included in `ContainsGuiPoint`.

## 4. Room info panel

When `SelectedRoom` is a Research lab:

- Keep selection summary, economy lines, staff stepper, broken state as today.
- **Remove** branch/level Start/Pause UI from the info panel.
- Show note copy (exact or close):  
  `Use Research in the top bar to start or pause projects.`

## 5. Code structure

| Unit | Role |
|------|------|
| `ResearchHudPanel` (static drawer) | Draw full research UI given `ResearchSystem`, grid, climate, styles; owns pick branch/level state OR accepts refs from HUD |
| `EconomySystem.CountResearchLabs` | Count all labs by id (broken included) |
| `TowerHudController` | Button visibility/caption; `_researchOpen` + `_researchDropdownRect`; call drawer; slim lab selection note |
| Tests | Caption helper; lab count includes broken; drawer/catalog wiring smoke if cheap |

Pick branch/level state (`_researchPickBranch` / `_researchPickLevel`) can live on HUD or inside the drawer as long as Start/Pause still hit `ResearchSystem`.

## 6. Out of scope

- New research branches/levels or economy rebalance
- Auto-focus camera on a lab
- Separate floating Research window
- Medical/cleanliness systems

## 7. Verification

### Manual

1. No labs → no Research button.
2. Place a lab → button appears Idle; open dropdown; Start a project; caption updates; Pause works.
3. Select lab → note visible; no Start grid in info panel.
4. Click dropdown controls → selection not cleared; no accidental place.

### EditMode

- `CountResearchLabs` counts broken + healthy.
- Status caption strings for idle / running / paused.
