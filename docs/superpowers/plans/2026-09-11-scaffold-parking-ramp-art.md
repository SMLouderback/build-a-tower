# Scaffolding & 3×1 Parking Ramp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert parking ramps to SimTower-style 3×1 stack-to-lobby pieces with matching art, and give scaffolding 1×1 wood stud structure art.

**Architecture:** Update `ParkingRamp` size and `CanPlaceParkingRamp` to lobby-attach (B1) or exact-X ramp above. Load 3×1 ramp and 1×1 scaffold sprites via `StructureCutawayArt`. Paint ramps as overlays and scaffolds as structure sprites in `TilemapTowerView`. Update parking accessibility and help text.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode

## Global Constraints

- Ramp size **3×1** (replace 3×2).
- Place only if `y == -1` (B1 / lobby attach) **or** exact same `Origin.x` + width-3 ramp exists on `y + 1`.
- No floors above lobby; no soft X overlap.
- Art: Structure `parking_ramp_3x1`, `scaffolding_stud`; do not wire Dollhouse `parking_ramp_3x2`.
- Missing art → procedural fallback.
- Spec: `docs/superpowers/specs/2026-09-11-scaffold-parking-ramp-art-design.md`

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Resources/Rooms/ParkingRamp.asset` (+ ScriptableObjects copy if present) | size 3×1 |
| `Assets/Resources/Art/Structure/parking_ramp_3x1.png` (+ meta) | SimTower-style ramp art |
| `Assets/Resources/Art/Structure/scaffolding_stud.png` (+ meta) | Wood stud tile |
| `Assets/Scripts/Core/TowerGrid.cs` | Placement rules |
| `Assets/Scripts/Economy/ParkingStalls.cs` | 3×1 chain accessibility |
| `Assets/Scripts/Rendering/StructureCutawayArt.cs` | Loaders |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Paint paths |
| `Assets/Scripts/Build/BuildController.cs` | Help text |
| Tests | Placement, accessibility, art |

---

### Task 1: 3×1 size + placement rules (TDD)

**Files:**
- Modify: `Assets/Resources/Rooms/ParkingRamp.asset` (and `Assets/ScriptableObjects/Rooms/ParkingRamp.asset` if it exists)
- Modify: `Assets/Scripts/Core/TowerGrid.cs`
- Modify/Create: `Assets/Tests/EditMode/ParkingRampPlacementTests.cs` (or extend existing parking tests)

**Interfaces:**
- Produces `CanPlaceParkingRamp` lobby-attach / exact-stack semantics
- Ramp `type.size == (3,1)`

- [ ] **Step 1: Write failing placement tests**

```csharp
[Test]
public void B1_ramp_places_as_lobby_attach()
{
    // lobby + support; TryPlace parking ramp 3×1 at y=-1 succeeds
}

[Test]
public void B2_ramp_requires_exact_X_ramp_on_B1()
{
    // B2 alone fails; after B1 same Origin.x, B2 succeeds
    // B2 with Origin.x shifted by 1 fails
}

[Test]
public void Ramp_size_is_3x1()
{
    Assert.AreEqual(new Vector2Int(3, 1), LoadParkingRampType().size);
}
```

Use existing lobby/scaffold helpers from other parking/stairs tests.

- [ ] **Step 2: Run red** — Unity EditMode filter for these tests; expect fail on size and/or B2 rules.

- [ ] **Step 3: Implement**

- Set asset `size: {x: 3, y: 1}`.
- Rewrite `CanPlaceParkingRamp` validation after cell checks:

```csharp
bool LobbyAttach(Vector2Int origin) => origin.y == -1;

bool ExactRampAbove(Vector2Int origin, int width)
{
    var aboveY = origin.y + 1;
    foreach (var room in _rooms)
    {
        if (!IsParkingRamp(room) || room.IsBroken) continue;
        if (room.Origin.y != aboveY) continue;
        if (room.Origin.x == origin.x && room.Size.x == width) return true;
    }
    return false;
}

// require LobbyAttach(origin) || ExactRampAbove(origin, type.size.x)
```

Keep elevator bans, floor limits, scaffolding clear, underlay bookmarking.

- [ ] **Step 4: Green + commit**

```bash
git commit -m "feat: make parking ramps 3x1 stack-to-lobby"
```

---

### Task 2: Accessibility + help text

**Files:**
- Modify: `Assets/Scripts/Economy/ParkingStalls.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Modify: parking accessibility tests

- [ ] **Step 1: Failing test** — B2 lot reachable only with B1+B2 exact-X ramp stack; broken if B1 missing.

- [ ] **Step 2: Update `ExpandRampFloors` / reachability** to treat each 3×1 ramp floor in an exact-X column as part of the chain to B1/lobby (inspect current flood and adjust for height-1 flights).

- [ ] **Step 3: Help text**

```text
Parking Ramp (3×1): place on B1 (connects to Lobby) or exactly under another ramp with the same X span to continue the shaft downward.
```

- [ ] **Step 4: Green + commit**

```bash
git commit -m "feat: update parking access and help for 3x1 ramps"
```

---

### Task 3: Art assets

**Files:**
- Create: `Assets/Resources/Art/Structure/parking_ramp_3x1.png` (+ meta)
- Create: `Assets/Resources/Art/Structure/scaffolding_stud.png` (+ meta)

- [ ] **Step 1:** Generate SimTower-style **3×1** ramp (curve + landing/pillar, two-lane feel, hazard stripes). Prefer user-approved candidate; regenerate until approved.

- [ ] **Step 2:** Generate **1×1** wood stud scaffold tile (magenta key).

- [ ] **Step 3:** Install under Structure with Unity-compatible meta (follow stairs/structure import conventions).

- [ ] **Step 4: Commit**

```bash
git commit -m "art: add 3x1 parking ramp and scaffolding stud"
```

---

### Task 4: Loaders + paint

**Files:**
- Modify: `Assets/Scripts/Rendering/StructureCutawayArt.cs`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Modify: `Assets/Tests/EditMode/StructureCutawayArtTests.cs`

- [ ] **Step 1: Failing loader tests** for `TryRampSprite` / `TryScaffoldSprite`.

- [ ] **Step 2: Implement loaders** for `Art/Structure/parking_ramp_3x1` and `scaffolding_stud` (crop/key, BL pivot, PPU for 3×1 / 1×1).

- [ ] **Step 3: Paint**

- Ramp branch in `PaintRoom`/`PaintCell`: overlay like stairs when sprite exists; else procedural.
- Scaffold: structure-layer sprite tile when present.
- Clear ramp overlays on demolish; refresh with structure refresh hooks.

- [ ] **Step 4: Green + commit**

```bash
git commit -m "feat: paint 3x1 ramps and scaffolding with structure art"
```

---

### Task 5: Verification + spec Implemented

- [ ] Focused EditMode: placement + parking access + StructureCutawayArtTests.
- [ ] Play smoke: B1+B2 stack looks continuous; orphan B2 blocked; scaffold wood bands; lots still need ramp chain.
- [ ] Mark spec **Implemented** and commit.

```bash
git commit -m "docs: mark scaffold and 3x1 ramp art implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Size 3×1 | 1 |
| Lobby attach / exact-X stack | 1 |
| Accessibility chain | 2 |
| Help text | 2 |
| Art assets | 3 |
| Loaders + paint | 4 |
| Tests + Implemented | 1–5 |

Supersedes the prior 3×2 multi-floor cutaway plan in the same filenames.
