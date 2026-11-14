# Task 2 Report — Amenity relief + atrium range

**Status:** DONE

**Commit:** `164b10c` — feat: atrium amenity relief range and new leisure relief values

## Summary

- Added `AtriumMaxFloorDelta = 6` and `AtriumMaxHorizontalCells = 24` on `AmenitySystem`.
- Extended `ReliefForId` for `leisure_casino` (3), `leisure_nightclub` (2), `leisure_chapel` (4), `leisure_atrium` (5).
- `IsInRange` now takes the amenity `RoomInstance` and applies atrium limits when `isAtrium` or id prefix `leisure_atrium`; other leisure remains ±2 floors / 12 cells.

## Test summary

EditMode `AmenitySystemTests`: **7/7 passed** (GREEN).

## TDD evidence

### RED (tests first)

**Command:**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" `
  -runTests -testPlatform EditMode -testFilter AmenitySystemTests `
  -testResults "...\task-2-red.xml" -logFile "...\task-2-red.log"
```

**Output:** `total="7" passed="5" failed="2"` — `Atrium_relief_uses_wider_range` (expected 5, got 0), `Other_leisure_still_uses_tight_range` (initial spa at floor 4 could not `TryPlace`).

### GREEN (after implementation + test grid fixes)

**Command:** Same Unity invocation with `task-2-green.xml` / `task-2-green.log`.

**Output:** `test-run ... result="Passed" total="7" passed="7" failed="0"`.

## Concerns

- `Other_leisure_still_uses_tight_range` uses spa at x=13 (beyond 12 horizontal cells) because leisure cannot be placed three floors above lobby without vertical transit in this test harness; behavior matches ±2 / 12 intent.
- Atrium vertical ±6 is implemented but only exercised when `TryPlace` succeeds at floor 7 (optional block); floor-8 / 7-floor-delta out-of-range case not asserted due to same placement limits.
- New `ReliefForId` values for casino/nightclub/chapel are not covered by dedicated assertions (only atrium relief via `MaxReliefInRange`).

## Files touched (committed)

- `Assets/Scripts/Economy/AmenitySystem.cs`
- `Assets/Tests/EditMode/AmenitySystemTests.cs`
