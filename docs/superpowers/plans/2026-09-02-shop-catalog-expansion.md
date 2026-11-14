# Shop Catalog Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add six new commercial shop types (taco, chicken, Mexican sit-down, gag gifts, shoes, department store) to the build catalog with correct dwell rules, street-visitor weighting for Gag Gifts, and EditMode test coverage.

**Architecture:** Each shop is an authored `RoomTypeSO` asset (`IncomeModel.TrafficVariable`). `ShopVisitRules` owns dwell ranges and a new `PickWeightedShop` helper. `RoomTypeSO.streetVisitWeight` (default `1f`) biases street-visitor destination picks only in `AgentSystem.TrySpawnStreetVisitor`. Income, crime, and resident trip logic stay unchanged.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode tests, existing `AgentSystem` / `CrimeFloorLoads` / `BuildCatalog`

## Global Constraints

- Shop catalog only — no amenities, price tiers, or in-store multi-floor pathing
- Income = rolled spend capped by `baseIncome`; batched at **midnight** (`ShopEarningsToday`)
- `streetVisitWeight`: Gag Gifts = **2f**; all others = **1f**
- Department store footprint: **16×2**; visits enter at `room.Origin` only
- Star gates: Mexican **2★**, Gag Gifts **1★**, Shoes **2★**, Department **3★**
- Existing four shops unchanged
- Spec: `docs/superpowers/specs/2026-09-02-shop-catalog-expansion-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Data/RoomTypeSO.cs` | Add `streetVisitWeight` field |
| `Assets/Scripts/Economy/ShopVisitRules.cs` | Dwell branches + `PickWeightedShop` |
| `Assets/Scripts/Agents/AgentSystem.cs` | Use weighted pick in `TrySpawnStreetVisitor` |
| `Assets/Resources/Rooms/Shop*.asset` | Six new shop assets (+ existing four untouched) |
| `Assets/ScriptableObjects/Rooms/Shop*.asset` | Mirror copies (match existing shop pattern) |
| `Assets/Scripts/UI/TowerHudController.cs` | `AddRoomButton` for six new Resources paths |
| `Assets/Tests/EditMode/ShopVisitRulesTests.cs` | Dwell + weighted pick tests |
| `Assets/Tests/EditMode/RoomTypeAssetTests.cs` | Resource asset assertions |
| `Assets/Tests/EditMode/CrimeSystemTests.cs` | Multi-floor shop load test |
| `Assets/Tests/EditMode/BuildCatalogTests.cs` | Food/retail subgroup counts |

**Test runner:** Unity → Window → General → Test Runner → EditMode. Filter by fixture name after each task.

---

### Task 1: Extend dwell rules (`ShopVisitRules`)

**Files:**
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs`
- Test: `Assets/Tests/EditMode/ShopVisitRulesTests.cs`

**Interfaces:**
- Consumes: existing `ShopVisitRules.PickDwellMinutes(RoomTypeSO, Random)`
- Produces: updated `DwellRange` ordering — specific id checks before generic `restaurant` / `retail` substrings

- [ ] **Step 1: Add failing dwell tests**

Append to `ShopVisitRulesTests.cs`:

```csharp
[Test]
public void PickDwellMinutes_fine_dining_uses_restaurant_range_not_fast_food()
{
    var fine = ScriptableObject.CreateInstance<RoomTypeSO>();
    fine.id = "shop_food_fine";
    fine.incomeModel = IncomeModel.TrafficVariable;

    var rng = new System.Random(42);
    for (var i = 0; i < 20; i++)
        Assert.That(ShopVisitRules.PickDwellMinutes(fine, rng), Is.InRange(40, 60));
}

[Test]
public void PickDwellMinutes_mexican_uses_mid_sit_down_not_restaurant_range()
{
    var mexican = ScriptableObject.CreateInstance<RoomTypeSO>();
    mexican.id = "shop_food_mexican";
    mexican.incomeModel = IncomeModel.TrafficVariable;

    var rng = new System.Random(42);
    for (var i = 0; i < 20; i++)
        Assert.That(ShopVisitRules.PickDwellMinutes(mexican, rng), Is.InRange(35, 50));
}

[Test]
public void PickDwellMinutes_new_shop_ids_use_authored_ranges()
{
    var rng = new System.Random(7);
    AssertDwell("shop_food_taco", 12, 20, rng);
    AssertDwell("shop_food_chicken", 15, 25, rng);
    AssertDwell("shop_retail_gifts", 15, 30, rng);
    AssertDwell("shop_retail_shoes", 25, 40, rng);
    AssertDwell("shop_retail_department", 30, 50, rng);
}

static void AssertDwell(string id, int lo, int hi, System.Random rng)
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.id = id;
    so.incomeModel = IncomeModel.TrafficVariable;
    for (var i = 0; i < 15; i++)
        Assert.That(ShopVisitRules.PickDwellMinutes(so, rng), Is.InRange(lo, hi));
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Filter: `ShopVisitRulesTests` — new tests fail (`food_fine` returns 15–25, `food_mexican` returns 40–60).

- [ ] **Step 3: Replace `DwellRange` in `ShopVisitRules.cs`**

```csharp
static (int lo, int hi) DwellRange(RoomTypeSO type)
{
    if (type != null && !string.IsNullOrEmpty(type.id))
    {
        var id = type.id;
        if (id.IndexOf("food_fine", StringComparison.OrdinalIgnoreCase) >= 0)
            return (40, 60);
        if (id.IndexOf("food_mexican", StringComparison.OrdinalIgnoreCase) >= 0)
            return (35, 50);
        if (id.IndexOf("food_taco", StringComparison.OrdinalIgnoreCase) >= 0)
            return (12, 20);
        if (id.IndexOf("food_chicken", StringComparison.OrdinalIgnoreCase) >= 0)
            return (15, 25);
        if (id.IndexOf("food_fast", StringComparison.OrdinalIgnoreCase) >= 0)
            return (15, 25);
        if (id.IndexOf("food_restaurant", StringComparison.OrdinalIgnoreCase) >= 0)
            return (40, 60);
        if (id.IndexOf("retail_gifts", StringComparison.OrdinalIgnoreCase) >= 0)
            return (15, 30);
        if (id.IndexOf("retail_department", StringComparison.OrdinalIgnoreCase) >= 0)
            return (30, 50);
        if (id.IndexOf("retail_shoes", StringComparison.OrdinalIgnoreCase) >= 0)
            return (25, 40);
        if (id.IndexOf("retail", StringComparison.OrdinalIgnoreCase) >= 0)
            return (20, 40);
    }

    if (type != null)
    {
        var subgroup = type.ResolvedBuildSubgroup();
        if (subgroup == BuildSubgroup.Food)
            return (15, 25);
        if (subgroup == BuildSubgroup.Retail)
            return (20, 40);
    }

    return (20, 40);
}
```

Remove the old branch that matched generic `"restaurant"` substring (it caused `food_mexican` to hit 40–60).

- [ ] **Step 4: Run tests — expect PASS**

All `ShopVisitRulesTests` green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Economy/ShopVisitRules.cs Assets/Tests/EditMode/ShopVisitRulesTests.cs
git commit -m "fix: extend shop dwell ranges for expanded catalog"
```

---

### Task 2: Street visit weight field + weighted pick helper

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs`
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs`
- Test: `Assets/Tests/EditMode/ShopVisitRulesTests.cs`

**Interfaces:**
- Produces: `RoomTypeSO.streetVisitWeight` (`float`, default `1f`, `[Min(0f)]`)
- Produces: `ShopVisitRules.PickWeightedShop(IReadOnlyList<RoomInstance> shops, System.Random rng)` → `RoomInstance` or `null`

- [ ] **Step 1: Add failing weighted-pick tests**

```csharp
[Test]
public void PickWeightedShop_null_or_empty_returns_null()
{
    Assert.IsNull(ShopVisitRules.PickWeightedShop(null, new System.Random(1)));
    Assert.IsNull(ShopVisitRules.PickWeightedShop(new List<RoomInstance>(), new System.Random(1)));
}

[Test]
public void PickWeightedShop_single_shop_returns_it()
{
    var grid = new TowerGrid();
    grid.TryPlaceLobby(LobbySo(), 0, 4, 0, out _);
    var type = ShopSo("shop_retail", 1f);
    grid.TryPlace(type, new Vector2Int(1, 0), out var shop);

    var picked = ShopVisitRules.PickWeightedShop(new List<RoomInstance> { shop }, new System.Random(1));
    Assert.AreSame(shop, picked);
}

[Test]
public void PickWeightedShop_biases_toward_higher_streetVisitWeight()
{
    var giftsType = ShopSo("shop_retail_gifts", 2f);
    var retailType = ShopSo("shop_retail", 1f);
    var gifts = new RoomInstance(1, giftsType, new Vector2Int(0, 0));
    var retail = new RoomInstance(2, retailType, new Vector2Int(4, 0));
    var shops = new List<RoomInstance> { gifts, retail };

    var rng = new System.Random(12345);
    var giftsPicks = 0;
    const int trials = 6000;
    for (var i = 0; i < trials; i++)
    {
        var pick = ShopVisitRules.PickWeightedShop(shops, rng);
        if (pick == gifts) giftsPicks++;
    }

    // Expected ~2/3; allow generous tolerance for RNG
    Assert.That(giftsPicks, Is.InRange(3600, 4800));
}

static RoomTypeSO LobbySo()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.id = "lobby";
    so.isLobby = true;
    so.size = Vector2Int.one;
    so.allowAboveGround = true;
    return so;
}

static RoomTypeSO ShopSo(string id, float streetVisitWeight)
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.id = id;
    so.category = RoomCategory.Commercial;
    so.incomeModel = IncomeModel.TrafficVariable;
    so.streetVisitWeight = streetVisitWeight;
    so.size = Vector2Int.one;
    so.allowAboveGround = true;
    so.maxOccupants = 4;
    return so;
}
```

Add `using System.Collections.Generic;` if missing.

- [ ] **Step 2: Run tests — expect FAIL** (`PickWeightedShop` not defined; `streetVisitWeight` missing)

- [ ] **Step 3: Add field to `RoomTypeSO.cs`** (after `maxOccupants`):

```csharp
[Min(0f)] public float streetVisitWeight = 1f;
```

- [ ] **Step 4: Add helper to `ShopVisitRules.cs`**

```csharp
using System.Collections.Generic;

public static RoomInstance PickWeightedShop(IReadOnlyList<RoomInstance> shops, System.Random rng)
{
    if (shops == null || shops.Count == 0) return null;
    if (shops.Count == 1) return shops[0];

    var total = 0f;
    foreach (var shop in shops)
    {
        var w = shop?.Type?.streetVisitWeight ?? 1f;
        if (w > 0f) total += w;
    }

    if (total <= 0f)
        return shops[rng.Next(shops.Count)];

    var roll = (float)(rng.NextDouble() * total);
    foreach (var shop in shops)
    {
        var w = shop?.Type?.streetVisitWeight ?? 1f;
        if (w <= 0f) continue;
        roll -= w;
        if (roll < 0f) return shop;
    }

    return shops[shops.Count - 1];
}
```

- [ ] **Step 5: Run tests — expect PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Data/RoomTypeSO.cs Assets/Scripts/Economy/ShopVisitRules.cs Assets/Tests/EditMode/ShopVisitRulesTests.cs
git commit -m "feat: add streetVisitWeight and weighted shop pick helper"
```

---

### Task 3: Wire weighted pick in `AgentSystem.TrySpawnStreetVisitor`

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`

**Interfaces:**
- Consumes: `ShopVisitRules.PickWeightedShop(List<RoomInstance>, Random)`
- `TryBeginCommercialTrip` must stay uniform: `shops[_rng.Next(shops.Count)]` unchanged

- [ ] **Step 1: Replace uniform pick in `TrySpawnStreetVisitor`**

Find:

```csharp
var shop = shops[_rng.Next(shops.Count)];
```

Replace with:

```csharp
var shop = ShopVisitRules.PickWeightedShop(shops, _rng);
if (shop == null) return false;
```

Leave `TryBeginCommercialTrip` line `var shop = shops[_rng.Next(shops.Count)];` **unchanged**.

- [ ] **Step 2: Run full EditMode suite — expect PASS**

Existing `CommercialVisitTests` must stay green (resident trips unaffected).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Agents/AgentSystem.cs
git commit -m "feat: weight street visitor shop destination by streetVisitWeight"
```

---

### Task 4: Create six shop ScriptableObject assets

**Files:**
- Create: `Assets/Resources/Rooms/ShopTacoCounter.asset`
- Create: `Assets/Resources/Rooms/ShopChickenShack.asset`
- Create: `Assets/Resources/Rooms/ShopMexicanRestaurant.asset`
- Create: `Assets/Resources/Rooms/ShopGagGifts.asset`
- Create: `Assets/Resources/Rooms/ShopShoeStore.asset`
- Create: `Assets/Resources/Rooms/ShopDepartmentStore.asset`
- Create: same six files under `Assets/ScriptableObjects/Rooms/` (copy YAML; Unity generates `.meta` on import)

**Script GUID:** `00fe1abdabb23524093ed643f4aca030` (same as existing `ShopFastFood.asset`)

**Enum values:** `category: 4` (Commercial), `incomeModel: 4` (TrafficVariable), `buildFamily: 4` (Shops), `buildSubgroup: 1` (Food) or `2` (Retail)

- [ ] **Step 1: Create `ShopTacoCounter.asset`**

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 00fe1abdabb23524093ed643f4aca030, type: 3}
  m_Name: ShopTacoCounter
  m_EditorClassIdentifier: BuildATower.Runtime::BuildATower.RoomTypeSO
  id: shop_food_taco
  displayName: Taco Counter
  category: 4
  size: {x: 12, y: 1}
  buildCost: 138000
  placeholderColor: {r: 0.55, g: 0.75, b: 0.25, a: 1}
  incomeModel: 4
  baseIncome: 28
  noiseOutput: 0.65
  noiseSensitivity: 0
  requiresHousekeeping: 0
  hasActiveHours: 1
  activeHoursStart: 660
  activeHoursEnd: 1320
  allowAboveGround: 1
  allowBasement: 1
  isLobby: 0
  isScaffolding: 0
  isStairs: 0
  isElevatorShaft: 0
  requiredStars: 0
  maxOccupants: 4
  streetVisitWeight: 1
  buildFamily: 4
  buildSubgroup: 1
```

- [ ] **Step 2: Create `ShopChickenShack.asset`**

Same template; change: `m_Name: ShopChickenShack`, `id: shop_food_chicken`, `displayName: Chicken Shack`, `buildCost: 148000`, `placeholderColor: {r: 0.9, g: 0.35, b: 0.2, a: 1}`, `baseIncome: 30`, `noiseOutput: 0.68`, `activeHoursEnd: 1260`, `maxOccupants: 5`

- [ ] **Step 3: Create `ShopMexicanRestaurant.asset`**

Change: `id: shop_food_mexican`, `displayName: Mexican Restaurant`, `buildCost: 235000`, `placeholderColor: {r: 0.82, g: 0.38, b: 0.22, a: 1}`, `baseIncome: 60`, `noiseOutput: 0.5`, `activeHoursStart: 720`, `requiredStars: 2`, `maxOccupants: 6`

- [ ] **Step 4: Create `ShopGagGifts.asset`**

Change: `id: shop_retail_gifts`, `displayName: Gag Gifts`, `size: {x: 10, y: 1}`, `buildCost: 158000`, `placeholderColor: {r: 0.92, g: 0.2, b: 0.75, a: 1}`, `baseIncome: 35`, `noiseOutput: 0.4`, `activeHoursEnd: 1260`, `requiredStars: 1`, `streetVisitWeight: 2`, `buildSubgroup: 2`

- [ ] **Step 5: Create `ShopShoeStore.asset`**

Change: `id: shop_retail_shoes`, `displayName: Shoe Store`, `size: {x: 12, y: 1}`, `buildCost: 205000`, `placeholderColor: {r: 0.2, g: 0.28, b: 0.55, a: 1}`, `baseIncome: 65`, `noiseOutput: 0.35`, `activeHoursEnd: 1140`, `requiredStars: 2`, `maxOccupants: 4`, `buildSubgroup: 2`

- [ ] **Step 6: Create `ShopDepartmentStore.asset`**

Change: `id: shop_retail_department`, `displayName: Department Store`, `size: {x: 16, y: 2}`, `buildCost: 385000`, `placeholderColor: {r: 0.45, g: 0.52, b: 0.62, a: 1}`, `baseIncome: 55`, `noiseOutput: 0.5`, `requiredStars: 3`, `maxOccupants: 12`, `buildSubgroup: 2`

- [ ] **Step 7: Copy all six to `Assets/ScriptableObjects/Rooms/`**

- [ ] **Step 8: Commit**

```bash
git add Assets/Resources/Rooms/ShopTacoCounter.asset Assets/Resources/Rooms/ShopChickenShack.asset Assets/Resources/Rooms/ShopMexicanRestaurant.asset Assets/Resources/Rooms/ShopGagGifts.asset Assets/Resources/Rooms/ShopShoeStore.asset Assets/Resources/Rooms/ShopDepartmentStore.asset Assets/ScriptableObjects/Rooms/
git commit -m "feat: add six new shop room type assets"
```

---

### Task 5: Resource asset tests

**Files:**
- Modify: `Assets/Tests/EditMode/RoomTypeAssetTests.cs`

- [ ] **Step 1: Add failing catalog test**

```csharp
[Test]
public void New_shop_resources_match_expanded_catalog()
{
    AssertShop("Rooms/ShopTacoCounter", "shop_food_taco", 0, 28, 4, new Vector2Int(12, 1), 1f);
    AssertShop("Rooms/ShopChickenShack", "shop_food_chicken", 0, 30, 5, new Vector2Int(12, 1), 1f);
    AssertShop("Rooms/ShopMexicanRestaurant", "shop_food_mexican", 2, 60, 6, new Vector2Int(16, 1), 1f);
    AssertShop("Rooms/ShopGagGifts", "shop_retail_gifts", 1, 35, 4, new Vector2Int(10, 1), 2f);
    AssertShop("Rooms/ShopShoeStore", "shop_retail_shoes", 2, 65, 4, new Vector2Int(12, 1), 1f);
    AssertShop("Rooms/ShopDepartmentStore", "shop_retail_department", 3, 55, 12, new Vector2Int(16, 2), 1f);
}

static void AssertShop(
    string path,
    string id,
    int stars,
    int payCap,
    int slots,
    Vector2Int size,
    float streetWeight)
{
    var room = Resources.Load<RoomTypeSO>(path);
    Assert.IsNotNull(room, $"{path} should load from Resources");
    Assert.AreEqual(id, room.id);
    Assert.AreEqual(stars, room.requiredStars);
    Assert.AreEqual(payCap, room.baseIncome);
    Assert.AreEqual(slots, room.maxOccupants);
    Assert.AreEqual(size, room.size);
    Assert.AreEqual(IncomeModel.TrafficVariable, room.incomeModel);
    Assert.AreEqual(streetWeight, room.streetVisitWeight, 0.001f);
    Assert.AreEqual(BuildFamily.Shops, room.ResolvedBuildFamily());
}
```

- [ ] **Step 2: Run test — expect PASS** (after Task 4 assets exist)

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/EditMode/RoomTypeAssetTests.cs
git commit -m "test: assert new shop resource catalog fields"
```

---

### Task 6: HUD registration

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (~line 225, after `ShopFineDining`)

- [ ] **Step 1: Add room buttons**

```csharp
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopTacoCounter"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopChickenShack"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopMexicanRestaurant"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopGagGifts"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopShoeStore"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopDepartmentStore"));
```

- [ ] **Step 2: Play Mode smoke** (manual)

Enter Play → Build → Shops → Food shows 6 entries, Retail shows 4. Star-gated shops hidden below required ★.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/TowerHudController.cs
git commit -m "feat: register expanded shop catalog in build HUD"
```

---

### Task 7: Build catalog + multi-floor crime tests

**Files:**
- Modify: `Assets/Tests/EditMode/BuildCatalogTests.cs`
- Modify: `Assets/Tests/EditMode/CrimeSystemTests.cs`

- [ ] **Step 1: Add BuildCatalog test**

```csharp
[Test]
public void Group_shop_subgroups_include_expanded_food_and_retail_counts()
{
    var rooms = new List<RoomTypeSO>
    {
        Resources.Load<RoomTypeSO>("Rooms/ShopFastFood"),
        Resources.Load<RoomTypeSO>("Rooms/ShopTacoCounter"),
        Resources.Load<RoomTypeSO>("Rooms/ShopChickenShack"),
        Resources.Load<RoomTypeSO>("Rooms/ShopRestaurant"),
        Resources.Load<RoomTypeSO>("Rooms/ShopMexicanRestaurant"),
        Resources.Load<RoomTypeSO>("Rooms/ShopFineDining"),
        Resources.Load<RoomTypeSO>("Rooms/ShopRetail"),
        Resources.Load<RoomTypeSO>("Rooms/ShopGagGifts"),
        Resources.Load<RoomTypeSO>("Rooms/ShopShoeStore"),
        Resources.Load<RoomTypeSO>("Rooms/ShopDepartmentStore"),
    };

    var groups = BuildCatalog.Group(rooms);
    var shops = groups.Find(g => g.Family == BuildFamily.Shops);
    Assert.IsNotNull(shops);
    Assert.AreEqual(2, shops.Subgroups.Count);

    var food = shops.Subgroups.Find(s => s.Subgroup == BuildSubgroup.Food);
    var retail = shops.Subgroups.Find(s => s.Subgroup == BuildSubgroup.Retail);
    Assert.IsNotNull(food);
    Assert.IsNotNull(retail);
    Assert.GreaterOrEqual(food.Rooms.Count, 6);
    Assert.GreaterOrEqual(retail.Rooms.Count, 4);
}
```

- [ ] **Step 2: Add multi-floor crime test to `CrimeSystemTests.cs`**

```csharp
[Test]
public void ShopLoadByFloor_spans_all_floors_for_multi_floor_shop()
{
    var grid = new TowerGrid();
    Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 16, 0, out _));
    Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));

    var dept = DepartmentStore();
    Assert.IsTrue(grid.TryPlace(dept, new Vector2Int(1, 3), out var shop));

    Assert.IsTrue(shop.TryOccupyVisitorSlot());
    Assert.IsTrue(shop.TryOccupyVisitorSlot());
    Assert.IsTrue(shop.TryOccupyVisitorSlot());

    var loads = CrimeFloorLoads.ShopLoadByFloor(grid);
    Assert.IsTrue(loads.TryGetValue(3, out var lower));
    Assert.IsTrue(loads.TryGetValue(4, out var upper));
    Assert.AreEqual(3f, lower);
    Assert.AreEqual(3f, upper);
}

static RoomTypeSO DepartmentStore()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.id = "shop_retail_department";
    so.category = RoomCategory.Commercial;
    so.size = new Vector2Int(16, 2);
    so.allowAboveGround = true;
    so.incomeModel = IncomeModel.TrafficVariable;
    so.baseIncome = 55;
    so.maxOccupants = 12;
    return so;
}
```

- [ ] **Step 3: Run tests — expect PASS**

- [ ] **Step 4: Commit**

```bash
git add Assets/Tests/EditMode/BuildCatalogTests.cs Assets/Tests/EditMode/CrimeSystemTests.cs
git commit -m "test: expanded shop catalog grouping and multi-floor crime load"
```

---

### Task 8: Spec status + final verification

**Files:**
- Modify: `docs/superpowers/specs/2026-09-02-shop-catalog-expansion-design.md` (status line only)

- [ ] **Step 1: Update spec status**

Change header status to: `**Status:** Implemented`

- [ ] **Step 2: Run all EditMode tests — expect full green**

- [ ] **Step 3: Play Mode smoke checklist**

1. Place Taco Counter + Gag Gifts on floor 1; advance to midday — visits and spend appear on selection panel.
2. Place Department Store on floors 5–6; placement ghost is 2 floors tall.
3. Below 3★, Department Store and Fine Dining buttons are gated; at 3★ they appear.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-09-02-shop-catalog-expansion-design.md
git commit -m "docs: mark shop catalog expansion spec implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| 10 shop types in HUD | Task 4 + 6 |
| Street weight for Gag Gifts | Task 2 + 3 |
| Department 16×2 | Task 4 + 5 |
| Dwell rules + food_fine fix | Task 1 |
| Crime on both floors | Task 7 |
| Existing shops unchanged | Global constraint; no edits to existing four `.asset` files |
| No amenities / price tiers | Out of scope — no tasks |

No placeholders. Type names consistent across tasks (`streetVisitWeight`, `PickWeightedShop`).
