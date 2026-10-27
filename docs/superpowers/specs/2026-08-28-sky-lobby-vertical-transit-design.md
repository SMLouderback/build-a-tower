# Build-A-Tower — Sky Lobby & Vertical Transit Design

**Date:** 2026-08-28  
**Status:** Implemented (Phases 1–4)  
**Depends on:** Slice #3 elevators, smart routing, hybrid stairs, selector/maintenance  
**Engine target:** Unity 2D Tilemap, desktop/Editor-first  

## 1. Goals

Enable **tall towers beyond ~30 floors** by adding **sky lobbies** (player-placed transfer floors) and **multi-shaft elevator routing**. Lay groundwork for **express** and **service/maid** elevator types in follow-on slices.

### Success criteria (this spec — Phases 1–2)

In Play mode a player can:

1. Place **sky lobbies** on upper floors with **≥15 floors** spacing from ground lobby and from every other sky lobby.
2. Extend **normal** elevator shafts in **≤30-floor segments** that meet at sky lobbies (or ground lobby).
3. Agents plan trips that **transfer** at a sky lobby when no single shaft serves both endpoints (walk → elevator → walk at lobby → elevator → walk).
4. Existing behavior unchanged for towers ≤30 floors with one shaft (no sky lobby required).

### Deferred to follow-on specs (Phases 3–4)

- **Express elevators** (2-wide, ground + sky lobbies only, faster motion) — target 3★ unlock.
- **Service / maid elevators** (staff-priority shaft type) — target 4★ unlock; distinct from player **maintenance mode** on normal shafts.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Sky lobby spacing | **Flexible minimum 15 floors** apart (not fixed slots at 15, 30, 45…) |
| Ground lobby | Still **one** at floor 0; counts as a lobby for spacing |
| Sky lobby count | **Multiple** allowed |
| Sky lobby horizontal span | Same rules as ground lobby extend (within tower envelope; punch-through transit) |
| Normal shaft span | **Unchanged:** ≤30 floors per contiguous shaft |
| Tall tower pattern | Stack normal shafts linked at sky lobbies (player strategy) |
| Transfer routing | **Multi-leg** trips through lobby floors when needed |
| Express / service types | **Out of scope** for Phase 1–2 implementation; interfaces reserved |
| SimTower binary parity | Out of scope |

**Player-facing help (draft):** “Sky lobbies are transfer floors. Place them at least **15 floors** apart. Stack normal elevators in segments of up to **30 floors** and link them at sky lobbies to reach higher floors.”

## 3. Architecture

```
TowerGrid (lobbies, shafts, spacing validation)
    ↓
LobbyRegistry / SkyLobbyIndex (floors 0 + sky lobby Y values)
    ↓
ElevatorSystem (unchanged shaft model per segment)
    ↓
TransitRouter (single-shaft + multi-transfer itineraries)
    ↓
AgentSystem (multi-leg TripLegs — already has list support)
```

New concepts:

- **`isSkyLobby`** on `RoomTypeSO` (or shared `isTransferLobby` with floor rules).
- **`TowerGrid` lobby spacing** — query nearest lobby floor(s), validate placement.
- **`TransferFloorGraph`** — helper used by `TransitRouter` to stitch shaft legs at lobby floors.

## 4. Sky lobby room type

### 4.1 Room asset

| Field | Value |
|-------|--------|
| `id` | `sky_lobby` |
| `displayName` | Sky Lobby |
| `isSkyLobby` | true (new flag; treated like lobby for punch-through / transit overlap) |
| `isLobby` | false (ground lobby remains special-cased) |
| `size` | `(6, 1)` default — same starter width as ground lobby |
| `allowAboveGround` | true |
| `buildCost` | Similar to ground lobby extension cost tier |
| `requiredStars` | 2 (tunable — available once mid-game towers matter) |

### 4.2 Placement rules

Let **L** = sorted list of lobby floors: `{0}` ∪ {each sky lobby floor}.

**Can place sky lobby at floor F, span [minX, maxX] when:**

1. `F >= MinSkyLobbyHeight` (default **15** — at least 15 floors above ground).
2. `|F - l| >= MinSkyLobbySpacing` for every `l` in **L** (default **15**).
3. Horizontal span within structural support (same as rooms above ground).
4. X span within **ground lobby horizontal bounds** (same constraint as normal elevator placement — keeps strategy focused on tower column).
5. Punch-through allowed for shafts/stairs; no overlap with stairs cells (hard ban unchanged).
6. Does not violate room placement rules for that floor.

**Extend sky lobby:** Same as ground lobby extend — widen horizontal span on same floor if cells are valid.

**Demolish:** Remove sky lobby room; re-validate spacing for remaining lobbies; routing rebuilds.

### 4.3 Runtime / walkability

- Sky lobby floor cells are **walkable** for agents (like ground lobby).
- Agents may **change shafts** horizontally on that floor (walk from shaft A column to shaft B column).
- Sky lobby is a valid **transfer node** for routing.
- Shell filler skips sky lobby cells (same as ground lobby / stairs / elevator).

### 4.4 Visuals (MVP)

- Reuse ground lobby cutaway art with distinct tint, **or** placeholder palette tile until dedicated art.
- HUD build tool: **Sky Lobby** (separate from ground Lobby tool).

## 5. Multi-shaft transfer routing (Phase 2)

### 5.1 Problem

`TransitRouter.TryPlanTrip` today picks **at most one** elevator leg (plus walk/stairs hybrids). A shaft serves contiguous `MinFloor..MaxFloor` with span ≤30. A trip from floor 52 → floor 8 needs:

```
walk → shaft@52 down to sky lobby@30 → walk across lobby → shaft@30 down to 8 → walk
```

### 5.2 Transfer floors

`TransferFloorProvider` returns sorted lobby floors from grid:

- Always include **0** (ground lobby) when `HasLobby`.
- Include each sky lobby floor **Y**.

### 5.3 Planning algorithm (MVP)

After existing same-floor walk and short-stairs checks:

1. **Try existing single-shaft plans** (full elevator + hybrids + over-cap stairs). If success, return (no change for simple towers).

2. **Try one-transfer plans** via each transfer floor **T** where:
   - `min(start.y, goal.y) <= T <= max(start.y, goal.y)` (transfer lies between endpoints vertically), **or**
   - `T == 0` or nearest sky lobby when endpoints straddle segments (handle ground ↔ upper segment).

   For each candidate **T**:
   - Find best shaft **A** serving `start.y` and **T** (existing `GetServingShafts`).
   - Find best shaft **B** serving **T** and `goal.y` (may equal **A** if same shaft covers both — then this collapses to single-shaft; skip duplicate).
   - Plan walks: `start → A@start.y`, `A@T → B@T` (same floor, horizontal), `B@goal.y → goal`.
   - Score total walk + wait(A) + wait(B) + stair penalties (same scoring helpers).
   - Pick best feasible itinerary.

3. **Try two-transfer** (optional MVP stretch): ground → sky₁ → sky₂ → goal if one-transfer fails and tower height warrants. **YAGNI:** start with **one transfer** only; add second transfer if tests require >60 floors.

4. If no plan: return false (agent stress / stuck behavior unchanged).

### 5.4 Trip leg shape

Reuse existing `TransitLeg` list on agent:

```
Walk(start → entryA)
Elevator(shaftA, start.y → T)
Walk(T, column A → column B)   // may be empty if same X
Elevator(shaftB, T → goal.y)
Walk(exitB → goal)
```

`AgentSystem` already iterates `TripLegs`; verify horizontal walk leg at transfer floor executes correctly (likely already works if cells list is populated).

### 5.5 Edge cases

| Case | Behavior |
|------|----------|
| Sky lobby at floor 30, shaft A 0–29 only | Shaft must **include floor 30** to serve transfer — player must extend shaft to lobby floor |
| Only one shaft in tower | Single-shaft routing only; sky lobby still useful as horizontal hub for wide towers |
| Shaft in maintenance | Exclude from planning (existing) |
| Agent waiting rescore | Re-plan trip; may switch transfer floor or shaft |
| No sky lobby built above 30 | Trips above single shaft span **fail** unless stairs path exists (stress) — intentional |

## 6. Express elevators (Phase 3 — implemented)

| Field | Target |
|-------|--------|
| Width | **2** cells |
| Served floors | Ground lobby (0) + **all sky lobby floors** only |
| Motion | **2.5×** faster than normal (`ElevatorShaftRuntime.ExpressSpeedMultiplier`) |
| Unlock | **3★** (`elevator_express`) |
| Max span | **100 floors** per contiguous shaft (lobby stops only) |
| Routing | Serves lobby-to-lobby vertical hops; pairs with sky-lobby transfer graph |

## 7. Service / maid elevators (Phase 4 — implemented)

| Field | Target |
|-------|--------|
| Width | **1** cell (normal footprint) |
| Served floors | Contiguous span ≤32 (like normal) |
| Passengers | Maids and handymen **prefer** service shafts in routing scores |
| Unlock | **4★** (`elevator_service`) |
| Distinction | **Not** the same as `InMaintenance` (player drain mode on normal shafts) |

`AgentSystem` service abandon after 45 min wait remains fallback until service shafts ship.

## 8. Files to touch (Phases 1–2)

| File | Change |
|------|--------|
| `RoomTypeSO.cs` | `isSkyLobby` flag |
| `TowerGrid.cs` | Sky lobby place/extend/demolish; spacing validation; lobby floor index |
| `BuildController.cs` | Sky lobby tool, ghost, validation messages |
| `TowerHudController.cs` | Sky lobby toolbar entry + help |
| `TilemapTowerView.cs` | Paint sky lobby like lobby cutaway |
| `BuildingShellEnvelope.cs` | Skip sky lobby cells |
| `TransferFloorProvider.cs` | **New** — list transfer floors |
| `TransitRouter.cs` | Multi-transfer planning |
| `AgentSystem.cs` | Verify multi-leg execution at transfer (minimal fixes) |
| `Resources/Rooms/SkyLobby.asset` | **New** |
| `ElevatorTests.cs` / new `SkyLobbyTests.cs` | Placement spacing tests |
| `TransitRouterTests.cs` | Multi-transfer routing tests |

## 9. Testing

### EditMode

- Spacing: floor 14 rejected; 15 accepted if no other lobby nearby; second at 29 rejected, 30 accepted.
- Ground lobby counts toward spacing.
- Router: tower with two shafts meeting at sky lobby 30 routes floor 45 → 5.
- Router: single shaft tower unchanged.

### PlayMode

- Build sky lobby at 32, stack two 30-floor shafts, agents commute above floor 30.
- Demolish sky lobby → trips above span fail gracefully.

## 10. Out of scope

- Express/service shaft types (Phases 3–4)
- Per-floor stop schedules / dayparts
- Multi-car per shaft
- Second transfer hop (unless needed by tests)
- Dedicated sky lobby art pass (placeholder OK)

## 11. Self-review

- [x] Spacing rule matches user intent (≥15 flexible, not fixed slots)
- [x] Ground lobby singleton preserved
- [x] Normal 30-floor cap unchanged
- [x] Maintenance **mode** naming not conflated with service **elevator type**
- [x] Phased delivery: sky lobby + routing first; express/service stubbed
- [x] No placeholder TBD in core Phase 1–2 rules
