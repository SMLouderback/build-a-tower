# Collapsible Top Info Bar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse optional top-bar stats into Shops / Elev / Tower dropdowns, keep money + stars + play controls always visible, show temporary context chips when a shop or elevator is selected.

**Architecture:** Extend `TowerHudController.DrawTopInfoBar` using the existing Goals dropdown overlay pattern. Add `_infoPanel` state and `_infoDropdownRect` hit-testing.

**Tech Stack:** Unity IMGUI (`TowerHudController`), no new packages.

**Spec:** `docs/superpowers/specs/2026-08-03-collapsible-top-info-bar-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Fixed bar height — dropdowns overlay; do not push the left build panel
- Preserve Goals behavior and unlock gating

---

### Task 1: HUD state + hit-test

- [x] Add `TopInfoPanel` enum (`None`, `Shops`, `Elev`, `Tower`)
- [x] Fields: `_infoPanel`, `_infoDropdownRect`
- [x] `ContainsGuiPoint` includes open info dropdown when `_infoPanel != None`

### Task 2: Slim core + dropdowns

- [x] Remove always-on shop / elev / pop / stress / crime / condo-jobs chips from the permanent row
- [x] Draw right-cluster buttons Shops · Elev · Tower · Goals with unlock gates
- [x] Render dropdown bodies per spec; Info panels mutually exclusive; Goals independent

### Task 3: Selection context chips

- [x] After money group, before stars: if selected shop → shop chips; if selected elevator → elev chips
- [x] Do not auto-open dropdowns

### Task 4: Docs

- [x] README bullet on collapsible Info buttons + selection chips
- [x] Mark design spec **Implemented**
