# Shop Demand Final Review Fix Report

Date: 2026-09-07
Base: `fa53724a70ba3403b9c1890be7af326f6e4ea741`
Branch: `feature/shop-catalog-expansion`

## Fixes

1. `AgentSystem` now partitions each family into native-tier and one-tier-lower candidates. A family exposes its native candidates whenever any are eligible and only falls back to spill candidates when its native set is empty. Competition weighting runs only inside the selected tier set.
2. `ShopDemandSystem.AvailableFor` now requires the candidate tier. Native candidates report origin `Remaining`; one-tier-lower candidates report `min(origin Remaining, remaining spill cap)`; all other tiers report zero. Street/Event proportional family selection uses that serviceable amount for the tier set AgentSystem can actually choose.
3. Selected-shop `Daily upkeep` now uses `ShopDemandBalance.DailyUpkeep(room.Type)`, so a newly built shop shows its known current daily rate before rollover. Archived revenue/net remain explicitly yesterday values.

## Tests added or updated

- `CommercialVisitTests.Native_tier_shop_wins_over_higher_weight_lower_tier_shop`
- `CommercialVisitTests.Event_family_weighting_uses_spill_capacity_not_origin_plus_spill`
- `ShopDemandSystemTests.Spill_only_availability_reports_remaining_spill_capacity`
- Updated native/spill `AvailableFor` expectations and API calls.
- `RoomEconomyFormatTests.Newly_built_shop_shows_known_daily_upkeep_before_first_rollover`

## TDD evidence

- First isolated Unity compile failed because the new tier-aware `AvailableFor` overload did not exist, plus the known unrelated `StairsPathfinderTests` missing-`System.Linq` baseline compile issue.
- After introducing the tier-aware availability API, the behavioral RED run reported the intended failures:
  - Native-tier preference selected the higher-weight lower-tier shop.
  - A newly built shop displayed `Daily upkeep: $0` instead of `$33`.
- Exact final regression cases: **4/4 passed**.

The isolated verification worktree used only the known uncommitted `using System.Linq;` workaround in `StairsPathfinderTests.cs`. It is not part of this change or commit.

## Covering verification

Command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower-final-fix-verify' `
  -runTests -testPlatform EditMode `
  -testFilter 'BuildATower.Tests.ShopDemandSystemTests|BuildATower.Tests.ShopDemandTripTests|BuildATower.Tests.CommercialVisitTests.Native_tier_shop_wins_over_higher_weight_lower_tier_shop|BuildATower.Tests.CommercialVisitTests.Event_family_weighting_uses_spill_capacity_not_origin_plus_spill|BuildATower.Tests.RoomEconomyFormatTests|BuildATower.Tests.ShopDemandFormatTests'
```

Result: **54/54 passed, 0 failed**.

Covered:

- `ShopDemandSystemTests`
- `ShopDemandTripTests`
- Both relevant new `CommercialVisitTests`
- `RoomEconomyFormatTests`
- `ShopDemandFormatTests`

`ShopVisitRules.cs` was not changed, so `ShopVisitRulesTests` was not required.

## Baseline failures

A broader exploratory `CommercialVisitTests` run produced four unrelated failures:

- `Condo_daytime_window_triggers_at_most_once_per_day`
- `Office_lunch_trip_records_visit_after_dwell`
- `Street_visitor_cap_is_eight`
- `SyncHomes_preserves_street_visitors`

The same four tests were run at baseline production commit `1b1ccfe` (the parent production state of docs-only `fa53724`) and failed **0/4 passed, 4/4 failed**. They are baseline failures and are outside this fix wave. The requested relevant new CommercialVisit cases pass.

## Self-review

- Native-first selection is enforced independently for Food and Retail before family choice.
- Street/Event family weights use only the demand serviceable by the exposed tier set.
- Spill availability cannot exceed either remaining origin demand or remaining 25% spill capacity.
- No upward, cross-family, or two-tier spill path was added.
- Demand is still consumed only after `BeginTrip` succeeds.
- Eligibility, competition weights, spill counters, null-demand behavior, and existing HUD lines remain unchanged except for correcting the daily-upkeep value.
- Scoped diff contains only three production files, three covering test files, and this report.
