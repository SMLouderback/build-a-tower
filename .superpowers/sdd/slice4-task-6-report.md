# Task 6 Report: HUD stars, economy summary, and time presets

## Status

Complete.

## Changes

- Added stars, population, average stress, and last income/expense/net to the HUD.
- Added highlighted pause, 1x, 2x, 5x, 10x, and 60x time controls via `TowerSimulation.SetSpeedPreset`.
- Disabled locked room, stairs, and elevator buttons with their star requirements shown.
- Loaded elevator and premium condo, hotel, and office room assets from `Resources`.

## Verification

- Added `GameClockTests.SetSpeedPreset_updates_clock_speed_and_pause_state`.
- `git diff --check -- Assets/Scripts/UI/TowerHudController.cs Assets/Scripts/Simulation/TowerSimulation.cs Assets/Tests/EditMode/GameClockTests.cs` passed.
- Unity batch EditMode test runs could not start because the project is open in another Unity instance.
