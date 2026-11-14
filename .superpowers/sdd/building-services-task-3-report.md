# Task 3 Report — Amenity Hooks + Loading Dock Demand

**Status:** DONE

**Commit:** `feat: amenity hooks for mail recycling loading dock`

## Summary

- Added Mail (−1) and accessible Recycling (−2) to once-daily strongest-amenity stress relief.
- Extended daily relief to office workers through their office `HomeRoom`; office workers only benefit from Mail.
- Added accessible Loading Dock +5% demand helpers, additive hotel fill (`0.08 + 0.05`) and shop demand weighting.
- Added focused EditMode coverage for beneficiary roles, once/day behavior, vehicle-access gating, strongest-wins, additive hotel fill, and shop demand.

## Test summary

**Added:** 7 NUnit methods in `BuildingServicesAmenityTests` (8 executions including condo/office test cases).

**Automated run:** Unity 6000.4.7f1 batchmode exited with code 1 before test discovery and did not produce a result XML. The log contains startup arguments only, with no compilation or test output. Both RED and GREEN runs were attempted; automated pass/fail verification remains unavailable in this environment.

**Static checks:** `git diff --check` passed for all scoped Task 3 files.

## Files committed

- `Assets/Scripts/Economy/AmenitySystem.cs`
- `Assets/Scripts/Economy/ShopVisitRules.cs`
- `Assets/Scripts/Agents/AgentSystem.cs`
- `Assets/Tests/EditMode/BuildingServicesAmenityTests.cs` (+ `.meta`)
- `.superpowers/sdd/building-services-progress.md`
- `.superpowers/sdd/building-services-task-3-report.md`

## Concerns

- Re-run `BuildingServicesAmenityTests` in Unity EditMode when batchmode test discovery is available.
