# Stars3 Task 6 Report: Maid and Handyman agents

## Status

DONE

## Commit

- `e2a4b9b` — `feat: visible maid and handyman service agents` on `feature/stars3-ops-services`

## Changes

### Roles / agent state

- `AgentRole.Maid`, `AgentRole.Handyman`
- `Agent.ServiceTarget`, `Agent.ServiceWorkRemaining` (game minutes)
- `AgentView` teal maid / rust handyman colors

### `AgentSystem`

1. **SyncServiceStaff** (from `SyncHomes`) — spawn/despawn staff to each HK/Maint `StaffedWorkers`
2. **Maid** — claim lowest-`InstanceId` Dirty hotel → trip → `CleanMinutes` → `ClearDirty`
3. **Handyman** — claim lowest Condition in 1..99 degradable (skip Broken/0) → 60 min → `ApplyRepairTick` (+10)
4. Idle `AtHome` when no jobs; try next job after finish
5. Population / average stress / low-condition stress exclude Maid/Handyman (like StreetVisitor)
6. Hooks: `TryAssignServiceJobs`, `ForceCompleteServiceWork`

### Tests (`ServiceAgentTests`)

- Staff sync + population exclude
- Maid clears Dirty after clean minutes / ForceComplete
- Handyman +10; ignores Broken/0
- Oldest Dirty hotel by InstanceId

## TDD

| Step | Action | Result |
|------|--------|--------|
| 1 | Failing `ServiceAgentTests` | **PASS (red)** — missing roles/hooks before implement |
| 2 | Implement staff sync + jobs | Done |
| 3 | Green Roslyn typecheck | **PASS** — exit 0 |
| 4 | Commit | `feat: visible maid and handyman service agents` |

## Verification

- **Roslyn typecheck:** Unity 6000.4.7f1 `csc.dll` via `.superpowers/sdd/stars3-task-6-typecheck/`; runtime + tests exit 0.
- **Unity EditMode batch:** not run (Roslyn; Editor not required).

### Command run

```powershell
$csc = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\DotNetSdkRoslyn\csc.dll"
$dir = ".superpowers/sdd/stars3-task-6-typecheck"
dotnet exec $csc /noconfig "@$dir\runtime.rsp"
dotnet exec $csc /noconfig "@$dir\tests.rsp"
```

### Output

```
=== RUNTIME ===
runtime exit: 0
=== TESTS ===
tests exit: 0
```

## Self-review

| Check | Result |
|-------|--------|
| Maid/Handyman roles | Pass |
| Sync to StaffedWorkers | Pass |
| Maid ClearDirty after work | Pass |
| Handyman +10 / skip Broken | Pass |
| Idle at home | Pass |
| Excluded from Population | Pass |
| Distinct AgentView colors | Pass |
| Public test hooks | Pass |
| No unrelated `.superpowers/sdd` in commit | Pass |

## FIX — Handyman must not revive Broken rooms

**Finding:** Claim skips `Condition == 0`, but `FinishServiceJob` / `ApplyRepairTick` did not re-check. Midnight can decay 1→0 while a job is in progress; completion then applied +10 → Condition 10.

**Changes:**
1. `ApplyRepairTick` — returns `false` (no-op) if room null or `IsBroken` / `Condition < 1`
2. `FinishServiceJob` — if handyman target is Broken on complete, clear claim without repairing
3. `UpdateServiceWork` — abort mid-job when handyman target becomes Broken
4. Regression: `Handyman_ForceComplete_does_not_revive_room_that_broke_mid_job` (+ `ApplyRepairTick_noop_when_broken_or_null`)
