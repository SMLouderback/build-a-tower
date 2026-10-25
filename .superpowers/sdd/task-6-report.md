# Conference Task 6 Report: EventVisitor agents

## Status

DONE_WITH_CONCERNS

## Commit

- `ec324b7` — `feat: spawn event visitors for major events`

## Changes

### `Assets/Scripts/Agents/AgentEnums.cs`

- Added `AgentRole.EventVisitor`

### `Assets/Scripts/Agents/AgentSystem.cs`

- Constants: `MaxConcurrentEventVisitors=24`, `EventHotelBookFraction=0.25`, hall dwell 18–40 min
- `IsEphemeralOrStaffRole` / `IsNonPopulationRole` include EventVisitor (SyncHomes skip; no pop/stress)
- `ComputeEventVisitorSpawnPerDay(bookedCapacity)` → `min(24, capacity/5)`
- `SyncEventVisitors(conference, grid, clock)`: while Live spawn once/day up to quota; otherwise force-despawn (hotel Dirty if checked in/staying)
- Day crowd: Outside → Event Hall (`Working` dwell) → optional commercial → Outside → despawn
- Hotel fraction (~25%): claim vacant clean bed or displace Outside `HotelGuest`; reuse `UpdateHotel` for event nights
- Tick: `UpdateEventHallDwell` + `DespawnFinishedEventVisitors`

### `Assets/Scripts/Economy/ConferenceSystem.cs`

- `SumBookedHallCapacity`, `FindBookedHall` for spawn targeting

### `Assets/Scripts/Simulation/TowerSimulation.cs`

- After `Conference.TickDay`, calls `_agents.SyncEventVisitors(...)`

### Other

- `AgentView`: magenta EventVisitor color
- `AgentWealth`: EventVisitor → Street band
- `CrimeFloorLoads.HotelLoadByFloor`: counts hotel-backed EventVisitors

### Tests

- `Assets/Tests/EditMode/EventVisitorTests.cs` (spawn cap helper, ephemeral SyncHomes, Live spawn/despawn, hotel claim)
- `AgentWealthTests.ResolveBand_event_visitor_is_street`

## Verification

- **Unity EditMode batch:** not run (Editor lock risk).
- **Roslyn:** `runtime.rsp` + `event-visitor-tests.rsp` — exit 0
- **net8 EventVisitorTestHost:** 6/6 PASS (FormatterServices grid; reflection seed for cap/ephemeral)
- **net8 ConferenceTestHost:** 16/16 PASS (no reg)

### Output

```
total=6 passed=6 failed=0
ev exit: 0
total=16 passed=16 failed=0
conf exit: 0
runtime: 0
evtests: 0
```

## Test summary

| Suite | Tests | net8 run | Roslyn compile | Unity EditMode |
|-------|-------|----------|----------------|----------------|
| EventVisitorTestHost | 6 | **PASS** | n/a | n/a |
| EventVisitorTests | 6 | n/a | **PASS** | not run |
| ConferenceSystemTests | 16 | **PASS** | prior | not run |

## Concerns

- Hotel beds are usually filled by SyncHomes; hotel fraction often displaces Outside `HotelGuest` rather than finding empty slots.
- Day-crowd path/AI is MVP (hall dwell as `Working`, optional one shop); not full itinerary AI.
- Unity EditMode runner not executed; spawn/path behavior verified via Roslyn + net8 host helpers.
- Overwrote prior unrelated elevator `task-6-report.md` with this conference report.
