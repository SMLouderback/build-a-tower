# Build-A-Tower — Agent Disposable Income & Market Climate

**Date:** 2026-07-31  
**Status:** Approved  
**Depends on:** Commercial visit traffic (E1); price tiers / `PricePricing`; `GameClock`  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** **Deeper economy** → higher stars → more transit → evaluation/heatmaps → polish

## 1. Goals

Give agents a **daily disposable-income budget** so higher-tier living rooms (Premium vs Basic) feed richer shoppers, and introduce a tower-wide **market climate** that shifts each **Gregorian month**. Climate scales spend and how many rent/sale price tiers the market tolerates. The HUD shows a real calendar starting **1 January 2000**.

### Success criteria

In Play Mode a player can:

1. See the HUD date advance from **Sat 01 Jan 2000** (Gregorian weekday + D Mon Y + clock).
2. See **market climate** start at **Normal** and sometimes change on the **1st of a new month**.
3. Observe Premium home agents afford restaurants more often than Basic home agents; street visitors stay on a low budget.
4. See shop midnight income equal **dollars spent**, not flat `visits × baseIncome`.
5. Feel Boom raise spend + price tolerance and Recession do the opposite (alongside stars).

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Wealth source | **Room type band only** (Basic vs Premium); not price tier |
| Shop payout | Spend `random(1 … min(shop$/visit, remaining))`; skip shops agent cannot afford |
| Disposable shape | **Range per band**, rolled per agent per day |
| Climate scale | **5 steps:** Recession → Slow → Normal → Strong → Boom |
| Climate cadence | Each **Gregorian month** boundary |
| Climate start | **Normal** |
| Calendar | Epoch **2000-01-01**; HUD shows day/month/year |
| Architecture | Clock date + `MarketClimate` + agent daily budget + shop spend tally + climate-aware demand |

## 3. Calendar

- `DayIndex == 0` ⇒ **1 January 2000**.
- Derive `DateTime` (or equivalent) as `new DateTime(2000, 1, 1).AddDays(DayIndex)`.
- Weekday in HUD must match Gregorian (1 Jan 2000 was **Saturday** — replace the current Mon-based `DayIndex % 7` mapping).
- HUD format (example): `Sat 01 Jan 2000  06:00`.
- Midnight / `DayRolled` behavior unchanged for economy sweeps.
- Emit **`MonthRolled`** (or equivalent) when the calendar month changes after a day roll.

## 4. Market climate

### 4.1 Steps

| Index | Name |
|-------|------|
| 0 | Recession |
| 1 | Slow |
| 2 | Normal |
| 3 | Strong |
| 4 | Boom |

### 4.2 Monthly random walk

On month rollover, roll a delta and clamp to `[0, 4]`:

| Delta | Suggested weight |
|-------|------------------|
| 0 (stay) | ~40% |
| ±1 | ~45% total (~22.5% each) |
| ±2 | ~15% total (~7.5% each) |

Exact weights are tuneable; keep stay + ±1 dominant.

### 4.3 Spend multiplier

Applied to the agent’s rolled daily disposable (after band roll):

| Climate | Multiplier |
|---------|------------|
| Recession | 0.70 |
| Slow | 0.85 |
| Normal | 1.00 |
| Strong | 1.15 |
| Boom | 1.30 |

### 4.4 Price comfort offset

Existing `PricePricing.ComfortMaxTier(stars)` stays the stars baseline. Climate adds an offset before demand checks:

| Climate | Tier offset |
|---------|-------------|
| Recession | −2 |
| Slow | −1 |
| Normal | 0 |
| Strong | +1 |
| Boom | +2 |

Clamp result to `Low…Max`. `DemandChance` / condo spawn / occupancy use **stars + climate**. Rent **payout multipliers** for the chosen tier are unchanged.

Market hint should mention climate when relevant, e.g. `Market: OK for 2★ · Strong economy`.

## 5. Disposable income

### 5.1 Bands

| Band | Who | Base daily range (before climate) |
|------|-----|-----------------------------------|
| Street | `StreetVisitor` | $20–$60 |
| Basic | Non-premium Office / Hotel / Condo homes | $40–$100 |
| Premium | Premium living rooms (`*Premium` and/or `requiredStars ≥ 2` for Office/Hotel/Condo) | $90–$200 |

Price tier on the room does **not** change the band.

### 5.2 Daily refill

- Each calendar day (on day roll or first schedule tick that day): roll uniform integer in band range × climate multiplier → `DisposableToday` / `DisposableRemaining` (round to int, floor at 0).
- New agents spawned mid-day get a fresh roll for that day.

### 5.3 Shop visit rules

Shop catalog prices unchanged: Fast Food **$40**, Retail **$80**, Restaurant **$120** (`baseIncome` = list price / max charge).

When choosing a commercial target:

1. Candidate shops must be open, have capacity, be reachable, **and** `PayPerVisit(shop) ≤ DisposableRemaining`.
2. If none, skip the trip (same as today when no shop available).

On **completed** visit (after dwell):

1. `spent = rng(1 … min(PayPerVisit, DisposableRemaining))`.
2. Subtract `spent` from remaining.
3. Add `spent` to that shop’s **day earnings** (not merely increment visit count for payout).
4. Still increment visit count for UI.

### 5.4 Midnight shop income

Replace `visitsToday × baseIncome` with **sum of spend** recorded that day for `TrafficVariable` rooms. Reset earnings with the visit counter.

## 6. Systems / files (expected)

| Area | Change |
|------|--------|
| `GameClock.cs` | Date from epoch; Gregorian weekday; `MonthRolled`; `FormatHud` date |
| `MarketClimate.cs` (new) or `EconomySystem` | Step, monthly walk, multipliers, comfort offset |
| `Agent.cs` | Disposable fields + day stamp |
| `AgentWealth.cs` (new) or helpers | Band resolve, roll, afford filter, spend |
| `AgentSystem.cs` | Refill; filter shops; record spend on visit complete |
| `RoomInstance.cs` | Day shop earnings accumulator |
| `EconomySystem.cs` | Pay shop day earnings; wire climate into demand |
| `PricePricing.cs` | Climate-aware comfort / demand / hints |
| `TowerHudController.cs` | Date string; climate display |
| Tests | Date/month roll; climate clamp; band rolls; afford skip; spend tally; demand offset |
| README | Calendar, climate, disposable shop spend |

## 7. Out of scope

- Price tier influencing wealth band  
- Multi-shop trips per day / savings across days  
- New fancy shop types  
- Climate history charts / player-controlled climate  
- Changing stairs span, elevators, or star formula beyond demand comfort

## 8. Verification

- EditMode: `DayIndex` 0 → 2000-01-01 Saturday; day 31 → 2000-02-01 fires month roll.  
- EditMode: climate walk stays in 0–4; Normal start.  
- EditMode: Basic remaining $30 cannot target Restaurant ($120); Premium with $150 can.  
- EditMode: completed visit reduces remaining and adds spend to shop day earnings; midnight pays that sum.  
- EditMode: Boom comfort > Normal comfort at same stars; Recession lower.  
- Play Mode: HUD date + climate readable; premium tower supports restaurant traffic better in Strong/Boom.

## 9. Roadmap note

This is a **deeper economy** slice: richer residents + macroeconomic cycle feeding shops and rent tolerance. Later: finer wealth bands, shop price tiers, or multi-visit days.
