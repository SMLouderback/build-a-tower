# Commercial Visit Traffic (E1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Shops earn from real visits by office/hotel/condo agents and Outside street visitors, with Fast Food / Restaurant / Retail catalog and midnight batch payouts.

**Architecture:** `ShopVisitRules` holds hours/dwell/slot helpers; `RoomInstance.VisitsToday` tallies completed visits; `EconomySystem.OnNewDay` pays `visits × baseIncome` for `TrafficVariable` rooms. `AgentSystem` schedules one commercial trip per day for home agents and spawns capped `StreetVisitor` agents from Outside. Population excludes street visitors.

**Tech Stack:** Unity 6000.4.7f1, C#, existing TransitRouter pathfinding, NUnit EditMode tests

## Global Constraints

- Real pathfinding visits (not formula-only)
- Visitors: office + hotel + condo + street from Outside
- Catalog: Fast Food · Restaurant · Retail only this pass
- Income batched at **midnight** (`visitsToday × baseIncome`)
- Street visitors **not** in star population
- Concurrent street cap **8**
- Spec: `docs/superpowers/specs/2026-07-31-commercial-visit-traffic-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/ShopVisitRules.cs` | Open hours, dwell range, slot count, pay-per-visit |
| `Assets/Scripts/Core/RoomInstance.cs` | `VisitsToday`, `ConcurrentVisitors`, record/reset |
| `Assets/Scripts/Economy/EconomySystem.cs` | Midnight traffic payout |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` | Visit income lines / button tags |
| `Assets/Scripts/Agents/AgentEnums.cs` | `StreetVisitor`, `VisitingShop` |
| `Assets/Scripts/Agents/Agent.cs` | Commercial trip fields |
| `Assets/Scripts/Agents/AgentSystem.cs` | Trips, dwell, street spawn |
| `Assets/Scripts/Agents/AgentView.cs` | Street visitor color |
| Room `.asset` files + Resources | Three shops |
| `TowerHudController.cs` | Load shops into catalog |
| `README.md` | Play steps |
| Tests | `ShopVisitRulesTests`, `CommercialVisitTests`, format/economy updates |

---

### Task 1: Shop rules + visit tally + midnight payout + format

**Files:**
- Create: `Assets/Scripts/Economy/ShopVisitRules.cs`
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs`
- Test: `Assets/Tests/EditMode/ShopVisitRulesTests.cs`, update `EconomySystemTests.cs`, `RoomEconomyFormatTests.cs`

**Interfaces:**
- Produces: `ShopVisitRules.IsShop(RoomTypeSO)`; `IsOpen(RoomTypeSO, int minuteOfDay)`; `SlotCount(RoomTypeSO)`; `PickDwellMinutes(RoomTypeSO, System.Random)`; `PayPerVisit(RoomTypeSO)` → `baseIncome`
- Produces: `RoomInstance.VisitsToday`; `ConcurrentVisitors`; `void RecordVisit()`; `void ResetVisitsToday()`; `bool TryOccupyVisitorSlot()`; `void ReleaseVisitorSlot()`
- Consumes: midnight sweep pays traffic rooms

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Fast_food_open_at_noon_closed_at_midnight()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.id = "shop_food_fast";
    so.category = RoomCategory.Commercial;
    so.incomeModel = IncomeModel.TrafficVariable;
    so.hasActiveHours = true;
    so.activeHoursStart = 11 * 60;
    so.activeHoursEnd = 21 * 60;
    so.baseIncome = 40;
    so.maxOccupants = 4;
    Assert.IsTrue(ShopVisitRules.IsOpen(so, 12 * 60));
    Assert.IsFalse(ShopVisitRules.IsOpen(so, 22 * 60));
}

[Test]
public void Midnight_pays_traffic_from_visits_and_clears_counter()
{
    var grid = new TowerGrid();
    // lobby + place TrafficVariable room with baseIncome 40
    // room.RecordVisit() three times
    // economy.OnNewDay → LastIncome includes 120; VisitsToday == 0; LifetimeIncome += 120
}

[Test]
public void Format_shows_per_visit_and_visits_today()
{
    // IncomeLine contains "/ visit"
    // SelectedUnitLines contains "Visits today:"
}
```

- [ ] **Step 2: Run — expect fail (missing API)**

- [ ] **Step 3: Implement `ShopVisitRules`**

```csharp
public static class ShopVisitRules
{
    public static bool IsShop(RoomTypeSO type) =>
        type != null && type.incomeModel == IncomeModel.TrafficVariable;

    public static bool IsOpen(RoomTypeSO type, int minuteOfDay)
    {
        if (!IsShop(type)) return false;
        if (!type.hasActiveHours) return true;
        var m = ((minuteOfDay % (24 * 60)) + 24 * 60) % (24 * 60);
        if (type.activeHoursStart <= type.activeHoursEnd)
            return m >= type.activeHoursStart && m < type.activeHoursEnd;
        return m >= type.activeHoursStart || m < type.activeHoursEnd;
    }

    public static int SlotCount(RoomTypeSO type) =>
        type == null ? 0 : Mathf.Max(1, type.maxOccupants);

    public static int PayPerVisit(RoomTypeSO type) =>
        type == null ? 0 : Math.Max(0, type.baseIncome);

    public static int PickDwellMinutes(RoomTypeSO type, System.Random rng)
    {
        // Fast food 15-25, restaurant 40-60, retail 20-40 via id/subgroup heuristics
        var (lo, hi) = DwellRange(type);
        return lo + rng.Next(0, hi - lo + 1);
    }
}
```

On `RoomInstance`:

```csharp
public int VisitsToday { get; private set; }
public int ConcurrentVisitors { get; private set; }

public void RecordVisit() => VisitsToday++;
public void ResetVisitsToday() => VisitsToday = 0;

public bool TryOccupyVisitorSlot()
{
    var cap = ShopVisitRules.SlotCount(Type);
    if (ConcurrentVisitors >= cap) return false;
    ConcurrentVisitors++;
    return true;
}

public void ReleaseVisitorSlot()
{
    if (ConcurrentVisitors > 0) ConcurrentVisitors--;
}
```

In `EconomySystem.OnNewDay`, after elevator/rent loops (or in same room loop):

```csharp
if (ShopVisitRules.IsShop(room.Type) && room.VisitsToday > 0)
{
    var amount = room.VisitsToday * ShopVisitRules.PayPerVisit(room.Type);
    LastIncome += amount;
    _lastIncomeByRoom[room.InstanceId] = amount; // or add if already present
    room.RecordLifetimeIncome(amount);
    room.ResetVisitsToday();
}
```

Update `RoomEconomyFormat.IncomeLine` / `SelectedUnitLines` / `ButtonTag` for `TrafficVariable`.

- [ ] **Step 4: Tests PASS — Commit**

```bash
git commit -m "feat: shop visit tallies and midnight traffic payout"
```

---

### Task 2: VisitingShop phase + office lunch trips

**Files:**
- Modify: `Assets/Scripts/Agents/AgentEnums.cs`, `Agent.cs`, `AgentSystem.cs`
- Test: `Assets/Tests/EditMode/CommercialVisitTests.cs`

**Interfaces:**
- Produces: `AgentPhase.VisitingShop`; agent fields `CommercialTripDay`, `VisitTarget`, `VisitDwellRemaining`, `PhaseAfterVisit`
- Produces: office midday once-per-day commercial trip when Working

- [ ] **Step 1: Failing test — office records visit after simulated dwell**

Build lobby + stairs + office + fast food; sync agents; advance clock into lunch; tick until visit completes; assert `shop.VisitsToday >= 1` (or assert `RecordVisit` path via forcing a completed visit helper if pathing is flaky — prefer full path with stairs on lobby-adjacent floors).

Also unit-test a package-visible helper if needed:

```csharp
// e.g. AgentSystem.TryBeginCommercialTrip(agent, grid, clock) returns true when open shop exists
```

- [ ] **Step 2: Implement enums + agent fields**

```csharp
// AgentRole — add later in Task 4 for StreetVisitor
// AgentPhase
VisitingShop,

// Agent
public int CommercialTripDay { get; set; } = -1;
public RoomInstance VisitTarget { get; set; }
public float VisitDwellRemaining { get; set; }
public AgentPhase PhaseAfterVisit { get; set; }
public Vector2Int? ReturnCell { get; set; }
```

- [ ] **Step 3: Selection + trip helpers in `AgentSystem`**

```csharp
List<RoomInstance> FindOpenShops(TowerGrid grid, int minuteOfDay) { ... reachable from lobby, open, has free slot ... }

bool TryBeginCommercialTrip(Agent agent, TowerGrid grid, GameClock clock, AgentPhase afterVisit)
{
    if (agent.CommercialTripDay == clock.DayIndex) return false;
    var shops = FindOpenShops(...);
    if (shops.Count == 0) return false;
    var shop = shops[_rng.Next(shops.Count)];
    if (!shop.TryOccupyVisitorSlot()) return false;
    agent.CommercialTripDay = clock.DayIndex;
    agent.VisitTarget = shop;
    agent.PhaseAfterVisit = afterVisit;
    agent.ReturnCell = agent.Cell; // or home cell
    agent.VisitDwellRemaining = ShopVisitRules.PickDwellMinutes(shop.Type, _rng);
    BeginTrip(agent, agent.Cell, ShopEntryCell(shop), AgentPhase.VisitingShop, grid);
    return true;
}
```

On arriving at shop (`PhaseAfterMove == VisitingShop`): stay in `VisitingShop`, decrement dwell by `deltaGameMinutes`; when ≤0: `RecordVisit()`, `ReleaseVisitorSlot()`, clear target, `BeginTrip` to `ReturnCell` with `PhaseAfterVisit`.

In `UpdateOffice`: when `Phase == Working` and minute in `[11*60+30, 13*60+30]` and `CommercialTripDay != DayIndex`, call `TryBeginCommercialTrip(..., AgentPhase.Working)`.

- [ ] **Step 4: Tests PASS — Commit**

```bash
git commit -m "feat: office lunch commercial visits"
```

---

### Task 3: Hotel + condo commercial windows

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: `CommercialVisitTests.cs`

- [ ] **Step 1: Tests for hotel evening + condo daytime windows**

Assert `TryBeginCommercialTrip` / schedule hooks fire once per day in windows:
- Hotel: `Staying`, minute in `[18*60, 21*60]`
- Condo: `HasMovedIn` and `AtHome`, minute in `[12*60, 17*60]`

- [ ] **Step 2: Wire `UpdateHotel` / `UpdateCondo`**

Same `TryBeginCommercialTrip` with `PhaseAfterVisit = Staying` / `AtHome`.

- [ ] **Step 3: Tests PASS — Commit**

```bash
git commit -m "feat: hotel and condo commercial visits"
```

---

### Task 4: Street visitors

**Files:**
- Modify: `AgentEnums.cs` (`StreetVisitor`), `Agent.cs`, `AgentSystem.cs`, `AgentView.cs`
- Modify: `Population` getter
- Test: `CommercialVisitTests.cs`

**Constants:** `MaxConcurrentStreetVisitors = 8`; spawn attempt every N game minutes during open shop hours; rate scales with `(1 + stars)` via optional stars arg on `Tick` or field set from simulation.

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Population_excludes_street_visitors()
{
    // add StreetVisitor agent → Population unchanged vs without
}

[Test]
public void Street_visitor_cap_is_eight()
{
    // spawn loop cannot exceed 8 StreetVisitor agents
}
```

- [ ] **Step 2: Implement spawn/despawn**

```csharp
void UpdateStreetTraffic(GameClock clock, TowerGrid grid, int stars)
{
    // count street visitors; if < 8 and any open shop and daytime, maybe spawn
    // new Agent(id, StreetVisitor, homeRoom: null?) — HomeRoom may need to allow null OR use first shop as soft home
}
```

Prefer: `HomeRoom` = chosen shop (not a living room); `SyncHomes` must **not** remove street visitors when scanning living rooms — skip `StreetVisitor` in the removal/sync living-room logic.

Flow: Outside → shop (`VisitingShop`) → Outside → remove agent from list.

Pass `currentStars` into `Tick` from `TowerSimulation` (same pattern as SyncHomes).

- [ ] **Step 3: `AgentView` color** distinct (e.g. orange/amber)

- [ ] **Step 4: Tests PASS — Commit**

```bash
git commit -m "feat: street visitors for shop traffic"
```

---

### Task 5: Assets, HUD catalog, README, closeout

**Files:**
- Create/update assets:
  - `Assets/ScriptableObjects/Rooms/ShopFastFood.asset` (id `shop_food_fast`, Food, pay 40, slots 4, hours 11–21)
  - `Assets/ScriptableObjects/Rooms/ShopRestaurant.asset` (id `shop_food_restaurant`, …)
  - `Assets/ScriptableObjects/Rooms/ShopRetail.asset` (id `shop_retail`, …)
  - Copy or mirror under `Assets/Resources/Rooms/` for HUD `Resources.Load`
  - Retire/repurpose `RetailFastFood.asset` (rename fields to Fast Food or leave unused)
- Modify: `TowerHudController.EnsureElevatorAndCatalog` — `AddRoomButton` for the three shops
- Modify: scene `placeableRooms` if needed (Resources load is enough)
- Modify: `README.md`
- Test: extend `BuildCatalogTests` for three shops Food/Retail grouping

- [x] **Step 1: Assets authored with `hasActiveHours`, costs, colors, sizes (16×1 commercial)**

- [x] **Step 2: HUD loads all three; catalog nests correctly**

- [x] **Step 3: README play bullets for Shops + visits + midnight payout**

- [x] **Step 4: Roslyn typecheck Scripts + EditMode — Commit**

```bash
git commit -m "feat: shop assets, HUD catalog, README for E1 visits"
```

---

### Task 6: Closeout

- [ ] Spec coverage checklist green
- [ ] Final commit if needed; push only when asked

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| Fast Food / Restaurant / Retail assets | 5 |
| Office lunch visits | 2 |
| Hotel + condo visits | 3 |
| Street visitors Outside→shop→leave | 4 |
| Midnight batch pay | 1 |
| Selection visit status | 1 |
| Population excludes street | 4 |
| Capacity / hours / unreachable skip | 1–2 |
| README | 5 |
