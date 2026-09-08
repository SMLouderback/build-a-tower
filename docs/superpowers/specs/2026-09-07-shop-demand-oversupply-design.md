# Build-A-Tower — Shop Demand and Oversupply

**Date:** 2026-09-07  
**Status:** Approved — implementation plan ready  
**Depends on:** Existing commercial trip scheduling, `AgentWealth`, `ShopVisitRules`, `EconomySystem`, agent stress  
**Engine target:** Unity 6000.4.7f1, desktop/Editor-first

## 1. Goal

Make shop performance depend on the occupied tower population rather than an effectively unlimited stream of random trips. Food and retail shops compete for finite, wealth-tiered demand; oversupplied shops can lose money through daily operating upkeep, and unmet demand increases occupant stress.

This is the first demand-system slice. It establishes reusable demand pools and history for later condo → commercial → hotel demand, parking demand, venue demand, graphs, and heatmaps.

## 2. Success criteria

1. Occupied office desks, condo units, and hotel beds generate explicit daily Food and Retail demand.
2. Demand is divided by customer wealth tier: Budget, Mid, and Premium.
3. Shops compete only inside their family/tier pool and cannot create additional demand by being built.
4. Commercial and street trips consume finite demand only after a valid trip is scheduled.
5. Up to 25% of unmet demand may spill down one tier, never upward or down two tiers.
6. Every placed shop pays daily upkeep equal to 50% of its pay cap, allowing negative daily net.
7. Remaining unmet demand adds matching-tier and tower-wide stress.
8. HUD data explains pool demand and each selected shop’s profit/loss.
9. Daily demand history is retained for later graph and heatmap work.

## 3. Demand pools

There are six independent pools:

| Family | Tiers |
|--------|-------|
| Food | Budget, Mid, Premium |
| Retail | Budget, Mid, Premium |

Shop family comes from `RoomTypeSO.ResolvedBuildSubgroup()`:

- `BuildSubgroup.Food` → Food
- `BuildSubgroup.Retail` → Retail

Shop tier comes from `requiredStars`:

| Required stars | Demand tier |
|----------------|-------------|
| 0 | Budget |
| 1–2 | Mid |
| 3+ | Premium |

This mapping is intentionally automatic. Version 1 does not add a per-shop tier override.

## 4. Demand generation

Demand is generated once at the start of each game day from actual occupied agents, not room capacity.

### 4.1 Per-occupant weights

| Occupant source | Food units | Retail units |
|-----------------|-----------:|-------------:|
| Office worker | 0.50 | 0.10 |
| Condo resident | 0.40 | 0.30 |
| Hotel guest | 0.70 | 0.25 |

Each occupant’s generated units enter the pool matching their wealth:

| `WealthBand` | Demand tier |
|--------------|-------------|
| Street, Basic | Budget |
| Mid | Mid |
| Upper, Premium | Premium |

Contributions are summed as floating-point units per pool, multiplied by the existing market-climate spend multiplier (approximately 0.7×–1.3×), and rounded to the nearest whole visit opportunity using midpoint-away-from-zero rounding. Generation is deterministic for the same population and climate.

Stars affect wealth composition through existing systems and street baseline, rather than multiplying occupied demand again.

### 4.2 Street baseline

Street demand prevents a small starter tower from having completely inert shops while avoiding free Premium demand.

Before climate scaling, the daily baseline is:

| Pool | Units |
|------|------:|
| Food / Budget | `2 + stars` |
| Retail / Budget | `1 + floor(stars / 2)` |
| Food / Mid | `max(0, stars - 1)` |
| Retail / Mid | `max(0, stars - 2)` |
| Food / Premium | 0 |
| Retail / Premium | 0 |

Street-generated units are part of the same finite pools as occupant demand.

## 5. Consumption and shop competition

### 5.1 Trip scheduling

When an eligible resident, worker, guest, event visitor, or street visitor attempts a commercial trip:

1. Resolve requested family (Food or Retail) using the role weights below.
2. Resolve the customer’s native demand tier from wealth.
3. Find shops that are open, reachable, affordable, not full, and assigned to that family/tier.
4. Select a destination using the competition weight below.
5. Schedule the trip through existing pathing.
6. Consume one demand unit only after scheduling succeeds.

Closed, broken, full, unreachable, or unaffordable shops receive no allocation. Failed pathing consumes no demand.

Existing dwell times, visitor slots, opening hours, affordability, and rolled spending remain authoritative.

### 5.2 Role-weighted family choice

The approved per-occupant generation weights also define trip-family preference:

| Role | Food | Retail |
|------|-----:|-------:|
| Office worker | 83% | 17% |
| Condo resident | 57% | 43% |
| Hotel guest | 74% | 26% |
| Street visitor | Proportional to remaining eligible Food/Retail demand |
| Event visitor | Proportional to remaining eligible Mid Food/Retail demand |

Percentages for office, condo, and hotel are the normalized ratios of their Food/Retail generation weights, rounded to whole percentages.

Only families with remaining serviceable demand and at least one eligible shop participate. If the preferred family has no valid option, use the other valid family. If neither family is valid, no trip is scheduled and no demand is consumed.

### 5.3 Competition weight

Eligible shops use:

```text
weight = max(1, free visitor slots) × fairness bonus × origin modifier
fairness bonus = 1 + 1 / (1 + visits today)
```

For internal tower occupants, `origin modifier = 1`.

For street visitors, `origin modifier = max(0.1, streetVisitWeight)`, preserving the field’s existing street-only meaning.

This gives under-visited shops a modest advantage without guaranteeing equal outcomes. More shops do not increase total pool demand; they divide the same finite opportunities.

Unused demand expires at midnight and never banks into a future day.

## 6. Tier spillover

Native demand is used first. If it cannot be served, a capped portion may move down exactly one tier:

- Premium → Mid
- Mid → Budget
- Budget → nowhere

Rules:

- At most 25% of a pool’s generated units may spill out.
- Premium cannot spill directly or indirectly to Budget.
- Demand never moves upward.
- Food and Retail never spill into each other.
- Spill-in and spill-out are tracked separately.
- Unused spill capacity expires at midnight.

## 7. Shop upkeep and net profit

Every placed shop incurs daily operating upkeep:

```text
daily upkeep = round(baseIncome × 0.50)
daily net = credited visit earnings − daily upkeep
```

Midpoint values round away from zero.

Examples:

| Shop | Pay cap | Daily upkeep |
|------|--------:|-------------:|
| Taco Counter | $28 | $14 |
| Restaurant | $50 | $25 |
| Shoe Store | $65 | $33 |
| Fine Dining | $100 | $50 |

Upkeep behavior:

- Charged at midnight even with zero visits.
- Charged while broken or income-blocked.
- Recorded separately in the tower expense ledger and room lifetime expense.
- Visit earnings remain separate income and retain existing build-economy scaling.
- A selected shop can show a negative daily net.

`ShopDemandBalance.ShopUpkeepRate = 0.50f` is the single tuning constant.

## 8. Unmet-demand stress

Stress is applied once when the completed day is archived, after spillover and service are known.

For each tier:

```text
tier unmet ratio = remaining unmet Food + Retail units
                   divided by generated Food + Retail units for that tier

matching-tier stress = 6 × tier unmet ratio
tower unmet ratio = all remaining unmet units / all generated units
tower-wide stress = 2 × tower unmet ratio
```

Zero generated demand produces an unmet ratio of zero.

Recipients:

- Condo residents, office workers, and hotel guests receive tower-wide stress.
- Those whose wealth maps to the affected tier also receive matching-tier stress.
- Other persistent tower staff receive only tower-wide stress.
- Street visitors do not retain demand stress.
- Final stress is clamped to the existing 0–100 range.

Maximum demand stress is +8 per day for a matching-tier occupant, equal to current low-condition daily stress and below maximum crime stress.

## 9. Daily lifecycle

At day rollover:

1. Archive the completed day’s generated, served, spill, and unmet values.
2. Apply unmet-demand stress.
3. `EconomySystem` settles shop earnings and upkeep.
4. Generate the next day’s six pools from current occupied agents, stars, climate, and street baseline.
5. During the new day, successful trips consume those pools.

On simulation startup, initialize the current day after agent/grid setup so demand exists before the first commercial-trip window.

If multiple days are advanced in one update, each day follows the same archive → stress → economy → generate sequence without reusing a pool.

## 10. Data model and ownership

`TowerSimulation` owns one `ShopDemandSystem`.

Suggested public surface:

```csharp
public enum ShopDemandFamily { Food, Retail }
public enum ShopDemandTier { Budget, Mid, Premium }

public readonly struct ShopDemandKey
{
    public ShopDemandFamily Family { get; }
    public ShopDemandTier Tier { get; }
}

public sealed class ShopDemandPool
{
    public int Generated { get; }
    public int Served { get; }
    public int SpilledIn { get; }
    public int SpilledOut { get; }
    public int Remaining { get; }
    public int Unmet { get; }
}

public sealed class ShopDemandSystem
{
    public void BeginDay(
        IReadOnlyList<Agent> agents,
        int stars,
        float climateMultiplier);

    public bool CanServe(
        RoomTypeSO shopType,
        WealthBand customerWealth);

    public bool TryConsume(
        RoomTypeSO shopType,
        WealthBand customerWealth);

    public void ArchiveAndApplyStress(IReadOnlyList<Agent> agents);
    public ShopDemandSnapshot Snapshot { get; }
}
```

Implementation may refine immutable/read-only types, but demand mutation remains inside `ShopDemandSystem`.

Maintain a 30-day ring history of pool snapshots for the later demand graph.

## 11. Player feedback

### 11.1 Demand summary

Expose for each of the six pools:

- Generated
- Served
- Spilled in
- Spilled out
- Remaining/unmet
- Saturation percentage (`served / generated`, zero-safe)

### 11.2 Selected shop

Show:

- Demand pool assignment
- Visits yesterday
- Revenue yesterday
- Daily upkeep
- Net profit/loss
- Competition status

Competition status:

- **Underserved:** pool unmet ratio ≥ 20%
- **Oversupplied:** unmet ratio < 20% and generated units are fewer than open shops in that pool
- **Balanced:** otherwise

The full graph and heatmap UI are deferred, but the 30-day history must be ready for them.

## 12. Testing

EditMode coverage:

1. Family and star-tier mappings.
2. Wealth-tier mappings.
3. Deterministic generation from known office/condo/hotel occupant mixes.
4. Climate scaling and street baselines.
5. Successful-trip-only consumption.
6. Finite competition across multiple shops.
7. Street-only `streetVisitWeight`.
8. 25% one-level spill cap.
9. No upward, cross-family, or Premium → Budget spill.
10. Daily upkeep rounding, ledger expense, lifetime expense, and negative net.
11. Matching-tier and tower-wide stress, zero-safe ratios, street exemption, and 100 cap.
12. Archive/reset and 30-day history capacity.
13. HUD formatting for demand summary and selected-shop economics.

Play Mode smoke:

1. Starter tower has small Budget demand but no free Premium demand.
2. Build multiple Budget Food shops; total visits remain finite and divide among them.
3. Add occupied condos/offices/hotels; relevant demand rises next day.
4. Empty/oversupplied shops report upkeep and negative net.
5. Premium demand spills at most 25% into Mid.
6. Unmet pools raise average stress at rollover.

## 13. Deferred work

- Condo → commercial → hotel fill cascade
- Luxury-driven parking demand
- Premium-office conference/event demand
- Full demand graph and economic heatmaps
- Shop closure, bankruptcy, or mothballing
- Dynamic shop pricing
- Room-art regeneration if footprints are widened later

## 14. Self-review

- [x] Six pool axes and all mappings are explicit.
- [x] Generation constants and street baseline are specified.
- [x] Competition cannot create demand.
- [x] Spillover is bounded and cannot cascade two tiers.
- [x] Upkeep and stress formulas are zero-safe and testable.
- [x] Existing affordability, pathing, dwell, spend, and street-weight semantics are preserved.
- [x] Graph-ready history is included without expanding this slice into full graph UI.
- [x] Parking, venue, and cross-family demand remain deferred.
