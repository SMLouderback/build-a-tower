# Condo Commute & Stair Capacity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Condo residents commute to in-tower office desks or Outside jobs (≥50% outside), and stair flights enforce shared two-way capacity with wait stress.

**Architecture:** Pure helpers `CondoEmployment` (mix + commute rolls + desk reservation plan) and `StairCapacity` (per-stairs-room concurrent cap). `AgentSystem` owns daily job assignment, SyncHomes office under-fill for reserved desks, condo workday FSM, and stair enter/leave hooks during movement. No displacement of office home agents.

**Tech Stack:** Unity C#, NUnit EditMode tests, existing `AgentSystem` / `TransitRouter` / `StairsPathfinder`.

**Spec:** `docs/superpowers/specs/2026-08-03-condo-commute-stair-capacity-design.md`

## Global Constraints

- Mix: `inTowerWanted = floor(min(0.5, officeDesks / max(1, condoResidents)) * condoResidents)`
- Condos claim reserved vacant desks only; never displace OfficeWorkers
- Outside commute one-way 15–60 min, bias ~30; work 8h; no tower lunch while Outside
- In-tower condo: office-like day; lunch attempt ~60% in 11:30–13:30
- Stairs: shared up/down cap (start 5); wait when full; stress while waiting
- Do not commit unless the user explicitly asked for commits in this conversation
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/CondoEmployment.cs` | Mix math, commute roll, reservation distribution |
| `Assets/Scripts/Transit/StairCapacity.cs` | Cap occupy/release/wait |
| `Assets/Scripts/Agents/AgentEnums.cs` or `Agent.cs` | `CondoJobKind`, workplace + commute fields |
| `Assets/Scripts/Agents/AgentSystem.cs` | SyncHomes reservation, AssignCondoJobs, UpdateCondo workday, stair hooks |
| `Assets/Scripts/UI/TowerHudController.cs` | Optional Economy line for condo jobs |
| `Assets/Tests/EditMode/CondoEmploymentTests.cs` | Mix + commute distribution |
| `Assets/Tests/EditMode/CondoCommuteTests.cs` | Assignment + schedule smoke |
| `Assets/Tests/EditMode/StairCapacityTests.cs` | Cap / wait |
| Spec + README | Status / play notes |

---

### Task 1: CondoEmployment pure math

**Files:**
- Create: `Assets/Scripts/Economy/CondoEmployment.cs`
- Test: `Assets/Tests/EditMode/CondoEmploymentTests.cs`

**Interfaces:**
- Produces: `CondoEmployment.InTowerWanted(int officeDesks, int condoResidents) -> int`
- Produces: `CondoEmployment.RollCommuteOneWayMinutes(Random rng) -> int` in [15,60] biased ~30
- Produces: `CondoEmployment.DistributeReservedDesks(IReadOnlyList<RoomInstance> offices, int reserveCount) -> Dictionary<int,int>` room InstanceId → reserved empty slots

- [ ] **Step 1: Write failing tests** for table cases (10/20→10, 40/20→10, 5/20→5, 0/20→0), commute bounds + mean in [25,35] over 500 samples, distribute reserves totaling `reserveCount` without exceeding per-office free capacity.

- [ ] **Step 2: Implement `CondoEmployment`** with triangular/quadratic commute bias (mode 30).

- [ ] **Step 3: Run EditMode / Roslyn host until green.**

---

### Task 2: Agent condo job fields

**Files:**
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Agents/AgentEnums.cs` (add `CondoJobKind` if enums live there; else nest in Agent.cs)
- Test: extend `CondoEmploymentTests` or tiny `AgentField` assert in `CondoCommuteTests.cs`

**Interfaces:**
- Produces on `Agent`: `CondoJobKind JobKind`, `RoomInstance WorkplaceRoom`, `int CommuteOneWayMinutes`, `int LeaveHomeMinute`, `float OutsideDwellRemaining`, `enum OutsidePhase` { None, ToWorkCommute, Working, ReturnCommute } or equivalent flags

- [ ] **Step 1: Add fields + defaults** (`JobKind=None`, dwell 0, workplace null).

- [ ] **Step 2: Smoke test that new Agent constructs cleanly.**

---

### Task 3: SyncHomes reserves desks for condos

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (`SyncHomes` office fill loop)
- Test: `Assets/Tests/EditMode/CondoCommuteTests.cs`

**Interfaces:**
- Consumes: `CondoEmployment.InTowerWanted`, `DistributeReservedDesks`
- Produces: fewer OfficeWorkers than `maxOccupants` by reserved slots when moved-in condos exist

- [ ] **Step 1: Failing test** — office `maxOccupants=2`, one moved-in condo, desks total 2, residents 1 → `inTowerWanted=0` (min 0.5*1=0)? Wait: 2 desks 1 condo → floor(min(0.5,2)*1)=0. Use 10 desks / 4 condos moved-in → inTowerWanted=2; sync leaves 2 seats empty across offices.

- [ ] **Step 2: Before filling offices, compute reservation from current moved-in count + Σ maxOccupants; set per-room fill cap = maxOccupants - reserved[room].**

- [ ] **Step 3: Green tests.**

---

### Task 4: Daily AssignCondoJobs

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: `Assets/Tests/EditMode/CondoCommuteTests.cs`

**Interfaces:**
- Produces: `AssignCondoJobs(TowerGrid grid, GameClock clock)` called from Tick once per day (e.g. minute &lt; 5:00 reset + assign, or first Tick after midnight)
- Sets JobKind InTower + WorkplaceRoom for up to inTowerWanted using empty home slots; else Outside + commute roll + LeaveHomeMinute

- [ ] **Step 1: Tests** — N in-tower ≤ inTowerWanted; workplaces point at offices with spare slots; outside get commute in range.

- [ ] **Step 2: Implement assignment** stable by agent Id; clear workplace when Outside.

- [ ] **Step 3: Green tests.**

---

### Task 5: UpdateCondo workday (Outside + InTower)

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (`UpdateCondo`)
- Test: `Assets/Tests/EditMode/CondoCommuteTests.cs`

**Interfaces:**
- Outside: AtHome @ LeaveHome → trip to lobby Outside; on Outside run dwell phases (commute, work 8h, commute); then trip lobby→home
- InTower: leave home → WorkplaceRoom Working; WorkedMinutes; lunch 60% in window; then home
- Do not evening-shop while mid-work Outside/InTower shift

- [ ] **Step 1: Tests** — outside agent reaches Outside and after forced dwell returns; in-tower begins trip toward workplace.

- [ ] **Step 2: Implement FSM in `UpdateCondo`** (keep move-in path).

- [ ] **Step 3: Green tests.**

---

### Task 6: StairCapacity helper

**Files:**
- Create: `Assets/Scripts/Transit/StairCapacity.cs`
- Test: `Assets/Tests/EditMode/StairCapacityTests.cs`

**Interfaces:**
- `const int DefaultCap = 5`
- `bool TryEnter(int stairsRoomId, int agentId)`
- `void Leave(int stairsRoomId, int agentId)`
- `int Occupancy(int stairsRoomId)`
- Cap shared for all directions

- [ ] **Step 1: Failing tests** — 5 enter OK, 6th false; leave then enter OK.

- [ ] **Step 2: Implement dictionary of HashSet agent ids per stairs instance.**

- [ ] **Step 3: Green tests.**

---

### Task 7: Wire stair capacity into movement

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (StepMovement / stair cell transitions)
- Modify: map stairs cell → `RoomInstance` id (via grid)
- Test: `Assets/Tests/EditMode/StairCapacityTests.cs` integration or agent smoke

**Interfaces:**
- Before stepping onto stairs cell: `TryEnter`; if fail, stay put, add stair-wait stress (~0.1/min)
- On leaving stairs cells: `Leave`
- Rebuild/clear capacity on grid rebuild if needed

- [ ] **Step 1: Test** multi-agent blocked on full flight (minimal grid with stairs).

- [ ] **Step 2: Hook enter/leave + stress.**

- [ ] **Step 3: Green tests; Play Mode note in report.**

---

### Task 8: HUD line + docs

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (Economy section)
- Modify: `docs/superpowers/specs/2026-08-03-condo-commute-stair-capacity-design.md` status → Implemented
- Modify: `README.md` play bullet for condo commute + stairs cap

- [ ] **Step 1: Economy line** `Condo jobs: {inTower} in-tower / {outside} outside`

- [ ] **Step 2: Update spec status + README.**

- [ ] **Step 3: Self-check Play Mode checklist from spec §9.**

---

## Spec coverage

| Spec section | Task |
|--------------|------|
| §3 mix formula | 1 |
| §3.3 desk reservation SyncHomes | 3 |
| §3.2 daily assign | 4 |
| §4 schedules | 5 |
| §5 stair capacity | 6–7 |
| §7 HUD | 8 |
| §9 tests | 1–7 |

## Plan self-review

- No TBD placeholders in task contracts
- Commit steps omitted (user commits on request only)
- Types consistent: `InTowerWanted`, `CondoJobKind`, `StairCapacity.TryEnter/Leave`
