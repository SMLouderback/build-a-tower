# Task 2 Report: TowerGrid place, extend, stairs ban

## Status

DONE_WITH_CONCERNS

Implementation is committed as `2c71f06` (`feat: place and extend elevator shafts with stairs ban`). No Task 3 or `ElevatorSystem` work was started.

## Changes

- Added `TowerGrid.MaxElevatorSpan = 30`.
- Routed elevator room types through dedicated placement validation and placement paths.
- Added `_underElevator` separately from `_underStairs`.
- Allowed elevators to overlap lobby, rooms, and scaffolding while retaining/restoring underlays.
- Generalized room registration so rooms can be built behind stairs or elevators.
- Added mutual stairs/elevator exclusion using both visible cell ownership and covering-room lookup.
- Added `CanExtendElevator` and `TryExtendElevator`, preserving the shaft instance ID and enforcing a 2–30 floor span that contains and enlarges the old span.
- Extended demolition and lobby-extension restoration behavior to elevator underlays.

## Tests

Added EditMode coverage for:

- Placing a 1x2 elevator and rejecting stairs overlap.
- Extending a 2-floor shaft to 30 floors, reporting 28 added cells, and rejecting 31 floors.
- Rejecting elevator placement on stairs.
- Building a room behind an elevator and restoring lobby/room cells after elevator demolition.

TDD red execution and final Unity EditMode execution could not be performed from CLI: Unity 6000.4.7f1 is not on PATH, no registered/known editor executable was found, and the Unity MCP server was unavailable. Static IDE diagnostics report no linter errors in the two changed C# files. `git show --check` completed successfully for the commit.

## Self-review

Reviewed the complete committed diff against the task brief. The implementation keeps stairs and elevator underlays separate, checks hidden covering transit rooms for the mutual ban, preserves elevator underlays across extension, and keeps the `InstanceId` stable when replacing the shaft instance. No correctness issue was found in the reviewed diff.

The remaining concern is verification only: the Unity compiler and EditMode tests were not executable in this environment, so runtime pass status is unconfirmed.

## Repository state

Only `Assets/Scripts/Core/TowerGrid.cs` and `Assets/Tests/EditMode/ElevatorTests.cs` were included in commit `2c71f06`. Pre-existing modified/untracked SDD files were left untouched and uncommitted; this report is also intentionally left outside the code commit because the brief's commit scope names only the two implementation files.

## Important review fixes

- Elevator placement now rejects definitions that are not exactly 1 cell wide and 2 cells high; the shared placement validator also rejects footprints outside the 2–30-cell shaft span.
- Elevator extension now requires the exact shaft instance to remain registered in the target grid, rejecting foreign and demolished shaft instances through both `CanExtendElevator` and `TryExtendElevator`.
- Added EditMode regression cases for initial sizes `2x2` and `1x31`, plus foreign and demolished shaft extension attempts.

## Fix verification

- A Unity EditMode run for `BuildATower.Tests.ElevatorTests` was attempted with Unity `6000.4.7f1`, but the batch runner correctly refused to open the project while the interactive Unity editor already owned it.
- The active Unity editor performed a forced script recompile and completed assembly reload successfully (`Mono: successfully reloaded assembly`) after the changes, with no subsequent C# compiler errors for `TowerGrid.cs` or `ElevatorTests.cs`.
- Cursor static diagnostics report no linter errors in either changed C# file.
