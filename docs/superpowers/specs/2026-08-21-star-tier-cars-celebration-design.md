# Build-A-Tower — Star-Tier Elevator Cars + Star Celebration

**Date:** 2026-08-21  
**Status:** Implemented  
**Depends on:** `StarSystem`; `ElevatorView`; `TowerHudController` pause/speed restore; `StructureCutawayArt.SetStarRating` / `SyncStructureArtToStars`; day-night sky  
**Parent:** Visual polish → star progression readable on cars + a “wow” on tier change  
**Follow-ups:** Star-tier elevator shaft tiles; celebration SFX; room cutaway kits

## 1. Goals

1. Elevator **cars** show exact star progression 0★–5★ (current car art = 5★).  
2. On each star **gain**, queue a celebration: procedural fireworks in the sky behind the tower + pause modal with Continue.  
3. On each star **loss**, queue a quieter pause modal (no fireworks) with Continue.  
4. Continue restores the **pre-modal** sim speed and paused state.  
5. Multi-step promote/demote = **one modal per step**, FIFO.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Architecture | Event queue + HUD modal + world fireworks (`StarCelebrationController`) |
| Car art | Exact star 0–5; rename current → `elevator_car_s05`; generate `s00`–`s04` |
| Shaft tiles | Out of scope (car only) |
| Promote UX | Fireworks + modal + pause; queue per star gained |
| Demote UX | Quieter modal + pause; no fireworks; queue if multiple |
| Fireworks | Procedural particles, world space, behind tower |
| Modal UI | IMGUI (same family as Esc pause) |
| Esc during celebration | Continue-only; Esc does not open quit menu or skip |
| Save / `ForceStars` | No celebration events |
| Art refresh | Lobby/stairs/cars jump to **final** star immediately; celebration is presentation only |

## 3. Runtime

### Star change events

```
StarChangeEvent { Kind: Promoted | Demoted, Stars: int }
```

- `TryPromote`: each `CurrentStars++` appends `Promoted(newStars)`.  
- Quarterly demote: each demotion appends `Demoted(newStars)` (today usually one step).  
- `ForceStars` / load: clear pending list; do **not** enqueue.  
- Callers keep existing `bool` / void APIs; celebration consumer drains the step list after promote/quarterly.

After a batch, `SyncStructureArtToStars` still runs so structure art matches the **final** rating before/while modals play.

### `StarCelebrationController`

1. FIFO queue of `StarChangeEvent`.  
2. When idle and queue non-empty → start next:  
   - Snapshot `MinutesPerRealSecond` + `Paused` (same idea as Esc pause).  
   - Pause via `SetSpeedPreset(speed, paused: true)`.  
   - If **Promoted**: start fireworks.  
   - Show IMGUI modal for that step.  
3. **Continue**: stop fireworks → restore snapshot → dequeue → if more events, start next (fresh snapshot each time).  
4. While active: `BlocksWorldInput == true`; Esc pause menu does not open.  
5. If Esc pause is already open when a star fires: wait until that pause closes, then run the queue.

### Fireworks

- World-space particle bursts above/behind the tower silhouette.  
- Sorting: above sky, below room/structure/car layers.  
- Lifetime tied to the open promote modal; destroyed on Continue.  
- No SFX in v1.

### Elevator cars

```
resource = $"elevator_car_s{star:00}"
fallback = nearest lower star → nearest higher → "elevator_car" → procedural
```

`ElevatorView` refreshes all car sprites when star sync runs.

## 4. Art

**Cars** — `Assets/Resources/Art/Structure/elevator_car_s{SS}.{png,bytes}` (+ metas)

- One-cell footprint, side-on cutaway car, cables/winch on top, transparent background (no black plate).  
- **5★:** existing ornate brass / warm glow (renamed).  
- **4★:** clean brushed metal, softer gold, tidy light.  
- **3★:** plain steel, fluorescent wash, modest trim.  
- **2★:** scuffed budget panels, dull light.  
- **1★:** dented, uneven paint, weak bulbs.  
- **0★:** rust, dark/boarded windows, bare bulb, scuffs.

**Modal**
- Promote: warm panel, clear star gain to N★, short congrats, **Continue**.  
- Demote: muted panel, “Demoted to N★”, quieter quarterly copy, **Continue**.  
- Dim full-screen backdrop.

## 5. Code touchpoints

| Piece | Role |
|-------|------|
| `StarSystem` | Produce/clear step event list on promote/demote/`ForceStars` |
| `StarCelebrationController` | Queue, pause gate, modal, fireworks orchestration |
| `StarFireworks` (helper) | Procedural bursts behind tower |
| `ElevatorView` | Star-tier car load + refresh |
| `TowerSimulation` | After promote/quarterly → enqueue celebration + sync art (incl. cars) |
| `TowerHudController` | OR celebration into `BlocksWorldInput`; don’t steal Esc |

**Tests**
- Promote 0→2 enqueues Promoted(1), Promoted(2).  
- Demote enqueues Demoted(n); `ForceStars` enqueues nothing.  
- `ElevatorCarResource(3)` → `elevator_car_s03`.  
- Continue restores snapshotted speed + paused flag (thin pause-gate unit if needed).

## 6. Success criteria

- Cars visibly change from dingy (0★) to glam (5★) with rating.  
- Multi-star promote shows one modal per step with fireworks each promote step.  
- Demote acknowledges without fireworks; game resumes at prior speed after Continue.  
- Load/`ForceStars` never pops the modal.  
- Missing car art falls back without pink cars.
