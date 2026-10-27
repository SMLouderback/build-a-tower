# Room Art Face Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove runtime side-window masks and family wall tints; rebalance room catalog sizes; load four authored face plates per mapped room with sky-transparent back windows.

**Architecture:** Phase 0 strips `DollhouseContextArt` mask/accent bake. Phase 1 applies the locked size table to `RoomTypeSO` assets. Phase 2 adds face-suffix leaf resolution (`_ext` / `_L` / `_R` / `_int`) with single-plate fallback. Phase 3 authors all four PNG+bytes plates per mapped type at the new `W×H`. Sky remains behind overlays; back-window alpha is preserved through load/bake.

**Tech Stack:** Unity 6, IMGUI/HUD unchanged, `RoomDollhouseArt` / `DollhouseContextArt` / `RoomExteriorFaces`, Resources `Art/Dollhouse`, NUnit EditMode.

## Global Constraints

- Magenta `#FF00FF` is key plate only — never scene color.
- No runtime `FillInteriorSideBand` or `ApplyFamilyAccents` after Task 1.
- Four face variants per mapped dollhouse room; missing leaf → current single plate, still no mask/tint.
- Catalog sizes follow `docs/superpowers/specs/2026-10-14-room-art-face-variants-design.md` §4.
- Back-wall window panes must keep alpha so sky/clouds show through.
- Lobby / stairs / elevators / parking_ramp / legacy premiums stay outside the four-variant set.
- Warm industrial dollhouse look; Build-A-Tower menu aesthetic.

---

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Rendering/DollhouseContextArt.cs` | Bake without mask/accent; face-aware source resolve |
| `Assets/Scripts/Rendering/RoomDollhouseArt.cs` | Leaf stems + face suffix helper + path API |
| `Assets/Scripts/Rendering/RoomExteriorFaces.cs` | Unchanged L/R truth |
| `Assets/Resources/Rooms/*.asset` | Size (+ maxOccupants) updates from §4 |
| `Assets/Resources/Art/Dollhouse/*` | Four plates per mapped room (+ atrium mid set) |
| `Assets/Tests/EditMode/DollhouseContextArtTests.cs` | No mask/tint; alpha preserved |
| `Assets/Tests/EditMode/RoomDollhouseArtTests.cs` | Face leaf paths + fallback |
| `Assets/Tests/EditMode/*Catalog* / size tests` | Assert new sizes (add focused test file if needed) |
| Spec / this plan | Source of truth |

---

### Task 1: Strip mask + family accent bake

**Files:**
- Modify: `Assets/Scripts/Rendering/DollhouseContextArt.cs`
- Modify: `Assets/Tests/EditMode/DollhouseContextArtTests.cs`
- Modify: cache key (drop `family` if unused)

- [ ] **Step 1: Write failing tests**

Assert bake/context path does **not** rewrite interior side bands to beige and does **not** lerp family colors onto accentable pixels. Prefer behavior tests: with a fixture sprite that has side “glass” + saturated wall, `GetSprite(..., leftExterior:false, ...)` leaves glass pixels unchanged (or within epsilon of source); family param ignored / removed.

- [ ] **Step 2: Run tests — expect fail** (mask/accent still present)

- [ ] **Step 3: Implement** — remove `FillInteriorSideBand` / `ApplyFamilyAccents` calls from `Bake`; delete or dead-strip helpers if unused; simplify `CacheKey`.

- [ ] **Step 4: Run EditMode tests for `DollhouseContextArt*` — green**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Rendering/DollhouseContextArt.cs Assets/Tests/EditMode/DollhouseContextArtTests.cs
git commit -m "$(cat <<'EOF'
fix(art): remove dollhouse side-window mask and family tint bake

EOF
)"
```

---

### Task 2: Apply catalog size table

**Files:**
- Modify: `Assets/Resources/Rooms/*.asset` per spec §4
- Create or modify: `Assets/Tests/EditMode/RoomSizeRebalanceTests.cs` (or extend an existing catalog test)

- [ ] **Step 1: Failing size assertions** for every typeId in §4 (Office through parking/valet; leisure keep sizes still asserted).

- [ ] **Step 2: Run — fail on old sizes**

- [ ] **Step 3: Patch YAML/assets** (`size.x` / `size.y`, `maxOccupants` where listed)

- [ ] **Step 4: EditMode green**

- [ ] **Step 5: Commit**

```bash
git commit -m "$(cat <<'EOF'
balance: rebalance room footprints for cutaway aspect

EOF
)"
```

---

### Task 3: Four-leaf face resolution

**Files:**
- Modify: `Assets/Scripts/Rendering/RoomDollhouseArt.cs`
- Modify: `Assets/Scripts/Rendering/DollhouseContextArt.cs` (resolve face leaf before bake)
- Modify: `Assets/Tests/EditMode/RoomDollhouseArtTests.cs`

- [ ] **Step 1: Failing tests** for suffix selection:

| leftExt | rightExt | suffix |
|---------|----------|--------|
| T | T | `_ext` |
| T | F | `_L` |
| F | T | `_R` |
| F | F | `_int` |

Path form: `Art/Dollhouse/{stem}{suffix}`. Fallback: if face leaf missing, load legacy single stem (current `LeafByTypeId` value) without mask.

- [ ] **Step 2: Run — fail**

- [ ] **Step 3: Implement** `FaceSuffix(left,right)`, `ResourcePathForFaces(typeId, left, right)`, wire `DollhouseContextArt.ResolveSourceSprite`.

- [ ] **Step 4: EditMode green**

- [ ] **Step 5: Commit**

```bash
git commit -m "$(cat <<'EOF'
feat(art): resolve dollhouse plates by exterior face variant

EOF
)"
```

---

### Task 4: Art authoring — Office family

**Files:**
- Create: `Assets/Resources/Art/Dollhouse/{stem}_{ext|L|R|int}.png` + `.bytes` + `.meta` for each office mapped type at **new** WxH
- Modify: `RoomDollhouseArt.LeafByTypeId` stems to new names
- Update tests that assert leaf names

- [ ] **Step 1: For each office typeId**, author four magenta-keyed plates (depth OK; back glass alpha; side windows only on exterior edges of that variant)

- [ ] **Step 2: Map stems; EditMode Resources.Load for all four leaves**

- [ ] **Step 3: Play Mode smoke — office mid-tower vs edge**

- [ ] **Step 4: Commit** `art: office face variants at rebalanced sizes`

---

### Task 5: Art authoring — Hotel family

Same pattern as Task 4 for all mapped hotel types (skip `hotel_premium`).

- [ ] Steps: author → map → tests → Play smoke → commit `art: hotel face variants`

---

### Task 6: Art authoring — Condo family

Same pattern (skip `condo_premium`).

- [ ] Steps: author → map → tests → Play smoke → commit `art: condo face variants`

---

### Task 7: Art authoring — Shops

All `shop_*` mapped types at new sizes (dept store `12×2`).

- [ ] Steps: author → map → tests → Play smoke → commit `art: shop face variants`

---

### Task 8: Art authoring — Service + parking dollhouse

Mapped service + `parking_underground` + valet if mapped.

- [ ] Steps: author → map → tests → Play smoke → commit `art: service face variants`

---

### Task 9: Art authoring — Leisure + metro + atrium mid

Keep sizes; four face variants each; atrium mid stack keeps open-well plate **and** face suffixes as required by resolver (document chosen naming: e.g. `atrium_8x3_mid_int` etc.).

- [ ] Steps: author → map → tests → Play smoke → commit `art: leisure and metro face variants`

---

### Task 10: Retire obsolete plates + final verification

- [ ] **Step 1:** Delete unused old stems (stretched leaf names) once unreferenced
- [ ] **Step 2:** Full EditMode dollhouse / catalog / context suite green
- [ ] **Step 3:** Play Mode checklist from spec §7
- [ ] **Step 4:** Mark spec status Implemented; commit `docs: room art face variants complete`

---

## Play Mode checklist (Task 10)

- [ ] No tan edge blocks mid-tower  
- [ ] True exterior ends show side windows  
- [ ] No green/purple/blue family wall wash  
- [ ] No stretched furniture  
- [ ] Sky/clouds visible through back windows as time-of-day changes  
- [ ] Atrium stack mid vs roof still correct  

---

## Notes for agents

- Prefer TDD on Tasks 1–3; Tasks 4–9 are art-heavy — keep mapping/tests honest.  
- Do not reintroduce runtime masks “temporarily.”  
- Unity batchmode may fail if the Editor has the project open — run EditMode in-Editor if needed.  
- After this plan is approved for execution, use **subagent-driven-development**.
