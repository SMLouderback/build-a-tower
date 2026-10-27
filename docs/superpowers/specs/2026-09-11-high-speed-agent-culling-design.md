# High-Speed Agent Culling Design

**Status:** Approved  
**Date:** 2026-09-11

## 1. Goal

Reduce GPU/CPU cost at high simulation speeds by hiding outdoor walking agent sprites while keeping the simulation, elevator cars, idle-in-room pops, and map overlays fully accurate.

## 2. Speed rules

HUD presets use `GameClock.MinutesPerRealSecond`: `{0, 1, 2, 5, 10, 60}`.

- **Full people (walkers visible):** rates below `10` (`1x`, `2x`, `5x`).
- **High-speed LOD:** rates `≥ 10` (`10x`, `60x`).

Pause (`||`) freezes fade progress. LOD mode follows the last non-zero rate when play resumes.

## 3. What stays visible at high speed

| Layer | High-speed behavior |
|-------|---------------------|
| Idle agents in rooms (`AtHome`, `Working`, `Staying`, `VisitingShop`) | Stay visible; pop in/out on room arrival/departure |
| Outdoor walkers, stairs, lobby, `WaitingAtElevator` | Fade out (staggered) |
| Elevator riders (`Riding`) | Remain hidden (existing rule) |
| Elevator cars (`ElevatorView`) | Keep animating |
| Traffic / Economic / other map overlays | Unchanged; continue using real sampled data |
| Stress / traffic HUD numbers | Unchanged |
| Agent simulation (`AgentSystem.Tick`) | Unchanged; full updates continue |

## 4. Architecture

### 4.1 Pure helper — `AgentSpeedLod`

Centralize constants and classification:

- `HighSpeedThreshold = 10f`
- `IsHighSpeed(float minutesPerRealSecond)` → `minutesPerRealSecond >= HighSpeedThreshold`
- `IsIdleInRoom(AgentPhase)` → the four idle room phases above
- `NeedsLodHide(Agent)` → would normally render (`ShouldRenderSprite`) and is **not** idle-in-room
- Fade timing / per-frame start budget constants

### 4.2 Wiring

- `TowerSimulation` passes `GameClock.MinutesPerRealSecond` into `AgentView.Sync`.
- `AgentView` owns per-sprite fade state (target alpha, current alpha, stagger delay).
- Elevator car rendering stays exclusively in `ElevatorView`.

### 4.3 Classification each Sync

For each agent that passes existing `ShouldRenderSprite` / shaft occlusion rules:

1. **Idle-in-room** → force full opacity and enabled; cancel any hide fade.
2. **LOD target** → drive toward alpha `0` when high-speed, toward `1` when not.
3. At alpha `0`, disable the renderer and skip walk-frame animation work.

## 5. Staggered fades

Both directions use staggered fades (raise to high speed and drop below it).

- Randomize start delay or shuffle which agents may begin a fade this frame.
- Cap **new fade starts** per real frame (budget), so large populations do not spike GPU/CPU.
- Spread completion over roughly **1–2+ seconds** of real time via stagger + per-sprite fade duration, not one global flash.
- While paused, do not advance fade timers.

## 6. Edge cases

- Agent arrives into an idle room mid fade-out → immediately full-opacity idle (room pop wins).
- Agent leaves a room into a trip while high-speed → becomes a LOD target and joins the fade-out queue.
- Mid-path walkers that reappear after dropping below 10x do so via staggered fade-in at their current simulated positions.
- No changes to demand, stress application, pathing, or overlay sampling.

## 7. Testing

EditMode coverage:

- Threshold helper (`< 10` vs `≥ 10`).
- Idle-in-room vs LOD-hide classification.
- Idle room forces visible / cancels hide.
- Per-frame fade-start budget is respected.
- High-speed does not claim to alter simulation or overlays (documentation / no-op assertion where useful).

## 8. Out of scope

- Elevator car LOD or car pooling changes
- Frustum / camera distance LOD
- Reducing simulation tick rate
- Economy / demand rebalance
- Map overlay redesign

## 9. Success criteria

1. At `10x`/`60x`, outdoor walkers disappear via staggered fade; idle room pops and elevator cars remain.
2. Traffic and economic overlays still reflect real data with overlays on.
3. Dropping below `10x` restores walkers via staggered randomized fade-in without a single-frame spike.
4. Raising to high speed also fades out staggered (not instant).
5. Focused EditMode tests for the helper and fade budget pass.
