# Visual Polish (Cutaway Truth) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mid-tower rooms show solid interior side walls; atriums use skylight only on stack tops; family material accents match build-menu colors; land sky/weather/metro cohesion WIP.

**Architecture:** Pure face/stack helpers feed a cached `DollhouseContextArt` bake (window mask + accent remap + atrium leaf). `TilemapTowerView` requests context sprites on paint and rebuilds edge neighbors. Shared `BuildFamilyColors` aligns HUD + accents. Cohesion tasks wire existing metro/sky/weather scripts.

**Tech Stack:** Unity 6000.x, SpriteRenderer dollhouse overlays, NUnit EditMode / net8 hosts.

**Spec:** `docs/superpowers/specs/2026-10-12-visual-polish-cutaway-design.md`

## Global Constraints

- Do not commit unless the user asks (SDD progress docs may be local-only)
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Prefer SDD; no parallel-cli
- No full-room family tint via `SpriteRenderer.color`
- Keep atrium fatigue-free climb; no mid-atrium upkeep change this slice
- Magenta `#FF00FF` remains key plate only
- Warm industrial dollhouse look; Build-A-Tower menu icon rules unchanged

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Rendering/RoomExteriorFaces.cs` | L/R exterior booleans |
| `Assets/Scripts/Rendering/AtriumStackRoles.cs` | Roof / Mid / Solo |
| `Assets/Scripts/Rendering/BuildFamilyColors.cs` | Shared family palette |
| `Assets/Scripts/Rendering/DollhouseContextArt.cs` | Context sprite cache + bake |
| `Assets/Scripts/Rendering/RoomDollhouseArt.cs` | Mid atrium leaf; hook for context |
| `Assets/Scripts/UI/TowerHudController.cs` | Use `BuildFamilyColors` |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Paint context sprite; neighbor rebuild |
| `Assets/Resources/Art/Dollhouse/atrium_8x3_mid.*` | Open-well atrium art |
| `Assets/Scripts/Rendering/SkyCloudLayer.cs` (+ style) | Wire if not already |
| `Assets/Scripts/Rendering/MetroTrainMotion.cs` | Wire if not already |
| `Assets/Scripts/Rendering/WeatherFx.cs` | Ground clip cohesion |
| `Assets/Scripts/Rendering/InteriorLighting.cs` | Mid-tower sky blend |
| `Assets/Scripts/Rendering/AgentView.cs` (or equivalent) | Contrast/silhouette |
| Tests under `Assets/Tests/EditMode/` | Face, stack, accent, context |

---

### Task 1: RoomExteriorFaces + tests

**Files:**
- Create: `Assets/Scripts/Rendering/RoomExteriorFaces.cs`
- Create: `Assets/Tests/EditMode/RoomExteriorFacesTests.cs`

**Interfaces:**
- Produces: `RoomExteriorFaces.Evaluate(TowerGrid grid, RoomInstance room) -> (bool leftExterior, bool rightExterior)`
- Consumes: `TowerGrid.TryGetRoomAt`, room occupied cells / origin+size

- [ ] **Step 1: Failing tests** — isolated room both exterior; neighbor on left clears leftExterior; stairs beside counts as occupied
- [ ] **Step 2: Implement** `RoomExteriorFaces`
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 2: AtriumStackRoles + tests

**Files:**
- Create: `Assets/Scripts/Rendering/AtriumStackRoles.cs`
- Create: `Assets/Tests/EditMode/AtriumStackRolesTests.cs`

**Interfaces:**
- Produces: `enum AtriumStackRole { Solo, Roof, Mid }` and `AtriumStackRoles.RoleFor(TowerGrid, RoomInstance)` / `RolesByInstanceId(TowerGrid)`
- Stack rule: same origin.x + size.x, contiguous Y via atrium rooms only

- [ ] **Step 1: Failing tests** — solo=Solo; two stacked → lower Mid, upper Roof; gap → two Solos or separate stacks
- [ ] **Step 2: Implement**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 3: BuildFamilyColors shared palette

**Files:**
- Create: `Assets/Scripts/Rendering/BuildFamilyColors.cs` (or `Assets/Scripts/UI/` if preferred — prefer Rendering/Core shared)
- Modify: `TowerHudController.FamilyColor` → delegate to shared helper
- Test: `BuildFamilyColorsTests.cs` — Office/Hotel/Condo match prior RGB

**Interfaces:**
- Produces: `BuildFamilyColors.For(BuildFamily) -> Color`

- [ ] **Step 1: Failing test** for known Hotel purple / Office blue / Condo green values
- [ ] **Step 2: Extract + wire HUD**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 4: DollhouseContextArt bake (windows + accents)

**Files:**
- Create: `Assets/Scripts/Rendering/DollhouseContextArt.cs`
- Create: `Assets/Tests/EditMode/DollhouseContextArtTests.cs`
- Modify: `RoomDollhouseArt.cs` as needed for readable base textures in tests

**Interfaces:**
- Produces: `DollhouseContextArt.GetSprite(RoomInstance, leftExt, rightExt, AtriumStackRole, BuildFamily) -> Sprite`
- Cache keyed by `(typeId, left, right, role, family)`
- Window mask: interior side strip → sample wall fill
- Accent: blend matching pixels toward `BuildFamilyColors.For(family)` at ~0.3
- `ResetForTests()` clears cache

- [ ] **Step 1: Failing tests** — interior left changes pixels vs base; accent changes carpet-like hue band without full average shift to family color; cache returns same instance
- [ ] **Step 2: Implement bake**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 5: Atrium mid art + leaf resolution

**Files:**
- Create: `Assets/Resources/Art/Dollhouse/atrium_8x3_mid.png` (+ `.bytes` if pipeline requires) + metas
- Modify: `RoomDollhouseArt` leaf map / `ResourceLeaf` for role
- Test: leaf path for Mid ≠ roof

- [ ] **Step 1: Failing test** — Mid role resolves `atrium_8x3_mid`
- [ ] **Step 2: Add art asset (open well, no skylight) + mapping
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 6: Wire TilemapTowerView context paint + neighbor rebuild

**Files:**
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Test: integration-style EditMode if feasible (paint path with fake sprites via `LoadSpriteForTests`)

**Interfaces:**
- `TryPaintDollhouseArt` uses exterior faces + atrium role + family → `DollhouseContextArt.GetSprite`
- After place/remove, repaint rooms that shared an edge with the changed footprint
- Keep dirty/broken `DollhouseTint` behavior

- [ ] **Step 1: Failing test** or focused host test for neighbor rebuild invocation
- [ ] **Step 2: Wire paint + rebuild**
- [ ] **Step 3: PASS**
- [ ] **Step 4: Commit** only if asked

---

### Task 7: Cohesion — sky, weather, metro, lighting, agents

**Files:**
- Wire/finish: `SkyCloudLayer`, `SkyCloudStyle`, `MetroTrainMotion`, `WeatherFx`, `InteriorLighting`, agent view contrast
- Tests: existing `SkyCloudStyleTests`, `WeatherFxPrecipTests`, `MetroTrainMotionTests` green; add mid-tower lighting test if new API

**Notes:**
- Prefer landing already-untracked WIP rather than rewrite
- InteriorLighting: reduce sky blend for non-exterior / mid floors if API allows without breaking basements

- [ ] **Step 1: Inventory WIP compile + existing tests**
- [ ] **Step 2: Wire into scene/simulation as needed**
- [ ] **Step 3: PASS cohesion tests**
- [ ] **Step 4: Commit** only if asked

---

### Task 8: Spec status + smoke checklist

**Files:**
- Modify: spec status → `Implemented` when Play acceptance done
- Optional: short note in README Maps/visual section if one exists

- [ ] **Step 1: Play checklist from spec §8**
- [ ] **Step 2: Mark spec Implemented**
- [ ] **Step 3: Commit** only if asked

---

## Execution

Use **Subagent-Driven Development**. One task per subagent; verify tests before next task. Do not batch unrelated polish into Task 4.
