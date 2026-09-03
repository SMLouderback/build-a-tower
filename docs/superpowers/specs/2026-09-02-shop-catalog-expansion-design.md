# Build-A-Tower — Shop Catalog Expansion

**Date:** 2026-09-02  
**Status:** Implemented  
**Depends on:** Commercial visit traffic (E1), `AgentWealth` spend model, crime floor loads, star gates on `RoomTypeSO`  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Commercial catalog expansion → amenities/attractions → star + wealth demand hooks → numeric rebalance → heatmaps → visual polish

## 1. Goals

Expand the placeable **Shops** catalog so players have a meaningful food and retail ladder without new amenity systems. All new rooms reuse the existing commercial visit pipeline: path → dwell → rolled spend → midnight payout.

### Success criteria

In Play Mode a player can:

1. Place **10 shop types** (6 food, 4 retail) from the build HUD under Shops → Food / Retail.
2. See residents and street visitors visit new shops using existing trip scheduling and afford gates.
3. See **Gag Gifts** receive more street-visitor picks than other open shops (weighted destination, not higher spawn rate).
4. Place a **Department Store** as a **16×2** multi-floor commercial room; visits enter at bottom-left origin only.
5. See crime load scale on both floors when the department store is busy.
6. At midnight, new shops credit `ShopEarningsToday` like existing shops.
7. EditMode tests cover dwell ranges, star gates, department footprint, and street weight pick.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | Shop catalog only — no pool/casino/spa/chapel/atrium |
| Income model | Rolled spend capped by `baseIncome`; batched at **midnight** (`ShopEarningsToday`) |
| `baseIncome` meaning | Pay **cap** per visit, not guaranteed revenue |
| New assets | 7 new `RoomTypeSO` assets; 4 existing shops unchanged |
| Street preference | Optional `streetVisitWeight` on SO; Gag Gifts = `2f`; default `1f` |
| Internal visitor pick | Uniform random among affordable open reachable shops (unchanged) |
| Department store | Multi-cell footprint (`size.y = 2`); no in-store floor routing |
| Star promotion hooks | Out of scope — gates are build unlocks only |
| Shop price tiers | Out of scope |
| Dollhouse art | Placeholder colors until PNGs exist; optional `RoomDollhouseArt` keys |

## 3. Shop catalog

All shops use `IncomeModel.TrafficVariable`, `category = Commercial`, `buildFamily = Shops`.

### 3.1 Food (6 types)

| id | Display | ★ | Size | Slots | Pay cap | Build cost | Dwell (min) | Hours | Noise |
|----|---------|---|------|-------|---------|------------|-------------|-------|-------|
| `shop_food_fast` | Fast Food | 0 | 16×1 | 4 | $25 | $145,000 | 15–25 | 11:00–21:00 | 0.70 |
| `shop_food_taco` | Taco Counter | 0 | 12×1 | 4 | $28 | $138,000 | 12–20 | 11:00–22:00 | 0.65 |
| `shop_food_chicken` | Chicken Shack | 0 | 12×1 | 5 | $30 | $148,000 | 15–25 | 11:00–21:00 | 0.68 |
| `shop_food_restaurant` | Restaurant | 0 | 16×1 | 6 | $50 | $218,000 | 40–60 | 11:00–22:00 | 0.55 |
| `shop_food_mexican` | Mexican Restaurant | 2 | 16×1 | 6 | $60 | $235,000 | 35–50 | 12:00–22:00 | 0.50 |
| `shop_food_fine` | Fine Dining | 3 | 4×1 | 8 | $100 | $290,000 | 40–60 | 12:00–23:00 | 0.45 |

**Food ladder:** three 0★ fast options → two sit-down (0★ Restaurant, 2★ Mexican) → 3★ Fine Dining.

### 3.2 Retail (4 types)

| id | Display | ★ | Size | Slots | Pay cap | Build cost | Dwell (min) | Hours | Noise | Street weight |
|----|---------|---|------|-------|---------|------------|-------------|-------|-------|---------------|
| `shop_retail` | Retail | 0 | 16×1 | 5 | $50 | $174,000 | 20–40 | 10:00–20:00 | 0.45 | 1 |
| `shop_retail_gifts` | Gag Gifts | 1 | 10×1 | 4 | $35 | $158,000 | 15–30 | 10:00–21:00 | 0.40 | **2** |
| `shop_retail_shoes` | Shoe Store | 2 | 12×1 | 4 | $65 | $205,000 | 25–40 | 10:00–19:00 | 0.35 | 1 |
| `shop_retail_department` | Department Store | 3 | **16×2** | 12 | $55 | $385,000 | 30–50 | 10:00–20:00 | 0.50 | 1 |

### 3.3 Department store (multi-floor)

- **Footprint:** `size = (16, 2)` — one `RoomInstance` spanning two floors.
- **Placement:** Standard room rules (support below, not over lobby).
- **Visits:** `ShopEntryCell` = `room.Origin` (bottom-left on lower floor). Upper floor is visual/capacity only; agents do not path inside the store across floors.
- **Crime:** `CrimeFloorLoads.ShopLoadByFloor` already iterates `Origin.y` … `Origin.y + Size.y - 1`; concurrent visitors add load to **both** floors.
- **Anchor role:** High slot count (12), 3★ gate, highest build cost in retail line; strong transit and security demand at peak.

### 3.4 Asset files

Create under `Assets/Resources/Rooms/`:

- `ShopTacoCounter.asset`
- `ShopChickenShack.asset`
- `ShopMexicanRestaurant.asset`
- `ShopGagGifts.asset`
- `ShopShoeStore.asset`
- `ShopDepartmentStore.asset`

Mirror copies in `Assets/ScriptableObjects/Rooms/` if that folder is kept in sync with Resources (match existing shop asset pattern).

## 4. Economy & visitor behavior

### 4.1 Income (unchanged mechanics)

- On dwell completion: `AgentWealth.RollSpend(remaining, shop.Type, rng)` → `room.RecordShopSpend(amount)` + `RecordVisit()`.
- Midnight (`EconomySystem`): credit `ShopEarningsToday`, reset visit/spend counters.
- `baseIncome` is the list-price cap and afford gate input (`AgentWealth.CanAfford`).

### 4.2 Affordability (existing gates)

Street visitors roll `WealthBand.Street` disposable ($35–90). Higher pay caps filter street traffic naturally:

| Shop | Afford gate (approx) | Street-friendly |
|------|----------------------|-----------------|
| Gag Gifts ($35) | $25 | Yes |
| Taco / Chicken ($28–30) | $25 | Yes |
| Department ($55) | ~$28 | Most |
| Mexican ($60) | $30 | Most |
| Shoes ($65) | ~$33 | Many |
| Fine Dining ($100) | $50 | Mostly residents |

No changes to `AgentWealth` band ranges this pass.

### 4.3 Street-visitor destination weight

Add to `RoomTypeSO`:

```csharp
[Min(0f)] public float streetVisitWeight = 1f;
```

- Default `1f` for all shops (including new ones except Gag Gifts).
- **Gag Gifts:** `streetVisitWeight = 2f`.
- Use **only** in `AgentSystem.TrySpawnStreetVisitor` when picking among `FindOpenShops` results: weighted random by `streetVisitWeight` (shops with weight `0` are excluded from street spawn pick only if we ever author that; default stays ≥ 1).
- `TryBeginCommercialTrip` (office/hotel/condo) stays uniform random.

### 4.4 Crime & noise (existing systems)

- **Crime:** Shop concurrent visitors → per-floor load via `CrimeFloorLoads.ShopLoadByFloor`. Department store doubles floor exposure when busy.
- **Noise:** Authored `noiseOutput` on each SO for future heatmaps; no new noise formulas.

## 5. Dwell rules (`ShopVisitRules`)

Extend `DwellRange` with **specific id checks before generic substrings** (fixes `shop_food_mexican` matching `"restaurant"` and `shop_food_fine` falling through to fast-food defaults):

| Id pattern (check order) | Dwell (min) |
|--------------------------|-------------|
| `food_fine` | 40–60 |
| `food_mexican` | 35–50 |
| `food_taco` | 12–20 |
| `food_chicken` | 15–25 |
| `food_fast` | 15–25 |
| `food_restaurant` | 40–60 |
| `retail_gifts` | 15–30 |
| `retail_department` | 30–50 |
| `retail_shoes` | 25–40 |
| `retail` (generic) | 20–40 |
| Fallback by `BuildSubgroup` | Food 15–25, Retail 20–40 |

## 6. Architecture & files

| Area | Change |
|------|--------|
| `RoomTypeSO` | Add `streetVisitWeight` (default `1f`) |
| `Assets/Resources/Rooms/` | 6 new shop `.asset` files |
| `ShopVisitRules.cs` | Dwell branches above; `food_fine` fix |
| `AgentSystem.cs` | Weighted shop pick in `TrySpawnStreetVisitor` |
| `TowerHudController.cs` | `AddRoomButton` for 6 new Resources paths |
| `BuildCatalog` | No code change — auto-groups by `buildSubgroup` |
| `RoomDollhouseArt.cs` | Optional placeholder keys (can omit until art exists) |
| `CrimeFloorLoads` | No change — already multi-floor aware |
| `EconomySystem` | No change |
| Tests | See §7 |

### HUD registration

Add after existing shop lines in `TowerHudController`:

```csharp
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopTacoCounter"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopChickenShack"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopMexicanRestaurant"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopGagGifts"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopShoeStore"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopDepartmentStore"));
```

### Placeholder colors (suggested)

Distinct hues until dollhouse PNGs ship — e.g. taco green-gold, chicken warm red, mexican terracotta, gifts bright magenta, shoes navy, department slate blue-gray.

## 7. Verification

### EditMode tests

| Test | Assert |
|------|--------|
| `ShopVisitRules` dwell | Each new id returns in-range dwell; `food_fine` → 40–60; `food_mexican` → 35–50 (not 40–60 restaurant) |
| `RoomTypeAssetTests` or new catalog test | Loaded Resources assets: correct `id`, `requiredStars`, `size`, `maxOccupants`, `streetVisitWeight` on gifts |
| Department footprint | `shop_retail_department` has `size.y == 2` |
| Street weight pick | Weighted helper: gifts selected ~2× as often as peer with weight 1 over many trials (seeded RNG) |
| `CrimeFloorLoads` | Mock room 16×2 with 3 visitors → load on two floors |
| `BuildCatalog` | Food subgroup count ≥ 6, Retail ≥ 4 when all shops in list |

### Play Mode smoke

1. Place taco + gag gifts on floor 1 near lobby; run to midday — visitors arrive, selection shows visits/spend.
2. Place department store spanning floors 5–6; confirm placement ghost is 2 floors tall.
3. At 3★, department store and fine dining appear in HUD; below 3★ they are gated.

## 8. Out of scope

- Amenities (pool, bowling, theater, spa, atrium, chapel, casino)
- Star evaluation requiring specific shop mixes
- Shop price tiers / comfort demand
- In-store multi-floor agent pathing inside department store
- New global street spawn rate or concurrent cap changes
- Numeric rebalance pass across entire tower economy
- Dollhouse PNG production (placeholder colors only)

## 9. Implementation notes

- **Order of work:** SO assets → `ShopVisitRules` → `streetVisitWeight` + street pick → HUD → tests.
- **No shared shop template system** — each room is an authored asset (matches existing Fast Food / Restaurant pattern).
- **Regression:** Existing four shops unchanged; commercial visit tests must stay green.
