# Task 7 Report — Placement/HUD floor-fit warnings + agent stress hooks

**Status:** DONE

**Commit:** `52502f0` — feat: surface floor-fit warnings and class stress hooks

## Summary

- `RoomEconomyFormat.FloorFitWarningOrNull` wraps `FloorValueRules.TryWarning` for HUD copy.
- `TowerHudController` shows the reason under cost/economy lines for selected room, catalog pick + placement ghost (`HoverCell` floor), and catalog tooltips (ghost floor or floor 0).
- `AgentSystem.UpdateStress` uses `TenantClassStress.ElevWaitStress` for living tenants (`ElevatorWaitMinutes` as game-time seconds at 1x); staff/street keep legacy 5-minute start.
- Office/hotel/condo check-in fill multiplies by worst nearby `ClassClashRules.PenaltyMult` (Upper subject only).

## Test summary

EditMode filter (worktree `economy-vpsf-task7`): **39/39 passed** (GREEN).

| Suite / case | Result |
|--------------|--------|
| FloorValueRulesTests | Passed |
| ClassClashRulesTests | Passed |
| OfficeFillTests | Passed |
| RoomEconomyFormatTests | Passed |
| MetroSnapshotTests.ElevatorWaitStressMultiplier | Passed |
| HotelLuxuryTests.CheckInFillMultiplier / SyncHomes | Passed |
| CondoFillTests.FillCondoVacancies / rejects_premium | Passed |

**Command:**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "...\Build-A-Tower\.worktrees\economy-vpsf-task7" `
  -runTests -testPlatform EditMode `
  -testFilter "FloorValueRulesTests|ClassClashRulesTests|OfficeFillTests|RoomEconomyFormatTests|MetroSnapshotTests.ElevatorWaitStressMultiplier|HotelLuxuryTests.CheckInFillMultiplier|HotelLuxuryTests.SyncHomes|CondoFillTests.FillCondoVacancies|CondoFillTests.TryFindCondo_rejects" `
  -testResults "...\TestResults\economy-vpsf-task7b.xml" -logFile "...\unity-economy-vpsf-task7b.log"
```

**Output:** `result="Passed" total="39" passed="39" failed="0"`.

## Concerns

- Main project Unity instance was open; tests ran via worktree + Library junction to `economy-vpsf-task5`.
- Pre-existing (unrelated to this diff): `CondoFillTests.TryFindCondo_accepts_basic_for_studio` and two `HotelLuxuryTests.TryFindHotelRoomForGuest_*` fail on this worktree baseline without Task 7 hooks.
- Elev wait maps `ElevatorWaitMinutes` 1:1 to `TenantClassStress` “seconds” (1x clock); Mid living now waits to 25 before elev stress (was 5 for everyone).
- Clash scan is vertical floor-delta only (same as rules); horizontal neighbors not scored.

---

## Follow-up — elev wait minutes→seconds (CRITICAL)

**Status:** FIXED

**Commit:** *(pending hash)* — `fix: convert elev wait minutes to seconds for class stress`

### Bug

`AgentSystem.ShouldApplyElevatorWaitStress` passed `agent.ElevatorWaitMinutes` into `TenantClassStress.ElevWaitStress`, which expects **seconds**. Upper therefore stressed at 15 **minutes**, not 15 seconds.

### Fix

- Call site converts minutes→seconds: `agent.ElevatorWaitMinutes * 60f`.
- `TenantClassStress` remains seconds-based (Upper=15s, Mid=25s).
- Comment above `ShouldApplyElevatorWaitStress` corrected; method made `public static` for EditMode coverage.

### Test

`ClassClashRulesTests.Upper_elev_wait_stresses_above_15s_not_at_14s` now also drives `AgentSystem.ShouldApplyElevatorWaitStress` with an Upper condo resident: no stress at `14/60` and `0.25` minutes; stress at `16/60` minutes.

## Files touched (committed)

- `Assets/Scripts/UI/RoomEconomyFormat.cs`
- `Assets/Scripts/UI/TowerHudController.cs`
- `Assets/Scripts/Agents/AgentSystem.cs`
- `.superpowers/sdd/economy-vpsf-task-7-report.md`
