# Build-A-Tower — Research Lab Tech Tree & Daily Burn

**Date:** 2026-08-02  
**Status:** Implemented (Play Mode pending) — plan (`docs/superpowers/plans/2026-08-02-research-lab.md`)  
**Depends on:** Stars3 (`service_research` placeable); staffed-service pattern; `MarketClimate`; shop spend; elevators; crime/security; maid/handyman timings  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → ops usefulness → **research** → more transit → evaluation/heatmaps → polish  
**Prior slice:** Crime/security (`docs/superpowers/specs/2026-08-01-crime-security-design.md`)

## 1. Goals

Make the **Research Lab** meaningful. The player picks techs in a **5-branch × 3-level tree**, pools researchers from all labs into **one tower-wide project**, pays **low idle + high active daily costs** (climate-scaled), sees **ETA and estimated total/$ remaining**, and loses progress slowly when paused so they want to resume quickly.

### Success criteria

In Play Mode a player can:

1. Place Research Labs (3★), hire **0–4** researchers (auto-hire **1**), and see pooled researcher count.
2. Open Selection on a lab and pick an unlocked tech (branch level I–III with prerequisites).
3. See **ETA**, **est. remaining $**, idle vs active rates, a **tech effect summary** for the selected level, and a climate note before/while researching.
4. Watch progress advance faster with more researchers / more labs; higher levels take longer.
5. Pay idle daily when idle and higher burn while a project is active; costs move with Recession↔Boom.
6. Auto-pause when broke or when researcher pool hits 0; progress **decays slowly** while paused.
7. Complete Marketing / Elevator Ops / Security Training / Housekeeping / Maintenance I–III and feel the matching buffs.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Project picker | Player picks tech (list/tree UI) |
| Tree shape | **5 branches × 3 levels** (I → II → III); higher needs lower in same branch |
| Parallelism | **One** active project tower-wide; all labs pool researchers |
| Duration | Higher levels take longer (hire/build incentive) |
| Cost model | **Approach 1** — daily idle + active burn; estimate remaining $ |
| Climate | Research burn cheaper in Recession, pricier in Boom |
| Researchers | More researchers **shorten duration**; estimate reflects that |
| Pause decay | Progress **burns down slowly** while paused (keep completed techs) |
| Idle cost basis | **Per non-broken Research Lab** (even if staff 0) |
| Active cost | Flat tower **active surcharge** while a project is running (plus idle) |
| 0 researchers | Project **auto-pauses** (idle only; decay applies) |
| Broke | Midnight charge fails → **auto-pause**; progress kept then decays |

## 3. Labs & staffing

### 3.1 Room

- Existing `service_research` / Research Lab (3★ unlock).
- Extend staffed service: auto-hire **1** on place; Selection **0–4**.
- Daily **researcher wage** (suggested **$350**/day per staffed worker) — separate from lab idle/active research burn, charged with other wages at midnight.

### 3.2 Pool

```
researcherPool = sum(StaffedWorkers) over non-broken service_research rooms
```

Broken labs: no researchers, **no idle lab burn**.

## 4. Tech tree

### 4.1 Branches

| Id | Branch | Level effects (cumulative; tune in implementation) |
|----|--------|-----------------------------------------------------|
| `marketing` | Marketing | Shop spend / visit payout × (1.10 / 1.20 / 1.35) |
| `elevator` | Elevator Ops | Car speed × (1.10 / 1.20 / 1.35); II+ better multi-stop / queue scoring; III further wait/busy tuning |
| `security` | Security Training | Crime baseline + patrol decay × (1.15 / 1.30 / 1.50) |
| `housekeeping` | Housekeeping | Clean minutes × (0.90 / 0.80 / 0.65) |
| `maintenance` | Maintenance | Repair minutes × (0.90 / 0.80 / 0.65); repair chunk × (1.10 / 1.25 / 1.45) |

Exact multipliers are starting points; relative “higher = stronger” is normative.

### 4.2 Prerequisites

- Level I: always available once ≥1 Research Lab exists (or always listable but Start disabled until a lab exists).
- Level II: requires I complete in that branch.
- Level III: requires II complete in that branch.
- Branches are independent.

### 4.3 Base work (game minutes)

| Level | BaseWorkMinutes (suggested) |
|------:|----------------------------:|
| I | 1 day (24 × 60 = **1440**) |
| II | 3 days (**4320**) |
| III | 7 days (**10080**) |

Higher = longer is locked; exact numbers tunable.

## 5. Progress & speed

### 5.1 Rate

With `n = researcherPool`:

```
if n <= 0: no progress (auto-pause)
else: workPerGameMinute = 1 + (n - 1) * ResearcherSpeedBonus
```

Suggested `ResearcherSpeedBonus = 0.35` (2 researchers ≈ 1.35×, 4 ≈ 2.05×, 8 ≈ 3.45×).

Progress accumulates `workPerGameMinute * dt` toward `BaseWorkMinutes`.

### 5.2 Completion

On fill: mark tech complete, clear active project, apply buffs permanently (saved on a `ResearchSystem` / run state). Toast: `"{Branch} {I|II|III} complete."`

## 6. Costs

### 6.1 Lab idle / active (research overhead)

Suggested defaults (before climate):

| Cost | Amount |
|------|--------|
| Idle per non-broken lab / day | **$500** |
| Active surcharge / day (tower-wide while project running) | **$2,000** |

Charged at **midnight** with economy wages (or same day-roll hook).

```
labIdle = IdlePerLab * nonBrokenLabCount
active = projectRunning ? ActivePerDay : 0
researchBurn = (labIdle + active) * ResearchClimateMultiplier(climate)
```

### 6.2 Climate multiplier

Reuse `MarketClimate.SpendMultiplier` (Recession 0.7 … Boom 1.3) **or** a dedicated table with the same direction. Spec default: **reuse SpendMultiplier** as `ResearchClimateMultiplier`.

### 6.3 Researcher wages

`StaffedWorkers * ResearchWagePerDay` ($350 suggested) via existing wage switch for `service_research`.

### 6.4 Estimates (Selection UI)

```
remainingWork = BaseWorkMinutes - progress
etaMinutes = remainingWork / max(workPerGameMinute, ε)
etaDays = etaMinutes / (24*60)
estRemaining$ = etaDays * (labIdle + ActivePerDay) * climateMult
```

Also show **est. full project $ from 0%** when selecting a locked-not-started node (same formula with `remainingWork = BaseWorkMinutes`).

Refresh when staff, lab count, or climate changes.

Label clearly: *Estimate at current climate & staff; actual burn changes if climate shifts.*

## 7. Pause & progress decay

### 7.1 Auto-pause triggers

- Wallet cannot pay midnight research burn (after attempting charge).
- `researcherPool == 0` while a project is selected/active.
- Player chooses Pause (if UI offers it).

Paused: no progress; idle lab burn still applies; active surcharge **off**.

### 7.2 Decay

While paused and `progress > 0`:

```
progress = max(0, progress - DecayWorkPerDay * dayFraction)
```

Suggested `DecayWorkPerDay = 0.05 * BaseWorkMinutes` of the **active** project (5% of that project’s base per day) — painful over a few days, not an instant wipe.

- Decay never affects **completed** techs.
- Hitting 0% while paused keeps the project selected but empty (player can restart without re-unlocking).

### 7.3 Switching projects

Selecting a different tech pauses the previous (decay applies to its stored progress). Each incomplete node stores its own progress independently.

## 8. Buff application (hooks)

| Branch | Hook sites (suggested) |
|--------|------------------------|
| Marketing | Shop spend tally / `AgentWealth` spend or `RoomInstance.RecordShopSpend` path |
| Elevator | `ElevatorCar.MinutesPerFloor` (or runtime multiplier); `ElevatorRouting` weights for II/III |
| Security | `CrimeSystem` baseline/patrol decay multipliers |
| Housekeeping | `RoomConditionRules.CleanMinutes` |
| Maintenance | Repair duration + `ApplyRepairTick` chunk |

Prefer a single `ResearchEffects` (or `ResearchSystem`) read by those systems — no scattered magic numbers.

## 9. UI

### 9.1 Research Lab Selection

When a Research Lab is selected:

- Staff stepper 0–4  
- `Researchers in pool: N` (tower-wide)  
- Branch list with I/II/III status: locked / available / in progress % / complete ✓  
- Selected tech: Start / Pause  
- Lines: ETA, est. remaining $, idle/day, active/day, climate name + mult  
- Optional: “Paused — progress decaying”

### 9.2 Top bar

Optional `Research 62%` chip — **out of scope for v1** unless cheap; Selection is enough.

## 10. Architecture (suggested)

| Unit | Responsibility |
|------|----------------|
| `ResearchSystem` | Tree state, active project, progress, pause, decay, complete |
| `ResearchCatalog` / static defs | Branch ids, levels, BaseWorkMinutes, buff magnitudes |
| `ResearchEffects` | Query completed levels → multipliers for other systems |
| `EconomySystem` | Idle/active research burn + research wages at midnight |
| `BuildController` | Auto-hire + `IsStaffedServiceRoom` includes research |
| `TowerSimulation` | Tick progress/decay; wire climate |
| `TowerHudController` | Selection research panel |
| EditMode tests | Prerequisites, speed vs staff, climate burn, pause decay, buff hooks |

## 11. Tuning defaults (starting points)

| Constant | Suggested |
|----------|-----------|
| Levels per branch | 3 |
| Branches | 5 (above) |
| Base work I/II/III | 1 / 3 / 7 days |
| ResearcherSpeedBonus | 0.35 per extra researcher |
| Idle per lab / day | $500 |
| Active / day | $2,000 |
| Research wage / worker / day | $350 |
| Climate mult | MarketClimate.SpendMultiplier |
| Pause decay | 5% of project BaseWork / day |
| Max consecutive extreme climate | (unchanged; climate slice) |
| Staff | 0–4, auto 1 |

## 12. Test plan (acceptance)

### EditMode

1. Cannot start II without I; can start I with a lab.
2. More researchers reduce ETA and est. $ vs 1 researcher (same climate).
3. Midnight charges idle×labs; +active while running; scaled by climate.
4. Broke / 0 researchers → paused; progress declines over days; complete techs untouched.
5. Completing Marketing I increases shop payout vs baseline; Security I increases crime decay; etc. (one assert per branch minimum).

### Play Mode

1. Build 2 labs, hire staff, start Elevator Ops I — ETA drops vs 1 lab.  
2. Boom raises est. $; Recession lowers it.  
3. Drain wallet → pause → progress ticks down → refund/hire → resume.  
4. Finish I → II unlocks; buffs visible (faster elevators / cheaper cleans / etc.).

## 13. Non-goals

- Multiple simultaneous projects  
- Visible scientist agents pathfinding  
- Research as a star gate  
- Entertainment research branch  
- Persisted multi-run meta unlocks (single tower run state only for now)

## 14. Self-review notes

- No TBD placeholders for core loop; multipliers/durations marked as tunable starting points.  
- Earlier “fixed $ / researchers don’t change cost” superseded by daily burn + ETA-based estimate (locked in brainstorm).  
- Idle **per lab** and active **flat surcharge** match approved design Q&A.  
- Elevator II/III “smarter routing” may be thin in v1 (speed + weight tweaks) without a full dispatcher rewrite — acceptable if I ships speed and II/III adjust routing constants.
