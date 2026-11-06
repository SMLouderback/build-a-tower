# Player-Built Scaffolding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dedicated Scaffold tool so players can click/drag-paint $500 walkable structural fill cells to support upper floors with gaps below.

**Architecture:** Extend `BuildTool` with `Scaffold`. Add `TowerGrid.CanPlaceScaffold` / `TryPlaceScaffold` using the shared scaffolding type (cost 500). Wire paint + wallet in `BuildController`. HUD exposes an always-visible Scaffold button. Pathfinding already walks occupied cells.

**Tech Stack:** Unity 6000.4.x, TowerGrid / BuildController / TowerHudController, NUnit EditMode (net8 hosts OK if Editor busy).

**Spec:** `docs/superpowers/specs/2026-08-05-player-scaffolding-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Do not implement scaffold refunds, auto-collapse, corridor art, star gates, or upkeep
- Prefer Subagent-Driven Development; if quota exhausted, implement inline
- No parallel-cli
- Cost is exactly **$500** per cell

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Core/BuildTool.cs` | Add `Scaffold` |
| `Assets/Scripts/Core/TowerGrid.cs` | `CanPlaceScaffold` / `TryPlaceScaffold`; `ScaffoldBuildCost = 500` |
| `Assets/Scripts/Build/BuildController.cs` | Scaffold tool, click/drag paint, wallet, ghost, help |
| `Assets/Scripts/UI/TowerHudController.cs` | Always-visible Scaffold button |
| `Assets/Tests/EditMode/ScaffoldPlacementTests.cs` | Place, support, path, demolish, wallet |
| `README.md` | Document tool |
| Spec → Implemented |

---

### Task 1: Grid API + tests

**Files:**
- Modify: `Assets/Scripts/Core/TowerGrid.cs`
- Create: `Assets/Tests/EditMode/ScaffoldPlacementTests.cs` (+ `.meta`)
- Update: `Assets/Tests/EditMode/ServiceAgentTests.cs` — use `TryPlaceScaffold` instead of `TryPlace(Scaffold())` where needed

**Interfaces:**
- `TowerGrid.ScaffoldBuildCost` = `500`
- `bool CanPlaceScaffold(Vector2Int cell)`
- `bool TryPlaceScaffold(Vector2Int cell, out RoomInstance room)`
- Update `CreateScaffoldingType` → `buildCost = ScaffoldBuildCost`

- [x] **Step 1: Write failing tests** (from spec §8 — support gap, path across scaffold, load-bearing lock, wallet debit)
- [x] **Step 2: Run — expect FAIL**
- [x] **Step 3: Implement CanPlaceScaffold / TryPlaceScaffold**
- [x] **Step 4: Re-run — expect PASS**
- [ ] **Step 5: Commit** (only if user asked)

Implementation sketch:

```csharp
public const int ScaffoldBuildCost = 500;

public bool CanPlaceScaffold(Vector2Int cell)
{
    if (!HasLobby) return false;
    if (cell.y == LobbyFloor) return false;
    if (cell.x < MinX || cell.x > MaxX) return false;
    if (_cells.ContainsKey(cell)) return false;
    // Match CreateScaffoldingType: allowAboveGround + allowBasement.
    if (cell.y == LobbyFloor) return false;
    return HasSupportFromAdjacentLevel(cell, new HashSet<Vector2Int> { cell });
}

public bool TryPlaceScaffold(Vector2Int cell, out RoomInstance room)
{
    room = null;
    if (!CanPlaceScaffold(cell)) return false;
    if (_scaffoldingType != null)
        _scaffoldingType.buildCost = ScaffoldBuildCost;
    room = new RoomInstance(_nextId++, _scaffoldingType, cell, Vector2Int.one);
    Register(room);
    return true;
}
```

Keep `CanPlace` rejecting `isScaffolding` so PlaceRoom cannot place scaffolds accidentally.

Update ServiceAgentTests line that does `grid.TryPlace(Scaffold(...))` to place support via `TryPlaceScaffold` in a loop across width, or place a real pad room — prefer looping `TryPlaceScaffold` for x=6..14 on floor 2 if that was the intent of the 9-wide scaffold helper.

---

### Task 2: BuildController paint + HUD + README

**Files:**
- Modify: `Assets/Scripts/Core/BuildTool.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `README.md`
- Spec → Implemented

- [x] **Step 1: Add enum + SelectScaffoldTool + TryPlaceScaffoldAt + HandleScaffoldDrag**
- [x] **Step 2: HUD Scaffold button in DrawToolIcons**
- [x] **Step 3: README + mark spec Implemented**
- [ ] **Step 4: Commit** (only if user asked)

Paint: on mouse down start drag + place current cell; while held, place each newly hovered cell once (`HashSet`); on mouse up end drag. Hover ghost for single cell validity when not dragging.

---

## Execution

After approval, SDD (user preference). If quota exhausted, implement inline.
