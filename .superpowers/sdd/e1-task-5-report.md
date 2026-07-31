# E1 Task 5 Report: Assets, HUD catalog, README, closeout

## Status

DONE_WITH_CONCERNS

## Commits

- `ec40b92` — `fix: exclude StreetVisitor from AverageStress` (Task 4 follow-up)
- `0bd9f14` — `feat: shop assets, HUD catalog, README for E1 visits`

## Changes

### Task 4 follow-up

- `AgentSystem.AverageStress` skips `AgentRole.StreetVisitor` (same as `Population`)
- Test: `AverageStress_excludes_street_visitors` in `CommercialVisitTests`

### Shop assets

Created (ScriptableObjects + Resources mirrors for HUD `Resources.Load`):

| Asset | Id | Subgroup | Pay | Slots | Hours (minute-of-day) | Size |
|-------|-----|----------|-----|-------|------------------------|------|
| `ShopFastFood` | `shop_food_fast` | Food | 40 | 4 | 660–1260 (11–21) | 16×1 |
| `ShopRestaurant` | `shop_food_restaurant` | Food | 120 | 6 | 660–1320 (11–22) | 16×1 |
| `ShopRetail` | `shop_retail` | Retail | 80 | 5 | 600–1200 (10–20) | 16×1 |

- All: `IncomeModel.TrafficVariable`, `RoomCategory.Commercial`, above+basement
- `RetailFastFood.asset` left unused; removed from `TowerSandbox` `placeableRooms`

### HUD / catalog / docs

- `TowerHudController.EnsureElevatorAndCatalog` loads `Rooms/ShopFastFood`, `ShopRestaurant`, `ShopRetail`
- `BuildCatalogTests.Group_nests_three_shops_under_food_and_retail`
- README play bullets 26–27 for shops, visits, midnight payout; E1 spec link
- Plan Task 5 step checkboxes marked done

## Verification

- **Roslyn typecheck:** Unity 6000.4.7f1 `csc.dll` via `.superpowers/sdd/e1-task-5-typecheck/`; runtime + tests exit 0
- **Unity EditMode batch:** not run (open Editor lock pattern from prior E1 tasks)

## Concerns

- Unity EditMode execution unconfirmed until Test Runner / Editor unlock
- Active hours authored as **minute-of-day** (required by `ShopVisitRules`); older Office SOs still use hour integers (unrelated to shops)
- Duplicate SO vs Resources shop assets (same pattern as premium rooms); edits must be mirrored
- `RetailFastFood` still on disk if anything references GUID `e787759d…`
