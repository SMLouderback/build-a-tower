# Build-A-Tower — Numeric Rebalance & Difficulty Economy

**Date:** 2026-08-05  
**Status:** Implemented  
**Depends on:** Main menu & difficulty shell (`GameSession` / `GameDifficulty` / `BuildEconomy`); room catalogs (office / hotel / condo / commercial); `EconomySystem` payouts; star gates (`StarSystem`)  
**Parent:** Post-menu roadmap item 1 (numeric rebalance)  
**Follow-ups:** Demand/climate graph + heatmaps; visual polish; above-ground parking (lower priority); fill-rate/climate tuning if still needed after play

## 1. Goals

1. Slow the **cash snowball** on Normal (too rich too fast).  
2. Make room types **more even** within bands (fewer auto-win / worthless outliers).  
3. Improve **star progression feel** via economy only — **do not change** star population, stress, or `requiredStars` gates.  
4. Make **Easy / Normal / Hard / Extreme** meaningfully different via starting funds + cost/income multipliers on a retuned Normal baseline.  
5. Keep **Sandbox** free builds (existing behavior).

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Architecture | **DifficultyProfile** + retuned Normal `RoomTypeSO` assets |
| Normal squeeze | **Medium** — ~40–50% higher costs / lower incomes; start ~**$1.125M** |
| Difficulty spread | **Clear steps** (table in §3) |
| Primary levers | Build costs + incomes (incl. condo sales); not fill/climate this slice |
| Star gates | **Unchanged** |
| Sandbox | Free place; income mult **1.0** |
| Scaffold | Scale with Normal squeeze (e.g. **$500 → ~$750**); still free in Sandbox |

## 3. Difficulty profile

### Table

| Difficulty | Start $ | Build cost mult | Income / condo-sale mult |
|------------|---------|-----------------|--------------------------|
| Sandbox | Display wallet OK at Normal start or $2M; spend unused | **0** (free builds) | **1.0** |
| Easy | **1,500,000** | **0.75** | **1.25** |
| Normal | **1,125,000** | **1.0** | **1.0** |
| Hard | **900,000** | **1.25** | **0.80** |
| Extreme | **600,000** | **1.50** | **0.65** |

### Wiring

- On tower boot, set wallet from `DifficultyProfile.StartingFunds(GameSession.Difficulty)` (override serialized `$2M` default).  
- `BuildEconomy` (or successor): effective build cost = `ceil(nominal * BuildCostMult)` except Sandbox (free). Afford checks use the same effective cost.  
- At **payout** time (office/hotel rent, condo sale, commercial visit income, and other `baseIncome`-driven player income): multiply by `IncomeMult`.  
- Upkeep / expenses: either leave at nominal or apply the same income mult inversely only if needed for Hard fairness — **default: expenses stay nominal** (Hard hurts income more than it discounts bills). Document in implementation if playtest demands expense scaling.  
- Grace refunds: refund what was actually charged (effective spend), not raw SO cost.  
- Star criteria and `RoomTypeSO.requiredStars` untouched.

### Menu copy

Replace “economy tuning coming soon” hints:

- **Sandbox** — Free builds — test layouts without money pressure.  
- **Easy** — More starting cash; cheaper builds; stronger income.  
- **Normal** — Tuned baseline.  
- **Hard** — Tighter cash; costlier builds; weaker income.  
- **Extreme** — Scarce cash; expensive builds; lean income.

## 4. Normal catalog retune

### Payback targets (authoring guide)

Assume Normal price tier, full occupancy, ignore climate variance:

| Category | Target |
|----------|--------|
| Office / Hotel | **45–75 days** of rent ≈ build cost |
| Condo | Sale (`baseIncome`) ≈ **1.6–2.2×** build cost |
| Shops / commercial | **60–90 days** at healthy traffic (`baseIncome` tune) |
| Structure / transit / parking / ops / scaffold | Cost-only; raise costs ~**40–50%**; no income |

### Method

1. Global Normal pass on living/commercial catalogs: increase `buildCost`, decrease `baseIncome` into the medium band.  
2. Hand-fix outliers so entry tiers aren’t traps and Upper/Penthouse aren’t free money.  
3. Keep size, occupants, `luxuryBand`, `requiredStars`, colors, ids.  
4. Keep **Resources** and **ScriptableObjects** room assets in sync.  
5. Update `TowerGrid.ScaffoldBuildCost` (and scaffolding type) to ~**750**.

## 5. Non-goals

- Changing star population / stress / quarterly rules  
- Fill-rate or climate formula retune  
- Demand graphs / heatmaps  
- Save/load  
- Final visual polish  
- Above-ground parking  
- Per-difficulty duplicate room assets  

## 6. Acceptance criteria

1. Normal starts at **$1,125,000** (± trivial rounding). Easy/Hard/Extreme match §3 starts.  
2. Identical nominal room: Hard charges more to build than Normal; Easy less.  
3. Identical payout event: Hard pays less than Normal; Easy more.  
4. Sandbox placement does not debit; income mult 1.0.  
5. Star thresholds and room `requiredStars` unchanged from pre-slice values.  
6. Spot-check: Normal office/hotel paybacks in **45–75d**; condo sale/cost in **1.6–2.2×**.  
7. EditMode tests for profile values + spend mult + one income mult path.  
8. Main menu difficulty hints match §3 copy.

## 7. Implementation sketch (non-binding)

| Piece | Role |
|-------|------|
| `DifficultyProfile` | Lookup start $, cost mult, income mult |
| `BuildEconomy` | Apply cost mult; Sandbox free; recorded spend = charged |
| `BuildController` Awake | Wallet from profile |
| `EconomySystem` / sale / visit payouts | Apply income mult |
| Room `.asset` pass | Normal retune both Resources + ScriptableObjects |
| `TowerGrid.ScaffoldBuildCost` | ~750 |
| `MainMenu.uxml` hints | Updated copy |
| Tests | Profile + BuildEconomy + sample payout |

## 8. Roadmap reminder

Next after this: **demand/climate graph + heatmaps** (crime, noise, traffic, economic) → **visual polish**. Above-ground parking remains lower priority.
