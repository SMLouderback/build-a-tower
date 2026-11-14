# Task 4 Report — HUD Utility strip wiring

**Status:** DONE

**Commit:** (see git log — `feat: wire building services into Utility build menu`)

## Summary

- Added `TryAddRoomButton` entries for `Rooms/ServiceMail`, `Rooms/ServiceRecycling`, and `Rooms/ServiceLoadingDock` in `TowerHudController.CollectMenuRoomButtons`, alongside other Utility service rooms.
- Added `BuildingServicesHudTests` (2 EditMode tests) asserting the three rooms appear under `BuildFamily.Utility` via `BuildMenuCatalogForTests` and retain star gates (Mail 1★, Recycling/Loading Dock 2★).

## Test summary

**Expected:** `BuildingServicesHudTests` **2/2 passed** when Unity runs EditMode.

**Automated run:** Unity batchmode **blocked** — another Editor instance has the project open. Re-run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" `
  -runTests -testPlatform EditMode -testFilter BuildingServicesHudTests `
  -testResults "...\building-services-task-4-green.xml" -logFile "...\building-services-task-4-green.log"
```

**TDD RED (logical):** Before HUD wiring, catalog tests fail because Utility group lacks the three service ids.

## Files committed

- `Assets/Scripts/UI/TowerHudController.cs`
- `Assets/Tests/EditMode/BuildingServicesHudTests.cs` (+ `.meta`)
- `.superpowers/sdd/building-services-progress.md`
- `.superpowers/sdd/building-services-task-4-report.md`
