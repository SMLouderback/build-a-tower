# Main Menu Lobby Card Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put brand + all root menu actions inside one lobby plaque border, fit without overflow, polish atmosphere and light motion.

**Architecture:** Restructure `MainMenu.uxml` so brand/subtitle live inside the bordered plaque; restyle `MainMenu.uss` for lobby atmosphere + density; keep `MainMenuController` wiring/IDs.

**Tech Stack:** Unity UIToolkit (UXML/USS), existing MainMenuController, NUnit EditMode

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-02-main-menu-lobby-card-design.md`
- Do not rename button/`panel-*` names used by controller/tests
- All root options inside border; subpanels same chrome
- No new menu features

---

### Task 1: UXML structure — brand inside plaque

**Files:**
- Modify: `Assets/Scripts/UI/MainMenu.uxml`
- Modify: `Assets/Scripts/UI/MainMenuController.cs` (only if compact-header logic assumes brand outside panel)

- [ ] Nest `brand-title` + `subtitle` inside `panel-root` (top of plaque)
- [ ] Ensure other panels keep `class="panel"` for shared chrome
- [ ] Update controller compact-header toggles if they query brand as screen sibling
- [ ] Commit `feat: nest main menu brand inside lobby plaque`

---

### Task 2: USS lobby atmosphere + density + motion

**Files:**
- Modify: `Assets/Scripts/UI/MainMenu.uss`

- [ ] Screen: warm dark lobby gradient (multi-stop), subtle depth
- [ ] Panel/plaque: gold border, padding, max-height, centered
- [ ] Buttons: shorter height/margins so 8 buttons + brand fit; hover polish
- [ ] Optional ScrollView styles if UXML adds inner scroll for short screens
- [ ] Transitions: opacity/translate on panels or hover
- [ ] Commit `style: lobby card atmosphere and menu density`

---

### Task 3: Tests + Play Mode smoke

**Files:**
- Modify if needed: `Assets/Tests/EditMode/PlaytestMenuFlowTests.cs`

- [ ] Confirm Feedback/root buttons still queryable
- [ ] Human: 1080p + shorter height — all options inside border; subpanels match chrome
- [ ] Commit only if tests change

---

## Spec coverage

| Spec | Task |
|------|------|
| Brand inside plaque | 1 |
| No overflow | 2 |
| Subpanel chrome | 1–2 |
| Atmosphere + motion | 2 |
| IDs unchanged / tests | 3 |
