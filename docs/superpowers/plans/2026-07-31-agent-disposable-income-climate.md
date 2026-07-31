# Agent Disposable Income & Market Climate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agents roll daily disposable income by Basic/Premium/Street band (scaled by market climate); shops earn spent dollars; climate shifts each Gregorian month and adjusts price tolerance; HUD shows date from 1 Jan 2000.

**Architecture:** `GameClock` exposes Gregorian date + `MonthRolled`. `MarketClimate` holds the 5-step rating and APIs for spend multiplier / comfort offset. `AgentWealth` resolves bands and spends. `AgentSystem` filters affordable shops and records spend into `RoomInstance` day earnings; `EconomySystem` pays that sum at midnight. `PricePricing` demand/hints take climate.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode, existing `GameClock` / `EconomySystem` / `AgentSystem` / `PricePricing`

## Global Constraints

- Wealth from **room type band only** (Basic / Premium / Street) — not price tier
- Shop spend: `random(1 … min(shop$/visit, remaining))`; skip unaffordable shops
- Climate: Recession→Slow→Normal→Strong→Boom; start **Normal**; roll on **Gregorian month** change
- Calendar epoch **2000-01-01**; HUD day/month/year + Gregorian weekday
- Shop midnight = **sum of spend**, not `visits × baseIncome`
- Spec: `docs/superpowers/specs/2026-07-31-agent-disposable-income-climate-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Time/GameClock.cs` | Date, `MonthRolled`, HUD format |
| `Assets/Scripts/Economy/MarketClimate.cs` | Step, monthly walk, multipliers, offsets |
| `Assets/Scripts/Economy/PricePricing.cs` | Climate-aware comfort / demand / hints |
| `Assets/Scripts/Economy/AgentWealth.cs` | Band resolve, roll, afford, spend amount |
| `Assets/Scripts/Agents/Agent.cs` | Disposable fields |
| `Assets/Scripts/Core/RoomInstance.cs` | `ShopEarningsToday` |
| `Assets/Scripts/Agents/AgentSystem.cs` | Refill, filter, spend on visit |
| `Assets/Scripts/Economy/EconomySystem.cs` | Pay earnings; demand with climate |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Wire climate + month roll |
| `Assets/Scripts/UI/TowerHudController.cs` | Date + climate |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` | Show shop earnings |
| Tests + `README.md` | Coverage + play notes |

---

### Task 1: Gregorian calendar on GameClock

**Files:**
- Modify: `Assets/Scripts/Time/GameClock.cs`
- Test: `Assets/Tests/EditMode/GameClockCalendarTests.cs` (create)

**Interfaces:**
- Produces: `DateTime CalendarDate` (date part from epoch + DayIndex)
- Produces: `event Action MonthRolled`
- Produces: `FormatHud()` → e.g. `Sat 01 Jan 2000  06:00`
- Epoch: `new DateTime(2000, 1, 1)` when `DayIndex == 0` (Saturday)

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Day0_is_saturday_1_jan_2000()
{
    var clock = new GameClock();
    Assert.AreEqual(new DateTime(2000, 1, 1), clock.CalendarDate.Date);
    Assert.AreEqual(DayOfWeek.Saturday, clock.CalendarDate.DayOfWeek);
}

[Test]
public void Crossing_jan_31_fires_MonthRolled()
{
    var clock = new GameClock(startMinuteOfDay: 23 * 60 + 59);
    // Advance DayIndex to 30 (31 Jan), then one more day → Feb 1
    var months = 0;
    clock.MonthRolled += () => months++;
    // ... advance to day 31 boundary
    Assert.AreEqual(1, months);
    Assert.AreEqual(2, clock.CalendarDate.Month);
}
```

- [ ] **Step 2: Implement** — track previous month on day roll; invoke `MonthRolled` when month changes; fix weekday via `CalendarDate`; update `FormatHud`.

- [ ] **Step 3: Tests PASS — Commit** `feat: Gregorian calendar and month roll on GameClock`

---

### Task 2: MarketClimate

**Files:**
- Create: `Assets/Scripts/Economy/MarketClimate.cs`
- Test: `Assets/Tests/EditMode/MarketClimateTests.cs`

**Interfaces:**
- Produces: `MarketClimate` with `Step` (0–4), `Name`, starts at Normal (2)
- Produces: `float SpendMultiplier` — Recession 0.7 … Boom 1.3
- Produces: `int ComfortTierOffset` — −2…+2
- Produces: `void OnMonthRolled(System.Random rng)` — weighted stay/±1/±2, clamp 0–4
- Produces: `static readonly string[] Labels`

```csharp
public sealed class MarketClimate
{
    public const int Recession = 0, Slow = 1, Normal = 2, Strong = 3, Boom = 4;
    public int Step { get; private set; } = Normal;
    public string Name => Labels[Step];
    public float SpendMultiplier => Step switch { 0 => 0.7f, 1 => 0.85f, 3 => 1.15f, 4 => 1.3f, _ => 1f };
    public int ComfortTierOffset => Step - Normal; // -2..+2
    public void OnMonthRolled(Random rng) { /* weighted delta */ }
}
```

- [ ] **Step 1: Failing tests** — start Normal; multipliers; offset; many rolls stay in 0–4; forced delta clamp at ends.

- [ ] **Step 2: Implement + Commit** `feat: market climate monthly random walk`

---

### Task 3: Climate-aware PricePricing

**Files:**
- Modify: `Assets/Scripts/Economy/PricePricing.cs`
- Modify callers that use `DemandChance` / `MarketHint` / `ComfortMaxTier` for occupancy (`EconomySystem`, `AgentSystem`, HUD)
- Test: `Assets/Tests/EditMode/PricePricingClimateTests.cs` or extend existing price tests

**Interfaces:**
- Produces: `ComfortMaxTier(int stars, int climateOffset)` — clamp Low…Max after stars baseline + offset
- Produces: `DemandChance(int tier, int stars, int climateOffset)`
- Produces: `MarketHint(int tier, int stars, int climateOffset)` — include climate name when offset ≠ 0
- Keep old overloads forwarding with `climateOffset: 0` **or** update all call sites in this task (prefer update all call sites + simulation climate)

- [ ] **Step 1: Failing tests** — at 2★, Boom comfort > Normal; Recession lower; demand improves under Boom for High tier.

- [ ] **Step 2: Implement + wire `TowerSimulation` to hold `MarketClimate`, subscribe `MonthRolled`, pass offset into economy/agents/HUD.**

- [ ] **Step 3: Commit** `feat: price demand respects market climate`

---

### Task 4: AgentWealth + disposable fields + shop earnings

**Files:**
- Create: `Assets/Scripts/Economy/AgentWealth.cs`
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Test: `Assets/Tests/EditMode/AgentWealthTests.cs`

**Interfaces:**
- Produces: `enum WealthBand { Street, Basic, Premium }`
- Produces: `AgentWealth.ResolveBand(AgentRole role, RoomTypeSO homeType)`
- Produces: `int RollDailyDisposable(WealthBand band, float climateMult, Random rng)` — ranges Street 20–60, Basic 40–100, Premium 90–200 then × mult, round, ≥0
- Produces: `bool CanAfford(int remaining, RoomTypeSO shop)` — `PayPerVisit <= remaining`
- Produces: `int RollSpend(int remaining, RoomTypeSO shop, Random rng)` — 1…min(price, remaining)
- Produces on `Agent`: `DisposableRemaining`, `DisposableDayIndex` (or similar)
- Produces on `RoomInstance`: `ShopEarningsToday`, `RecordShopSpend(int)`, reset in `ResetVisitsToday`

Premium detect: living category + (`requiredStars >= 2` OR id/display contains "premium" case-insensitive).

- [ ] **Step 1: Failing tests** for bands, roll bounds, afford, spend, earnings accumulator.

- [ ] **Step 2: Implement + Commit** `feat: agent wealth bands and shop day earnings`

---

### Task 5: AgentSystem spend on commercial visits

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: extend `CommercialVisitTests.cs`

**Interfaces:**
- Consumes: `MarketClimate` (or spend multiplier + refill API injected)
- On spawn / day change: refill disposable via `AgentWealth`
- Shop pick: filter `CanAfford`
- On visit complete (where `RecordVisit` is today): `spent = RollSpend`; subtract; `RecordShopSpend(spent)`; still `RecordVisit()`

- [ ] **Step 1: Failing tests** — agent with $30 skips restaurant; spend reduces remaining and increases `ShopEarningsToday`.

- [ ] **Step 2: Implement — pass climate from simulation into `AgentSystem.Tick` or store reference set at construction.

- [ ] **Step 3: Commit** `feat: commercial visits spend disposable income`

---

### Task 6: EconomySystem pays shop earnings

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs` (earnings line)
- Test: update `CommercialVisitTests` midnight assertions

**Interfaces:**
- Replace `visitsToday * PayPerVisit` with `room.ShopEarningsToday`
- Demand / condo paths use climate offset (if not fully done in Task 3)

- [ ] **Step 1: Failing test** — two visits spending 25+40 → midnight income 65, not 2× list price.

- [ ] **Step 2: Implement + Commit** `feat: midnight shop income from spent dollars`

---

### Task 7: HUD + README

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `README.md`

- [ ] **Step 1:** Core strip shows `FormatHud()` date; Economy (or core) shows `Climate: Normal` (live name).

- [ ] **Step 2:** README — calendar from 2000, monthly climate, disposable shop spend, Basic vs Premium.

- [ ] **Step 3:** Roslyn typecheck Scripts + EditMode; Commit `docs: disposable income and climate play notes`

---

### Task 8: Closeout

- [ ] Spec success criteria checklist
- [ ] Push only when asked

## Spec coverage

| Spec requirement | Task |
|------------------|------|
| Calendar + MonthRolled + HUD date | 1, 7 |
| MarketClimate walk + multipliers | 2 |
| Climate × demand / hints | 3 |
| Wealth bands + disposable roll | 4–5 |
| Afford filter + spend | 5 |
| Midnight sum of spend | 6 |
| README | 7 |
