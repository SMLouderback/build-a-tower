# Crime Task 5 Report: Criminal agents — spawn, roam, capture

## Status

DONE

## Commit

- `bbe0291` — `feat: criminal visitors and security capture`

## Changes

### `Assets/Scripts/Agents/AgentEnums.cs`

- Added `AgentRole.Criminal`

### `Assets/Scripts/Agents/Agent.cs`

- Added `CriminalDwellRemaining` (total life before leave/despawn)

### `Assets/Scripts/Agents/CrimeCapture.cs`

- Static `CrimeCapture.TryCapture(IList<Agent>, CrimeSystem, out string message)`
- One Criminal per Security per tick; same floor; both not Outside; applies `ApplyCaptureDrop`

### `Assets/Scripts/Agents/AgentSystem.cs`

- Constants: `MaxConcurrentCriminals=3`, `CriminalSpawnMinAvg=15`, `CriminalSpawnChancePerMinute=0.08`, `CriminalLifeMinutes=180`, `CriminalFloorDwellMinutes=8`
- `LastCaptureMessage`, `TrySpawnCriminal`, `CaptureCriminalsNow` (test hook)
- Spawn when lobby + avg crime ≥ min + cap; chance `× dt × (avg/100)`
- Roam to high-crime shop/hotel floors; life timeout → leave via lobby → despawn
- Capture at start and end of `Tick`
- Criminal in `IsNonPopulationRole` / `IsEphemeralOrStaffRole` (not `IsServiceRole`)

### `Assets/Scripts/Agents/AgentView.cs`

- Criminal color `(0.75, 0.1, 0.15, 1)`

### `Assets/Scripts/Simulation/TowerSimulation.cs`

- Collects Criminal floors into `_criminalFloors` for `Crime.Tick` (mirrors Security)

### `Assets/Tests/EditMode/CriminalCaptureTests.cs`

- Capture same-floor / one-per-guard / Outside ignored
- AgentSystem capture message, population exclusion, spawn cap, CollectFloors

## Verification

| Check | Result |
|-------|--------|
| Roslyn runtime + CriminalCaptureTests compile | **PASS** |
| net8 reflection host `CriminalCaptureTests` | **PASS** — 7/7 |
| Unity EditMode | Skipped — main Editor locked; worktree Library PackageCache broken (2d.animation) |

## Test summary

```
dotnet run --project .superpowers/sdd/crime-task-5-typecheck/CriminalTestHost
→ total=7 passed=7 failed=0
```

## Concerns

- Unity EditMode not re-run this task; Roslyn/net8 host covers the suite without ScriptableObject grid fixtures.
- `CaptureCriminalsNow` / `_agents` reflection inject used so AgentSystem tests run outside Unity.
- Spawn chance `0.08` is a first-pass tuneable.

## Review fix notes (Important findings)

### Fixes

1. **One capture pass per Tick** — removed the pre-movement `TryCaptureCriminals` call; capture runs once after movement/traffic. Test: `AgentSystem_Tick_one_security_captures_at_most_one_criminal`.
2. **Outside + life > 0 soft-lock** — leave/no-roam paths zero `CriminalDwellRemaining`; failed spawn removes the agent; `DespawnFinishedCriminals` clears any Outside Criminal (and leftover life). Arrive-Outside also zeros life. Test: `Criminal_Outside_with_remaining_life_is_despawned`.
3. **Tighter tests** — `TrySpawnCriminal_enters_via_lobby_when_grid_has_roam_target` (grid+lobby+hotel); `Criminal_life_timeout_starts_leave_and_zeros_life`.

### Also

- Outside spawn with no path still enters via `PlaceCriminalWorkingAt` (avoids soft-lock when pathfinding fails).
- Roam floor pick uses `is null` for `RoomTypeSO` so net8 uninitialized fixtures work (Unity `==` treats them as null).

### Verification (review fix)

```
dotnet exec ".../DotNetSdkRoslyn/csc.dll" /noconfig @.superpowers/sdd/crime-task-5-typecheck/runtime.rsp
dotnet exec ".../DotNetSdkRoslyn/csc.dll" /noconfig @.superpowers/sdd/crime-task-5-typecheck/criminal-tests.rsp
dotnet run --project .superpowers/sdd/crime-task-5-typecheck/CriminalTestHost
→ total=11 passed=11 failed=0
```
