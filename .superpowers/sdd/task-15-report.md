# Task 15 Report: Extras spend hooks + polish pass

## Summary
- Wired reward build credit consumption into successful paid build placements in `BuildController`.
- Applied active reward demand multipliers at the daily demand/climate sampling sites in `TowerSimulation`.
- Polished contract expiry news copy.
- Fixed `RewardBookHud.ClaimAllReady` so an extras override is honored during batch claims.

## Notes
- Build credit is consumed only after the grid accepts placement; failed placement/refund paths do not consume it.
- The demand bump is included in shop demand, economy/conference daily sampling, and the demand graph spend sample.
- Crew-day spend hook skipped: repair/clean work is currently assigned through staff service jobs, and no clear player repair/clean spend path exists to consume the token without changing gameplay semantics.

## Verification
- Passed: EditMode filter `Rewards|ClaimLedger|ChallengeCatalog|ContractBoard|TrophyRoom|RewardBook|RewardTutorial|RewardClaim|RewardExtras|RewardsSnapshot`.
- Result: 49 passed, 0 failed.
