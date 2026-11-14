# Build-A-Tower — Condo Commute & Stair Capacity

**Date:** 2026-08-03  
**Status:** Implemented — plan `docs/superpowers/plans/2026-08-03-condo-commute-stair-capacity.md`  
**Depends on:** Condo move-in / sale; office workers + schedules; commercial lunch visits; hybrid stairs + elevator routing; agent transit  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → **more transit / population realism** → evaluation/heatmaps → polish  
**Prior slice:** Conference / events + news; camera scroll margins  

## 1. Goals

Make **condo residents** participate in weekday work traffic so towers feel lived-in and elevators/stairs face realistic rush-hour load. Split condo jobs between **in-tower office desks** (vacant seats only, condo priority for claiming) and **outside employers** (≥50% of condo population). Add **stair capacity** so flights bottleneck under two-way rush instead of acting as unlimited pipes.

### Success criteria

In Play Mode a player can:

1. See moved-in condo residents leave for work in the morning and return after an ~8h day.
2. Observe **≥50%** of condo residents commuting **Outside** (lobby → Outside → later return), with one-way commute **15–60 min** biased toward **~30**.
3. See remaining condo workers claim **vacant office desks** (never displacing a seated office worker); surplus demand becomes outside commute.
4. Watch office workers still fill remaining desks from Outside.
5. Notice **stair flights fill up** during rush; agents wait when at capacity; waits add stress; opposing flows share the same cap.
6. Keep hotel / shop / event visitor behavior intact aside from shared transit contention.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| In-tower condo job | Path to a **real office room** with free `maxOccupants` capacity |
| Desk priority | Condos claim vacant desks **before** office workers for that day; **no displacement** of already-assigned office agents |
| If no vacant desk | That condo works **outside** that day |
| Mix formula | `inTowerWanted = floor(min(0.5, officeDesks / condoResidents) * condoResidents)` |
| Outside floor | Always **≥50%** outside when residents > 0 (via the `min(0.5, …)` cap on in-tower share) |
| Outside schedule | Mimic office day length; leave home → lobby → Outside; return after commute + work (+ optional non-shop lunch) |
| Commute length | One-way **15–60** game minutes, average / bias **~30** |
| Lunch | In-tower condo workers: same shop lunch window as offices (~11:30–13:30), some skip (desk lunch). Outside condo workers: no tower shop trip while Outside (desk lunch / no action) |
| Stairs scope | **Fuller** same-slice: concurrent cap per flight, shared up/down, wait + stress |
| Architecture | Approach 1 — schedule/desk logic in agent pipeline with pure helpers for employment math + stair capacity |

## 3. Employment mix

### 3.1 Counts

```
officeDesks     = Σ maxOccupants over non-broken Office rooms
condoResidents  = count CondoResident with HasMovedIn
inTowerWanted   = floor(min(0.5, officeDesks / max(1, condoResidents)) * condoResidents)
```

Examples:

| Desks | Condos | inTowerWanted | Outside (min) |
|-------|--------|---------------|---------------|
| 10 | 20 | floor(min(0.5, 0.5)*20)=10 | 10 (50%) |
| 40 | 20 | floor(min(0.5, 2)*20)=10 | 10 (50%) |
| 5 | 20 | floor(min(0.5, 0.25)*20)=5 | 15 (75%) |
| 0 | 20 | 0 | 20 (100%) |

### 3.2 Daily assignment

Recompute when useful (recommended: **each morning planning pass** before the first condo leave, and when SyncHomes changes condo/office stock):

1. Build list of moved-in condos (stable order: `InstanceId` / agent `Id`).
2. Build list of vacant desk slots: each office room contributes `maxOccupants - CountHomeOccupants(office)` seats (office workers already home-assigned still count as occupying their home office — see §3.3).
3. Assign up to `inTowerWanted` condos to vacant seats (**condo claim first**).
4. All other moved-in condos → **OutsideJob**.
5. Office workers only begin Outside→office commute if their home office still has a free seat after condo claims **or** they are the room’s home occupant and not displaced (they are never displaced; condo only takes seats that are empty of *any* home agent — see clarification below).

### 3.3 Desk model clarification (normative)

Today each office room syncs `maxOccupants` **OfficeWorker** agents with `HomeRoom = that office`. Those agents “own” the desks.

For condo in-tower work without displacement:

- A desk is **vacant for condo claim** only if the office has **unfilled home slots** (`existing HomeRoom agents < maxOccupants`), **or** we introduce an explicit `CondoDeskClaim` that uses spare capacity beyond synced office workers.

**Preferred implementation (locked):** Keep SyncHomes office population as today. Condo in-tower workers claim **soft seats** tracked as `CondoWorkplace` / `WorkplaceRoom` on the condo agent, counting toward a room’s **concurrent workplace occupancy**:

```
workplaceOccupancy(office) = Count office HomeRoom agents currently Working/At desk phases
                           + Count condo agents with WorkplaceRoom == office and present/working
cap = maxOccupants
```

Condo may claim only if `workplaceOccupancy < cap` at claim time **and** total condo in-tower assignments ≤ `inTowerWanted`.

Office workers still always have a home office; if the room is “full” of condo daytime workers, office workers still commute in — **soft conflict**: allow both until we enforce hard cap on *daytime* presence.

**Hard-cap (locked for this slice):** Enforce daytime presence ≤ `maxOccupants`:

- Condos claim first up to `inTowerWanted` and ≤ free seats where  
  `free = maxOccupants - Count(OfficeWorker home agents for room)`  
  i.e. only **truly empty home slots** (rooms built with spare `maxOccupants`, or after SyncHomes under-fill).  
- If offices are always synced full (`existing == maxOccupants`), **in-tower condo jobs never get desks**.

That would make A/B meaningless for normal towers. Therefore SyncHomes / desk policy must change:

**Locked SyncHomes adjustment:** Offices still define `maxOccupants` as desk capacity. OfficeWorker spawn count becomes:

```
officeWorkersWanted = max(0, maxOccupants - reservedCondoDeskSlots)
```

where `reservedCondoDeskSlots` is computed tower-wide from `inTowerWanted` distributed across offices (or per-office reservation). Simpler tower-wide approach:

1. Compute `inTowerWanted` from full `Σ maxOccupants` and condo count.  
2. Reserve that many desks: when syncing offices, leave **global** `inTowerWanted` home slots unfilled by OfficeWorkers (distributed across offices, lowest InstanceId first).  
3. Condos claim those reserved empty home slots as `WorkplaceRoom` (not changing Condo `HomeRoom`).  
4. Office workers only fill non-reserved seats.

This preserves “no displacement,” gives condos priority on the reserved pool, and keeps outside ≥50%.

## 4. Condo schedules

### 4.1 Shared work length

- Base **8h** (`WorkMinutes = 8 * 60`), rare overtime optional (mirror office ~8% chance +30–120 min) — optional; default **exactly 8h** if simpler.
- Personal morning offset analogous to office `ArrivalMinute` style, but measured as **leave-home minute**.

### 4.2 Outside job

Fields (suggested): `CondoJobKind` { None, InTower, Outside }, `WorkplaceRoom`, `CommuteOneWayMinutes`, `LeaveHomeMinute`, `WorkMinutes`.

Flow:

1. At `LeaveHomeMinute`, if AtHome: trip condo → lobby → **Outside**.  
2. On reaching Outside: start dwell = `CommuteOneWayMinutes` (travel already represented by trip; **additional** Outside dwell = commute remainder? — see timing note).  
3. **Timing note (locked):** Door-to-door:  
   - Trip home→lobby time is real path time.  
   - Then Outside dwell `CommuteOneWayMinutes` (external travel).  
   - Then work dwell `WorkMinutes`.  
   - Then Outside dwell `CommuteOneWayMinutes` (return commute).  
   - Then trip lobby→home.  
4. `CommuteOneWayMinutes`: roll **15–60**, bias toward 30 (e.g. triangular distribution mode 30, or quadratic ease toward 30).  
5. Lunch: no tower commercial trip while Outside.

### 4.3 In-tower job

1. At leave time: trip condo → `WorkplaceRoom` entry; phase Working.  
2. Accrue `WorkedMinutes` like office.  
3. Lunch window 11:30–13:30: with probability ~office behavior, attempt commercial trip (some skip = desk lunch). Tunable: e.g. **60%** attempt lunch if shops open.  
4. After `WorkMinutes`, trip workplace → condo AtHome.

### 4.4 Commercial evenings

Keep existing condo evening shop window only when AtHome and not mid-workday commute.

## 5. Stair capacity (fuller)

### 5.1 Unit of capacity

- Each **stairs room instance** (or each vertical stair column segment spanning two floors) has `StairConcurrentCap` (suggested start **5** agents).  
- Upbound and downbound agents **share** the same cap (two-way bottleneck).

### 5.2 Enforcement

- Before an agent steps onto a stair cell / begins a stair leg, if occupancy ≥ cap → **wait** (stay on approach cell / landing).  
- While waiting for stairs: add stress per game minute (tunable; stack with elevator wait stress philosophy).  
- When a slot frees, highest-priority waiter (FIFO per approach side) enters.  
- Elevator routing unchanged except agents may choose stairs less when stair waits are bad (optional v1: no rescore; capacity alone creates delay). **v1 locked:** capacity + wait only; no new cost term in router unless cheap.

### 5.3 Visibility

- Optional: Selection on stairs shows `Occupancy/Cap`. Not required for MVP if time-boxed.

## 6. Stress & stars

- Stair waits increase agent stress → feeds average stress / star gates.  
- Outside dwell does not add tower floor crime stress (agent Outside).  
- In-tower condo workers count toward population as today (moved-in).

## 7. HUD / debug (light)

- Economy or Selection (optional): `Condo jobs: N in-tower / M outside`.  
- Not blocking for slice success if omitted; prefer a single Economy line.

## 8. Files (expected)

| Area | Responsibility |
|------|----------------|
| `CondoEmployment.cs` (new) | Mix math, desk reservation, commute minute roll |
| `StairCapacity.cs` (new) | Cap, occupy/release, wait eligibility |
| `Agent` / `AgentSystem` | Condo job fields; UpdateCondo workday; office sync reservation; stair enter checks in movement |
| `StairsPathfinder` / movement step | Hook occupy/release on stair cells |
| Tests | Mix formula; reservation; outside timing; stair cap blocks 6th agent |

## 9. Test plan

### EditMode

1. Mix table cases in §3.1.  
2. With 10 desks / 20 condos → 10 reserved seats unfilled by office sync; 10 condos InTower max.  
3. Vacant seat claim; no claim when none free → Outside.  
4. Commute roll in [15,60], mean near 30 over many samples.  
5. Stair: 5 agents enter, 6th waits; release allows enter.  
6. Office lunch still works; condo in-tower lunch optional path.

### Play Mode

1. Condo-heavy tower: morning lobby exit wave; evening return wave.  
2. Office-heavy: more in-tower condo→office trips (up to 50%).  
3. Narrow stairs at rush: visible pile-up / slower vertical move; stress creeps.  
4. Hotels/events still function.

## 10. Non-goals

- Sky lobbies / express elevators  
- Condo association dues / demands  
- Changing condo sale price  
- Visual stair congestion meshes beyond agent blocking  
- Parallel-cli / paid tooling  

## 11. Open tunables (implementation may adjust)

| Tunable | Start |
|---------|-------|
| Stair concurrent cap | 5 |
| Stair wait stress / min | ~0.05–0.15 (tune in play) |
| In-tower lunch attempt rate | 0.6 |
| Outside overtime | off or rare |
| Commute distribution | triangular 15–60 mode 30 |

## 12. Spec self-review

- [x] No unresolved placeholders for locked decisions  
- [x] Desk reservation called out so full SyncHomes offices don’t zero in-tower jobs  
- [x] Stairs and commute both in scope per user choice C  
- [x] Non-goals separate sky lobby work  
- [ ] User review of this file before implementation plan
