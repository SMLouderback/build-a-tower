# Elevator Shaft Floor Labels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show world-space floor labels on every elevator shaft cell (`L` / `Bn` / `n` / `SLn`), muting floors the shaft does not `Serve` (Express non-stops).

**Architecture:** Pure `FloorLabels` formatter + pooled `TextMesh` overlays synced from `ElevatorSystem.Shafts` after `SyncFromGrid` / transit rebuild. No routing changes, no AI art.

**Tech Stack:** Unity 6000.4.7f1, C#, UnityEngine.TextMesh (built-in; no TMP package), NUnit EditMode

## Global Constraints

- Naming: `L` at y=0; `B{n}` for y&lt;0; `SL{n}` for sky lobby floors; else decimal
- Mute when `!ElevatorShaftRuntime.Serves(floor)` (~35–45% opacity)
- One label per floor, centered on shaft width (Express 2-wide = one label)
- Sorting above transit occlusion (22), below elevator cars (30) → use **24**
- No Parking Ramp / Scaffolding art in this plan
- Spec: `docs/superpowers/specs/2026-09-05-elevator-shaft-floor-labels-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Rendering/FloorLabels.cs` | Pure formatter |
| `Assets/Scripts/Rendering/ElevatorShaftFloorLabels.cs` | Pooled TextMesh sync |
| `Assets/Scripts/Transit/TransitRouter.cs` or `TowerSimulation` / `TilemapTowerView` | Call Sync after shaft rebuild |
| `Assets/Tests/EditMode/FloorLabelsTests.cs` | Formatter + mute helpers |
| `Assets/Tests/EditMode/ElevatorShaftFloorLabelsTests.cs` | Sync count / Serves coloring (EditMode-safe) |

---

### Task 1: FloorLabels formatter

**Files:**
- Create: `Assets/Scripts/Rendering/FloorLabels.cs`
- Create: `Assets/Tests/EditMode/FloorLabelsTests.cs`

**Interfaces:**
- Produces: `FloorLabels.Format(int floor, TowerGrid grid)` → `string`
- Produces: `FloorLabels.IsActive(ElevatorShaftRuntime shaft, int floor)` → `shaft != null && shaft.Serves(floor)`

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Format_lobby_basement_and_above()
{
    var grid = new TowerGrid();
    // ensure lobby exists if required by grid ctor used in other tests — follow TowerGridTests setup
    Assert.AreEqual("L", FloorLabels.Format(0, grid));
    Assert.AreEqual("B1", FloorLabels.Format(-1, grid));
    Assert.AreEqual("B2", FloorLabels.Format(-2, grid));
    Assert.AreEqual("3", FloorLabels.Format(3, grid));
}

[Test]
public void Format_sky_lobby_uses_SL_prefix()
{
    // Place or stub sky lobby at y=15 via same helpers as sky-lobby tests
    Assert.AreEqual("SL15", FloorLabels.Format(15, gridWithSkyLobbyAt15));
}

[Test]
public void IsActive_follows_Serves()
{
    var shaft = new ElevatorShaftRuntime { Kind = ElevatorShaftKind.Express, MinFloor = 0, MaxFloor = 10 };
    shaft.StopFloors = new HashSet<int> { 0, 15 }; // only 0 in span
    Assert.IsTrue(FloorLabels.IsActive(shaft, 0));
    Assert.IsFalse(FloorLabels.IsActive(shaft, 5));
}
```

- [ ] **Step 2: Run EditMode filter `FloorLabelsTests` — expect FAIL**

- [ ] **Step 3: Implement**

```csharp
public static class FloorLabels
{
    public static string Format(int floor, TowerGrid grid)
    {
        if (grid != null && grid.TryGetSkyLobbyOnFloor(floor, out _))
            return "SL" + floor;
        if (floor == TowerGrid.LobbyFloor) return "L";
        if (floor < 0) return "B" + (-floor);
        return floor.ToString();
    }

    public static bool IsActive(ElevatorShaftRuntime shaft, int floor) =>
        shaft != null && shaft.Serves(floor);
}
```

- [ ] **Step 4: Tests PASS — Commit** `feat: add FloorLabels formatter for elevator shafts`

---

### Task 2: ElevatorShaftFloorLabels pool + Sync

**Files:**
- Create: `Assets/Scripts/Rendering/ElevatorShaftFloorLabels.cs`
- Create: `Assets/Tests/EditMode/ElevatorShaftFloorLabelsTests.cs`

**Interfaces:**
- Consumes: `FloorLabels`, `ElevatorSystem.Shafts`, `TowerGrid`
- Produces: `ElevatorShaftFloorLabels.Sync(TowerGrid grid, ElevatorSystem elevators, Transform parent)`
- Produces: `ActiveColor`, `MutedColor`, `SortingOrder = 24`

- [ ] **Step 1: Failing Sync test** (create under a disposable root; assert label count = sum of floors in shafts)

```csharp
[Test]
public void Sync_creates_one_label_per_floor_centered()
{
    // Build grid + SyncFromGrid with one Normal shaft Origin (0,0) size (1,5)
    // Sync labels; assert 5 enabled TextMeshes; text on y=0 is "L"
}
```

- [ ] **Step 2: Implement pool**

```csharp
public sealed class ElevatorShaftFloorLabels
{
    public const int SortingOrder = 24;
    public static readonly Color ActiveColor = new(0.95f, 0.95f, 0.98f, 1f);
    public static readonly Color MutedColor = new(0.7f, 0.72f, 0.78f, 0.4f);

    readonly List<TextMesh> _pool = new();

    public void Sync(TowerGrid grid, ElevatorSystem elevators, Transform parent)
    {
        // For each shaft in elevators.Shafts:
        //   for y = MinFloor..MaxFloor:
        //     ensure TextMesh; set text FloorLabels.Format(y, grid);
        //     position (shaft.X + Width*0.5f, y + 0.5f, 0);
        //     color Active/Muted via FloorLabels.IsActive
        //     characterSize ~0.08–0.12, anchor middle-center, font Style.Bold
        // Disable unused pool entries
    }

    public void Clear() { /* disable/destroy pool */ }
}
```

Use `TextMesh` + `MeshRenderer.sortingOrder = SortingOrder`. Parent under a dedicated `"ElevatorFloorLabels"` child of the view root.

- [ ] **Step 3: Tests PASS — Commit** `feat: add pooled elevator shaft floor label overlays`

---

### Task 3: Wire Sync into transit rebuild

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs` and/or `Assets/Scripts/Rendering/TilemapTowerView.cs` and/or `Assets/Scripts/Transit/ElevatorView.cs`
- Prefer: hold `ElevatorShaftFloorLabels` on `ElevatorView` or `TilemapTowerView`; call `Sync` whenever `TransitRouter.Rebuild` / elevator paint runs

**Interfaces:**
- Consumes: Task 2 Sync
- After `elevators.SyncFromGrid(grid)`, call label Sync with same grid

- [ ] **Step 1: Find call sites** — `TransitRouter.Rebuild`, `BuildController.RepaintTransitOnTop`, simulation init

- [ ] **Step 2: Wire** so place/remove/resize elevator and sky lobby updates labels (sky lobby changes Express `StopFloors` on next SyncFromGrid)

- [ ] **Step 3: Play Mode smoke**
  1. Place Normal shaft across lobby + floors — see `L`, `1`, `2`…
  2. Place Express spanning same — non-lobby floors muted
  3. Add sky lobby on Express span — that floor becomes active `SLn`

- [ ] **Step 4: Commit** `feat: sync elevator floor labels after shaft rebuild`

---

### Task 4: Spec status

**Files:**
- Modify: `docs/superpowers/specs/2026-09-05-elevator-shaft-floor-labels-design.md` → `Implemented`

- [ ] **Step 1: Mark Implemented — Commit** `docs: mark elevator shaft floor labels spec implemented`

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Formatter L/Bn/n/SLn | 1 |
| Mute via Serves | 1–2 |
| World TextMesh pool, centered | 2 |
| Refresh on shaft/lobby changes | 3 |
| EditMode tests | 1–2 |
| No art / no routing change | all |
