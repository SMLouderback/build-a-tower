# Final rewards branch fixes

## Changes
1. **Counter since armed:** `ActiveContract.CounterBaseline` + snapshot field; counter progress is metric minus baseline; seed metrics on `Refresh`/`RestoreDefault`.
2. **Refill on slot free:** `Evaluate` calls `Refresh` after success/expiry; weekly day-7 refresh unchanged.
3. **Duration streak persist:** `DurationStreakSnapshotV1[]` on `RewardsSnapshotV1`; `ChallengeTracker` capture/restore wired through `RewardsSystem`.

## Verification
- EditMode: `ContractBoardTests`, `RewardsSnapshotTests`, `ChallengeTrackerTests`, `RewardsSystemTests` — **24 passed, 0 failed** (`TestResults/final-rewards-fix3.xml`).

## Deferred
- Crew-day spend hook (unchanged).
