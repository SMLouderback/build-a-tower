# Shop Demand and Oversupply Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add finite Food/Retail × Budget/Mid/Premium demand pools, population-driven generation, one-tier spillover, shop competition, daily upkeep, unmet-demand stress, and player-facing demand economics.

**Architecture:** `ShopDemandSystem` owns six mutable daily pools and immutable 30-day snapshots. `AgentSystem` queries it to choose a role-weighted family and consumes demand only after trip scheduling succeeds; `TowerSimulation` owns the daily lifecycle. `EconomySystem` settles shop income/upkeep, while HUD formatters consume read-only demand and room economics.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode, existing `AgentSystem`, `EconomySystem`, `TowerSimulation`, and IMGUI HUD

## Global Constraints

- Exactly six pools: Food/Retail × Budget/Mid/Premium.
- Shop tier: 0★ Budget, 1–2★ Mid, 3+★ Premium.
- Wealth tier: Street/Basic Budget, Mid Mid, Upper/Premium Premium.
- Occupant generation: Office 0.50 Food/0.10 Retail; Condo 0.40/0.30; Hotel 0.70/0.25.
- Apply existing climate spend multiplier once to generation.
- Street baseline: Food/Budget `2+stars`; Retail/Budget `1+floor(stars/2)`; Food/Mid `max(0,stars-1)`; Retail/Mid `max(0,stars-2)`; no Premium baseline.
- Role family weights: Office 83/17, Condo 57/43, Hotel 74/26; Street/Event proportional to eligible remaining demand.
- Spill at most 25% down one tier only; never upward, cross-family, or Premium→Budget.
- Shop upkeep: midpoint-away-from-zero `baseIncome × 0.50`, charged even when broken/income-blocked.
- Demand stress: matching tier up to +6/day plus tower-wide up to +2/day; Street excluded; clamp at 100.
- Preserve opening hours, affordability, capacity, pathing, dwell, spend, and street-only `streetVisitWeight`.
- Retain 30 completed daily snapshots.
- Full demand graph/heatmaps, parking demand, venue demand, and condo→commercial→hotel cascade are out of scope.
- Spec: `docs/superpowers/specs/2026-09-07-shop-demand-oversupply-design.md`

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Economy/ShopDemandModel.cs` | Enums, key, read-only pool/snapshot/history data |
| `Assets/Scripts/Economy/ShopDemandBalance.cs` | Mappings, generation constants, upkeep, family choice |
| `Assets/Scripts/Economy/ShopDemandSystem.cs` | Six pools, BeginDay, consume/spill, archive, stress, history |
| `Assets/Scripts/Economy/ShopVisitRules.cs` | Demand-aware weighted destination selection |
| `Assets/Scripts/Agents/AgentSystem.cs` | Demand-aware internal/street commercial trips |
| `Assets/Scripts/Core/RoomInstance.cs` | Yesterday shop revenue/upkeep/net |
| `Assets/Scripts/Economy/EconomySystem.cs` | Daily shop upkeep settlement |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Ownership, initialization, rollover order |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` | Selected-shop demand/economics lines |
| `Assets/Scripts/UI/TowerHudController.cs` | Six-pool demand summary |
| `Assets/Tests/EditMode/ShopDemandSystemTests.cs` | Pool mapping, generation, spill, stress, history |
| `Assets/Tests/EditMode/ShopDemandTripTests.cs` | Family choice, weighted selection, successful consumption |
| Existing economy/UI tests | Upkeep, room history, formatting regressions |

---

### Task 1: Demand model, mappings, and deterministic generation

**Files:**
- Create: `Assets/Scripts/Economy/ShopDemandModel.cs`
- Create: `Assets/Scripts/Economy/ShopDemandBalance.cs`
- Create: `Assets/Scripts/Economy/ShopDemandSystem.cs`
- Create: `Assets/Tests/EditMode/ShopDemandSystemTests.cs`

**Interfaces:**
- Produces `ShopDemandFamily`, `ShopDemandTier`, `ShopDemandKey`
- Produces `ShopDemandPoolSnapshot`, `ShopDemandSnapshot`
- Produces mapping and balance functions on `ShopDemandBalance`
- Produces `ShopDemandSystem.BeginDay(...)`, `Snapshot`, and `History`

- [ ] **Step 1: Write failing mapping and generation tests**

```csharp
[TestCase(BuildSubgroup.Food, ShopDemandFamily.Food)]
[TestCase(BuildSubgroup.Retail, ShopDemandFamily.Retail)]
public void Family_maps_from_shop_subgroup(BuildSubgroup subgroup, ShopDemandFamily expected)
{
    var type = ScriptableObject.CreateInstance<RoomTypeSO>();
    type.buildFamily = BuildFamily.Shops;
    type.buildSubgroup = subgroup;
    Assert.AreEqual(expected, ShopDemandBalance.FamilyFor(type));
}

[TestCase(0, ShopDemandTier.Budget)]
[TestCase(1, ShopDemandTier.Mid)]
[TestCase(2, ShopDemandTier.Mid)]
[TestCase(3, ShopDemandTier.Premium)]
[TestCase(5, ShopDemandTier.Premium)]
public void Shop_tier_maps_from_required_stars(int stars, ShopDemandTier expected)
{
    var type = ScriptableObject.CreateInstance<RoomTypeSO>();
    type.requiredStars = stars;
    Assert.AreEqual(expected, ShopDemandBalance.TierForShop(type));
}

[TestCase(WealthBand.Street, ShopDemandTier.Budget)]
[TestCase(WealthBand.Basic, ShopDemandTier.Budget)]
[TestCase(WealthBand.Mid, ShopDemandTier.Mid)]
[TestCase(WealthBand.Upper, ShopDemandTier.Premium)]
[TestCase(WealthBand.Premium, ShopDemandTier.Premium)]
public void Demand_tier_maps_from_wealth(WealthBand wealth, ShopDemandTier expected) =>
    Assert.AreEqual(expected, ShopDemandBalance.TierForWealth(wealth));

[Test]
public void BeginDay_generates_deterministic_population_and_street_demand()
{
    var agents = new List<Agent>
    {
        AgentOf(AgentRole.OfficeWorker, WealthBand.Basic),
        AgentOf(AgentRole.OfficeWorker, WealthBand.Basic),
        AgentOf(AgentRole.CondoResident, WealthBand.Mid),
        AgentOf(AgentRole.HotelGuest, WealthBand.Premium),
    };
    var demand = new ShopDemandSystem();

    demand.BeginDay(agents, stars: 2, climateMultiplier: 1f);

    AssertPool(demand.Snapshot, ShopDemandFamily.Food, ShopDemandTier.Budget, generated: 5);
    AssertPool(demand.Snapshot, ShopDemandFamily.Retail, ShopDemandTier.Budget, generated: 2);
    AssertPool(demand.Snapshot, ShopDemandFamily.Food, ShopDemandTier.Mid, generated: 1);
    AssertPool(demand.Snapshot, ShopDemandFamily.Retail, ShopDemandTier.Mid, generated: 0);
    AssertPool(demand.Snapshot, ShopDemandFamily.Food, ShopDemandTier.Premium, generated: 1);
    AssertPool(demand.Snapshot, ShopDemandFamily.Retail, ShopDemandTier.Premium, generated: 0);
}
```

Use a private `AgentOf` helper that sets `Role` and `Wealth`. The expected values follow population sums + street baselines with `MidpointRounding.AwayFromZero`.

- [ ] **Step 2: Run red test**

Run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShopDemandSystemTests -testResults .superpowers/sdd/shop-demand-task1.xml -logFile .superpowers/sdd/shop-demand-task1.log
```

Expected: compile failure because demand types do not exist.

- [ ] **Step 3: Add model types**

```csharp
namespace BuildATower
{
    public enum ShopDemandFamily { Food, Retail }
    public enum ShopDemandTier { Budget, Mid, Premium }

    public readonly struct ShopDemandKey : System.IEquatable<ShopDemandKey>
    {
        public ShopDemandFamily Family { get; }
        public ShopDemandTier Tier { get; }
        public ShopDemandKey(ShopDemandFamily family, ShopDemandTier tier)
        {
            Family = family;
            Tier = tier;
        }
        public bool Equals(ShopDemandKey other) => Family == other.Family && Tier == other.Tier;
        public override bool Equals(object obj) => obj is ShopDemandKey other && Equals(other);
        public override int GetHashCode() => ((int)Family * 397) ^ (int)Tier;
    }

    public readonly struct ShopDemandPoolSnapshot
    {
        public int Generated { get; }
        public int Served { get; }
        public int SpilledIn { get; }
        public int SpilledOut { get; }
        public int Remaining { get; }
        public int Unmet => Remaining;
        public float Saturation => Generated <= 0 ? 0f :
            UnityEngine.Mathf.Clamp01((Served + SpilledOut) / (float)Generated);
        public ShopDemandPoolSnapshot(
            int generated,
            int served,
            int spilledIn,
            int spilledOut,
            int remaining)
        {
            Generated = generated;
            Served = served;
            SpilledIn = spilledIn;
            SpilledOut = spilledOut;
            Remaining = remaining;
        }
    }
}
```

`ShopDemandSnapshot` must hold a copied dictionary keyed by all six `ShopDemandKey` values and expose:

```csharp
public ShopDemandPoolSnapshot Pool(ShopDemandFamily family, ShopDemandTier tier);
public int TotalGenerated { get; }
public int TotalUnmet { get; }
public float TotalUnmetRatio { get; }
```

- [ ] **Step 4: Add centralized balance and mapping**

```csharp
public static class ShopDemandBalance
{
    public const float OfficeFood = 0.50f;
    public const float OfficeRetail = 0.10f;
    public const float CondoFood = 0.40f;
    public const float CondoRetail = 0.30f;
    public const float HotelFood = 0.70f;
    public const float HotelRetail = 0.25f;
    public const float SpillRate = 0.25f;
    public const float ShopUpkeepRate = 0.50f;
    public const float MatchingStressMax = 6f;
    public const float TowerStressMax = 2f;
    public const int HistoryCapacity = 30;

    public static ShopDemandFamily FamilyFor(RoomTypeSO type) =>
        type != null && type.ResolvedBuildSubgroup() == BuildSubgroup.Retail
            ? ShopDemandFamily.Retail
            : ShopDemandFamily.Food;

    public static ShopDemandTier TierForShop(RoomTypeSO type) =>
        type == null || type.requiredStars <= 0 ? ShopDemandTier.Budget :
        type.requiredStars <= 2 ? ShopDemandTier.Mid :
        ShopDemandTier.Premium;

    public static ShopDemandTier TierForWealth(WealthBand wealth) => wealth switch
    {
        WealthBand.Mid => ShopDemandTier.Mid,
        WealthBand.Upper or WealthBand.Premium => ShopDemandTier.Premium,
        _ => ShopDemandTier.Budget
    };

    public static int DailyUpkeep(RoomTypeSO type) =>
        type == null ? 0 : (int)System.Math.Round(
            System.Math.Max(0, type.baseIncome) * ShopUpkeepRate,
            System.MidpointRounding.AwayFromZero);
}
```

- [ ] **Step 5: Implement `ShopDemandSystem.BeginDay`**

Create six internal mutable pool records on construction. `BeginDay` clears current counters, sums role contributions by `agent.Wealth`, adds the exact street baseline, multiplies every pool by `Mathf.Max(0f, climateMultiplier)`, and rounds midpoint-away-from-zero.

Count only:

```csharp
AgentRole.OfficeWorker
AgentRole.CondoResident
AgentRole.HotelGuest
```

Do not count StreetVisitor, EventVisitor, Criminal, Maid, Handyman, or Security as occupant demand sources.

- [ ] **Step 6: Run green test**

Run the Task 1 command. Expected: all `ShopDemandSystemTests` pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Economy/ShopDemandModel.cs Assets/Scripts/Economy/ShopDemandModel.cs.meta Assets/Scripts/Economy/ShopDemandBalance.cs Assets/Scripts/Economy/ShopDemandBalance.cs.meta Assets/Scripts/Economy/ShopDemandSystem.cs Assets/Scripts/Economy/ShopDemandSystem.cs.meta Assets/Tests/EditMode/ShopDemandSystemTests.cs Assets/Tests/EditMode/ShopDemandSystemTests.cs.meta
git commit -m "feat: add population-driven shop demand pools"
```

---

### Task 2: Finite consumption, one-tier spill, and history

**Files:**
- Modify: `Assets/Scripts/Economy/ShopDemandSystem.cs`
- Modify: `Assets/Tests/EditMode/ShopDemandSystemTests.cs`

**Interfaces:**
- Consumes Task 1 pool model/mappings
- Produces `CanServe`, `TryConsume`, `AvailableFor`, `Archive`, and bounded `History`

- [ ] **Step 1: Add failing consumption/spill/history tests**

```csharp
[Test]
public void Consume_uses_native_pool_before_spill()
{
    var demand = DemandFrom(
        RepeatAgents(6, AgentRole.HotelGuest, WealthBand.Premium),
        stars: 0); // Premium Food = round(6 × 0.70) = 4
    Assert.IsTrue(demand.TryConsume(FoodShop(stars: 3), WealthBand.Premium));
    Assert.AreEqual(1, demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).Served);
    Assert.AreEqual(0, demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).SpilledOut);
}

[Test]
public void Spill_is_one_tier_down_and_capped_at_twenty_five_percent()
{
    var demand = DemandFrom(
        RepeatAgents(11, AgentRole.HotelGuest, WealthBand.Premium),
        stars: 0); // Premium Food = round(11 × 0.70) = 8
    var midShop = FoodShop(stars: 2);
    Assert.IsTrue(demand.TryConsume(midShop, WealthBand.Premium));
    Assert.IsTrue(demand.TryConsume(midShop, WealthBand.Premium));
    Assert.IsFalse(demand.TryConsume(midShop, WealthBand.Premium));
    Assert.IsFalse(demand.TryConsume(FoodShop(stars: 0), WealthBand.Premium));
    Assert.AreEqual(2, demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Premium).SpilledOut);
    Assert.AreEqual(2, demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Mid).SpilledIn);
}

[Test]
public void Demand_never_moves_up_or_cross_family()
{
    var demand = DemandFrom(
        RepeatAgents(2, AgentRole.OfficeWorker, WealthBand.Basic),
        stars: 0); // Budget Food = baseline 2 + office Food 1
    Assert.IsFalse(demand.TryConsume(FoodShop(stars: 2), WealthBand.Basic));
    Assert.IsFalse(demand.TryConsume(RetailShop(stars: 0), WealthBand.Basic));
}

[Test]
public void Archive_keeps_only_thirty_snapshots_and_next_day_resets()
{
    var demand = new ShopDemandSystem();
    for (var day = 0; day < 35; day++)
    {
        demand.BeginDay(new List<Agent>(), stars: 0, climateMultiplier: 1f);
        demand.Archive();
    }
    Assert.AreEqual(30, demand.History.Count);
}
```

Test helpers must create agents with explicit role/wealth, call `BeginDay`, and rely on the approved weights. Do not add a production-only seed API.

- [ ] **Step 2: Run red test**

Run Task 1 command. Expected: failures for missing consumption/history methods.

- [ ] **Step 3: Implement consumption**

```csharp
public int AvailableFor(ShopDemandFamily family, WealthBand wealth)
{
    var originTier = ShopDemandBalance.TierForWealth(wealth);
    var origin = Pool(family, originTier);
    var native = origin.Remaining;
    var spill = originTier == ShopDemandTier.Budget
        ? 0
        : Mathf.Max(0, SpillLimit(origin) - origin.SpilledOut);
    return native + spill;
}

public bool CanServe(RoomTypeSO shopType, WealthBand wealth)
{
    if (shopType == null || !ShopVisitRules.IsShop(shopType)) return false;
    var family = ShopDemandBalance.FamilyFor(shopType);
    var originTier = ShopDemandBalance.TierForWealth(wealth);
    var shopTier = ShopDemandBalance.TierForShop(shopType);
    if (shopTier == originTier) return Pool(family, originTier).Remaining > 0;
    if ((int)shopTier != (int)originTier - 1) return false;
    var origin = Pool(family, originTier);
    return origin.Remaining > 0 && origin.SpilledOut < SpillLimit(origin);
}
```

`SpillLimit = floor(Generated × 0.25)`. `TryConsume` increments native `Served`, or origin `SpilledOut` plus destination `SpilledIn`; every success reduces origin `Remaining`. It returns false without mutation for invalid family/tier/availability.

- [ ] **Step 4: Implement immutable archive**

`Archive()` copies the current six-pool snapshot into a list, removes the oldest while count exceeds 30, and leaves current pools untouched until the next `BeginDay` reset. Return the archived snapshot so Task 5 can apply stress from exactly the completed day.

- [ ] **Step 5: Run tests and commit**

Expected: all `ShopDemandSystemTests` pass.

```bash
git add Assets/Scripts/Economy/ShopDemandSystem.cs Assets/Tests/EditMode/ShopDemandSystemTests.cs
git commit -m "feat: add finite shop demand consumption and spillover"
```

---

### Task 3: Role-weighted family and demand-aware shop selection

**Files:**
- Modify: `Assets/Scripts/Economy/ShopDemandBalance.cs`
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Create: `Assets/Tests/EditMode/ShopDemandTripTests.cs`
- Modify: `Assets/Tests/EditMode/CommercialVisitTests.cs`
- Modify: `Assets/Tests/EditMode/ShopVisitRulesTests.cs`

**Interfaces:**
- Produces `ShopDemandBalance.PickFamily(...)`
- Produces `ShopVisitRules.PickDemandWeightedShop(...)`
- `AgentSystem` constructor accepts `ShopDemandSystem demand = null` without breaking existing tests

- [ ] **Step 1: Write failing pure family-choice tests**

```csharp
[TestCase(AgentRole.OfficeWorker, 0.00, ShopDemandFamily.Food)]
[TestCase(AgentRole.OfficeWorker, 0.82, ShopDemandFamily.Food)]
[TestCase(AgentRole.OfficeWorker, 0.84, ShopDemandFamily.Retail)]
[TestCase(AgentRole.CondoResident, 0.56, ShopDemandFamily.Food)]
[TestCase(AgentRole.CondoResident, 0.58, ShopDemandFamily.Retail)]
[TestCase(AgentRole.HotelGuest, 0.73, ShopDemandFamily.Food)]
[TestCase(AgentRole.HotelGuest, 0.75, ShopDemandFamily.Retail)]
public void Occupant_family_choice_uses_role_weight(
    AgentRole role, double roll, ShopDemandFamily expected) =>
    Assert.AreEqual(expected,
        ShopDemandBalance.PickFamily(
            role, foodAvailable: 5, retailAvailable: 5, roll: roll));

[Test]
public void Choice_falls_back_to_only_serviceable_family() =>
    Assert.AreEqual(ShopDemandFamily.Retail,
        ShopDemandBalance.PickFamily(
            AgentRole.OfficeWorker, foodAvailable: 0, retailAvailable: 3, roll: 0.01));

[Test]
public void Street_and_event_choice_is_proportional_to_remaining_demand()
{
    Assert.AreEqual(ShopDemandFamily.Food,
        ShopDemandBalance.PickFamily(AgentRole.StreetVisitor, 3, 1, 0.70));
    Assert.AreEqual(ShopDemandFamily.Retail,
        ShopDemandBalance.PickFamily(AgentRole.EventVisitor, 3, 1, 0.80));
}
```

Return `ShopDemandFamily?`; return null when both available counts are zero.

Implement:

```csharp
public static ShopDemandFamily? PickFamily(
    AgentRole role,
    int foodAvailable,
    int retailAvailable,
    double roll)
{
    if (foodAvailable <= 0 && retailAvailable <= 0) return null;
    if (foodAvailable <= 0) return ShopDemandFamily.Retail;
    if (retailAvailable <= 0) return ShopDemandFamily.Food;

    var foodChance = role switch
    {
        AgentRole.OfficeWorker => 0.83,
        AgentRole.CondoResident => 0.57,
        AgentRole.HotelGuest => 0.74,
        _ => foodAvailable / (double)(foodAvailable + retailAvailable)
    };
    return roll < foodChance ? ShopDemandFamily.Food : ShopDemandFamily.Retail;
}
```

- [ ] **Step 2: Write failing competition-weight tests**

```csharp
[Test]
public void Demand_pick_favors_free_slots_and_under_visited_shops()
{
    var busy = Shop(slots: 4, reserved: 3, visitsToday: 4, streetWeight: 1f);
    var open = Shop(slots: 4, reserved: 0, visitsToday: 0, streetWeight: 1f);
    Assert.Greater(
        ShopVisitRules.DemandWeight(open, streetOrigin: false),
        ShopVisitRules.DemandWeight(busy, streetOrigin: false));
}

[Test]
public void Street_weight_is_ignored_for_internal_origin()
{
    var shop = Shop(slots: 4, reserved: 0, visitsToday: 0, streetWeight: 2f);
    Assert.AreEqual(
        ShopVisitRules.DemandWeight(shop, false) * 2f,
        ShopVisitRules.DemandWeight(shop, true),
        0.001f);
}
```

Implement:

```csharp
public static float DemandWeight(RoomInstance shop, bool streetOrigin)
{
    if (shop?.Type == null) return 0f;
    var free = Mathf.Max(
        1,
        ShopVisitRules.SlotCount(shop.Type) - shop.ConcurrentVisitors);
    var fairness = 1f + 1f / (1f + Mathf.Max(0, shop.VisitsToday));
    var origin = streetOrigin ? Mathf.Max(0.1f, shop.Type.streetVisitWeight) : 1f;
    return free * fairness * origin;
}
```

`PickDemandWeightedShop` performs cumulative weighted random selection with the supplied `System.Random`.

- [ ] **Step 3: Integrate `AgentSystem`**

Add:

```csharp
readonly ShopDemandSystem _shopDemand;

public AgentSystem(
    TransitRouter router,
    MarketClimate climate = null,
    StairCapacity stairCapacity = null,
    ShopDemandSystem shopDemand = null)
{
    // preserve existing assignments
    _shopDemand = shopDemand;
}
```

Refactor commercial selection into one helper that:

1. Resolves effective wealth:
   - StreetVisitor → Street
   - EventVisitor → Mid
   - otherwise use stored `agent.Wealth`; repair any existing persistent-agent
     creation path that leaves the default `Street` value by assigning wealth
     through the same `AgentWealth.ResolveBand(..., _rng)` call used by the
     normal vacancy-fill path
2. Builds eligible candidates with existing open/capacity/affordability/reachability checks.
3. Separates candidates by family and keeps only shops where `_shopDemand == null || _shopDemand.CanServe(type, wealth)`.
4. Calls `PickFamily` using serviceable available units and `_rng.NextDouble()`.
5. Calls `PickDemandWeightedShop` with `streetOrigin`.
6. Reserves slot and calls `BeginTrip`.
7. Calls `_shopDemand.TryConsume(type, wealth)` only after `BeginTrip` returns true.
8. On any failure, releases the slot and leaves demand unchanged.

Preserve null-demand behavior for existing isolated tests: when `_shopDemand == null`, all valid shops remain eligible and no units are consumed.

- [ ] **Step 4: Add integration regression tests**

Extend `CommercialVisitTests`:

```csharp
[Test]
public void Failed_commercial_path_does_not_consume_demand()
{
    // Seed one Budget Food unit, make candidate pass lobby reachability but fail
    // the actual agent-to-shop route as the existing path-failure test does.
    Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.AtHome));
    Assert.AreEqual(1,
        demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Budget).Remaining);
}

[Test]
public void Successful_commercial_trip_consumes_exactly_one_unit()
{
    // Reuse the working connected-grid setup from the existing core scheduling test.
    Assert.IsTrue(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.AtHome));
    Assert.AreEqual(0,
        demand.Snapshot.Pool(ShopDemandFamily.Food, ShopDemandTier.Budget).Remaining);
}
```

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "ShopDemandTripTests|ShopVisitRulesTests|CommercialVisitTests" -testResults .superpowers/sdd/shop-demand-task3.xml -logFile .superpowers/sdd/shop-demand-task3.log
```

Expected: all filtered tests pass.

```bash
git add Assets/Scripts/Economy/ShopDemandBalance.cs Assets/Scripts/Economy/ShopVisitRules.cs Assets/Scripts/Agents/AgentSystem.cs Assets/Tests/EditMode/ShopDemandTripTests.cs Assets/Tests/EditMode/ShopDemandTripTests.cs.meta Assets/Tests/EditMode/CommercialVisitTests.cs Assets/Tests/EditMode/ShopVisitRulesTests.cs
git commit -m "feat: route commercial trips through finite shop demand"
```

---

### Task 4: Daily shop upkeep and completed-day economics

**Files:**
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: `Assets/Tests/EditMode/EconomySystemTests.cs`

**Interfaces:**
- Consumes `ShopDemandBalance.DailyUpkeep`
- Produces `ShopRevenueYesterday`, `ShopUpkeepYesterday`, `ShopNetYesterday`

- [ ] **Step 1: Write failing upkeep tests**

```csharp
[TestCase(28, 14)]
[TestCase(65, 33)]
[TestCase(100, 50)]
public void Shop_upkeep_rounds_half_pay_cap_away_from_zero(int cap, int expected)
{
    var type = FastFoodShop(baseIncome: cap);
    Assert.AreEqual(expected, ShopDemandBalance.DailyUpkeep(type));
}

[Test]
public void Empty_shop_charges_upkeep_and_reports_negative_net()
{
    var shop = PlaceShop(baseIncome: 50);
    economy.OnNewDay(grid, agents, wallet, 0, 0, null, 1f, null);
    Assert.AreEqual(25, economy.GetLastRoomExpense(shop));
    Assert.AreEqual(0, shop.ShopRevenueYesterday);
    Assert.AreEqual(25, shop.ShopUpkeepYesterday);
    Assert.AreEqual(-25, shop.ShopNetYesterday);
    Assert.AreEqual(25, shop.LifetimeExpense);
}

[Test]
public void Broken_shop_still_pays_upkeep_without_income()
{
    var shop = PlaceShop(baseIncome: 50);
    shop.RecordShopSpend(100);
    shop.Condition = 0;
    economy.OnNewDay(grid, agents, wallet, 0, 0, null, 1f, null);
    Assert.AreEqual(0, shop.ShopRevenueYesterday);
    Assert.AreEqual(25, shop.ShopUpkeepYesterday);
}
```

- [ ] **Step 2: Add room completed-day fields**

```csharp
public int ShopRevenueYesterday { get; private set; }
public int ShopUpkeepYesterday { get; private set; }
public int ShopNetYesterday => ShopRevenueYesterday - ShopUpkeepYesterday;

public void ArchiveShopDay(int creditedRevenue, int upkeep)
{
    ShopRevenueYesterday = Mathf.Max(0, creditedRevenue);
    ShopUpkeepYesterday = Mathf.Max(0, upkeep);
    PushVisitHistoryDay();
    ResetVisitsToday();
}
```

Avoid calling both `ArchiveShopDay` and the old push/reset sequence.

- [ ] **Step 3: Settle upkeep in `EconomySystem.OnNewDay`**

Inside the shop block:

```csharp
var creditedRevenue = 0;
if (!incomeBlocked && room.ShopEarningsToday > 0)
{
    creditedRevenue = BuildEconomy.ApplyIncome(room.ShopEarningsToday);
    AddRoomIncome(room, creditedRevenue);
}

var upkeep = ShopDemandBalance.DailyUpkeep(room.Type);
AddRoomExpense(room, upkeep);
room.ArchiveShopDay(creditedRevenue, upkeep);
```

`AddRoomIncome` and `AddRoomExpense` add to tower totals, merge room dictionaries, and record lifetime values. Keep the single wallet income/expense application at the end of `OnNewDay`.

- [ ] **Step 4: Run economy tests and commit**

Run EditMode filter `EconomySystemTests`. Expected: pass.

```bash
git add Assets/Scripts/Core/RoomInstance.cs Assets/Scripts/Economy/EconomySystem.cs Assets/Tests/EditMode/EconomySystemTests.cs
git commit -m "feat: charge daily shop operating upkeep"
```

---

### Task 5: Unmet-demand stress and daily lifecycle

**Files:**
- Modify: `Assets/Scripts/Economy/ShopDemandSystem.cs`
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Tests/EditMode/ShopDemandSystemTests.cs`

**Interfaces:**
- Produces `Agent.AddStress(float)`
- Produces `ShopDemandSystem.ArchiveAndApplyStress(...)`
- `TowerSimulation` owns/exposes one demand system

- [ ] **Step 1: Write failing stress tests**

```csharp
[Test]
public void Stress_formula_combines_matching_and_tower_ratios()
{
    Assert.AreEqual(
        3.5f,
        ShopDemandBalance.StressForTier(
            tierUnmetRatio: 0.5f,
            towerUnmetRatio: 0.25f),
        0.001f);
    Assert.AreEqual(
        0.5f,
        ShopDemandBalance.TowerStress(towerUnmetRatio: 0.25f),
        0.001f);
}

[Test]
public void Matching_tier_gets_more_stress_and_street_is_exempt()
{
    var basic = AgentOf(AgentRole.OfficeWorker, WealthBand.Basic, stress: 0);
    var premium = AgentOf(AgentRole.HotelGuest, WealthBand.Premium, stress: 0);
    var street = AgentOf(AgentRole.StreetVisitor, WealthBand.Street, stress: 0);
    var demand = DemandFrom(new[] { basic, premium }, stars: 0);
    ConsumeAllPremiumDemand(demand);

    demand.ArchiveAndApplyStress(new[] { basic, premium, street });

    Assert.Greater(basic.Stress, premium.Stress);
    Assert.Greater(premium.Stress, 0f);
    Assert.AreEqual(0f, street.Stress);
}

[Test]
public void Demand_stress_clamps_at_one_hundred()
{
    var agent = AgentOf(AgentRole.CondoResident, WealthBand.Basic, stress: 99);
    var demand = DemandFrom(
        RepeatAgents(2, AgentRole.OfficeWorker, WealthBand.Basic),
        stars: 0);
    demand.ArchiveAndApplyStress(new[] { agent });
    Assert.AreEqual(100f, agent.Stress);
}
```

- [ ] **Step 2: Implement stress**

```csharp
public void AddStress(float amount) =>
    Stress = Mathf.Clamp(Stress + Mathf.Max(0f, amount), 0f, 100f);
```

`ArchiveAndApplyStress` must archive exactly once, calculate generated-weighted unmet ratios across Food+Retail for each tier, calculate total ratio, and apply:

- OfficeWorker, CondoResident, HotelGuest: tower + matching
- Maid, Handyman, Security: tower only
- StreetVisitor, EventVisitor, Criminal: none

Add pure clamped formula helpers to `ShopDemandBalance`:

```csharp
public static float TowerStress(float towerUnmetRatio) =>
    TowerStressMax * Mathf.Clamp01(towerUnmetRatio);

public static float StressForTier(float tierUnmetRatio, float towerUnmetRatio) =>
    MatchingStressMax * Mathf.Clamp01(tierUnmetRatio) +
    TowerStress(towerUnmetRatio);
```

All test demand must come from explicit source-agent lists through `BeginDay`.
Do not expose test-only mutation on `ShopDemandSystem`.

- [ ] **Step 3: Wire ownership and rollover**

In `TowerSimulation.Awake()`:

```csharp
_shopDemand = new ShopDemandSystem();
_agents = new AgentSystem(_router, shopDemand: _shopDemand);
```

Expose:

```csharp
public ShopDemandSystem ShopDemand => _shopDemand;
```

After initial `OnGridChanged()`/agent synchronization in `Start()`:

```csharp
_shopDemand.BeginDay(
    _agents.Agents,
    _stars.CurrentStars,
    _climate?.SpendMultiplier ?? 1f);
```

Inside each `OnDayRolled` loop iteration:

```csharp
_shopDemand.ArchiveAndApplyStress(_agents.Agents);
// existing conference TickDay
// existing economy OnNewDay
_shopDemand.BeginDay(
    _agents.Agents,
    _stars.CurrentStars,
    climateSpendMult);
```

Do not regenerate on `OnGridChanged`; construction changes take effect next day.

- [ ] **Step 4: Add lifecycle-state test**

Add to `ShopDemandSystemTests`:

```csharp
[Test]
public void Archive_then_BeginDay_keeps_history_and_replaces_current_pools()
{
    var demand = new ShopDemandSystem();
    demand.BeginDay(
        RepeatAgents(2, AgentRole.OfficeWorker, WealthBand.Basic),
        stars: 0,
        climateMultiplier: 1f);
    var completedGenerated = demand.Snapshot.TotalGenerated;

    demand.ArchiveAndApplyStress(System.Array.Empty<Agent>());
    demand.BeginDay(
        RepeatAgents(2, AgentRole.HotelGuest, WealthBand.Premium),
        stars: 0,
        climateMultiplier: 1f);

    Assert.AreEqual(1, demand.History.Count);
    Assert.AreEqual(completedGenerated, demand.History[0].TotalGenerated);
    Assert.AreNotEqual(completedGenerated, demand.Snapshot.TotalGenerated);
}
```

Review `TowerSimulation.OnDayRolled` directly to verify the required
archive/stress → conference/economy → `BeginDay` call order.

- [ ] **Step 5: Run tests and commit**

Run filters `ShopDemandSystemTests|CommercialVisitTests`. Expected: pass.

```bash
git add Assets/Scripts/Economy/ShopDemandSystem.cs Assets/Scripts/Agents/Agent.cs Assets/Scripts/Simulation/TowerSimulation.cs Assets/Tests/EditMode/ShopDemandSystemTests.cs
git commit -m "feat: apply unmet shop demand stress at rollover"
```

---

### Task 6: Demand summary and selected-shop economics

**Files:**
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Tests/EditMode/RoomEconomyFormatTests.cs`
- Create: `Assets/Tests/EditMode/ShopDemandFormatTests.cs`

**Interfaces:**
- Consumes demand snapshots and room completed-day economics
- Produces pure demand formatting methods used by IMGUI

- [ ] **Step 1: Write failing formatter tests**

```csharp
[Test]
public void Selected_shop_lines_show_pool_upkeep_and_negative_net()
{
    var shop = ShopRoom(stars: 0, subgroup: BuildSubgroup.Food);
    shop.ArchiveShopDay(creditedRevenue: 10, upkeep: 25);
    var lines = RoomEconomyFormat.SelectedUnitLines(
        shop,
        economy: null,
        demand: EmptyDemand());
    CollectionAssert.Contains(lines, "Demand: Food / Budget");
    CollectionAssert.Contains(lines, "Yesterday revenue: $10");
    CollectionAssert.Contains(lines, "Daily upkeep: $25");
    CollectionAssert.Contains(lines, "Yesterday net: -$15");
}

[Test]
public void Pool_line_shows_generated_served_spill_and_unmet()
{
    var pool = new ShopDemandPoolSnapshot(
        generated: 10, served: 6, spilledIn: 1, spilledOut: 2, remaining: 2);
    Assert.AreEqual(
        "Food / Mid: 8/10 served, spill +1/-2, unmet 2 (80%)",
        ShopDemandFormat.PoolLine(ShopDemandFamily.Food, ShopDemandTier.Mid, pool));
}
```

`EmptyDemand()` constructs a `ShopDemandSystem`, calls `BeginDay` with an
empty agent list, zero stars, and climate multiplier 1, then returns it.

- [ ] **Step 2: Implement selected-shop lines**

Change signature compatibly:

```csharp
public static IEnumerable<string> SelectedUnitLines(
    RoomInstance room,
    EconomySystem economy,
    ShopDemandSystem demand = null,
    int openShopCountInPool = 0)
```

For shops, append:

```text
Demand: Food / Budget
Visits yesterday: N
Yesterday revenue: $N
Daily upkeep: $N
Yesterday net: ±$N
Competition: Underserved|Balanced|Oversupplied
```

Competition status uses spec thresholds:

```csharp
if (pool.Generated > 0 && pool.Unmet / (float)pool.Generated >= 0.20f)
    return "Underserved";
if (openShopCountInPool > pool.Generated)
    return "Oversupplied";
return "Balanced";
```

Pass the open-shop count from the HUD/grid query rather than storing grid ownership inside `ShopDemandSystem`.

- [ ] **Step 3: Add six-pool Shops panel**

In `TowerHudController`’s existing Shops top-info panel, retain existing visit history lines and add one line per pool using `ShopDemandFormat.PoolLine`. Pass `_simulation.ShopDemand` to selected-unit formatting.

Do not implement charts or heatmaps.

- [ ] **Step 4: Run UI tests and commit**

Run filters `RoomEconomyFormatTests|ShopDemandFormatTests`. Expected: pass.

```bash
git add Assets/Scripts/UI/RoomEconomyFormat.cs Assets/Scripts/UI/TowerHudController.cs Assets/Tests/EditMode/RoomEconomyFormatTests.cs Assets/Tests/EditMode/ShopDemandFormatTests.cs Assets/Tests/EditMode/ShopDemandFormatTests.cs.meta
git commit -m "feat: show shop demand and profit feedback"
```

---

### Task 7: Regression verification and spec completion

**Files:**
- Modify: `docs/superpowers/specs/2026-09-07-shop-demand-oversupply-design.md`

**Interfaces:**
- Verifies all prior tasks as one feature

- [ ] **Step 1: Run focused EditMode suite**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "ShopDemandSystemTests|ShopDemandTripTests|ShopDemandFormatTests|ShopVisitRulesTests|CommercialVisitTests|EconomySystemTests|RoomEconomyFormatTests" -testResults .superpowers/sdd/shop-demand-final.xml -logFile .superpowers/sdd/shop-demand-final.log
```

Expected: zero failed tests.

- [ ] **Step 2: Run full EditMode suite**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults .superpowers/sdd/shop-demand-full.xml -logFile .superpowers/sdd/shop-demand-full.log
```

Expected: zero failed tests.

- [ ] **Step 3: Play Mode smoke**

1. Start a low-star tower: Budget pools have street baseline; Premium baseline is zero.
2. Place multiple Budget Food shops: total visits remain finite and split across shops.
3. Add occupied office/condo/hotel rooms: next day’s demand rises by the approved weights.
4. Leave excess shops underused: each pays upkeep and can show negative net.
5. Create Premium demand with only Mid shops: no more than 25% spills; no Budget spill.
6. Leave demand unmet: affected wealth occupants receive more stress than other tiers.
7. Open Shops panel and select a shop: pool metrics and yesterday economics are readable.

- [ ] **Step 4: Mark spec implemented**

Change:

```markdown
**Status:** Implemented
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-09-07-shop-demand-oversupply-design.md
git commit -m "docs: mark shop demand system implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Six family/tier pools and mappings | 1 |
| Population weights, climate, street baseline | 1 |
| Finite consumption and expiration | 2, 5 |
| 25% one-level spill only | 2 |
| Role-weighted family choice | 3 |
| Free-slot/fairness/street-only destination weights | 3 |
| Consume after successful path only | 3 |
| 50% pay-cap upkeep and negative net | 4 |
| Matching + tower unmet stress | 5 |
| Archive → stress → economy → next generation | 5 |
| 30-day history | 2 |
| Six-pool HUD and selected-shop economics | 6 |
| Focused/full tests and Play Mode smoke | 7 |

Placeholder scan: no `TBD`, unresolved API, or unspecified implementation step remains. Types and signatures consumed by later tasks are introduced in earlier tasks. Deferred cascade, parking, venue, graph, and heatmap work is not included.
