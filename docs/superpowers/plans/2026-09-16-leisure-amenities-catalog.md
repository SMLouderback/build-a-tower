# Leisure Amenities Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship five Leisure rooms (gym, spa, pool, bowling, theater) with star gates, traffic visits, thin AmenitySystem proximity hooks, HUD family, and matching dollhouse/menu art.

**Architecture:** Reuse `TrafficVariable` visit/midnight earnings; split `IsShop` vs `IsTrafficVenue` so Leisure does not drain Food/Retail demand pools. New `BuildFamily.Leisure` + `AmenitySystem` for once-per-day stress relief and a small hotel demand nudge. Art follows the shop magenta-sheet dollhouse + square menu icon pipeline.

**Tech Stack:** Unity 6 C#, EditMode NUnit, `RoomTypeSO` YAML assets, Resources dollhouse/menu PNGs.

**Spec:** `docs/superpowers/specs/2026-09-16-leisure-amenities-catalog-design.md`

## Global Constraints

- `BuildFamily.Leisure` appended as enum value **7** (do not reorder existing values).
- Spa: `allowBasement = false`; other four allow basement.
- Leisure must not call `ShopDemandSystem.TryConsume` / must bypass Food/Retail pools.
- Dollhouse: warm architectural cutaway; magenta is key plate only; no painted-in people.
- No showtimes, memberships, lane queues, staffing, or new star promotion criteria.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Data/BuildFamily.cs` | Add `Leisure` |
| `Assets/Scripts/Data/RoomTypeSO.cs` | Map Leisure in `ResolvedBuildFamily` if needed |
| `Assets/Resources/Rooms/Leisure*.asset` | Five room blueprints |
| `Assets/Scripts/Economy/ShopVisitRules.cs` | `IsTrafficVenue` / `IsShop` split + leisure dwell |
| `Assets/Scripts/Agents/AgentSystem.cs` | Destination pick uses traffic venues; skip shop demand for leisure |
| `Assets/Scripts/Economy/AmenitySystem.cs` | Proximity relief + hotel nudge |
| `Assets/Scripts/Agents/Agent.cs` | `RelieveStress` |
| `Assets/Scripts/Economy/EconomySystem.cs` / sim tick | Wire amenity tick + hotel nudge |
| `Assets/Scripts/UI/TowerHudController.cs` | Leisure family strip |
| `Assets/Scripts/UI/MenuIconArt.cs` | `family_leisure` id |
| `Assets/Scripts/Rendering/RoomDollhouseArt.cs` | Leaf maps |
| `Assets/Scripts/Audio/SoundProfileMap.cs` | Leisure → nearest profiles |
| `Assets/Resources/Art/Dollhouse/*`, `Art/Menu/*` | Art assets |
| `Assets/Tests/EditMode/*` | Catalog, visit rules, amenity, art tests |

---

### Task 1: BuildFamily.Leisure + RoomTypeSO catalog assets

**Files:**
- Modify: `Assets/Scripts/Data/BuildFamily.cs`
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs` (`ResolvedBuildFamily` — Leisure stays explicit via `buildFamily` field; ensure Commercial + Leisure does not fall through to Shops)
- Create: `Assets/Resources/Rooms/LeisureGym.asset`, `LeisureSpa.asset`, `LeisurePool.asset`, `LeisureBowling.asset`, `LeisureTheater.asset` (+ `.meta` GUIDs)
- Test: `Assets/Tests/EditMode/LeisureCatalogTests.cs`

**Interfaces:**
- Produces: room ids `leisure_gym`, `leisure_spa`, `leisure_pool`, `leisure_bowling`, `leisure_theater` with spec sizes/stars/costs

- [ ] **Step 1: Write failing catalog tests**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class LeisureCatalogTests
    {
        static RoomTypeSO Load(string resourceName) =>
            Resources.Load<RoomTypeSO>("Rooms/" + resourceName);

        [Test]
        public void Leisure_assets_match_spec_footprints_and_stars()
        {
            AssertRoom(Load("LeisureGym"), "leisure_gym", 6, 1, 2, true);
            AssertRoom(Load("LeisureSpa"), "leisure_spa", 6, 1, 5, false);
            AssertRoom(Load("LeisurePool"), "leisure_pool", 8, 2, 3, true);
            AssertRoom(Load("LeisureBowling"), "leisure_bowling", 10, 1, 3, true);
            AssertRoom(Load("LeisureTheater"), "leisure_theater", 8, 2, 4, true);
        }

        static void AssertRoom(RoomTypeSO so, string id, int w, int h, int stars, bool basement)
        {
            Assert.IsNotNull(so, id);
            Assert.AreEqual(id, so.id);
            Assert.AreEqual(new Vector2Int(w, h), so.size);
            Assert.AreEqual(stars, so.requiredStars);
            Assert.AreEqual(BuildFamily.Leisure, so.ResolvedBuildFamily());
            Assert.AreEqual(IncomeModel.TrafficVariable, so.incomeModel);
            Assert.AreEqual(basement, so.allowBasement);
            Assert.IsTrue(so.allowAboveGround);
        }

        [Test]
        public void BuildFamily_Leisure_is_enum_value_7()
        {
            Assert.AreEqual(7, (int)BuildFamily.Leisure);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect fail (missing enum/assets)**

Run Unity EditMode filter `LeisureCatalogTests` (project’s usual batchmode test command).  
Expected: fail — `Leisure` missing and/or Resources null.

- [ ] **Step 3: Add enum + fix ResolvedBuildFamily**

In `BuildFamily.cs` append `Leisure` after `Transit` (value 7).

In `RoomTypeSO.ResolvedBuildFamily()`, when `buildFamily != None` return it first (already does). Ensure category fallback does **not** map Leisure-less Commercial to Shops only — keep existing Commercial → Shops fallback; Leisure assets set `buildFamily: 7`.

- [ ] **Step 4: Create five YAML assets**

Mirror `ShopFineDining.asset` structure. Script GUID: `00fe1abdabb23524093ed643f4aca030`.  
`category: 4` (Commercial), `incomeModel: 4` (TrafficVariable), `buildFamily: 7`, `buildSubgroup: 0`.

| Asset | id | display | size | cost | baseIncome | maxOccupants | stars | hours (min) | noise | street | basement | color hint |
|-------|-----|---------|------|------|------------|--------------|-------|-------------|-------|--------|----------|------------|
| LeisureGym | leisure_gym | Gym | 6×1 | 205000 | 40 | 8 | 2 | 360–1320 | 0.45 | 1 | 1 | cool grey-blue |
| LeisureSpa | leisure_spa | Spa | 6×1 | 310000 | 90 | 6 | 5 | 600–1260 | 0.25 | 1 | 0 | soft rose-beige |
| LeisurePool | leisure_pool | Pool | 8×2 | 340000 | 35 | 12 | 3 | 480–1260 | 0.55 | 1 | 1 | aqua |
| LeisureBowling | leisure_bowling | Bowling Alley | 10×1 | 275000 | 45 | 10 | 3 | 660–1380 | 0.70 | 1.25 | 1 | warm amber |
| LeisureTheater | leisure_theater | Movie Theater | 8×2 | 385000 | 55 | 16 | 4 | 720–1380 | 0.40 | 1.5 | 1 | deep burgundy |

Set `hasActiveHours: 1` on all. Generate valid 32-hex `.meta` GUIDs for each asset.

- [ ] **Step 5: Re-run tests — expect pass**

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Data/BuildFamily.cs Assets/Scripts/Data/RoomTypeSO.cs Assets/Resources/Rooms/Leisure*.asset Assets/Resources/Rooms/Leisure*.meta Assets/Tests/EditMode/LeisureCatalogTests.cs Assets/Tests/EditMode/LeisureCatalogTests.cs.meta
git commit -m "feat: add Leisure build family and five amenity room assets"
```

---

### Task 2: Traffic venue vs shop demand split + leisure dwell

**Files:**
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (FindOpenShops / CanServe consume path)
- Modify: any Economy midnight path that uses `IsShop` for earnings — keep earnings on **all** traffic venues
- Test: `Assets/Tests/EditMode/LeisureVisitRulesTests.cs`

**Interfaces:**
- Produces: `ShopVisitRules.IsTrafficVenue(RoomTypeSO)`, `IsShop` = traffic + `ResolvedBuildFamily() == Shops`
- Consumes: leisure room ids for dwell ranges

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void IsShop_excludes_leisure_traffic_venues()
{
    var gym = ScriptableObject.CreateInstance<RoomTypeSO>();
    gym.id = "leisure_gym";
    gym.incomeModel = IncomeModel.TrafficVariable;
    gym.buildFamily = BuildFamily.Leisure;
    Assert.IsTrue(ShopVisitRules.IsTrafficVenue(gym));
    Assert.IsFalse(ShopVisitRules.IsShop(gym));
}

[Test]
public void IsShop_still_true_for_food_shop()
{
    var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
    shop.id = "shop_food_fast";
    shop.incomeModel = IncomeModel.TrafficVariable;
    shop.buildFamily = BuildFamily.Shops;
    shop.buildSubgroup = BuildSubgroup.Food;
    Assert.IsTrue(ShopVisitRules.IsShop(shop));
}

[Test]
public void Leisure_dwell_ranges_match_spec()
{
    var rng = new System.Random(1);
    var theater = ScriptableObject.CreateInstance<RoomTypeSO>();
    theater.id = "leisure_theater";
    for (int i = 0; i < 20; i++)
    {
        var d = ShopVisitRules.PickDwellMinutes(theater, rng);
        Assert.GreaterOrEqual(d, 90);
        Assert.LessOrEqual(d, 130);
    }
}
```

- [ ] **Step 2: Run — expect fail**

- [ ] **Step 3: Implement ShopVisitRules**

```csharp
public static bool IsTrafficVenue(RoomTypeSO type) =>
    type != null && type.incomeModel == IncomeModel.TrafficVariable;

public static bool IsShop(RoomTypeSO type) =>
    IsTrafficVenue(type) && type.ResolvedBuildFamily() == BuildFamily.Shops;
```

Update `IsOpen` to use `IsTrafficVenue`.  
In `DwellRange`, add:

```csharp
if (id.StartsWith("leisure_gym", ...)) return (30, 50);
if (id.StartsWith("leisure_spa", ...)) return (45, 75);
if (id.StartsWith("leisure_pool", ...)) return (40, 70);
if (id.StartsWith("leisure_bowling", ...)) return (50, 80);
if (id.StartsWith("leisure_theater", ...)) return (90, 130);
```

- [ ] **Step 4: AgentSystem destination pick**

In `FindOpenShops` (or rename callers carefully): filter with `IsTrafficVenue` + open + slots.  
Where `ShopDemandSystem.CanServe` / `TryConsume` runs before picking a shop, **skip demand consume when `ResolvedBuildFamily() == Leisure`** (still allow pick). Shops keep existing demand gates.

Audit `EconomySystem` midnight: credit earnings when `IsTrafficVenue` (not only `IsShop`). Audit crime loads: concurrent visitors on traffic venues (leisure included) — use `IsTrafficVenue`.

- [ ] **Step 5: Run tests — pass**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat: split traffic venues from shop demand for leisure visits"
```

---

### Task 3: AmenitySystem + stress relief + hotel nudge

**Files:**
- Create: `Assets/Scripts/Economy/AmenitySystem.cs`
- Modify: `Assets/Scripts/Agents/Agent.cs` — `RelieveStress(float)`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs` (or AgentSystem daily tick) to call amenity evaluate
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` hotel occupancy chance to add amenity bonus
- Test: `Assets/Tests/EditMode/AmenitySystemTests.cs`

**Interfaces:**
- Produces:
  - `AmenitySystem.MaxReliefInRange(TowerGrid, RoomInstance home) -> float` (positive magnitude)
  - `AmenitySystem.TryApplyDailyRelief(Agent, TowerGrid, int dayIndex) -> bool`
  - `AmenitySystem.HotelDemandBonus(TowerGrid, RoomInstance hotel) -> float` (0 or `0.08f` if spa/gym in range)
  - Constants: floor delta `2`, horiz `12`, relief table per spec
- Consumes: `Agent.HomeRoom`, `Agent.RelieveStress`, leisure rooms on grid

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Spa_relief_stronger_than_bowling()
{
    Assert.Greater(AmenitySystem.ReliefForId("leisure_spa"), AmenitySystem.ReliefForId("leisure_bowling"));
}

[Test]
public void In_range_spa_relieves_once_per_day()
{
    // place condo home + spa within ±2 floors / ≤12 cells; agent condo resident
    // TryApplyDailyRelief day 1 => true, stress drops by 5
    // TryApplyDailyRelief day 1 again => false
}

[Test]
public void Broken_or_out_of_range_amenity_ignored()
{
    // broken spa in range => no relief
}

[Test]
public void HotelDemandBonus_requires_spa_or_gym_in_range()
{
    Assert.AreEqual(0f, AmenitySystem.HotelDemandBonus(emptyGrid, hotel));
    // with gym in range => 0.08f
}
```

- [ ] **Step 2: Run — fail**

- [ ] **Step 3: Agent.RelieveStress**

```csharp
public void RelieveStress(float amount) =>
    Stress = Mathf.Clamp(Stress - Mathf.Max(0f, amount), 0f, 100f);
```

Add `AmenityReliefDay` int field default `-1` on Agent (or store on AmenitySystem dictionary by agent id — prefer field on Agent for simplicity).

- [ ] **Step 4: Implement AmenitySystem**

Distance: `|amenity.Origin.y - home.Origin.y| <= 2` and horizontal from amenity origin to home origin `Abs(ax-hx) <= 12` (document origin-to-origin).  
Pick max relief among in-range unbroken Leisure rooms.  
`HotelDemandBonus`: `0.08f` if any in-range spa or gym; else `0`.

- [ ] **Step 5: Wire tick**

Once per agent per day during existing day-boundary / midnight stress pass (same place crime daily stress applies is fine): call `TryApplyDailyRelief`.  
In `EconomySystem` hotel fill roll: `chance = Mathf.Clamp01(chance + AmenitySystem.HotelDemandBonus(grid, room))`.

- [ ] **Step 6: Tests pass + commit**

```bash
git commit -m "feat: AmenitySystem proximity stress relief and hotel demand nudge"
```

---

### Task 4: HUD Leisure family + menu icon ids

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` — family list, glyph, color, expand Leisure variants
- Modify: `Assets/Scripts/UI/MenuIconArt.cs` — `MenuIconIdForFamily(Leisure) => "family_leisure"`
- Create: `Assets/Resources/Art/Menu/family_leisure.png` (+ room icon PNGs can land in Task 5; use temporary solid icons if needed for compile)
- Test: extend menu/family tests if present, or `LeisureHudCatalogTests` asserting family enum wired

- [ ] **Step 1: Failing test** — `MenuIconArt.MenuIconIdForFamily(BuildFamily.Leisure) == "family_leisure"`

- [ ] **Step 2: Implement HUD wiring**

Add Leisure to the family strip order (after Shops, before Utility).  
Load Resources rooms with `buildFamily == Leisure` into variant pane (same pattern as Utility/Shops).  
Glyph `"Le"`; color distinct (e.g. teal `0.25, 0.65, 0.70`).

- [ ] **Step 3: Minimal family_leisure PNG** (64×64 warm teal icon; replace in art task if needed)

- [ ] **Step 4: Pass + commit**

```bash
git commit -m "feat: add Leisure family to pictorial build menu"
```

---

### Task 5: Dollhouse + menu art for five rooms

**Files:**
- Create: `Assets/Resources/Art/Dollhouse/gym_6x1.png`, `spa_6x1.png`, `bowling_10x1.png`, `pool_8x2.png`, `theater_8x2.png` (+ `.bytes` twins if other dollhouse assets use them)
- Create: `Assets/Resources/Art/Menu/leisure_gym.png`, `leisure_spa.png`, `leisure_pool.png`, `leisure_bowling.png`, `leisure_theater.png`
- Modify: `Assets/Scripts/Rendering/RoomDollhouseArt.cs` `LeafByTypeId`
- Test: `Assets/Tests/EditMode/LeisureArtTests.cs`

**Interfaces:**
- Leaf names exactly as spec table

- [ ] **Step 1: Failing art map tests**

```csharp
[TestCase("leisure_gym", "gym_6x1")]
[TestCase("leisure_spa", "spa_6x1")]
[TestCase("leisure_pool", "pool_8x2")]
[TestCase("leisure_bowling", "bowling_10x1")]
[TestCase("leisure_theater", "theater_8x2")]
public void Leisure_dollhouse_leaf_mapped(string id, string leaf)
{
    Assert.AreEqual(leaf, RoomDollhouseArt.LeafForTypeId(id)); // use existing public API or test via IsMapped+path
}
```

- [ ] **Step 2: Generate art**

Order: Gym → Spa → Bowling → Pool → Theater.  
Style-lock: side cutaway, dark frame, warm architectural materials, magenta `#FF00FF` or `#FF00AA` key background, no people.  
Aspect: ~cell-proportional width × ~170–220px tall for 1-high; 2-high roughly 2× height.  
Crop contact sheets into whole-room strips; write PNG + `.bytes` + `.meta` matching existing dollhouse import settings.  
Square menu icons: muted materials, readable silhouettes, not pink toy look.

- [ ] **Step 3: Wire LeafByTypeId + verify Resources.Load**

- [ ] **Step 4: Pass + commit**

```bash
git commit -m "art: leisure dollhouse rooms and menu icons"
```

---

### Task 6: SoundProfileMap + smoke verification

**Files:**
- Modify: `Assets/Scripts/Audio/SoundProfileMap.cs`
- Test: extend `SoundProfileMapTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
// gym/spa → Hotel or Condo soft bed; pool → Hotel; bowling → Event or Restaurant; theater → Event
```

Map explicitly by id before family switch:

```csharp
if (type.id == "leisure_spa" || type.id == "leisure_gym") return SoundProfile.Hotel;
if (type.id == "leisure_pool") return SoundProfile.Hotel;
if (type.id == "leisure_bowling") return SoundProfile.Event;
if (type.id == "leisure_theater") return SoundProfile.Event;
```

Also handle `BuildFamily.Leisure` fallback → `SoundProfile.Hotel`.

- [ ] **Step 2: Implement + pass**

- [ ] **Step 3: Play Mode smoke checklist** (manual / note in commit body)

1. At 2★ place Gym; visitors dwell; earnings at midnight.  
2. At 3★ place Pool + Bowling; basement OK.  
3. At 4★ place Theater; 5★ Spa (spa rejects basement).  
4. Hotel near gym shows slightly better fill; condo near spa loses stress once/day.  
5. Dollhouse overlays visible; Leisure menu icons show.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: map leisure rooms to ambient sound profiles"
```

---

## Plan self-review

1. **Spec coverage:** Catalog/stars/sizes → T1; visits/demand split → T2; AmenitySystem → T3; HUD → T4; art → T5; audio → T6. Out-of-scope items omitted.
2. **Placeholders:** None intentional; art generation is concrete pipeline.
3. **Types:** `IsTrafficVenue` / `IsShop` / `RelieveStress` / `HotelDemandBonus` consistent across tasks.
4. **Risk:** Existing tests that assumed `IsShop == TrafficVariable` must be updated in Task 2 if they break.
