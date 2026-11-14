# Leisure Attractions Art Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Human pick gates** between contact sheets are required — do not wire a strip the user has not chosen.

**Goal:** Ship dollhouse cutaways and square menu icons for Casino, Nightclub, Chapel, and Atrium matching existing leisure art.

**Architecture:** Magenta contact-sheet generation → chroma crop → `RoomDollhouseArt` / `MenuIconArt` Resources → EditMode load tests. Cadence: per room dollhouse then menu; order Casino → Nightclub → Chapel → Atrium.

**Tech Stack:** Unity Resources PNGs + `.bytes`, C# map dictionaries, NUnit EditMode, PowerShell crop helper, image generation for contact sheets

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-01-leisure-attractions-art-design.md`
- Style: warm architectural cutaway; magenta key plate only; no painted agents
- Leaves: `casino_10x1`, `nightclub_8x1`, `chapel_6x1`, `atrium_8x3`
- Menu ids: `leisure_casino`, `leisure_nightclub`, `leisure_chapel`, `leisure_atrium`
- Human picks winner before crop/wire
- No gameplay changes

---

### Task 0: Crop helper

**Files:**
- Create: `.superpowers/sdd/leisure-art-crop.ps1` (or restore shop helper equivalent)

- [ ] **Step 1:** Implement ListOnly + strip crop writing PNG+bytes under Dollhouse/ or Menu/
- [ ] **Step 2:** Smoke ListOnly on an existing leisure PNG sheet or gym asset
- [ ] **Step 3:** Commit if helper is tracked; otherwise keep under `.superpowers/sdd/` (gitignored OK)

---

### Task 1: Casino dollhouse + menu

**Files:**
- Create: `Assets/Resources/Art/Dollhouse/casino_10x1.png` (+ `.bytes`, `.meta`)
- Create: `Assets/Resources/Art/Menu/leisure_casino.png` (+ `.bytes`, `.meta`)
- Modify: `Assets/Scripts/Rendering/RoomDollhouseArt.cs` (`["leisure_casino"] = "casino_10x1"`)
- Modify/extend: dollhouse/menu EditMode tests

- [ ] **Step 1:** Generate dollhouse contact sheet (3 strips, 10:1, casino brief, magenta bg)
- [ ] **Step 2:** **STOP — user picks strip index** (or regenerate)
- [ ] **Step 3:** Crop → Dollhouse assets; map LeafByTypeId; tests green; commit `art: add casino dollhouse sprite`
- [ ] **Step 4:** Generate menu contact sheet (3 square variants)
- [ ] **Step 5:** **STOP — user picks**
- [ ] **Step 6:** Crop → Menu assets; tests; commit `art: add casino menu icon`

---

### Task 2: Nightclub dollhouse + menu

Same pattern as Task 1 with `nightclub_8x1` / `leisure_nightclub`, aspect 8:1.

- [ ] Dollhouse generate → pick → crop/wire/commit
- [ ] Menu generate → pick → crop/wire/commit

---

### Task 3: Chapel dollhouse + menu

Same pattern: `chapel_6x1` / `leisure_chapel`, aspect 6:1.

---

### Task 4: Atrium dollhouse + menu

Same pattern: `atrium_8x3` / `leisure_atrium`, aspect **8:3** single multi-floor mass (like `theater_8x2` / department store).

---

### Task 5: Play Mode smoke

- [ ] Place all four; confirm dollhouse + menu icons (no placeholders / letterbox glyphs)

---

## Spec coverage

| Spec | Task |
|------|------|
| Casino arts | 1 |
| Nightclub arts | 2 |
| Chapel arts | 3 |
| Atrium arts | 4 |
| Crop helper | 0 |
| Smoke | 5 |
