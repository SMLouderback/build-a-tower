# New Player Tutorial Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Soft guided coach for new Sandbox/Easy/Normal games teaching lobby → living → stairs → elevator place/extend, with dual Options prefs and skip-for-run.

**Architecture:** `TutorialDirector` step machine observes grid/build state; `TutorialPrefs` owns PlayerPrefs; HUD draws coach card, pulse/arrow, and Options toggles; `GameSession` one-shot start flag distinguishes new game vs load.

**Tech Stack:** Unity IMGUI HUD, PlayerPrefs, existing BuildController/TowerGrid/StarSystem, NUnit EditMode.

**Spec:** `docs/superpowers/specs/2026-10-13-new-player-tutorial-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Prefer SDD; no parallel-cli
- Soft coach only — never hard-lock build tools
- No tutorial on Hard/Extreme or save load
- Magenta is key plate only; warm industrial HUD chrome

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Tutorial/TutorialPrefs.cs` | Guided + hover tip prefs |
| `Assets/Scripts/Tutorial/TutorialStep.cs` | Step enum |
| `Assets/Scripts/Tutorial/TutorialDirector.cs` | Advance/skip/evaluate |
| `Assets/Scripts/Tutorial/TutorialHighlight.cs` | Target + copy |
| `Assets/Scripts/Core/GameSession.cs` | New-game tutorial flag |
| `Assets/Scripts/UI/TowerHudController.cs` | Coach UI, Options, tip gate |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Hook director tick if needed |
| Tests under `Assets/Tests/EditMode/` | Prefs, director, start flag |

---

### Task 1: TutorialPrefs + tests

**Files:**
- Create: `Assets/Scripts/Tutorial/TutorialPrefs.cs`
- Create: `Assets/Tests/EditMode/TutorialPrefsTests.cs`

**Interfaces:**
- `TutorialPrefs.GuidedTutorialEnabled { get; set; }` default true
- `TutorialPrefs.HoverTipsEnabled { get; set; }` default true
- Keys: `bat.tutorial.guided`, `bat.tutorial.hoverTips`
- `ResetForTests()` clears keys

- [ ] **Step 1: Failing tests** — defaults on; set/get round-trip
- [ ] **Step 2: Implement**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 2: TutorialStep + TutorialDirector completion predicates

**Files:**
- Create: `TutorialStep.cs`, `TutorialDirector.cs`, `TutorialHighlight.cs`
- Create: `TutorialDirectorTests.cs`

**Interfaces:**
- Enum: `LobbyTool, PlaceLobby, PlaceLiving, PlaceStairs, WaitElevator, PlaceElevator, ExtendElevator, Done, Inactive`
- `TryStart()`, `Skip()`, `Tick(grid, stars, lobbyToolSelected)`, `CurrentStep`, `IsActive`
- Snapshot elevator max span when entering PlaceElevator for extend detection
- Living = Office/Condo/Hotel categories

- [ ] **Step 1: Failing tests** for each completion rule + Skip + skip-ahead when lobby already exists
- [ ] **Step 2: Implement**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 3: GameSession new-game flag

**Files:**
- Modify: `GameSession.cs`
- Create: `TutorialStartFlagTests.cs` (or extend GameSession tests)

**Interfaces:**
- `GameSession.TutorialMayStart` / `ConsumeTutorialMayStart()`
- `StartNewGame`: set true iff difficulty is Sandbox|Easy|Normal && `TutorialPrefs.GuidedTutorialEnabled`
- Load / ApplySnapshot path: set false

- [ ] **Step 1: Failing tests**
- [ ] **Step 2: Implement**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 4: Wire director into simulation / HUD boot

**Files:**
- Modify: `TowerSimulation.cs` and/or `TowerHudController.cs`
- Test: boot calls `TryStart` when flag consumed

- [ ] **Step 1: Failing test or thin seam test**
- [ ] **Step 2: Wire Tick after builds / each frame with grid+stars+tool state**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 5: Coach card + pulse/arrow UI

**Files:**
- Modify: `TowerHudController.cs`
- Optional helper: `TutorialHudChrome.cs` for pulse math

**Behavior:**
- Draw coach when `director.IsActive && step != Done`
- Skip button → `director.Skip()`
- Pulse target rects for lobby tool / family icons / stairs / elevator; world strip for lobby band / elevator top
- Short copy per step (const strings OK)

- [ ] **Step 1: Minimal EditMode test for copy/highlight helper if extracted**
- [ ] **Step 2: Draw coach + highlights**
- [ ] **Step 3: Play-smoke checklist manually noted in progress**
- [ ] **Step 4: Commit** only if asked

---

### Task 6: Options toggles + hover tip gating

**Files:**
- Modify: `TowerHudController.DrawPauseOptions`
- Gate `_hoverTooltip` / `GUI.tooltip` path when hover tips off

- [ ] **Step 1: Test prefs reflected / tip helper returns empty when off**
- [ ] **Step 2: UI toggles + gate**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 7: Spec status + Play checklist

- [ ] **Step 1: Play Mode walkthrough Easy new game**
- [ ] **Step 2: Mark spec Implemented**
- [ ] **Step 3: Commit** only if asked

---

## Execution

Use **Subagent-Driven Development**. One task per subagent; verify tests before the next task.
