### Task 9 Report: Fat challenge catalog

- Expanded `ChallengeCatalog` from seed stubs to 60 challenge definitions across Population, Stars, Construction, Demolition, Operations, Infamy, Commerce, Transit, Time, and Quirks.
- Preserved existing seed ids and first contract ordering (`pop_100`, `health_hold_7`, `demolish_10`, `contract_pop_150`, `contract_stars_2`, `contract_build_6`, `contract_clean_80`) so existing tracker/board tests keep their assumptions.
- Added 12 contract definitions to the rotating pool and used every trophy metal from Bronze through Galactic at least once.
- Deduplicated catalog initialization so `BuildAll()` feeds both `All` and the id index instead of rebuilding for the dictionary.
- Added `ChallengeCatalogTests` coverage for launch count, contract pool size, unique ids, subject coverage, and metal coverage.

Verification:

- `Unity.exe -batchmode -projectPath ... -runTests -testPlatform EditMode` did not reach tests because the dirty workspace currently has a pre-existing compile error: `Assets/Scripts/UI/TowerHudController.cs(4000,51): error CS1061: 'TowerSimulation' does not contain a definition for 'EffectiveCleanliness'`.
