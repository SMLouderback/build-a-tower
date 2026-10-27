# Crime Pressure & Security Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Per-floor crime rises with shop/hotel congestion; staffed Security Posts + patrol agents suppress it; Criminal visitors cause local stress until captured; crime raises stress for non-staff agents; HUD shows average crime.

**Architecture:** Pure `CrimeSystem` owns 0–100 per-floor scores (raise/decay/suppress). `TowerSimulation` ticks it each frame with grid + agent floor occupancy. `AgentSystem` syncs `AgentRole.Security` from Security Posts (like maids), patrols hot floors, spawns/roams/captures `AgentRole.Criminal`, and applies crime/criminal stress. `EconomySystem` pays security wages. HUD shows avg crime; `AgentView` colors Security/Criminal.

**Tech Stack:** Unity 6000.x, C#, NUnit EditMode, existing AgentSystem / EconomySystem / BuildController / TowerHudController

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-01-crime-security-design.md`
- Crime **per floor**, clamp **0–100**
- Sources this slice: **shops** (`IncomeModel.TrafficVariable`) + **hotel occupancy** only
- Security: hybrid baseline (staffed post) + patrol (±1 floor bleed at 50%)
- Capture: Security and Criminal on **same floor**
- Stress: all non-staff agents on a floor; **exempt** Security, Maid, Handyman, Criminal
- Staff: Security Post **0–4**, auto-hire **1**, wage **$250/day**
- Concurrent Criminals cap **3**
- Research Lab / entertainment / heatmaps: **out of scope**
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/CrimeSystem.cs` | Per-floor crime; raise/decay/suppress; avg |
| `Assets/Scripts/Economy/CrimeFloorLoads.cs` | Pure helpers: shop/hotel load per floor from grid+agents |
| `Assets/Scripts/Agents/AgentEnums.cs` | `Security`, `Criminal` roles |
| `Assets/Scripts/Agents/AgentSystem.cs` | Staff sync, patrol, criminal spawn/roam/capture, stress |
| `Assets/Scripts/Agents/Agent.cs` | Optional: `CrimeStressDay`, criminal dwell fields if needed |
| `Assets/Scripts/Agents/AgentView.cs` | Distinct colors |
| `Assets/Scripts/Economy/EconomySystem.cs` | Security wage |
| `Assets/Scripts/Build/BuildController.cs` | Staffed Security + auto-hire |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Own/tick `CrimeSystem`; expose property |
| `Assets/Scripts/UI/TowerHudController.cs` | Crime chip; patrol count on Selection |
| `Assets/Tests/EditMode/CrimeSystemTests.cs` | Math + loads |
| `Assets/Tests/EditMode/SecurityCrimeIntegrationTests.cs` | Staff wage, capture, stress (as needed) |
| `README.md` | Short play note if project habit requires |

---

### Task 1: CrimeSystem + floor load helpers

**Files:**
- Create: `Assets/Scripts/Economy/CrimeSystem.cs`
- Create: `Assets/Scripts/Economy/CrimeFloorLoads.cs`
- Test: `Assets/Tests/EditMode/CrimeSystemTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class CrimeSystem
{
    public const float MaxCrime = 100f;
    public const float ShopRaisePerVisitorPerMinute = 0.8f;
    public const float HotelRaisePerGuestPerMinute = 0.35f;
    public const float NaturalDecayPerMinute = 0.05f;
    public const float BaselineDecayPerStaffPerMinute = 0.08f;
    public const float PatrolDecayPerMinute = 1.2f;
    public const float PatrolAdjacentFactor = 0.5f;
    public const float CriminalRaisePerMinute = 2.5f;
    public const float CaptureCrimeDrop = 10f;

    public float GetCrime(int floor);
    public void SetCrime(int floor, float value); // tests
    public float AverageCrime { get; }
    public void Tick(
        float deltaGameMinutes,
        IReadOnlyDictionary<int, float> shopLoadByFloor,
        IReadOnlyDictionary<int, float> hotelLoadByFloor,
        int totalStaffedSecurityWorkers,
        IReadOnlyList<int> patrolFloors,
        IReadOnlyList<int> criminalFloors);
    public void ApplyCaptureDrop(int floor);
}

public static class CrimeFloorLoads
{
    public static Dictionary<int, float> ShopLoadByFloor(TowerGrid grid);
    // ConcurrentVisitors summed for TrafficVariable rooms on each floor (Origin.y .. Origin.y+Size.y-1)
    public static Dictionary<int, float> HotelLoadByFloor(TowerGrid grid, IReadOnlyList<Agent> agents);
    // Count HotelGuest agents whose Cell.y (or HomeRoom floor if AtHome) equals floor
}
```

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void Shop_load_raises_crime_on_that_floor()
{
    var crime = new CrimeSystem();
    var shop = new Dictionary<int, float> { [3] = 2f };
    var hotel = new Dictionary<int, float>();
    crime.Tick(10f, shop, hotel, totalStaffedSecurityWorkers: 0,
        patrolFloors: Array.Empty<int>(), criminalFloors: Array.Empty<int>());
    Assert.Greater(crime.GetCrime(3), 0f);
    Assert.AreEqual(0f, crime.GetCrime(2));
}

[Test]
public void Staffed_baseline_decays_all_floors_with_crime()
{
    var crime = new CrimeSystem();
    crime.SetCrime(1, 50f);
    crime.SetCrime(5, 50f);
    crime.Tick(10f,
        new Dictionary<int, float>(),
        new Dictionary<int, float>(),
        totalStaffedSecurityWorkers: 2,
        Array.Empty<int>(), Array.Empty<int>());
    Assert.Less(crime.GetCrime(1), 50f);
    Assert.Less(crime.GetCrime(5), 50f);
}

[Test]
public void Patrol_decays_local_floor_faster_than_baseline_alone()
{
    var withPatrol = new CrimeSystem();
    var baselineOnly = new CrimeSystem();
    withPatrol.SetCrime(4, 80f);
    baselineOnly.SetCrime(4, 80f);
    var empty = new Dictionary<int, float>();
    withPatrol.Tick(5f, empty, empty, 1, new[] { 4 }, Array.Empty<int>());
    baselineOnly.Tick(5f, empty, empty, 1, Array.Empty<int>(), Array.Empty<int>());
    Assert.Less(withPatrol.GetCrime(4), baselineOnly.GetCrime(4));
}

[Test]
public void Criminal_raises_floor_crime_and_capture_drops()
{
    var crime = new CrimeSystem();
    crime.Tick(4f, new Dictionary<int, float>(), new Dictionary<int, float>(), 0,
        Array.Empty<int>(), new[] { 2 });
    Assert.Greater(crime.GetCrime(2), 0f);
    var before = crime.GetCrime(2);
    crime.ApplyCaptureDrop(2);
    Assert.AreEqual(Mathf.Max(0f, before - CrimeSystem.CaptureCrimeDrop), crime.GetCrime(2));
}

[Test]
public void Crime_clamps_to_0_100()
{
    var crime = new CrimeSystem();
    crime.SetCrime(0, 200f);
    Assert.AreEqual(100f, crime.GetCrime(0));
    crime.SetCrime(0, -5f);
    Assert.AreEqual(0f, crime.GetCrime(0));
}
```

Also add a small `CrimeFloorLoads` test: place a TrafficVariable shop with `TryEnterVisitor` / set concurrent, assert `ShopLoadByFloor` counts that floor.

- [ ] **Step 2: Run tests — expect FAIL** (types missing)

- [ ] **Step 3: Implement `CrimeSystem` + `CrimeFloorLoads`**

```csharp
// CrimeSystem.Tick sketch:
foreach (var kv in shopLoadByFloor)
    Add(kv.Key, ShopRaisePerVisitorPerMinute * kv.Value * dt);
foreach (var kv in hotelLoadByFloor)
    Add(kv.Key, HotelRaisePerGuestPerMinute * kv.Value * dt);
foreach (var f in criminalFloors)
    Add(f, CriminalRaisePerMinute * dt);

// Natural decay on all tracked floors
// Baseline: totalStaffedSecurityWorkers * BaselineDecayPerStaffPerMinute * dt on each tracked floor
// Patrol: for each floor in patrolFloors, subtract PatrolDecayPerMinute * dt;
//         also f±1 at PatrolAdjacentFactor
```

Use a `Dictionary<int, float> _crime`. `GetCrime` returns 0 if missing. `AverageCrime` = mean of values (0 if empty).

`ShopLoadByFloor`: for each room with `ShopVisitRules.IsShop(type)`, for each floor the room occupies, add `room.ConcurrentVisitors`.

`HotelLoadByFloor`: for each `AgentRole.HotelGuest` that is not Outside, add 1 to `agent.Cell.y`.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Economy/CrimeSystem.cs Assets/Scripts/Economy/CrimeFloorLoads.cs Assets/Tests/EditMode/CrimeSystemTests.cs
git commit -m "$(cat <<'EOF'
feat: add per-floor crime pressure system

EOF
)"
```

---

### Task 2: Staff Security Post (auto-hire, wages, Selection)

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs` (`ApplyAutoHireOnPlace`, `IsStaffedServiceRoom`)
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` (`SecurityGuardWagePerDay = 250`, `WageForRoom`)
- Test: extend `Assets/Tests/EditMode/EconomySystemTests.cs` (or new `SecurityStaffTests.cs`)

**Interfaces:**
- Consumes: existing `SetStaffedWorkers`, Selection stepper (already gates on `IsStaffedServiceRoom`)
- Produces: Security id `service_security` treated as staffed; wage `StaffedWorkers * 250`

- [ ] **Step 1: Failing test**

```csharp
[Test]
public void Midnight_charges_security_wages()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
    var security = ScriptableObject.CreateInstance<RoomTypeSO>();
    security.id = "service_security";
    security.size = new Vector2Int(2, 1);
    security.allowAboveGround = true;
    Assert.IsTrue(grid.TryPlace(security, new Vector2Int(0, 1), out var room));
    room.SetStaffedWorkers(2);
    var wallet = new FundsWallet(50_000);
    var economy = new EconomySystem();
    economy.OnNewDay(grid, new List<Agent>(), wallet);
    Assert.AreEqual(50_000 - 2 * EconomySystem.SecurityGuardWagePerDay - /* elevator if any */ 0,
        wallet.Balance);
    Assert.AreEqual(2 * EconomySystem.SecurityGuardWagePerDay, economy.LastWageExpense);
}

[Test]
public void Security_is_staffed_service_and_auto_hires()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.id = "service_security";
    Assert.IsTrue(BuildController.IsStaffedServiceRoom(so));
    var room = new RoomInstance(1, so, Vector2Int.zero, new Vector2Int(2, 1));
    BuildController.ApplyAutoHireOnPlace(room);
    Assert.AreEqual(1, room.StaffedWorkers);
}
```

(Adjust wallet assert if lobby/elevator upkeep also fires — place no elevator.)

- [ ] **Step 2: Implement**

```csharp
// BuildController
public static void ApplyAutoHireOnPlace(RoomInstance room)
{
    if (room?.Type == null) return;
    if (room.Type.id is "service_housekeeping" or "service_maintenance" or "service_security")
        room.SetStaffedWorkers(1);
}

public static bool IsStaffedServiceRoom(RoomTypeSO type) =>
    type != null && type.id is "service_housekeeping" or "service_maintenance" or "service_security";

// EconomySystem
public const int SecurityGuardWagePerDay = 250;
const string SecurityId = "service_security";
// WageForRoom switch add: SecurityId => room.StaffedWorkers * SecurityGuardWagePerDay
```

- [ ] **Step 3: Tests PASS + Commit** `feat: staff security posts with daily wages`

---

### Task 3: Wire CrimeSystem into TowerSimulation

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` — add helpers to list patrol/criminal floors (or compute in simulation)

**Interfaces:**
- Produces: `public CrimeSystem Crime => _crime;`
- Each `Update` after agents tick (or before stress — order: elevators → agents → **crime tick using post-move positions** → star promote):

```csharp
_crime.Tick(
    _clock.LastTickGameMinutes,
    CrimeFloorLoads.ShopLoadByFloor(build.Grid),
    CrimeFloorLoads.HotelLoadByFloor(build.Grid, _agents.Agents),
    CountStaffedSecurity(build.Grid),
    CollectFloors(_agents.Agents, AgentRole.Security),
    CollectFloors(_agents.Agents, AgentRole.Criminal));
```

`CountStaffedSecurity`: sum `StaffedWorkers` on non-broken `service_security` rooms.

Until Security/Criminal roles exist (Task 4–5), patrol/criminal lists are empty — crime still rises from shops/hotels and decays with staffed posts (baseline uses staff count, not agent presence).

- [ ] **Step 1: Implement wiring** (no new test required if Task 1 covers math; optional smoke: simulation constructs Crime)

- [ ] **Step 2: Commit** `feat: tick CrimeSystem from TowerSimulation`

---

### Task 4: Security agents — sync + patrol

**Files:**
- Modify: `Assets/Scripts/Agents/AgentEnums.cs` — add `Security`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` — extend `SyncServiceStaff`, service role checks, patrol AI
- Modify: `Assets/Scripts/Agents/AgentView.cs` — blue-tinted Security color
- Test: `Assets/Tests/EditMode/SecurityPatrolTests.cs` (or extend existing agent tests)

**Interfaces:**
- Consumes: `CrimeSystem` passed into `AgentSystem.Tick` **or** `Tick` gains optional `CrimeSystem crime` parameter
- Prefer: add `CrimeSystem crime` parameter to `AgentSystem.Tick` (update `TowerSimulation` call site)
- Produces: Security agents synced from `service_security` like maids; `IsServiceRole` includes Security; `IsNonPopulationRole` / `IsEphemeralOrStaffRole` include Security

**Patrol AI (minimal):**
- When Security is `AtHome` or finished a dwell: call `PickPatrolFloor(crime)` → highest `GetCrime` among floors that have shop or hotel load > 0; else highest crime floor; else stay home.
- Path to a walkable cell on that floor (lobby x or room origin on floor) via existing `BeginTrip` / router patterns used by maids.
- On arrival: `ServiceWorkRemaining = PatrolDwellMinutes` (e.g. 8); phase Working/Staying; then replan.
- **Do not** replan every tick while `WaitingAtElevator` or `Riding` (same discipline as maid/handyman fix).

```csharp
const string SecurityId = "service_security";
// SyncServiceStaff: also handle SecurityId → AgentRole.Security
static bool IsServiceRole(AgentRole role) =>
    role is AgentRole.Maid or AgentRole.Handyman or AgentRole.Security;
```

- [ ] **Step 1: Failing test** — place Security Post staffed 1; after `SyncHomes`/`SyncServiceStaff` path, assert one `AgentRole.Security` with that home. (Call the same public entry `SyncHomes` uses, or extract/test via sync if already public through SyncHomes.)

If `SyncServiceStaff` is private, assert via `SyncHomes` after placing the post with staff 1 (may need lobby). Mirror existing maid sync tests if present.

- [ ] **Step 2: Implement enum + sync + patrol + color**

```csharp
// AgentView
AgentRole.Security => new Color(0.25f, 0.4f, 0.95f, 1f),
```

- [ ] **Step 3: Tests PASS + Commit** `feat: security guards patrol high-crime floors`

---

### Task 5: Criminal agents — spawn, roam, capture

**Files:**
- Modify: `Assets/Scripts/Agents/AgentEnums.cs` — add `Criminal`
- Modify: `Assets/Scripts/Agents/Agent.cs` — `float CriminalDwellRemaining` (or reuse a field)
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` — spawn/roam/capture
- Modify: `Assets/Scripts/Agents/AgentView.cs` — red/dark Criminal color
- Test: `Assets/Tests/EditMode/CriminalCaptureTests.cs`

**Interfaces:**
- Produces:
  - Max concurrent Criminals = **3**
  - Spawn: when lobby exists and `crime.AverageCrime` ≥ `CriminalSpawnMinAvg` (e.g. 15) and count < 3, roll chance `CriminalSpawnChancePerMinute * dt * (AverageCrime/100)`
  - Enter at lobby cell like street visitors (`Outside` → trip into tower)
  - Roam: every dwell, pick high-crime shop/hotel floor; set `CriminalDwellRemaining` total life (e.g. **180** game minutes) then leave/despawn
  - Capture: each tick, for each Security, if any Criminal has `Cell.y == security.Cell.y` (and both not Outside), remove Criminal and `crime.ApplyCaptureDrop(floor)`; set `LastCaptureMessage` optional string on AgentSystem or CrimeSystem

```csharp
public string LastCaptureMessage { get; private set; }

void TryCaptureCriminals(CrimeSystem crime)
{
    foreach (var guard in _agents)
    {
        if (guard.Role != AgentRole.Security) continue;
        if (guard.Phase == AgentPhase.Outside) continue;
        for (var i = _agents.Count - 1; i >= 0; i--)
        {
            var c = _agents[i];
            if (c.Role != AgentRole.Criminal) continue;
            if (c.Cell.y != guard.Cell.y) continue;
            crime.ApplyCaptureDrop(c.Cell.y);
            LastCaptureMessage = $"Security captured a criminal on floor {c.Cell.y}.";
            _agents.RemoveAt(i);
            break; // one capture per guard per tick
        }
    }
}
```

Include Criminal in `IsNonPopulationRole` / `IsEphemeralOrStaffRole`. **Do not** include in `IsServiceRole`.

- [ ] **Step 1: Failing test** — create Security + Criminal on same floor in an AgentSystem (or thin helper); call capture; Criminal removed; crime dropped.

For EditMode without full pathfinding, test a package-private/public `TryCaptureCriminals` by constructing agents with roles/cells and a CrimeSystem — may need `internal` + `InternalsVisibleTo` **or** a small `CrimeCapture.TryCapture(...)` static helper used by AgentSystem (prefer **static helper** for testability):

```csharp
public static class CrimeCapture
{
    public static int TryCapture(
        IList<Agent> agents,
        CrimeSystem crime,
        out string message);
}
```

- [ ] **Step 2: Implement spawn/roam/capture + color**

```csharp
AgentRole.Criminal => new Color(0.75f, 0.1f, 0.15f, 1f),
```

- [ ] **Step 3: Tests PASS + Commit** `feat: criminal visitors and security capture`

---

### Task 6: Crime → stress

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (`UpdateStress` and/or daily pulse)
- Modify: `Assets/Scripts/Agents/Agent.cs` — `int CrimeStressDay` if using daily pulse
- Test: `Assets/Tests/EditMode/CrimeStressTests.cs`

**Interfaces:**
- Consumes: `CrimeSystem.GetCrime(floor)`
- Constants:
```csharp
public const float CrimeStressPerDayAtMax = 12f;      // daily pulse * (crime/100)
public const float CriminalProximityStressPerMinute = 0.4f;
```

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void High_floor_crime_adds_daily_stress_to_worker()
{
    // Arrange agent OfficeWorker on floor 2; crime.SetCrime(2, 100);
    // ApplyCrimeStressDaily(agent, crime, dayIndex: 1);
    // Assert stress ~= CrimeStressPerDayAtMax
}

[Test]
public void Security_exempt_from_crime_stress()
{
    // Security on floor 2 with crime 100 → stress unchanged
}

[Test]
public void Criminal_on_same_floor_adds_proximity_stress()
{
    // Worker + Criminal same floor; tick UpdateCrimeProximityStress 10 minutes → stress up
}
```

Extract:

```csharp
public static void ApplyCrimeStressDaily(Agent agent, CrimeSystem crime, int dayIndex)
{
    if (agent == null || crime == null) return;
    if (IsCrimeStressExempt(agent.Role)) return;
    if (agent.CrimeStressDay == dayIndex) return;
    var c = crime.GetCrime(agent.Cell.y);
    if (c <= 0f) return;
    agent.Stress = Mathf.Min(100f, agent.Stress + CrimeStressPerDayAtMax * (c / 100f));
    agent.CrimeStressDay = dayIndex;
}

static bool IsCrimeStressExempt(AgentRole role) =>
    role is AgentRole.Security or AgentRole.Maid or AgentRole.Handyman or AgentRole.Criminal;
```

Call `ApplyCrimeStressDaily` from the same place as `ApplyLowConditionStress`. In `UpdateStress` (or adjacent), add proximity stress when any Criminal shares `Cell.y`.

- [ ] **Step 2: Implement + PASS + Commit** `feat: floor crime and criminals raise agent stress`

---

### Task 7: HUD — avg crime + Security patrol line

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs` `GetSelectionSummary` optional patrol note (or HUD-only)

**Interfaces:**
- Consumes: `simulation.Crime.AverageCrime`
- Top bar chip after stars (or after stress): `Crime {avg:0}`
- When Selection is Security Post: show `Guards on patrol: N` where N = count of Security agents with that `HomeRoom` whose phase is not solely AtHome **or** simply `StaffedWorkers` (spec: “Guards on patrol: N” — use count of Security agents with that home that are not despawned; if all staff sync 1:1, `StaffedWorkers` is enough; prefer live count of Security agents with `HomeRoom == selected`)

```csharp
DrawChip($"Crime {simulation.Crime?.AverageCrime ?? 0f:0}", 72f);
```

Keep Goals overlay fixed-height behavior (do not return goals dropdown height).

- [ ] **Step 1: Implement HUD + Selection line**

- [ ] **Step 2: Play Mode smoke** (manual): busy shops raise Crime chip; hire Security; Criminal can appear/capture.

- [ ] **Step 3: Commit** `feat: show tower crime and security patrol on HUD`

- [ ] **Step 4: Update spec status** in `docs/superpowers/specs/2026-08-01-crime-security-design.md` to `Implemented` when Play Mode acceptance passes.

---

## Self-review (plan vs spec)

| Spec requirement | Task |
|------------------|------|
| Per-floor 0–100 crime | 1 |
| Shop + hotel raise | 1 |
| Natural decay | 1 |
| Baseline from staffed posts | 1 + 3 |
| Patrol local ±1 | 1 + 4 |
| Criminal raise + capture drop | 1 + 5 |
| Security staff 0–4, auto 1, $250 | 2 |
| Security agents patrol | 4 |
| Criminal spawn/roam/capture | 5 |
| Stress everyone except staff/criminal | 6 |
| HUD avg crime + Selection | 7 |
| Research / entertainment | Explicitly out of scope |

No TBD placeholders in tasks. Types: `CrimeSystem`, `CrimeFloorLoads`, `CrimeCapture`, roles `Security`/`Criminal`, wage constant name consistent across tasks.
