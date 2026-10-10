# Task 6 Report — Wire BuildEconomy + EconomySystem + HUD through VPSF

**Status:** DONE

**Commit:** $commit — feat: route build and midnight economy through VPSF balancer

## Summary

- Sim money no longer reads `RoomTypeSO.buildCost` / `baseIncome` for build/place/midnight/HUD.
- `BuildEconomy` routes unit/placement/charged APIs through `EconomicBalancingManager.BuildCost` (charged path avoids double difficulty).
- `EconomySystem` midnight income/upkeep uses `PeriodIncome` / `PeriodUpkeep` with living vs commercial pulse, `FloorValueRules.Fit01`, climate spend.
- Living rooms charge midnight upkeep; shops still credit visit-backed period income at midnight.
- `ShopVisitRules.PayPerVisit` = period income × `VpsfCatalog.VisitPriceOfPeriodIncome` (0.35).
- `VpsfCatalog` maps shops/leisure + Infrastructure (lobby/elevator/scaffold) rows; afford checks default floor-fit `1f` when floor unknown.
- `RoomEconomyFormat` + `TowerHudController` + `TowerSimulation` pass climate + `MacroEconomy` living/commercial pulse into balancer call sites.

## Test summary

EditMode filter (worktree `economy-vpsf-task6`): **119/119 passed** (GREEN).

| Suite / case | Result |
|--------------|--------|
| EconomySystemTests | Passed |
| RoomEconomyFormatTests | Passed |
| ShopVisitRulesTests | Passed |
| VpsfCatalogTests | Passed |
| EconomicBalancingManagerTests | Passed |
| BuildGraceRefundTests | Passed |
| CommercialVisitTests | Passed |
| SandboxBuildTests | Passed |
| DifficultyProfileTests | Passed |
| AgentSystemTests.Reachable_condo / Inaccessible_condo | Passed |

**Command:**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "...\Build-A-Tower\.worktrees\economy-vpsf-task6" `
  -runTests -testPlatform EditMode `
  -testFilter "EconomySystemTests|RoomEconomyFormatTests|ShopVisitRulesTests|VpsfCatalogTests|EconomicBalancingManagerTests|BuildGraceRefundTests|CommercialVisitTests|SandboxBuildTests|DifficultyProfileTests|AgentSystemTests.Reachable_condo|AgentSystemTests.Inaccessible_condo" `
  -testResults "...\TestResults\economy-vpsf-task6e.xml" -logFile "...\unity-economy-vpsf-task6e.log"
```

**Output:** `result="Passed" total="119" passed="119" failed="0"`.

## Concerns

- Street Mid living floor-fit (0.75) changes golden incomes vs old SO constants; tests updated to VPSF expectations.
- Visit list price is 35% of period income — restaurant/street afford gates retuned; may need balance pass.
- Infrastructure catalog rows preferred over SO pass-through; scaffold/lobby/elevator values may still need tuning vs feel.
- Worktree `economy-vpsf-task6` Library junction; safe to remove after parent cleanup.

## Files touched (committed)

- `Assets/Scripts/Build/BuildController.cs`, `BuildEconomy.cs`
- `Assets/Scripts/Economy/EconomicBalancingManager.cs`, `EconomySystem.cs`, `ShopDemandBalance.cs`, `ShopVisitRules.cs`, `VpsfCatalog.cs`
- `Assets/Scripts/Simulation/TowerSimulation.cs`
- `Assets/Scripts/UI/RoomEconomyFormat.cs`, `TowerHudController.cs`
- `Assets/Tests/EditMode/` — AgentSystem, BuildGraceRefund, CommercialVisit, EconomicBalancingManager, EconomySystem, RoomEconomyFormat, ShopVisitRules, VpsfCatalog tests
- `.superpowers/sdd/economy-vpsf-task-6-report.md`
