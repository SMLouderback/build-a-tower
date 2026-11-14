# Sky Lobby & Multi-Shaft Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add player-placed sky lobbies (≥15 floors apart) and multi-shaft elevator transfer routing so towers can grow beyond 30 floors per shaft segment.

**Architecture:** Extend `RoomTypeSO` + `TowerGrid` with sky lobby placement/spacing; add `TransferFloorProvider` for lobby floor index; extend `TransitRouter.TryPlanTrip` with one-transfer itineraries; wire build HUD + rendering like ground lobby.

**Tech Stack:** Unity 2D, C# EditMode tests (NUnit), existing `BuildController` / `TilemapTowerView` patterns.

**Spec:** `docs/superpowers/specs/2026-08-28-sky-lobby-vertical-transit-design.md`

## Global Constraints

- `MinSkyLobbySpacing = 15` floors between any two lobbies (ground floor 0 counts).
- `MinSkyLobbyHeight = 15` — first sky lobby must be at floor ≥15.
- Normal elevator span remains `TowerGrid.MaxElevatorSpan = 30`.
- Sky lobby X span must stay within ground lobby horizontal bounds (same as elevator placement).
- Stairs ↔ elevator overlap ban unchanged.
- Express and service elevator types are **not** in this plan.

---

### Task 1: Room type + grid spacing API

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs`
- Modify: `Assets/Scripts/Core/TowerGrid.cs`
- Create: `Assets/Resources/Rooms/SkyLobby.asset`
- Create: `Assets/Tests/EditMode/SkyLobbyGridTests.cs`

**Interfaces:**
- Produces: `RoomTypeSO.isSkyLobby`, `TowerGrid.MinSkyLobbySpacing`, `TowerGrid.GetLobbyFloors()`, `TowerGrid.CanPlaceSkyLobby(minX,maxX,floor)`, `TowerGrid.TryPlaceSkyLobby(...)`, `TowerGrid.CanExtendSkyLobby(...)`, `TowerGrid.TryExtendSkyLobby(...)`

- [ ] **Step 1: Write failing tests**

```csharp
// SkyLobbyGridTests.cs — key cases
[Test] public void CanPlaceSkyLobby_rejects_floor_14() =>
    Assert.IsFalse(grid.CanPlaceSkyLobby(0, 5, 14));

[Test] public void CanPlaceSkyLobby_accepts_floor_15_when_spaced() =>
    Assert.IsTrue(grid.CanPlaceSkyLobby(0, 5, 15));

[Test] public void CanPlaceSkyLobby_rejects_second_within_14_floors() {
    grid.TryPlaceSkyLobby(skyType, 0, 5, 15, out _);
    Assert.IsFalse(grid.CanPlaceSkyLobby(0, 5, 29));
    Assert.IsTrue(grid.CanPlaceSkyLobby(0, 5, 30));
}

[Test] public void GetLobbyFloors_includes_ground_and_sky() {
    PlaceGroundLobby();
    grid.TryPlaceSkyLobby(skyType, 0, 5, 30, out _);
    CollectionAssert.AreEquivalent(new[] { 0, 30 }, grid.GetLobbyFloors());
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run Unity EditMode tests or `dotnet test` for `SkyLobbyGridTests`.

- [ ] **Step 3: Implement**

Add to `RoomTypeSO.cs`:
```csharp
public bool isSkyLobby;
```

Add to `TowerGrid.cs` (mirror lobby extend patterns):
```csharp
public const int MinSkyLobbySpacing = 15;
public const int MinSkyLobbyHeight = 15;
readonly List<int> _skyLobbyFloors = new();

public IReadOnlyList<int> GetLobbyFloors() { /* 0 if HasLobby + sorted sky */ }
public bool IsLobbyFloor(int floor) { /* 0 or in _skyLobbyFloors */ }

public bool CanPlaceSkyLobby(int minX, int maxX, int floor) {
    if (!HasLobby || floor < MinSkyLobbyHeight) return false;
    if (minX < LobbyMinX || maxX > LobbyMaxX) return false; // track bounds from ground lobby
    foreach (var l in GetLobbyFloors())
        if (Mathf.Abs(floor - l) < MinSkyLobbySpacing) return false;
    // + structural / occupancy checks like CanPlaceRoom above ground
    return true;
}
```

Create `SkyLobby.asset` duplicating lobby cost/size pattern with `isSkyLobby: 1`, `id: sky_lobby`.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit** `feat: sky lobby grid placement and spacing rules`

---

### Task 2: Build UX + rendering

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Modify: `Assets/Scripts/Rendering/BuildingShellEnvelope.cs`
- Modify: `Assets/Tests/EditMode/SkyLobbyGridTests.cs` (extend if needed)

**Interfaces:**
- Consumes: Task 1 grid API
- Produces: Sky lobby build tool selectable in HUD; ghost + drag place/extend on chosen floor

- [ ] **Step 1: Add `BuildTool.SkyLobby` or reuse room picker with `SkyLobby.asset`**

Mirror `HandleLobbyDrag` but:
- Floor comes from mouse cell Y (not locked to `LobbyFloor`).
- Calls `CanPlaceSkyLobby` / `TryPlaceSkyLobby`.

- [ ] **Step 2: Paint sky lobby**

In `TilemapTowerView`, branch `room.Type.isSkyLobby` → use lobby cutaway art (reuse `StructureCutawayArt.TryLobbyTile` or tint variant).

- [ ] **Step 3: Shell skip**

In `BuildingShellEnvelope.ShouldSkipShellCell`, add `room.Type.isSkyLobby`.

- [ ] **Step 4: Manual PlayMode check** — place sky lobby at 15+, extend width, see cutaway tile.

- [ ] **Step 5: Commit** `feat: sky lobby build tool and rendering`

---

### Task 3: TransferFloorProvider

**Files:**
- Create: `Assets/Scripts/Transit/TransferFloorProvider.cs`
- Create: `Assets/Tests/EditMode/TransferFloorProviderTests.cs`

**Interfaces:**
- Consumes: `TowerGrid.GetLobbyFloors()`
- Produces: `TransferFloorProvider.GetSortedTransferFloors(TowerGrid grid)`

```csharp
public static class TransferFloorProvider
{
    public static List<int> GetSortedTransferFloors(TowerGrid grid) {
        var floors = new List<int>(grid.GetLobbyFloors());
        floors.Sort();
        return floors;
    }

    public static IEnumerable<int> TransferFloorsBetween(int y0, int y1, TowerGrid grid) {
        var lo = Mathf.Min(y0, y1);
        var hi = Mathf.Max(y0, y1);
        foreach (var f in GetSortedTransferFloors(grid))
            if (f >= lo && f <= hi) yield return f;
    }
}
```

- [ ] **Step 1–4: TDD tests + implementation**

- [ ] **Step 5: Commit** `feat: transfer floor provider for lobby routing`

---

### Task 4: Multi-transfer TransitRouter

**Files:**
- Modify: `Assets/Scripts/Transit/TransitRouter.cs`
- Create: `Assets/Tests/EditMode/SkyLobbyRoutingTests.cs`

**Interfaces:**
- Consumes: `TransferFloorProvider`, existing `TryShaftWalkPaths`, `BuildFullElevatorLegs`, `BuildHybridLegs`
- Produces: `TryPlanTrip` succeeds for two-segment shaft trips via sky lobby

**Algorithm (add after single-shaft block, before return false):**

```csharp
foreach (var transferFloor in TransferFloorProvider.TransferFloorsBetween(start.y, goal.y, grid))
{
    foreach (var shaftA in _elevators.GetServingShafts(start.y, transferFloor))
    foreach (var shaftB in _elevators.GetServingShafts(transferFloor, goal.y))
    {
        if (shaftA == shaftB && shaftA.Serves(start.y) && shaftA.Serves(goal.y)) continue;
        // walks: start→A@start, A@transfer→B@transfer (same Y), B@goal→goal
        // score and keep best
    }
}
```

Pass `TowerGrid` into router rebuild or store reference on `TransitRouter.Rebuild(grid)`.

- [ ] **Step 1: Write failing test** — two shafts sharing sky lobby floor 30, trip 45→5 plans 2 elevator legs.

- [ ] **Step 2–4: Implement + pass**

- [ ] **Step 5: Commit** `feat: multi-shaft routing via sky lobbies`

---

### Task 5: AgentSystem transfer walk verification

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (only if horizontal lobby walk leg fails)
- Extend: `Assets/Tests/EditMode/SkyLobbyRoutingTests.cs` or PlayMode smoke

**Interfaces:**
- Consumes: Task 4 multi-leg plans

- [ ] **Step 1: Trace `TripLegs` execution for consecutive elevator legs with walk at same Y**

Ensure agent completes leg 1 at transfer floor, walks horizontally, boards leg 2.

- [ ] **Step 2: Fix minimal bugs** (path index reset between legs, cell at transfer floor)

- [ ] **Step 3: PlayMode** — agents above floor 30 reach ground via sky lobby transfer

- [ ] **Step 4: Commit** `fix: agent execution for sky lobby transfers`

---

### Task 6: Docs + help string

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (help tooltip)
- Modify: `docs/superpowers/specs/2026-08-28-sky-lobby-vertical-transit-design.md` → Status: Implemented (Phases 1–2)

- [ ] **Step 1: Add HUD help** per spec draft copy

- [ ] **Step 2: Mark spec implemented**

- [ ] **Step 3: Commit** `docs: mark sky lobby vertical transit implemented`

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Flexible ≥15 spacing | Task 1 |
| Multiple sky lobbies | Task 1 |
| Ground lobby singleton | Task 1 (unchanged) |
| Build UX | Task 2 |
| Shell skip | Task 2 |
| Multi-transfer routing | Task 4 |
| Agent execution | Task 5 |
| Express/service deferred | Global constraints |

No placeholders remain in task steps.

## Follow-on (separate plans)

- `2026-08-XX-express-elevators-design.md` — Phase 3
- `2026-08-XX-service-elevators-design.md` — Phase 4
