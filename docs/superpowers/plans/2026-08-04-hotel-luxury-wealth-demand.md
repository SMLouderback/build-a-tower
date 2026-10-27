# Hotel Luxury Catalog + Four Wealth Bands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship nine hotel room types with Base/Mid/Upper luxury, explicit clean times, climate×luxury fill/tier pressure, and tower-wide four wealth bands (Basic/Mid/Upper/Premium + Street).

**Architecture:** Add `LuxuryBand` on `RoomTypeSO` plus `cleanMinutes`. New `HotelLuxury` / extend `AgentWealth` helpers for band mix, room acceptance, and climate bias. Wire hotel check-in and nightly demand through those helpers. Office/condo rooms stay two-tier; their residents resolve into the four wealth bands via star-based rolls.

**Tech Stack:** Unity 6000.4.x, existing RoomTypeSO / AgentSystem / PricePricing / MarketClimate / NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-08-04-hotel-luxury-wealth-demand-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Do not implement Demand graph UI, spa/gym, proximity/noise heatmaps, or full office/condo luxury catalogs
- Amenity mix multipliers stay 1.0 (stub only)
- Prefer Subagent-Driven Development for this multi-task plan
- No parallel-cli

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Data/LuxuryBand.cs` (new) | Enum `None, Base, Mid, Upper` |
| `Assets/Scripts/Data/RoomTypeSO.cs` | `luxuryBand`, `cleanMinutes` fields |
| `Assets/Scripts/Economy/AgentWealth.cs` | Four living bands + ranges; `ResolveBand` with rng; hotel/office/condo rules |
| `Assets/Scripts/Economy/HotelLuxury.cs` (new) | Guest mix weights; room acceptance; climate bias; fill multipliers; clean fallback |
| `Assets/Scripts/Economy/RoomConditionRules.cs` | `CleanMinutes` uses `cleanMinutes` / band fallback |
| `Assets/Scripts/Economy/PricePricing.cs` or call sites | Hotel effective climate offset = climate + luxury bias |
| `Assets/Scripts/Economy/EconomySystem.cs` | Pass luxury bias into hotel `PassesDemand` |
| `Assets/Scripts/Agents/Agent.cs` | Optional `WealthBand Wealth` stored when rolled |
| `Assets/Scripts/Agents/AgentSystem.cs` | Hotel check-in: roll wealth → accept-set rooms; climate fill gate |
| `Assets/Scripts/UI/TowerHudController.cs` | Load nine hotel buttons; selection hint |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` | Show clean minutes / band if useful |
| `Assets/Resources/Rooms/Hotel*.asset` (+ ScriptableObjects mirrors) | Nine hotel assets; retire single/premium from Resources catalog |
| `Assets/Tests/EditMode/AgentWealthTests.cs` | Update for 4 bands |
| `Assets/Tests/EditMode/RoomConditionTests.cs` | Clean minutes from field |
| `Assets/Tests/EditMode/HotelLuxuryTests.cs` (new) | Mix, acceptance, climate bias |
| `README.md` | Hotel ladder + wealth + deferred demand note |

---

### Task 1: Schema — `LuxuryBand` + `RoomTypeSO` fields

**Files:**
- Create: `Assets/Scripts/Data/LuxuryBand.cs`
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs`
- Test: `Assets/Tests/EditMode/HotelLuxuryTests.cs` (create; smoke field defaults)

**Interfaces:**
- Produces: `public enum LuxuryBand { None = 0, Base = 1, Mid = 2, Upper = 3 }`
- Produces: `RoomTypeSO.luxuryBand` (`LuxuryBand`), `RoomTypeSO.cleanMinutes` (`float`, default `0`)

- [ ] **Step 1: Write failing test for defaults**

```csharp
[Test]
public void RoomTypeSO_luxury_defaults()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    Assert.AreEqual(LuxuryBand.None, so.luxuryBand);
    Assert.AreEqual(0f, so.cleanMinutes);
}
```

- [ ] **Step 2: Run EditMode filter `HotelLuxuryTests.RoomTypeSO_luxury_defaults`**

Expected: FAIL (type/field missing) or compile error.

- [ ] **Step 3: Add enum + fields**

```csharp
// LuxuryBand.cs
namespace BuildATower
{
    public enum LuxuryBand
    {
        None = 0,
        Base = 1,
        Mid = 2,
        Upper = 3
    }
}
```

On `RoomTypeSO` after `isParkingRamp`:

```csharp
public LuxuryBand luxuryBand = LuxuryBand.None;
[Min(0f)] public float cleanMinutes;
```

- [ ] **Step 4: Re-run test — PASS**

- [ ] **Step 5: Commit only if user asked** (otherwise stop)

---

### Task 2: `HotelLuxury` helpers (mix, acceptance, climate)

**Files:**
- Create: `Assets/Scripts/Economy/HotelLuxury.cs`
- Test: `Assets/Tests/EditMode/HotelLuxuryTests.cs`

**Interfaces:**
- Consumes: `LuxuryBand`, `MarketClimate` step constants, `WealthBand` (Task 3 may land in parallel order — if `WealthBand.Mid` missing, do Task 3 first)
- Produces:
  - `static bool AcceptsGuest(LuxuryBand roomBand, WealthBand guest, string roomId = null)`
  - `static WealthBand RollGuestBand(int stars, float averageCrime, int climateStep, Random rng)`
  - `static int LuxuryClimateBias(LuxuryBand band, int climateStep)`
  - `static float CheckInFillMultiplier(LuxuryBand band, int climateStep)`
  - `static float ResolveCleanMinutes(RoomTypeSO type)`

**Crime threshold:** treat `averageCrime >= 40` as “high” for Premium≈0 / Upper cut (document constant `HotelLuxury.HighCrimeThreshold = 40f`).

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void AcceptsGuest_basic_only_base()
{
    Assert.IsTrue(HotelLuxury.AcceptsGuest(LuxuryBand.Base, WealthBand.Basic));
    Assert.IsFalse(HotelLuxury.AcceptsGuest(LuxuryBand.Mid, WealthBand.Basic));
}

[Test]
public void AcceptsGuest_premium_upper_king_or_suite()
{
    Assert.IsTrue(HotelLuxury.AcceptsGuest(LuxuryBand.Upper, WealthBand.Premium, "hotel_upper_suite"));
    Assert.IsTrue(HotelLuxury.AcceptsGuest(LuxuryBand.Upper, WealthBand.Premium, "hotel_upper_king"));
    Assert.IsFalse(HotelLuxury.AcceptsGuest(LuxuryBand.Upper, WealthBand.Premium, "hotel_upper_standard"));
}

[Test]
public void LuxuryClimateBias_upper_recession_is_minus_two()
{
    Assert.AreEqual(-2, HotelLuxury.LuxuryClimateBias(LuxuryBand.Upper, MarketClimate.Recession));
}

[Test]
public void CheckInFillMultiplier_upper_recession_low()
{
    Assert.AreEqual(0.2f, HotelLuxury.CheckInFillMultiplier(LuxuryBand.Upper, MarketClimate.Recession), 0.0001f);
}

[Test]
public void RollGuestBand_high_crime_never_premium()
{
    var rng = new System.Random(1);
    for (var i = 0; i < 80; i++)
    {
        var band = HotelLuxury.RollGuestBand(stars: 5, averageCrime: 50f, climateStep: MarketClimate.Boom, rng);
        Assert.AreNotEqual(WealthBand.Premium, band);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement `HotelLuxury` per spec §6–§7 tables**

Acceptance sets:

| Guest | Rooms |
|-------|-------|
| Basic | Base only |
| Mid | Mid only |
| Upper | Mid Extended (`hotel_mid_extended`) + all Upper |
| Premium | `hotel_upper_king`, `hotel_upper_suite` |

Mix: start 0.40/0.30/0.20/0.10; apply star/crime/climate weight multipliers from spec; renormalize; roll.

`ResolveCleanMinutes`: if `type.cleanMinutes > 0` return it; else band fallback Base=12, Mid=22, Upper=35; else `CleanBasicMinutes`.

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit if asked**

---

### Task 3: Migrate `AgentWealth` to four living bands

**Files:**
- Modify: `Assets/Scripts/Economy/AgentWealth.cs`
- Modify: `Assets/Tests/EditMode/AgentWealthTests.cs`
- Modify: any call sites of `WealthBand.Premium` / `IsPremiumLiving` that assume two bands

**Interfaces:**
- Produces: `WealthBand { Street, Basic, Mid, Upper, Premium }`
- Produces: `ResolveBand(AgentRole role, RoomTypeSO homeType, Random rng)` — overload keeping old 2-arg as `ResolveBand(role, home, new Random(0))` **only if** deterministic fallback documented; prefer updating call sites to pass simulation rng
- Produces: updated `BandRange` per spec §6
- Event visitor → **Mid**

Hotel home mapping:

- `luxuryBand == Base` → Basic  
- `Mid` → Mid  
- `Upper` && id contains `suite` → 50/50 Upper/Premium  
- `Upper` else → Upper  
- Legacy `hotel_premium` / name premium without band → treat as Mid  

Office/Condo:

- `requiredStars < 2` → 30% Basic / 70% Mid  
- `requiredStars ≥ 2` → 70% Upper / 30% Premium  

- [ ] **Step 1: Update/rewrite failing tests in `AgentWealthTests`**

Replace Basic-only living assertions with Mid/Upper/Premium cases; update disposable ranges to spec (Basic 55–110, Mid 90–160, Upper 140–220, Premium 200–320, Street 35–90). Event visitor → Mid.

- [ ] **Step 2: Run `AgentWealthTests` — FAIL**

- [ ] **Step 3: Implement enum + resolve + ranges; fix compile breakages**

Grep for `WealthBand.Premium` and `IsPremiumLiving` / `requiredStars >= 2` wealth checks.

- [ ] **Step 4: Run `AgentWealthTests` — PASS**

- [ ] **Step 5: Commit if asked**

---

### Task 4: Clean minutes + nightly demand luxury bias

**Files:**
- Modify: `Assets/Scripts/Economy/RoomConditionRules.cs`
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` (`PassesDemand` / hotel midnight path)
- Modify: `Assets/Tests/EditMode/RoomConditionTests.cs`
- Test: add `HotelLuxuryTests` demand helper coverage or `EconomySystem` unit if exists

**Interfaces:**
- Consumes: `HotelLuxury.ResolveCleanMinutes`, `HotelLuxury.LuxuryClimateBias`
- Produces: `RoomConditionRules.CleanMinutes` ignores star≥2 for hotels when `cleanMinutes` or `luxuryBand` set
- Produces: effective climate offset for hotel rooms = `climateOffset + LuxuryClimateBias(band, climateStep)`

- [ ] **Step 1: Failing clean-minutes tests**

```csharp
[Test]
public void CleanMinutes_uses_explicit_field()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.category = RoomCategory.Hotel;
    so.cleanMinutes = 55f;
    so.requiredStars = 0;
    Assert.AreEqual(55f, RoomConditionRules.CleanMinutes(so));
}
```

Update old `CleanMinutes_basic_vs_premium_hotel` to band/field behavior (non-hotel or `cleanMinutes==0` + no band may keep legacy star fallback for one release).

- [ ] **Step 2: Implement `CleanMinutes` via `HotelLuxury.ResolveCleanMinutes`**

- [ ] **Step 3: In `EconomySystem.PassesDemand`, if room is hotel with luxury band, add bias using `simulation` climate step**

Signature may need `climateStep` or pass already-adjusted offset from caller. Prefer computing at call site:

```csharp
var offset = climateOffset;
if (room.Type.category == RoomCategory.Hotel)
    offset += HotelLuxury.LuxuryClimateBias(room.Type.luxuryBand, climateStep);
```

Base Recession demand floor (≥0.85 when overprice steps ≤1): implement in `HotelLuxury.DemandChanceFloor` or inside PassesDemand.

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit if asked**

---

### Task 5: Hotel check-in wiring in `AgentSystem`

**Files:**
- Modify: `Assets/Scripts/Agents/Agent.cs` — add `public WealthBand Wealth { get; set; }`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` — SyncHomes / hotel spawn / claim paths
- Test: `Assets/Tests/EditMode/HotelLuxuryTests.cs` or `HotelDirtySyncTests.cs` extension

**Interfaces:**
- Consumes: `HotelLuxury.RollGuestBand`, `AcceptsGuest`, `CheckInFillMultiplier`
- Behavior: when creating/assigning a hotel guest vacancy fill:
  1. Roll wealth from stars + `Crime.AverageCrime` + climate step  
  2. Pick vacant non-dirty hotel room where `AcceptsGuest`  
  3. Apply per-room `CheckInFillMultiplier` as rng gate before claim  
  4. Store `agent.Wealth`; disposable uses that band  

Event hotel visitors: roll wealth Mid-biased or use `RollGuestBand` then acceptance (spec: event visitors are Mid band for spend — may still need Mid-accepting rooms).

- [ ] **Step 1: Write integration-style EditMode test with tiny grid + one Base and one Upper room; Recession + forced rng → Base fills, Upper rarely**

Keep test deterministic with fixed `Random` seed and assert acceptance filtering at helper level if full AgentSystem seed injection is hard — minimum: unit-test a new `TryFindHotelRoomForGuest(grid, wealth, rng)` extracted method.

- [ ] **Step 2: Extract `TryFindHotelRoomForGuest` / wire SyncHomes hotel branch**

- [ ] **Step 3: Tests PASS; existing `HotelDirtySyncTests` still pass**

- [ ] **Step 4: Commit if asked**

---

### Task 6: Hotel room assets + HUD catalog

**Files:**
- Create Resources (+ optional ScriptableObjects mirrors) for all nine hotels per spec economics table  
- Modify: remove or stop loading `HotelPremium` / missing `HotelSingle` from HUD; load nine new ids  
- Modify: `Assets/Scripts/UI/TowerHudController.cs`  
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs` (optional band/clean line)

**Asset checklist (Resources/Rooms):**

| File | id | band | ★ | size | maxOcc | cost | income | clean | color distinct |
|------|----|------|---|------|--------|------|--------|-------|----------------|
| HotelBase.asset | hotel_base | Base | 0 | 3×1 | 2 | 18000 | 1800 | 12 | light lavender |
| HotelAccessible.asset | hotel_accessible | Base | 0 | 3×1 | 2 | 22000 | 1900 | 14 | lavender + slight shift |
| HotelMidStandard.asset | hotel_mid_standard | Mid | 2 | 4×1 | 4 | 45000 | 4000 | 22 | mid purple |
| HotelMidExtended.asset | hotel_mid_extended | Mid | 2 | 6×1 | 6 | 70000 | 5500 | 32 | deeper purple |
| HotelStudio.asset | hotel_studio | Mid | 2 | 5×1 | 3 | 55000 | 4200 | 25 | purple-gray |
| HotelJuniorSuite.asset | hotel_junior_suite | Mid | 2 | 5×1 | 4 | 60000 | 4500 | 28 | purple-rose |
| HotelUpperStandard.asset | hotel_upper_standard | Upper | 3 | 5×1 | 4 | 95000 | 7500 | 35 | rich purple |
| HotelUpperKing.asset | hotel_upper_king | Upper | 3 | 5×1 | 2 | 100000 | 8000 | 35 | deep violet |
| HotelUpperSuite.asset | hotel_upper_suite | Upper | 3 | 8×1 | 8 | 160000 | 12000 | 55 | darkest violet |

All: `category: Hotel`, `incomeModel: NightlyRate`, `requiresHousekeeping: 1`, `allowAboveGround: 1`, `buildFamily: Hotel`.

- [ ] **Step 1: Create assets + `.meta` guids (unique)**

- [ ] **Step 2: HUD `AddRoomButton` for all nine; drop Premium-only button**

- [ ] **Step 3: Selection shows band + clean minutes**

- [ ] **Step 4: Play Mode smoke (manual): 0★ place Base; 2★ Mid; 3★ Upper locks work**

- [ ] **Step 5: Commit if asked**

---

### Task 7: Docs + README

**Files:**
- Modify: `README.md` — hotel ladder, wealth bands, climate note; Demand graph / heatmaps deferred  
- Spec status already Approved; leave Implemented when code lands (flip in this task)

- [ ] **Step 1: Update README play bullets for hotels / wealth**

- [ ] **Step 2: Mark spec `Status: Implemented`**

- [ ] **Step 3: Commit if asked**

---

## Spec coverage check

| Spec section | Task |
|--------------|------|
| §3 Catalog + migration | 6 |
| §4 Schema | 1 |
| §5 Economics numbers | 6 |
| §6 Wealth bands + acceptance + shop light touch | 2, 3, 5 |
| §7 Climate × luxury | 2, 4, 5 |
| §8 HUD | 6 |
| §9–10 Deferred | 7 (notes only) |
| §11 Success criteria / tests | 1–5 |

## Placeholder scan

No TBD steps; crime threshold and acceptance ids are explicit.

## Type consistency

- `LuxuryBand` / `WealthBand` naming stable across tasks  
- `HotelLuxury.*` signatures listed in Task 2; Tasks 4–5 consume them  
- `ResolveBand(..., Random rng)` required after Task 3

---

Plan complete and saved to `docs/superpowers/plans/2026-08-04-hotel-luxury-wealth-demand.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — run tasks in this session with checkpoints  

Which approach?