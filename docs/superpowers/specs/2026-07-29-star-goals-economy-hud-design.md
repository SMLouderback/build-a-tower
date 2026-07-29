# Build-A-Tower — Star Goals, Instant Promote, Economy HUD & Retune

**Date:** 2026-07-29  
**Status:** Approved  
**Depends on:** Slice #4 (economy + stars 0–2)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first

## 1. Goals

Improve Slice #4 clarity and balance so players always know:

1. What they need for the **next star**
2. When a star is **earned** (immediately) vs **lost** (quarterly only)
3. What each room **costs** and how it **pays** (one-time vs recurring)

Also retune build/income/upkeep numbers toward SimTower-aligned values that stay fun for the current feature set. Full rent tiers, refunds, and traffic income remain out of scope; economics may be revisited later when more systems exist.

### Success criteria

In Play Mode a player can:

1. See next-star requirements (pop / stress / facilities) with current vs target values.
2. Gain a star as soon as criteria are met (no wait for day 90).
3. Lose a star **only** on the quarterly review (days 90, 180, …).
4. See compact cost/income tags on room buttons and fuller detail for the selected room tool.
5. Experience retuned costs/incomes/upkeep that make offices the early earner, condos a one-shot cash spike, and elevators a meaningful investment.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Promote cadence | Event-driven + daily safety net (**option C**) |
| Demote cadence | Quarterly review only |
| Next-star HUD | Always show progress toward `CurrentStars + 1` (or “Max ★” at 2) |
| Cost/income display | Both: compact on buttons + full under selected tool (**option C**) |
| Economy numbers | SimTower-scaled daily table below; revisit later with deeper systems |
| Rent-tier UI / condo refunds / 3-day ledger | Out of scope |
| Retail traffic income | Still $0 |

## 3. Stars

### 3.1 Criteria (unchanged)

**Earn / keep 1★**

| Metric | Threshold |
|--------|-----------|
| Population | ≥ 10 |
| Average stress | ≤ 40 |
| Facilities | Lobby present |

**Earn / keep 2★**

| Metric | Threshold |
|--------|-----------|
| Population | ≥ 30 |
| Average stress | ≤ 25 |
| Facilities | Lobby + ≥ 1 elevator shaft |

Population = agent count (existing). Stress = `AgentSystem.AverageStress`.

### 3.2 API split

Replace the single promote+demote `Evaluate` path with:

| Method | When | Behavior |
|--------|------|----------|
| `TryPromote(grid, avgStress, population)` | Events + daily | If `CurrentStars < Max` and next tier met → `CurrentStars++`. **Never demotes.** |
| `EvaluateQuarterly(grid, avgStress, population)` | Day 90, 180, … | If current tier fails → demote one. Then call promote once as same-day safety. |

`ForceStars` / `CanBuild` unchanged.

### 3.3 Promote call sites

Call `TryPromote` when:

1. **Grid / homes change** — after `OnGridChanged` / `SyncHomes` (new agents, elevator placed).
2. **Daily midnight** — each day roll in `OnDayRolled` (safety net for stress drift).
3. **Stress-sensitive path** — if stress is already sampled daily via (2), no per-tick poll. Optional: call after agent tick batches only if stress can drop mid-day enough to unlock; MVP may rely on daily + events. Prefer also calling `TryPromote` from the end of `AgentSystem` daily-relevant updates if stress changes without a day roll — simplest MVP: events (1) + daily (2) are enough; stress improvements overnight are caught at midnight.

Do **not** poll every sim tick.

### 3.4 Quarterly demotion

On day multiples of `StarSystem.QuarterDays` (90): call `EvaluateQuarterly` instead of (or after) the daily `TryPromote` for that day. Order: demote check first, then promote.

### 3.5 HUD — next star goals

Under the existing `Stars: N/2` line, show one progress line, e.g.:

- At 0★ aiming for 1★: `Next ★: Pop 8/10 · Stress 22≤40 · Lobby ✓`
- At 1★ aiming for 2★: `Next ★: Pop 20/30 · Stress 18≤25 · Elevator ✗`
- At 2★: `Next ★: Max`

Helpers on `StarSystem` (pure string or structured snapshot) keep HUD dumb.

Optional: show `LastResult` from last quarterly review on a second short line when non-empty.

## 4. Cost & income HUD

### 4.1 Selected tool (full)

When a room type is selected, under the Tool line:

- `Cost: $40,000`
- Income by `IncomeModel`:
  - `QuarterlyRent` / `NightlyRate` → `Income: $3,000 / day` (when occupied)
  - `UpfrontSale` → `Income: $150,000 once` (first resident)
  - `None` / `TrafficVariable` → `Income: —`
- Elevator shafts: also `Upkeep: $3,000 / day`
- Star-locked tools: keep `Needs N★` help; still show cost/income

### 4.2 Room buttons (compact)

Two-line or short compound labels:

- `Office` / `$40k · $3k/d`
- `Condo` / `$80k · $150k once`
- `Elevator` / `$100k/fl · -$3k/d`
- Star-locked: keep `(N★)` and grey-out

### 4.3 Formatting

Shared formatter (e.g. `RoomEconomyFormat`):

- Buttons: abbreviate thousands as `$40k`
- Detail: full `$40,000`
- Elevator build shown as per-floor cost when `buildCost` is per floor segment

## 5. Economy retune (asset + constant values)

Aligned with SimTower / tower-together, adapted to **daily** midnight sweeps (quarterly SimTower amounts ≈ ÷ 3).

| Room | `buildCost` | `baseIncome` / upkeep | Notes |
|------|-------------|------------------------|-------|
| Lobby | 5_000 / cell | 0 | Unchanged |
| Stairs | 5_000 | 0 | Unchanged |
| Office | 40_000 | 3_000 / day | Was 5_000/day |
| Premium Office | 60_000 | 5_000 / day | Was 80k / 12k |
| Hotel | 20_000 | 2_000 / day | Unchanged income |
| Premium Hotel | 50_000 | 4_500 / day | Was 40k / 4k |
| Condo | 80_000 | 150_000 once | Was 250_000 sale |
| Premium Condo | 120_000 | 200_000 once | Was 160k / 500k |
| Elevator | **100_000 / floor** | **Upkeep 3_000 / day** | Was 20k/floor build, 10k/day upkeep; min 2 floors ≈ $200k |
| Retail | 100_000 | 0 | Unchanged placeholder |
| Starting funds | 2_000_000 | — | Unchanged |

Update:

- All matching `Assets/ScriptableObjects/Rooms/*.asset` and `Assets/Resources/Rooms/*.asset` copies
- `EconomySystem.ElevatorDailyUpkeep` → `3_000`
- README play steps / Slice 4 tuning notes if they cite old numbers
- EditMode tests that hard-code old upkeep or example incomes

**Explicit non-goal:** this is a first pass. Revisit economics when more features (retail traffic, rent tiers, multi-car elevators, etc.) land.

## 6. Implementation sketch

| Area | Change |
|------|--------|
| `StarSystem.cs` | `TryPromote`, `EvaluateQuarterly`, goal snapshot / format helpers; deprecate combined demote+promote `Evaluate` or make it call quarterly path only for tests |
| `TowerSimulation.cs` | Daily `TryPromote`; quarterly `EvaluateQuarterly`; promote after `OnGridChanged` |
| `TowerHudController.cs` | Next-star line; selected cost/income; button tags |
| New small helper | `RoomEconomyFormat` (or static methods on a UI util) |
| Room assets + `ElevatorDailyUpkeep` | Retune table |
| Tests | Promote without demote on `TryPromote`; demote only on quarterly; formatter strings; upkeep constant |
| README | Star timing + example costs |

## 7. Verification

- EditMode: `TryPromote` grants 1★ when met and does not demote when stress is high; `EvaluateQuarterly` demotes; elevator upkeep = 3_000.
- Play Mode: place offices until pop/stress hit 1★ before day 90; star bumps immediately; fail criteria until day 90 without losing the star; day 90 demotes if still failing.
- HUD: next-star line updates; room buttons show `$` tags; selected office shows cost + daily income; selected elevator shows upkeep.

## 8. Out of scope

- Stars 3–5
- Rent-tier player UI
- Condo refunds / repurchase
- Per-car elevator pricing
- Retail `TrafficVariable` payouts
- Evaluation / traffic overlays (Slice 5)

## 9. References

- Slice #4 design: `docs/superpowers/specs/2026-07-29-build-a-tower-slice4-design.md`
- tower-together: `docs/reference/tower-together/specs/ECONOMY.md`, `facility/OFFICE.md`, `facility/HOTEL.md`, `facility/CONDO.md`
- SimTower community references used for tuning alignment (GameFAQs / Relentless Optimizer tables)
