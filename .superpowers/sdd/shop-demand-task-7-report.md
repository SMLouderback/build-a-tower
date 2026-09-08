# Task 7 report: regression verification and spec completion

## Status

Implemented. The feature-introduced stale assertion was corrected, its isolated test passed, and every remaining focused failure reproduces at the pre-feature baseline. The design spec is now marked `Implemented`.

## Verification environment

- Feature commit: `9ee999b82cd4f238e6e8e2d4e3fdb4fd479612b0`
- Feature verification worktree: `C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower-task7-9ee999b` (detached)
- Pre-feature baseline: `495cb2a` (parent history before Tasks 1–6)
- Baseline worktree: `C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower-task7-baseline-495cb2a` (detached)
- Unity: `6000.4.7f1`
- The primary checkout was not used for Unity verification because it was dirty and open in Unity.

## Commands and results

### Focused EditMode

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "ShopDemandSystemTests|ShopDemandTripTests|ShopDemandFormatTests|ShopVisitRulesTests|CommercialVisitTests|EconomySystemTests|RoomEconomyFormatTests" -testResults .superpowers/sdd/shop-demand-final.xml -logFile .superpowers/sdd/shop-demand-final.log
```

First run, clean `9ee999b`: compilation stopped on the known baseline defect:

```text
Assets\Tests\EditMode\StairsPathfinderTests.cs(81,39): error CS1061:
'IReadOnlyList<RoomInstance>' does not contain a definition for 'Contains'
```

After the disposable patch below, the focused suite ran:

```text
total=120 passed=114 failed=6
```

Fixture summary:

```text
ShopDemandSystemTests  total=22 passed=22 failed=0
ShopDemandTripTests    total=10 passed=10 failed=0
ShopDemandFormatTests  total=6  passed=6  failed=0
ShopVisitRulesTests    total=14 passed=14 failed=0
CommercialVisitTests   total=21 passed=16 failed=5
EconomySystemTests     total=35 passed=34 failed=1
RoomEconomyFormatTests total=12 passed=12 failed=0
```

Five focused failures reproduce before the feature at `495cb2a`:

- `CommercialVisitTests.Condo_daytime_window_triggers_at_most_once_per_day`
- `CommercialVisitTests.Office_lunch_trip_records_visit_after_dwell`
- `CommercialVisitTests.Street_visitor_cap_is_eight`
- `CommercialVisitTests.SyncHomes_preserves_street_visitors`
- `EconomySystemTests.Midnight_pays_daily_rent_for_occupied_office`

One focused failure is introduced by the feature:

- `CommercialVisitTests.OnNewDay_pays_shop_earnings_not_visits_times_list_price`
  - expected wallet balance: `65`
  - actual wallet balance: `45`
  - the shop has a `$40` pay cap, so Task 4's specified 50% upkeep is `$20`; `$65` gross revenue minus `$20` upkeep gives the correct `$45` net wallet change.
  - Task 4 updated the upkeep-specific economy tests but did not update this older commercial test's wallet assertion.

### Full EditMode

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults .superpowers/sdd/shop-demand-full.xml -logFile .superpowers/sdd/shop-demand-full.log
```

Feature result:

```text
total=678 passed=622 failed=56
```

The same full suite was run at the pre-feature baseline `495cb2a`:

```text
total=626 passed=571 failed=55
```

Failure-set comparison:

```text
55 shared baseline failures
1 current-only failure:
BuildATower.Tests.CommercialVisitTests.OnNewDay_pays_shop_earnings_not_visits_times_list_price
0 baseline-only failures
```

Therefore, the shop-demand work adds one stale-test regression to an already failing baseline; the other 55 failures are not attributable to Tasks 1–6.

## Isolated patches

Applied only in both disposable verification worktrees and not committed:

```diff
+using System.Linq;
 using BuildATower;
```

File: `Assets/Tests/EditMode/StairsPathfinderTests.cs`

No source or test patch was applied to the primary checkout.

## Play Mode smoke

Not attempted. The available batch environment can execute Unity tests, but there is no automated Play Mode smoke covering the seven required interactive scenarios, and the primary Editor is occupied. No manual or visual check is claimed.

Remaining manual checks:

1. Start a low-star tower and verify Budget street baselines exist while Premium baseline is zero.
2. Place multiple Budget Food shops and verify finite visits split across shops.
3. Add occupied office, condo, and hotel rooms and verify next-day demand rises by the approved weights.
4. Leave excess shops underused and verify upkeep plus negative net.
5. Create Premium demand with only Mid shops and verify at most 25% spills, with no Budget spill.
6. Leave demand unmet and verify matching-wealth occupants receive more stress than other tiers.
7. Open the Shops panel, select a shop, and verify pool metrics and yesterday economics are readable.

## Self-review

- Verified the requested focused and full EditMode commands in detached worktrees.
- Reproduced and isolated the known `System.Linq` compile defect without committing it.
- Compared full failure names against a pre-feature run rather than assuming all failures were feature regressions.
- Confirmed all 52 direct demand/mapping/format fixtures in the focused command passed.
- Marked the spec `Implemented` after confirming that no remaining focused failure is feature-attributable.
- Did not alter or commit unrelated primary working-tree changes.

## Concerns

1. The repository baseline has 55 additional full-suite failures after the disposable compile fix; these need separate triage.
2. The seven-item manual Play Mode visual/gameplay smoke remains outstanding.

## Verification-fix addendum

### Test correction

Commit `1b1ccfe` updates `CommercialVisitTests.OnNewDay_pays_shop_earnings_not_visits_times_list_price` to preserve the `$65` gross-income assertions while expecting the approved `$20` shop upkeep and `$45` net wallet result. It also verifies:

- `EconomySystem.LastExpense == 20`
- room lifetime expense and last room expense are `$20`
- yesterday revenue is `$65`
- yesterday upkeep is `$20`
- yesterday net is `$45`

This changes test expectations only; production behavior is unchanged.

### Isolated verification at `1b1ccfe`

Detached worktree:

```text
C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower-task7-1b1ccfe
```

The only worktree-only source patch was the known compile workaround:

```diff
+using System.Linq;
 using BuildATower;
```

No `StairsPathfinderTests.cs` change was committed.

Changed-test command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "CommercialVisitTests.OnNewDay_pays_shop_earnings_not_visits_times_list_price" -testResults .superpowers/sdd/shop-demand-task7-changed-test.xml -logFile .superpowers/sdd/shop-demand-task7-changed-test.log
```

Exact result:

```text
testcasecount=1 result=Passed total=1 passed=1 failed=0 inconclusive=0 skipped=0
```

Focused-suite command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "ShopDemandSystemTests|ShopDemandTripTests|ShopDemandFormatTests|ShopVisitRulesTests|CommercialVisitTests|EconomySystemTests|RoomEconomyFormatTests" -testResults .superpowers/sdd/shop-demand-task7-focused.xml -logFile .superpowers/sdd/shop-demand-task7-focused.log
```

Exact result:

```text
testcasecount=120 total=120 passed=115 failed=5 inconclusive=0 skipped=0
```

All five remaining focused failures are present in the pre-feature `495cb2a` full-suite baseline:

- `CommercialVisitTests.Condo_daytime_window_triggers_at_most_once_per_day`
- `CommercialVisitTests.Office_lunch_trip_records_visit_after_dwell`
- `CommercialVisitTests.Street_visitor_cap_is_eight`
- `CommercialVisitTests.SyncHomes_preserves_street_visitors`
- `EconomySystemTests.AverageDailyProfit_tracks_running_average_of_LastNet`

Failure-set comparison:

```text
focused failures not present in pre-feature baseline: 0
all remaining focused failures match baseline: True
```

No interactive Play Mode smoke is claimed; the seven manual checks above remain unverified.
