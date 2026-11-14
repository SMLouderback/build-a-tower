# Dollhouse Face Quality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore concept-tower dollhouse look: adjacency selects authored `_ext/_L/_R/_int` plates with finished plaster walls (not grey paint-over); keep sky-alpha windows; class accents in art only; quality must match or beat current best plates.

**Architecture:** Demote `SealInteriorSides` to missing-face-leaf fallback. Rebuild office (then hotel/condo) face plates from full-res contact sheets with surgical keying — interior sides authored solid with decor, exterior/back glass alpha. Visual-verify adjacency before expanding families.

**Tech Stack:** Unity 6, `RoomDollhouseArt` / `DollhouseContextArt` / `RoomExteriorFaces`, Resources `Art/Dollhouse`, Python pillow tools under `Tools/`, NUnit EditMode, `Tools/visual-verify`.

**Spec:** `docs/superpowers/specs/2026-10-18-dollhouse-face-quality-design.md`

## Global Constraints

- Magenta `#FF00FF` is key plate only — never scene color.
- Correct face leaf present → **no** `SealInteriorSides`.
- Shared sides = finished plaster/paint + optional decor (not flat grey, not bare concrete as the visible wall).
- Back (and exterior side) windows keep alpha for sky.
- No blanket family wall tint; accents are furniture/art/rugs/lamps/feature panels only.
- Quality: match or beat prior best plate; no cheaper/softer/more pixelated output; canvas = cells×128 PPU; fit-to-canvas once.
- Rollout: offices → hotel → condo → rest.
- Thin infrastructure underlay for seams only; must not override authored side finish.

---

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Rendering/DollhouseContextArt.cs` | Skip seal when face leaf resolves; seal only on fallback |
| `Assets/Tests/EditMode/DollhouseContextArtTests.cs` | Face-present preserves pixels; fallback still seals |
| `Tools/crop_face_contact_sheet.py` | Shared key/strip/frame helpers (quality-safe) |
| `Tools/fix_office_face_plates.py` (evolve) | Office rebuild: authored interior walls, glass alpha, no grey-fill product |
| `Tools/visual-verify/scenarios/*.json` | Solo + adjacency QA |
| `Assets/Resources/Art/Dollhouse/*` | Regenerated office (then hotel/condo) PNG+bytes |
| `Temp/RefreshDollhouse.cs` | Force reimport after plate writes |

---

### Task 1: Demote SealInteriorSides to missing-leaf fallback

**Files:**
- Modify: `Assets/Scripts/Rendering/DollhouseContextArt.cs`
- Modify: `Assets/Tests/EditMode/DollhouseContextArtTests.cs`

- [ ] **Step 1: Write failing tests**

Add:
- `Face_leaf_present_skips_seal` — stub `RoomDollhouseArt.LoadSpriteForTests` so the face path for `(office_micro, leftExterior:false, rightExterior:true)` returns a sprite with left-band glass; assert baked left-band glass equals source (no wall paint-over).
- Keep / retarget existing seal tests to a **typeId with no face leaves** (or LoadSpriteForTests returns null for face paths) so fallback still seals glass → plaster sample.

- [ ] **Step 2: Run EditMode filter `DollhouseContextArtTests` — expect new test fail**

- [ ] **Step 3: Implement**

In `ResolveSourceSprite` / `Bake`, track whether the resolved sprite came from the exact face leaf path. `Bake(source, leftExterior, rightExterior, sealMissingFace: bool)` — call `SealInteriorSides` only when `sealMissingFace` is true. When sealing, keep plaster sampling; never hardcode flat grey.

- [ ] **Step 4: Run `DollhouseContextArtTests` — green**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Rendering/DollhouseContextArt.cs Assets/Tests/EditMode/DollhouseContextArtTests.cs
git commit -m "fix(art): seal interior sides only when face leaf is missing"
```

---

### Task 2: Quality-safe office face rebuild tool

**Files:**
- Modify: `Tools/fix_office_face_plates.py`
- Modify: `Tools/crop_face_contact_sheet.py` (only if shared helpers need quality fixes)
- Reference: contact sheets under Cursor `assets/office_*_v2_face_contact.jpg` (and v3 when better)

- [ ] **Step 1: Align tool with spec §5**

Rules in code comments + logic:
- Prefer contact band that already has correct solid interior sides (`_L`/`_R`/`_int` bands); do not grey-fill exterior glass then call it a wall.
- Interior sides: keep finished plaster + decor from source; only fill hot plate magenta that is false wall/ceiling key.
- Exterior/back: key magenta + clear sky-blue pane fills to alpha.
- One `fit_to_cell_canvas`; strip modular outer shell; no “fill all clear with grey then punch holes” as primary wall strategy.
- Snapshot prior best `*_ext.png` to `Temp/plate-baseline/<stem>_ext.png` before overwrite for compare gate.

- [ ] **Step 2: Dry-run micro + studio + small — print clear%/pink counts; open review composites**

Reject stem if review looks softer/more artifacted than baseline.

- [ ] **Step 3: Commit tool changes (not plates yet if still iterating)**

```bash
git add Tools/fix_office_face_plates.py Tools/crop_face_contact_sheet.py
git commit -m "tooling: quality-safe office face rebuild per face-quality spec"
```

---

### Task 3: Rebuild and ship office face plates

**Files:**
- Modify: `Assets/Resources/Art/Dollhouse/{micro,studio,small,mid,professional,team,upper,corner,corporate}_*` PNG+bytes(+meta as needed)

- [ ] **Step 1: Run rebuild for all office stems in the tool’s sheet map**

- [ ] **Step 2: Force reimport** via `unity command run_script --file Temp/RefreshDollhouse.cs --entry RefreshDollhouse.Run` (create helper if missing)

- [ ] **Step 3: Compare gate** — for each stem, side-by-side baseline vs new; reject regressions

- [ ] **Step 4: Commit office plates**

```bash
git add Assets/Resources/Art/Dollhouse/*office* Assets/Resources/Art/Dollhouse/professional_suite* Assets/Resources/Art/Dollhouse/team_bay* Assets/Resources/Art/Dollhouse/corner_suite* Assets/Resources/Art/Dollhouse/corporate*
git commit -m "art: rebuild office face plates with authored interior walls"
```

---

### Task 4: Visual-verify adjacency + solo (offices)

**Files:**
- Create/modify: `Tools/visual-verify/scenarios/office-face-adjacency.json`
- Create/modify: `Tools/visual-verify/scenarios/office-face-solo.json`

- [ ] **Step 1: Solo scenario** — one `office_studio` / `office_micro` on lobby edge; expect sky through back/side glass; no hot magenta

- [ ] **Step 2: Adjacency scenario** — two micros side-by-side + stack; expect shared side = finished wall (not grey glass); party underlay thin

- [ ] **Step 3: Run `.\Tools\visual-verify\run.ps1` for both; Read PNGs; fix plates/runtime if QA fails**

- [ ] **Step 4: Commit scenarios + any harness fixes**

```bash
git add Tools/visual-verify/
git commit -m "test: visual-verify office face solo and adjacency"
```

---

### Task 5: Hotel face plates (same contract)

**Files:**
- Extend tool sheet map for hotel stems (or `fix_hotel_face_plates.py` parallel)
- Modify: hotel dollhouse PNG+bytes
- Scenario: `hotel-face-adjacency.json` (optional if time-boxed — at least one hotel pair)

- [ ] **Step 1: Rebuild hotel faces with mauve plaster preserved (not keyed as magenta)**

- [ ] **Step 2: Reimport + compare gate + visual-verify smoke**

- [ ] **Step 3: Commit**

```bash
git commit -m "art: rebuild hotel face plates with authored interior walls"
```

---

### Task 6: Condo face plates (same contract)

**Files:**
- Condo stems + accents (soft green trim/plants in source art — do not add runtime tint)

- [ ] **Step 1: Rebuild condo faces**

- [ ] **Step 2: Reimport + compare gate + smoke verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "art: rebuild condo face plates with authored interior walls"
```

---

## Done when

1. Neighbor placement uses face leaf; shared side reads as finished wall + decor.  
2. Solo exterior shows sky through glass.  
3. `DollhouseContextArtTests` green with seal-on-fallback-only.  
4. Office (then hotel/condo) plates pass compare gate vs baseline.  
5. Visual-verify magenta gate passes; manual PNG QA matches success criteria in the spec.
