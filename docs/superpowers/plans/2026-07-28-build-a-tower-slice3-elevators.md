# Build-A-Tower Slice #3 Elevators Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Placeable 1-wide normal elevator shafts (start 2 floors, extend ≤30) with one car, queues, and routing so agents use stairs for short hops and elevators for taller trips.

**Architecture:** Elevator shafts are punch-through grid rooms (`isElevatorShaft`) like stairs, with a hard ban on stairs↔shaft overlap. `ElevatorSystem` ticks cars/queues; `TransitRouter` chooses walk / stairs / elevator legs; `AgentSystem` gains WaitingAtElevator + Riding phases.

**Tech Stack:** Unity 6000.4.x, C#, Tilemaps + IMGUI HUD, NUnit EditMode/PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-07-28-build-a-tower-slice3-design.md`

## Global Constraints

- Normal elevators only; width **1**; initial height **2**; extend anytime ≤ **30** floors
- Punch-through lobby/rooms; **stairs and shafts never share cells**
- One car per shaft; capacity **8**; move **1 floor / 2 game minutes**; door dwell **1 game minute**
- Routing: stairs if `|Δfloor| ≤ 3` and stairs path exists; else elevator if one shaft serves both floors; else fail → stress
- No express, sky lobbies, multi-car, or multi-shaft transfers in this slice
- Floor G lobby remains `TowerGrid.LobbyFloor = 0`
- Do not edit the Cursor plan file under `.cursor/plans/`

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Data/RoomTypeSO.cs` | Add `isElevatorShaft` |
| `Assets/ScriptableObjects/Rooms/ElevatorNormal.asset` | Catalog asset (1×2, cost per cell) |
| `Assets/Resources/Rooms/ElevatorNormal.asset` | Resources load for HUD (mirror Stairs) |
| `Assets/Scripts/Core/TowerGrid.cs` | Place / extend / demolish shafts; mutual ban with stairs |
| `Assets/Scripts/Transit/ElevatorCar.cs` | Car state + passengers |
| `Assets/Scripts/Transit/ElevatorShaftRuntime.cs` | Runtime shaft + queues (not the RoomInstance) |
| `Assets/Scripts/Transit/ElevatorSystem.cs` | Sync from grid, tick cars, enqueue/board/alight API |
| `Assets/Scripts/Transit/TransitRouter.cs` | Trip plan: walk / stairs / elevator |
| `Assets/Scripts/Transit/StairsPathfinder.cs` | Unchanged short-hop BFS (still rejects >3) |
| `Assets/Scripts/Agents/AgentEnums.cs` | Add `WaitingAtElevator`, `Riding` |
| `Assets/Scripts/Agents/Agent.cs` | Elevator trip fields |
| `Assets/Scripts/Agents/AgentSystem.cs` | Use TransitRouter; wait/ride handling |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Own ElevatorSystem + TransitRouter |
| `Assets/Scripts/Build/BuildController.cs` | Place/extend elevator UX |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Paint shafts like stairs (on top) |
| `Assets/Scripts/UI/TowerHudController.cs` | Elevator tool button |
| `Assets/Tests/EditMode/ElevatorTests.cs` | Placement, extend, routing, car boarding |
| `Assets/Tests/PlayMode/TowerSandboxBuildSmokeTests.cs` | Tall stack + elevator path |
| `README.md` | Slice #3 play steps |

---

### Task 1: Room type flag + ElevatorNormal assets

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs`
- Create: `Assets/ScriptableObjects/Rooms/ElevatorNormal.asset` (+ `.meta` via Unity or copy Stairs YAML pattern)
- Create: `Assets/Resources/Rooms/ElevatorNormal.asset` (+ `.meta`)
- Test: `Assets/Tests/EditMode/ElevatorTests.cs` (scaffold)

**Interfaces:**
- Produces: `RoomTypeSO.isElevatorShaft`; asset `id = "elevator_normal"`, `size = (1,2)`, `buildCost = 20000` (per-cell; place charges `height * buildCost`), `category = Transit`, `allowAboveGround = true`, `allowBasement = true`, dark steel color

- [ ] **Step 1: Add failing test helper that loads elevator type shape**

Create `Assets/Tests/EditMode/ElevatorTests.cs`:

```csharp
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorTests
    {
        RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator_normal";
            so.displayName = "Elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.buildCost = 20000;
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        [Test]
        public void Elevator_type_flags_shaft()
        {
            var e = Elevator();
            Assert.IsTrue(e.isElevatorShaft);
            Assert.AreEqual(new Vector2Int(1, 2), e.size);
        }
    }
}
```

- [ ] **Step 2: Add `isElevatorShaft` to `RoomTypeSO`**

```csharp
public bool isStairs;
public bool isElevatorShaft;
[Min(0)] public int maxOccupants;
```

- [ ] **Step 3: Create assets**

Copy `Assets/ScriptableObjects/Rooms/Stairs.asset` → `ElevatorNormal.asset` and set:

```yaml
id: elevator_normal
displayName: Elevator
category: 5
size: {x: 1, y: 2}
buildCost: 20000
placeholderColor: {r: 0.2, g: 0.25, b: 0.35, a: 1}
isStairs: 0
isElevatorShaft: 1
allowAboveGround: 1
allowBasement: 1
```

Duplicate under `Assets/Resources/Rooms/ElevatorNormal.asset`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/RoomTypeSO.cs Assets/ScriptableObjects/Rooms/ElevatorNormal.asset* Assets/Resources/Rooms/ElevatorNormal.asset* Assets/Tests/EditMode/ElevatorTests.cs*
git commit -m "feat: add elevator room type and assets"
```

---

### Task 2: TowerGrid place, extend, stairs ban

**Files:**
- Modify: `Assets/Scripts/Core/TowerGrid.cs`
- Modify: `Assets/Tests/EditMode/ElevatorTests.cs`

**Interfaces:**
- Consumes: `RoomTypeSO.isElevatorShaft`, stairs underlay helpers
- Produces:
  - `bool CanPlace(RoomTypeSO type, Vector2Int origin)` — elevators via `CanPlaceElevator`
  - `bool CanExtendElevator(RoomInstance shaft, int newMinY, int newMaxY)`
  - `bool TryExtendElevator(RoomInstance shaft, int newMinY, int newMaxY, out int addedCells)`
  - Constants: `public const int MaxElevatorSpan = 30;`
  - Stairs placement rejects any cell owned by elevator (and vice versa)

- [ ] **Step 1: Write failing placement tests**

```csharp
RoomTypeSO Lobby() { /* same as StairsPathfinderTests */ }
RoomTypeSO Stairs() { /* 2x2 isStairs */ }

[Test]
public void Place_elevator_1x2_and_reject_stairs_overlap()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
    Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var shaft));
    Assert.IsTrue(shaft.Type.isElevatorShaft);
    Assert.IsFalse(grid.CanPlace(Stairs(), new Vector2Int(0, 0)));
}

[Test]
public void Extend_elevator_up_to_30_rejects_31()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
    Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var shaft));
    Assert.IsTrue(grid.CanExtendElevator(shaft, 0, 29));
    Assert.IsTrue(grid.TryExtendElevator(shaft, 0, 29, out var added));
    Assert.AreEqual(28, added); // was 2 floors (0..1), now 0..29 = 30
    Assert.IsFalse(grid.CanExtendElevator(shaft, 0, 30));
}

[Test]
public void Elevator_rejects_stairs_cell()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
    Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
    Assert.IsFalse(grid.CanPlace(Elevator(), new Vector2Int(0, 0)));
}
```

- [ ] **Step 2: Run EditMode tests — expect FAIL** (missing APIs / place path)

In Unity: Window → General → Test Runner → EditMode → `ElevatorTests`.

- [ ] **Step 3: Implement elevator placement in `TowerGrid`**

Mirror stairs punch-through:

```csharp
public const int MaxElevatorSpan = 30;

// In CanPlace:
if (type.isElevatorShaft)
    return CanPlaceElevator(type, origin, footprint);

// CanPlaceElevator: IsFloorAllowed; bounds; reject if occupant is stairs OR existing elevator (unless extending same); allow lobby/rooms/scaffold overlap; empty needs HasSupportForStairs-style support
// Also reject if FindStairsCovering(cell) != null even when _cells owner is room under stairs

// PlaceElevator: bookmark _underStairs-equivalent — reuse _underStairs dict OR rename to _underTransit; simplest: reuse _underStairs for both transit underlays (document in comment) OR add _underElevator. Prefer separate `_underElevator` dictionary parallel to `_underStairs`.

// Mutual ban in CanPlaceStairs: if IsElevator(occupant) || FindElevatorCovering(cell) return false
// Mutual ban in CanPlace for rooms behind stairs: unchanged; rooms may still sit behind elevators like stairs (RegisterBehindElevator or generalize RegisterBehindTransit)
```

Generalize underlay: rooms may build behind elevators the same way as stairs. Extract shared helper or duplicate `RegisterBehindStairs` → `RegisterBehindTransit` checking `IsStairs || IsElevator`.

```csharp
public bool CanExtendElevator(RoomInstance shaft, int newMinY, int newMaxY)
{
    if (!IsElevator(shaft)) return false;
    var span = newMaxY - newMinY + 1;
    if (span < 2 || span > MaxElevatorSpan) return false;
    if (newMinY > shaft.Origin.y || newMaxY < shaft.Origin.y + shaft.Size.y - 1) return false; // must contain old span
    if (newMinY == shaft.Origin.y && newMaxY == shaft.Origin.y + shaft.Size.y - 1) return false;
    // Validate each new cell at (shaft.Origin.x, y) with CanPlaceElevator cell rules
    return true;
}

public bool TryExtendElevator(RoomInstance shaft, int newMinY, int newMaxY, out int addedCells)
{
    // Remove old shaft registration; create new RoomInstance same id/type with origin (x,newMinY) size (1, span); re-bookmark underlays; return added cell count
}
```

Keep `InstanceId` stable if agents reference shaft room — easiest: mutate by RemoveRoom + new RoomInstance (new id) and let ElevatorSystem resync by cell X. Spec allows resync on grid change.

Demolish: if elevator, restore `_underElevator` like stairs path in `TryDemolishAt`.

- [ ] **Step 4: Re-run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/TowerGrid.cs Assets/Tests/EditMode/ElevatorTests.cs
git commit -m "feat: place and extend elevator shafts with stairs ban"
```

---

### Task 3: ElevatorSystem (car + queues)

**Files:**
- Create: `Assets/Scripts/Transit/ElevatorCar.cs`
- Create: `Assets/Scripts/Transit/ElevatorShaftRuntime.cs`
- Create: `Assets/Scripts/Transit/ElevatorSystem.cs`
- Modify: `Assets/Tests/EditMode/ElevatorTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum ElevatorDirection { None, Up, Down }
public enum ElevatorCarState { Idle, Moving, DoorsOpen }

public sealed class ElevatorCar {
  public int Floor;
  public ElevatorDirection Direction;
  public ElevatorCarState State;
  public readonly List<int> PassengerIds = new();
  public const int Capacity = 8;
  public const float MinutesPerFloor = 2f;
  public const float DoorDwellMinutes = 1f;
}

public sealed class ElevatorShaftRuntime {
  public int RoomInstanceId;
  public int X;
  public int MinFloor;
  public int MaxFloor;
  public ElevatorCar Car;
  public Dictionary<int, Queue<int>> UpQueues;   // floor -> agent ids
  public Dictionary<int, Queue<int>> DownQueues;
  public bool Serves(int floor) => floor >= MinFloor && floor <= MaxFloor;
}

public sealed class ElevatorSystem {
  public IReadOnlyList<ElevatorShaftRuntime> Shafts { get; }
  public void SyncFromGrid(TowerGrid grid);
  public void Tick(float deltaGameMinutes);
  public bool TryEnqueue(int agentId, int x, int floor, ElevatorDirection dir);
  public ElevatorShaftRuntime FindServing(int x, int floorA, int floorB); // any shaft whose X matches landing walk target — for MVP find by Serves both floors (any X), caller walks to (shaft.X, floor)
  public ElevatorShaftRuntime FindServing(int floorA, int floorB);
}
```

- [ ] **Step 1: Failing test — car moves toward demand**

```csharp
[Test]
public void Elevator_car_moves_toward_queued_floor()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
    grid.TryPlace(Elevator(), new Vector2Int(0, 0), out _); // floors 0-1
    grid.TryExtendElevator(grid.Rooms.First(r => r.Type.isElevatorShaft), 0, 4, out _);

    var sys = new ElevatorSystem();
    sys.SyncFromGrid(grid);
    var shaft = sys.Shafts[0];
    Assert.AreEqual(0, shaft.Car.Floor); // home G
    Assert.IsTrue(sys.TryEnqueue(99, shaft.X, 4, ElevatorDirection.Down));
    // Tick enough game minutes to reach floor 4: 4 floors * 2 min = 8
    for (var i = 0; i < 20; i++)
        sys.Tick(1f);
    Assert.AreEqual(4, shaft.Car.Floor);
}
```

- [ ] **Step 2: Implement car dispatch (minimal)**

`Tick`: accumulate minutes; when Moving and enough time → Floor += dir; on arrival DoorsOpen dwell then board/alight; Idle seeks highest-priority queue.

Board: dequeue agents at floor matching car direction or destination; Alight: remove passengers whose `Agent` dest floor == current (ElevatorSystem stores `Dictionary<int,int> passengerDestFloor` set at enqueue/board time via `SetPassengerDestination(agentId, floor)`).

Keep agent object updates in AgentSystem — ElevatorSystem only tracks ids + dest floors.

- [ ] **Step 3: Tests PASS; commit**

```bash
git add Assets/Scripts/Transit/Elevator*.cs Assets/Tests/EditMode/ElevatorTests.cs
git commit -m "feat: add elevator car motion and floor queues"
```

---

### Task 4: TransitRouter

**Files:**
- Create: `Assets/Scripts/Transit/TransitRouter.cs`
- Modify: `Assets/Tests/EditMode/ElevatorTests.cs`

**Interfaces:**
```csharp
public enum TransitLegKind { Walk, Stairs, Elevator }

public sealed class TransitLeg {
  public TransitLegKind Kind;
  public List<Vector2Int> Cells; // walk/stairs path cells; for Elevator: [entryLanding, exitLanding]
  public int ElevatorX;
  public int EntryFloor;
  public int ExitFloor;
}

public sealed class TransitRouter {
  public TransitRouter(StairsPathfinder stairs, ElevatorSystem elevators);
  public void Rebuild(TowerGrid grid); // rebuild stairs pf; elevators.SyncFromGrid
  public bool TryPlanTrip(Vector2Int start, Vector2Int goal, out List<TransitLeg> legs);
}
```

Logic (exact):

```csharp
public bool TryPlanTrip(Vector2Int start, Vector2Int goal, out List<TransitLeg> legs)
{
    legs = new List<TransitLeg>();
    if (start == goal) {
        legs.Add(new TransitLeg { Kind = TransitLegKind.Walk, Cells = new List<Vector2Int> { start } });
        return true;
    }

    if (start.y == goal.y) {
        if (!_stairs.TryFindPath(start, goal, out var walk) || walk == null) return false;
        // StairsPathfinder already does horizontal on walkable — OK same floor
        legs.Add(new TransitLeg { Kind = TransitLegKind.Walk, Cells = walk });
        return true;
    }

    var dy = Mathf.Abs(goal.y - start.y);
    if (dy <= StairsPathfinder.MaxStairsFloorSpan &&
        _stairs.TryFindPath(start, goal, out var stairsPath) && stairsPath != null && stairsPath.Count > 0)
    {
        legs.Add(new TransitLeg { Kind = TransitLegKind.Stairs, Cells = stairsPath });
        return true;
    }

    var shaft = _elevators.FindServing(start.y, goal.y);
    if (shaft == null) return false;

    var entry = new Vector2Int(shaft.X, start.y);
    var exit = new Vector2Int(shaft.X, goal.y);
    if (!_stairs.TryFindPath(start, entry, out var toShaft) || toShaft == null) return false;
    if (!_stairs.TryFindPath(exit, goal, out var fromShaft) || fromShaft == null) return false;
    // Note: shaft cells must be walkable — Sync/Rebuild adds elevator cells to walkable via Rooms

    legs.Add(new TransitLeg { Kind = TransitLegKind.Walk, Cells = toShaft });
    legs.Add(new TransitLeg {
        Kind = TransitLegKind.Elevator,
        ElevatorX = shaft.X,
        EntryFloor = start.y,
        ExitFloor = goal.y,
        Cells = new List<Vector2Int> { entry, exit }
    });
    legs.Add(new TransitLeg { Kind = TransitLegKind.Walk, Cells = fromShaft });
    return true;
}
```

**Important:** `StairsPathfinder` currently rejects `|Δfloor| > 3` globally — same-floor and short paths OK. For walk-to-shaft on same floor, Δfloor=0 works. Shaft cells must be in `_walkable` (elevator rooms registered on grid).

- [ ] **Step 1: Tests**

```csharp
[Test]
public void Router_uses_stairs_when_span_le_3()
{
    // lobby, office floor 1, stairs 0..1 — plan (5,0)->(5,1) is Stairs or Walk vertical on stairs
}

[Test]
public void Router_needs_elevator_when_span_gt_3()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
    // pads on floors 1..4 at x=5 for walking? offices full width easier:
    // Place elevator 0..4 at x=0; place pad/office cells so (10,0) and (10,4) walkable
    // Without elevator: TryPlanTrip fails; with elevator: succeeds with Elevator leg
}
```

Ensure walkable floors: place 1×1 pads or offices on each floor at destination X, and ensure path along lobby/shaft.

- [ ] **Step 2: Implement `TransitRouter`; tests PASS; commit**

```bash
git add Assets/Scripts/Transit/TransitRouter.cs Assets/Tests/EditMode/ElevatorTests.cs
git commit -m "feat: add TransitRouter stairs-vs-elevator planning"
```

---

### Task 5: AgentSystem + TowerSimulation wiring

**Files:**
- Modify: `Assets/Scripts/Agents/AgentEnums.cs`
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Modify: `Assets/Scripts/Agents/AgentView.cs` (riding follows car Y)
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`

**Interfaces:**
- Agent fields: `List<TransitLeg> TripLegs`, `int TripLegIndex`, `int ElevatorDestFloor`, `float ElevatorWaitMinutes`
- `AgentSystem` constructor takes `TransitRouter` (or stairs+elevators)
- Phases: `WaitingAtElevator`, `Riding`
- Stress: waiting > **10 game minutes** increases stress; empty path still stresses

- [ ] **Step 1: Extend enums and Agent**

```csharp
public enum AgentPhase {
    Outside, Moving, AtHome, Working, Staying,
    WaitingAtElevator, Riding
}
```

- [ ] **Step 2: Change `BeginTrip` to use router**

```csharp
if (_router.TryPlanTrip(agent.Cell, to, out var legs) && legs.Count > 0)
{
    agent.TripLegs = legs;
    agent.TripLegIndex = 0;
    StartLeg(agent, legs[0]);
}
else { /* stuck moving empty path → stress */ }
```

`StartLeg`: Walk/Stairs → set `Path` from Cells, Phase=Moving. Elevator → Phase=WaitingAtElevator, enqueue with dir toward ExitFloor, set ElevatorDestFloor.

`StepMovement`: on path complete, `AdvanceLeg`. If next is Elevator, wait. When ElevatorSystem doors open and boards agent (poll: car floor==entry && DoorsOpen → set Riding). When Riding && car.Floor==dest && DoorsOpen → alight, next walk leg.

Cleaner API on ElevatorSystem:

```csharp
public bool TryBoard(int agentId, ElevatorShaftRuntime shaft); // if doors open at agent floor
public bool ShouldAlight(int agentId, ElevatorShaftRuntime shaft);
```

AgentSystem each tick: update wait stress; if Waiting and TryBoard → Riding; if Riding and ShouldAlight → start next walk leg.

- [ ] **Step 3: TowerSimulation**

```csharp
ElevatorSystem _elevators;
TransitRouter _router;

_elevators = new ElevatorSystem();
_pathfinder = new StairsPathfinder();
_router = new TransitRouter(_pathfinder, _elevators);
_agents = new AgentSystem(_router);

// Update:
_elevators.Tick(_clock.DeltaGameMinutes); // expose delta minutes from last Tick on GameClock OR compute minutesPerRealSecond * dt
_agents.Tick(...);

// OnGridChanged:
_router.Rebuild(build.Grid);
```

If `GameClock` lacks delta minutes, add `public float LastTickGameMinutes { get; private set; }` set inside `Tick`.

- [ ] **Step 4: EditMode agent trip test optional; commit**

```bash
git add Assets/Scripts/Agents Assets/Scripts/Simulation/TowerSimulation.cs Assets/Scripts/Time/GameClock.cs
git commit -m "feat: agents ride elevators via TransitRouter"
```

---

### Task 6: Build UX, HUD, view, README

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Modify: `Assets/Scripts/Agents/AgentView.cs` (optional car marker MonoBehaviour)
- Modify: `README.md`
- Create: `Assets/Scripts/Transit/ElevatorView.cs` (simple GL/quad or tile for car)

**BuildController:**
- Place elevator like other rooms via `TryPlaceSelected`
- When elevator selected and click existing shaft column: enter extend drag (vertical) → `TryExtendElevator`
- Cost: `size.y * buildCost` on place; `added * buildCost` on extend
- Paint shaft on top like stairs; after place room behind shaft, repaint shaft cells

**HUD:** `EnsureElevatorAndCatalog` like Stairs; Tools button "Elevator"

**ElevatorView:** each frame read `ElevatorSystem.Shafts` and draw car at `(X+0.5f, Floor+0.5f)`

**README:** add steps 11+ for elevators; note max 30 floors, no stairs overlap

- [ ] **Step 1: Implement UX + view**
- [ ] **Step 2: Manual Play Mode smoke**
- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Build Assets/Scripts/UI Assets/Scripts/Rendering Assets/Scripts/Transit/ElevatorView.cs README.md
git commit -m "feat: elevator HUD placement extend and car view"
```

---

### Task 7: PlayMode smoke + slice closeout docs

**Files:**
- Modify: `Assets/Tests/PlayMode/TowerSandboxBuildSmokeTests.cs`
- Modify: `docs/superpowers/specs/2026-07-28-build-a-tower-slice3-design.md` (Status → Done when shipping)
- Modify: `docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md` (check completed MVP items)

- [ ] **Step 1: Extend smoke test**

After lobby + office on floor 1 + stairs: place elevator, extend to floor 4, place office/pad on floor 4, assert `TransitRouter.TryPlanTrip((5,0),(5,4))` or pathfinder/router via sim.

- [ ] **Step 2: Run EditMode `ElevatorTests` + PlayMode smoke**
- [ ] **Step 3: Commit**

```bash
git add Assets/Tests docs README.md
git commit -m "test: elevator routing smoke and slice 3 docs"
```

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| 1×2 place, punch-through | 2 |
| Extend ≤30 | 2 |
| Stairs ↔ elevator ban | 2 |
| 1 car, cap 8, motion/dwell | 3 |
| Queues up/down | 3 |
| Stairs ≤3 else elevator | 4 |
| Waiting/Riding + stress | 5 |
| HUD / ghost / help / car marker | 6 |
| Demolish restores underlay | 2 (demolish path) |
| EditMode + PlayMode tests | 2–4, 7 |
| No express/sky lobby | Global Constraints |

## Plan self-review

- No TBDs; underlay dict named `_underElevator` explicitly.
- `FindServing(floorA,floorB)` matches single-shaft MVP (no multi-transfer).
- StairsPathfinder still used for walkable BFS including elevator cells after Rebuild.
- Type names consistent: `ElevatorShaftRuntime`, `ElevatorSystem`, `TransitRouter`, `TransitLeg`.
