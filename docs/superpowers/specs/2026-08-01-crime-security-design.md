# Build-A-Tower — Crime Pressure, Security Patrols & Criminals

**Date:** 2026-08-01  
**Status:** Implemented  
**Depends on:** Stars3 ops (Security Post placeable + staffed-service pattern); agents + transit; commercial shop visits; hotel occupancy; stress / star gates  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → **ops usefulness** → more transit → evaluation/heatmaps → polish  
**Follow-up slice:** Research Lab upgrades (deferred — see §8)

## 1. Goals

Make the **Security Post** meaningful. **Per-floor crime** rises with shop congestion and hotel crowding; **hired security** suppress crime (tower baseline + floor patrols); **Criminal** visitors create localized stress until captured. Higher crime raises **stress for everyone**, feeding existing star stress gates.

### Success criteria

In Play Mode a player can:

1. See **per-floor crime** rise when shops and hotels on a floor get busy.
2. Hire **0–4** guards on a Security Post (auto-hire 1 on place, same as HK/Maint) and pay daily wages.
3. Observe **tower baseline** crime decay from staffed posts, plus **stronger decay** on floors where patrol agents are present (±1 floor).
4. See occasional **Criminal** agents enter via the lobby, roam toward shop/hotel floors, raise local crime/stress, and be **removed when a Security agent shares their floor**.
5. See **average tower crime** on the HUD; Selection shows Security staff + patrol count.
6. Confirm agents gain **stress from crime on their current floor** (and extra while sharing a floor with a Criminal).

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Slice order | **Security / crime first**; Research Lab upgrades next |
| Crime model | **Approach 1** — continuous per-floor pressure (0–100) + suppression |
| Geography | **Per floor** |
| Stress targets | **Everyone** (all agents on a floor feel that floor’s crime) |
| Security model | **Hybrid** — Post + staff = baseline; patrol agents boost hot floors |
| Congestion sources (this slice) | **Shops** + **hotel occupancy** on the floor |
| Entertainment rooms | **Out of scope** (wire later when rooms exist) |
| Criminal agents | **Yes** — random visitors; roam until captured or dwell timeout |
| Capture rule | Security agent on the **same floor** as the Criminal |
| Research buffs | **Out of scope** (including security-training multiplier) |

## 3. Per-floor crime pressure

### 3.1 State

- `CrimeSystem` (or equivalent) holds `float Crime[floor]` clamped **0–100**.
- Floors with no built cells may omit entries (default 0).

### 3.2 Sources (raise)

Each simulation tick (or short game-minute cadence), for each occupied floor:

| Source | Intent |
|--------|--------|
| Shop congestion | Scale with concurrent shop visitors and/or recent `VisitsToday` on `TrafficVariable` shops on that floor |
| Hotel crowding | Scale with occupied hotel rooms / guests currently on that floor |

Suggested shape (tunable constants, not sacred):

```
raise = ShopWeight * shopLoad + HotelWeight * hotelLoad
Crime[f] = min(100, Crime[f] + raise * dt)
```

No entertainment contribution in this slice.

### 3.3 Natural decay

Quiet floors decay slowly:

```
Crime[f] = max(0, Crime[f] - NaturalDecayPerDay * dayFraction)
```

Natural decay alone should **not** fully offset a busy shop floor without security.

### 3.4 Security suppression

**Baseline (tower-wide, weak):**  
For each non-broken `service_security` with `StaffedWorkers > 0`:

```
baselineDecay += BaselinePerStaffedPost * StaffedWorkers  // or flat per post + small per staff
```

Apply a share of `baselineDecay` to **every** floor with crime > 0 each tick.

**Patrol (strong, local):**  
While a Security agent is on floor `f` (and optionally apply 50% to `f±1`):

```
Crime[f] -= PatrolDecayPerSecond * dt
```

Recent-visit linger (optional v1): keep a short “covered until” timestamp per floor after a patrol leaves (~5–15 game minutes at reduced rate). Prefer implementing linger only if playtests feel too binary.

### 3.5 Criminal contribution

While a Criminal is on floor `f`:

```
Crime[f] += CriminalRaisePerSecond * dt
```

On capture: subtract a flat **CaptureCrimeDrop** on that floor (e.g. 5–15 points).

## 4. Security Post & patrol agents

### 4.1 Room

- Existing `service_security` / Security Post (3★ unlock).
- Extend **staffed service** pattern: auto-hire **1** on place; Selection **0–4**; same UI stepper as HK/Maint.
- Daily wage: **SecurityGuardWagePerDay** (suggested **$250**/day — between maid and handyman; tune in implementation).

### 4.2 Spawning patrol agents

- New `AgentRole.Security`.
- Mirror maid/handyman sync: `StaffedWorkers` on each Security Post ↔ that many Security agents with `Home` at the post.
- Non-population / ephemeral-or-staff treatment like Maid/Handyman (do not count toward star population).
- Broken post: treat like other ops — agents idle/despawn per existing staffed-room broken rules if any; post does not contribute baseline while broken.

### 4.3 Patrol behavior

- Idle at post when crime is globally low (optional threshold).
- Otherwise pick a target floor preferring **highest crime** among floors that have shops or hotels; fall back to highest crime overall.
- Path via existing transit (stairs/elevators).
- On arrival: linger a short patrol dwell, apply local decay, then replan.
- Do **not** replan every wait tick in elevators (reuse the service-agent wait/ride discipline from the maid/handyman elevator fix).

### 4.4 Capture

- Each agent tick (or on floor change): if any Security and any Criminal share the **same floor**, remove the Criminal (capture).
- If multiple Criminals on one floor, capture one per Security per tick (or all — pick **one per Security per tick** to keep it readable).
- Toast / `LastResult`-style one-liner optional: `"Security captured a criminal on floor N."`

## 5. Criminal agents

### 5.1 Spawn

- Role: `AgentRole.Criminal`.
- Enter via **lobby** like street visitors.
- Spawn cadence: low base rate; increases with **tower average crime** and/or total shop congestion (tunable; must be rare at low crime).
- Cap concurrent Criminals (e.g. **1–3**) so they stay special.

### 5.2 Behavior

- Roam: bias path goals toward floors with shops/hotels and high crime.
- Not renting rooms; not counting toward population.
- **Dwell timeout:** leave via lobby after N game hours if uncaptured; still leave residual floor crime from their raise while present.

### 5.3 Localized stress

While a Criminal is on floor `f`, agents currently on `f` gain extra stress (see §6).

### 5.4 Visual

- Distinct agent color/size so players can spot them (e.g. darker / red-tinted dot vs guests).
- Security agents: distinct from maids/handymen (e.g. blue-tinted).

## 6. Stress integration

### 6.1 Floor crime stress

On a daily pulse (mirror `ApplyLowConditionStress`) **and/or** a light continuous tick:

```
stress += CrimeStressPerDay * (Crime[agentFloor] / 100)   // daily pulse
// or
stress += CrimeStressPerSecond * (Crime[agentFloor] / 100) * dt
```

**Everyone** on that floor is affected (workers, guests, residents, visitors). Staff/Criminals may be exempt from crime stress to avoid feedback noise — **exempt Security, Maid, Handyman, Criminal** from crime-based stress.

### 6.2 Criminal proximity

```
if Criminal on same floor:
  stress += CriminalProximityStressPerSecond * dt
```

Same exemptions as above for staff roles.

### 6.3 Stars

No new star criteria. Existing avg-stress caps (1★/2★/3★) automatically punish unmanaged crime.

## 7. HUD & Selection

| Surface | Content |
|---------|---------|
| Top bar | `Crime {avg:0}` (tower mean of floors with content, or mean of all tracked floors) |
| Goals / optional | Hottest floor line later — **not required** for v1 |
| Security Selection | Staff 0–4 stepper; `Guards on patrol: N`; optional local floor crime if selected post’s floor |
| Agent dots | Security + Criminal distinct colors |

Build catalog: Security already placeable; ensure staffing UI includes `service_security` via `IsStaffedServiceRoom`.

## 8. Out of scope (Research slice — later)

Parked decisions for the follow-up spec:

- Research Lab projects: **fixed $ cost** + **base duration**; **more researchers shorten time only** (cost unchanged).
- Buffs: shop marketing, elevator speed/dispatch smarts, security training, housekeeping speed, maintenance speed/quality.
- Project picker UX still TBD (list vs tree vs auto-cycle).
- Entertainment rooms and entertainment→crime weighting.
- Classic SimTower bomb/fire events.
- Floor crime heat-tint on the tilemap (evaluation/heatmaps later).

## 9. Architecture (suggested)

| Unit | Responsibility |
|------|----------------|
| `CrimeSystem` | Per-floor crime; raise/decay/suppress API; avg crime |
| `AgentRole.Security` / `Criminal` | Roles + spawn/sync/patrol/capture/roam |
| `AgentSystem` | Staff sync for Security; criminal spawn; stress hooks; capture check |
| `EconomySystem` | Security wages at midnight |
| `BuildController` | `IsStaffedServiceRoom` includes Security; auto-hire 1 |
| `TowerHudController` | Avg crime chip; Selection patrol line |
| `TowerSimulation` | Tick `CrimeSystem`; wire day pulse |
| EditMode tests | Raise from congestion; baseline/patrol decay; capture; stress from crime; wage |

Keep crime math out of `StarSystem`. Prefer pure helpers testable without Play Mode.

## 10. Tuning defaults (starting points)

| Constant | Suggested start |
|----------|-----------------|
| Crime clamp | 0–100 |
| Natural decay | Low (busy floor stays elevated without security) |
| Baseline per staffed post | Mild tower-wide |
| Patrol decay | Clearly stronger than baseline on local floor |
| ±1 floor patrol bleed | 50% of patrol rate (optional) |
| Criminal concurrent cap | 3 |
| Security wage | $250/day |
| Staff range | 0–4, auto 1 on place |
| Capture | Same floor |
| Crime stress | Noticeable at crime ≥ 40 on populated floors |

Exact numbers locked during implementation/tests; behavior and relative strengths above are normative.

## 11. Test plan (acceptance)

### EditMode

1. Floor with shop load gains crime over time; empty floor does not (or gains far less).
2. Staffed Security Post reduces crime tower-wide vs unstaffed.
3. Security agent on floor F reduces `Crime[F]` faster than baseline alone.
4. Criminal on F raises `Crime[F]`; Security on F removes Criminal and drops crime.
5. Agent on high-crime floor gains more stress than on zero-crime floor.
6. Midnight charges Security wages × staffed workers.

### Play Mode

1. Cluster shops + busy hotels → avg crime rises → stress climbs.
2. Hire guards → patrols move → crime eases on visited floors.
3. Spot a Criminal → guard reaches floor → Criminal disappears.
4. HUD crime and Security Selection staffing work without shifting left HUD when Goals open.

## 12. Non-goals

- Changing star facility gates to require Security.
- Research multipliers on security effectiveness.
- Per-tile crime or navmesh.
- Multiplayer / persistence of crime across save (follow save-system norms when saves exist).
