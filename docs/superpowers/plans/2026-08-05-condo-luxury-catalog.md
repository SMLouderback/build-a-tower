# Condo Luxury Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship nine condo room types with Base/Mid/Upper luxury, hotel/office-like wealth roll + sale acceptance, shared climate×luxury demand, distinct green placeholder colors, and a blue color ramp fix for the nine offices.

**Architecture:** Add `CondoLuxury` (ids + acceptance + Premium preference). Reuse `LivingLuxury` for mix/climate. Replace blind unsold-condo SyncHomes fill with `FillCondoVacancies` that sells whole units (all `maxOccupants` share one rolled wealth). Extend `EconomySystem` luxury bias to Condo. Nine condo `RoomTypeSO` assets; recolor offices.

**Tech Stack:** Unity 6000.4.x, existing RoomTypeSO / AgentSystem / EconomySystem / AgentWealth / NUnit EditMode tests (net8 hosts OK if Editor busy).

**Spec:** `docs/superpowers/specs/2026-08-05-condo-luxury-catalog-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Do not implement multi-unit household entities, Demand graph UI, spa/gym, amenity heatmaps, or hotel/office desk ladder changes (except office placeholder colors)
- Prefer Subagent-Driven Development for this multi-task plan
- No parallel-cli
- Economics numbers may be rebalanced later; use spec tables verbatim for v1
- `LivingLuxury` already exists — do **not** re-extract from hotels

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Economy/CondoLuxury.cs` (new) | Condo acceptance; id constants; Premium unit preference |
| `Assets/Scripts/Economy/EconomySystem.cs` | Apply luxury climate bias + demand floor for **Condo** (with Hotel/Office) |
| `Assets/Scripts/Agents/AgentSystem.cs` | Skip blind unsold condo fill; `FillCondoVacancies` + `TryFindCondoForBuyer` |
| `Assets/Scripts/Economy/AgentWealth.cs` | Condo `luxuryBand` resolution when Wealth unset |
| `Assets/Scripts/UI/TowerHudController.cs` | Load nine condos; glyphs/short labels; drop Premium-only add |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` | Append condo band + occupant lines |
| `Assets/Resources/Rooms/Condo*.asset` (+ ScriptableObjects mirrors) | Nine condos; remap legacy |
| `Assets/Resources/Rooms/Office*.asset` (+ ScriptableObjects mirrors) | Distinct blue placeholder colors |
| `Assets/Tests/EditMode/CondoLuxuryTests.cs` (new) | Acceptance + preference + find-buyer |
| `Assets/Tests/EditMode/EconomySystemTests.cs` | Condo climate offset |
| `Assets/Tests/EditMode/AgentWealthTests.cs` | Condo luxuryBand path |
| `README.md` | Condo ladder docs; office color note; drop “condo catalog deferred” |
| Spec status → Implemented when done |

---

### Task 1: `CondoLuxury` acceptance

**Files:**
- Create: `Assets/Scripts/Economy/CondoLuxury.cs` (+ `.meta` via Unity or copy pattern from `OfficeLuxury.cs.meta`)
- Create: `Assets/Tests/EditMode/CondoLuxuryTests.cs` (+ `.meta`)

**Interfaces:**
- Produces:
  - Id constants: `StudioId`, `AlcoveId`, `BaseId`, `MidStandardId`, `MidLoftId`, `MidFamilyId`, `UpperStandardId`, `UpperCornerId`, `UpperPenthouseId`
  - `CondoLuxury.AcceptsBuyer(LuxuryBand roomBand, WealthBand buyer, string roomId = null)` → `bool`
  - `CondoLuxury.PremiumUnitPreferenceRank(WealthBand wealth, string roomId)` → `int` (lower better; only meaningful for Premium)
- Consumes: existing `LuxuryBand`, `WealthBand`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class CondoLuxuryTests
    {
        [Test]
        public void Basic_accepts_base_only()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Base, WealthBand.Basic, CondoLuxury.StudioId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Basic, CondoLuxury.MidStandardId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Basic, CondoLuxury.UpperPenthouseId));
        }

        [Test]
        public void Mid_accepts_mid_only()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Mid, CondoLuxury.MidLoftId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Base, WealthBand.Mid, CondoLuxury.BaseId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Mid, CondoLuxury.UpperStandardId));
        }

        [Test]
        public void Upper_accepts_family_and_all_upper()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Upper, CondoLuxury.MidFamilyId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Upper, CondoLuxury.MidStandardId));
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Upper, CondoLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_accepts_corner_and_penthouse_only()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Premium, CondoLuxury.UpperCornerId));
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Premium, CondoLuxury.UpperPenthouseId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Premium, CondoLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_prefers_penthouse_over_corner()
        {
            Assert.Less(
                CondoLuxury.PremiumUnitPreferenceRank(WealthBand.Premium, CondoLuxury.UpperPenthouseId),
                CondoLuxury.PremiumUnitPreferenceRank(WealthBand.Premium, CondoLuxury.UpperCornerId));
        }
    }
}
```

- [ ] **Step 2: Run `CondoLuxuryTests`**

Expected: FAIL (type missing) or compile error.

Run via Unity EditMode filter `CondoLuxury` or net8 host mirroring prior office tasks.

- [ ] **Step 3: Implement `CondoLuxury`**

```csharp
using System;

namespace BuildATower
{
    public static class CondoLuxury
    {
        public const string StudioId = "condo_studio";
        public const string AlcoveId = "condo_alcove";
        public const string BaseId = "condo_base";
        public const string MidStandardId = "condo_mid_standard";
        public const string MidLoftId = "condo_mid_loft";
        public const string MidFamilyId = "condo_mid_family";
        public const string UpperStandardId = "condo_upper_standard";
        public const string UpperCornerId = "condo_upper_corner";
        public const string UpperPenthouseId = "condo_upper_penthouse";

        public static bool AcceptsBuyer(LuxuryBand roomBand, WealthBand buyer, string roomId = null)
        {
            switch (buyer)
            {
                case WealthBand.Basic:
                    return roomBand == LuxuryBand.Base;
                case WealthBand.Mid:
                    return roomBand == LuxuryBand.Mid;
                case WealthBand.Upper:
                    if (roomBand == LuxuryBand.Upper) return true;
                    return roomBand == LuxuryBand.Mid &&
                           string.Equals(roomId, MidFamilyId, StringComparison.Ordinal);
                case WealthBand.Premium:
                    return string.Equals(roomId, UpperCornerId, StringComparison.Ordinal) ||
                           string.Equals(roomId, UpperPenthouseId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary>Lower is better. Premium prefers Penthouse, then Corner Condo.</summary>
        public static int PremiumUnitPreferenceRank(WealthBand wealth, string roomId)
        {
            if (wealth != WealthBand.Premium) return 0;
            if (string.Equals(roomId, UpperPenthouseId, StringComparison.Ordinal)) return 0;
            if (string.Equals(roomId, UpperCornerId, StringComparison.Ordinal)) return 1;
            return 2;
        }
    }
}
```

- [ ] **Step 4: Re-run `CondoLuxuryTests`**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 2: Economy — condo climate bias on demand

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` (`EffectiveDemandClimateOffset`, `PassesDemand`)
- Modify: `Assets/Tests/EditMode/EconomySystemTests.cs`

**Interfaces:**
- Consumes: `LivingLuxury.LuxuryClimateBias`, `LivingLuxury.DemandChanceFloor`
- Produces: Hotel **or** Office **or** Condo rooms apply luxury climate bias/floor

- [ ] **Step 1: Write failing test**

```csharp
[Test]
public void EffectiveDemandClimateOffset_condo_upper_recession_applies_bias()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.category = RoomCategory.Condo;
    so.luxuryBand = LuxuryBand.Upper;
    var climateOffset = MarketClimate.Recession - MarketClimate.Normal;
    var effective = EconomySystem.EffectiveDemandClimateOffset(so, climateOffset);
    Assert.AreEqual(
        climateOffset + LivingLuxury.LuxuryClimateBias(LuxuryBand.Upper, MarketClimate.Recession),
        effective);
}
```

- [ ] **Step 2: Run test — expect FAIL** (Condo branch missing).

- [ ] **Step 3: Update `EconomySystem`**

Broaden both Hotel/Office checks to include Condo:

```csharp
public static int EffectiveDemandClimateOffset(RoomTypeSO type, int climateOffset)
{
    var offset = climateOffset;
    if (type != null &&
        (type.category == RoomCategory.Hotel ||
         type.category == RoomCategory.Office ||
         type.category == RoomCategory.Condo))
    {
        var climateStep = Math.Clamp(
            MarketClimate.Normal + climateOffset,
            MarketClimate.Recession,
            MarketClimate.Boom);
        offset += LivingLuxury.LuxuryClimateBias(type.luxuryBand, climateStep);
    }

    return offset;
}

// In PassesDemand floor block:
if (room?.Type != null &&
    (room.Type.category == RoomCategory.Hotel ||
     room.Type.category == RoomCategory.Office ||
     room.Type.category == RoomCategory.Condo))
{
    var steps = PricePricing.OverpriceSteps(room.PriceTier, currentStars, offset);
    var floor = LivingLuxury.DemandChanceFloor(room.Type.luxuryBand, climateStep, steps);
    if (floor > chance)
        chance = floor;
}
```

Also update the XML doc comment on `EffectiveDemandClimateOffset` to mention Condo.

- [ ] **Step 4: Run economy tests including new case**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 3: `AgentSystem` — wealth-gated condo sale fill

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Modify: `Assets/Tests/EditMode/CondoLuxuryTests.cs` (add find-buyer / fill tests)

**Interfaces:**
- Consumes: `LivingLuxury.RollLivingBand`, `LivingLuxury.CheckInFillMultiplier`, `CondoLuxury.AcceptsBuyer`, `CondoLuxury.PremiumUnitPreferenceRank`, `EconomySystem.EffectiveDemandClimateOffset` (or equivalent demand chance with luxury bias), existing `CanReachCondoFromLobby`
- Produces:
  - `FillCondoVacancies(TowerGrid grid, int stars, float averageCrime, int climateStep, int climateOffset)` (private OK)
  - `TryFindCondoForBuyer(...)` (public for tests — mirror `TryFindOfficeDeskForWorker`)
- Behavior:
  - SyncHomes **skips** creating residents for **unsold empty** condos (`continue` like hotels/offices).
  - **Sold** (or already occupied) condos keep the existing `while (existing < want)` maintain loop.
  - After office/hotel fill, call `FillCondoVacancies`.
  - Each successful buyer attempt fills **the whole unit** (`maxOccupants` agents, same `Wealth`).
  - Claimable unit: Condo category, not broken, `!CondoSold`, zero home occupants, reachable from lobby, price-tier demand passes with luxury-aware offset, acceptance matches wealth, fill multiplier gate, Premium preference.

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void TryFindCondo_rejects_premium_for_studio()
{
    // Build small grid: lobby + stairs + one Studio condo (empty, unsold, reachable).
    // Assert agents.TryFindCondoForBuyer(grid, WealthBand.Premium, climateStep: Normal,
    //     climateOffset: 0, currentStars: 0, rng, out _, out _) == false.
}

[Test]
public void TryFindCondo_accepts_basic_for_studio()
{
    // Same Studio; Basic → true.
}

[Test]
public void FillCondoVacancies_spawns_full_household_same_wealth()
{
    // One Mid Condo (maxOccupants 3), Normal climate, force Basic-fail path with only Mid room:
    // After SyncHomes/Fill with Mid-friendly setup, assert 3 CondoResidents on that room,
    // all Wealth == Mid (use controlled rng seed or direct TryFind + manual spawn assert via public API).
}
```

Mirror existing condo/office test harnesses in `CondoCommuteTests` / `OfficeLuxuryTests` for grid setup (lobby, stairs, place room). Prefer calling public `TryFindCondoForBuyer` for acceptance; add a focused SyncHomes integration test if one already exists for condo demand.

Update private `PassesCondoDemand` to use luxury-aware offset:

```csharp
bool PassesCondoDemand(RoomInstance room, int currentStars, int climateOffset = 0)
{
    var offset = EconomySystem.EffectiveDemandClimateOffset(room?.Type, climateOffset);
    var climateStep = System.Math.Clamp(
        MarketClimate.Normal + climateOffset,
        MarketClimate.Recession,
        MarketClimate.Boom);
    var chance = PricePricing.DemandChance(room.PriceTier, currentStars, offset);
    if (room?.Type != null && room.Type.category == RoomCategory.Condo)
    {
        var steps = PricePricing.OverpriceSteps(room.PriceTier, currentStars, offset);
        var floor = LivingLuxury.DemandChanceFloor(room.Type.luxuryBand, climateStep, steps);
        if (floor > chance)
            chance = floor;
    }
    if (chance >= 1f) return true;
    if (chance <= 0f) return false;
    return _rng.NextDouble() < chance;
}
```

(Or call `EconomySystem.PassesDemand` if an instance is available without new wiring — prefer duplicating the chance math above to avoid constructor coupling.)

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement fill path**

In SyncHomes condo branch, before the shared `while (existing < want)`:

```csharp
if (role == AgentRole.CondoResident && !room.CondoSold && existing == 0)
    continue; // vacancies filled in FillCondoVacancies
```

Remove the old early `CanReachCondoFromLobby` / `PassesCondoDemand` continues that only existed to gate that blind fill (reachability + demand move into `TryFindCondoForBuyer`). Keep replan-for-moving block only if still relevant for in-flight buyers after fill.

After `FillHotelVacancies(...)`:

```csharp
FillCondoVacancies(grid, currentStars, averageCrime, climateStep, climateOffset);
```

Implement:

```csharp
void FillCondoVacancies(
    TowerGrid grid, int stars, float averageCrime, int climateStep, int climateOffset)
{
    if (grid == null) return;
    var vacancies = CountUnsoldEmptyCondos(grid);
    if (vacancies <= 0) return;
    var maxAttempts = System.Math.Max(vacancies * 8, 8);
    for (var attempt = 0; attempt < maxAttempts && vacancies > 0; attempt++)
    {
        var wealth = LivingLuxury.RollLivingBand(stars, averageCrime, climateStep, _rng);
        if (!TryFindCondoForBuyer(
                grid, wealth, climateStep, climateOffset, stars, _rng, out var room))
            continue;

        var want = System.Math.Max(1, room.Type.maxOccupants);
        for (var slot = 0; slot < want; slot++)
        {
            var homeCell = HomeCell(room, slot);
            var agent = new Agent(_nextId++, AgentRole.CondoResident, room, homeCell)
            {
                HomeSlot = slot,
                Wealth = wealth
            };
            ConfigureSchedule(agent);
            _agents.Add(agent);
        }
        // Replan empty Moving paths like the old unsold block if needed.
        vacancies--;
    }
}

public bool TryFindCondoForBuyer(
    TowerGrid grid,
    WealthBand wealth,
    int climateStep,
    int climateOffset,
    int currentStars,
    System.Random rng,
    out RoomInstance room)
{
    room = null;
    if (grid == null || rng == null) return false;

    RoomInstance best = null;
    var bestRank = int.MaxValue;

    foreach (var candidate in grid.Rooms)
    {
        if (!IsClaimableUnsoldCondo(candidate)) continue;
        if (!CanReachCondoFromLobby(grid, candidate)) continue;
        if (!PassesCondoDemand(candidate, currentStars, climateOffset)) continue;

        var band = EffectiveCondoLuxuryBand(candidate.Type);
        if (!CondoLuxury.AcceptsBuyer(band, wealth, candidate.Type.id))
            continue;

        var rank = CondoLuxury.PremiumUnitPreferenceRank(wealth, candidate.Type.id);
        if (rank >= bestRank) continue;
        bestRank = rank;
        best = candidate;
    }

    if (best == null) return false;

    var fill = LivingLuxury.CheckInFillMultiplier(EffectiveCondoLuxuryBand(best.Type), climateStep);
    if (fill < 1f && rng.NextDouble() >= fill)
        return false;

    room = best;
    return true;
}

static LuxuryBand EffectiveCondoLuxuryBand(RoomTypeSO type)
{
    if (ReferenceEquals(type, null)) return LuxuryBand.None;
    return type.luxuryBand == LuxuryBand.None ? LuxuryBand.Base : type.luxuryBand;
}

static bool IsClaimableUnsoldCondo(RoomInstance room)
{
    if (room == null || ReferenceEquals(room.Type, null)) return false;
    if (room.Type.category != RoomCategory.Condo) return false;
    if (room.IsBroken) return false;
    if (room.CondoSold) return false;
    if (room.Type.maxOccupants <= 0) return false;
    return true;
}

int CountUnsoldEmptyCondos(TowerGrid grid)
{
    var n = 0;
    foreach (var room in grid.Rooms)
    {
        if (!IsClaimableUnsoldCondo(room)) continue;
        if (CountHomeOccupants(room) > 0) continue;
        n++;
    }
    return n;
}
```

`TryFindCondoForBuyer` must also require `CountHomeOccupants(candidate) == 0` inside the loop (empty unit only).

- [ ] **Step 4: Re-run condo luxury + commute regression tests**

Expected: PASS (`CondoCommuteTests`, `CondoLuxuryTests`, related `AgentSystemTests` sale/move-in).

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 4: `AgentWealth` — condo `luxuryBand` path

**Files:**
- Modify: `Assets/Scripts/Economy/AgentWealth.cs`
- Modify: `Assets/Tests/EditMode/AgentWealthTests.cs`

**Interfaces:**
- Consumes: `CondoLuxury.UpperPenthouseId`, `CondoLuxury.UpperCornerId`
- Produces: Condo with `luxuryBand != None` resolves like offices (Base→Basic, Mid→Mid, Upper→Upper, Corner/Penthouse 50/50 Upper/Premium)

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void ResolveBand_condo_mid_is_mid()
{
    var so = Living(RoomCategory.Condo, requiredStars: 2, luxuryBand: LuxuryBand.Mid);
    Assert.AreEqual(WealthBand.Mid,
        AgentWealth.ResolveBand(AgentRole.CondoResident, so, new System.Random(1)));
}

[Test]
public void ResolveBand_condo_penthouse_mixes_upper_premium()
{
    var so = Living(RoomCategory.Condo, "condo_upper_penthouse",
        requiredStars: 3, luxuryBand: LuxuryBand.Upper);
    // Sample many rolls; expect both Upper and Premium appear (mirror office corner/floor test).
}
```

Adjust `ResolveBand_office_condo_low_stars_mix` / high-stars tests: office unbanded + condo **unbanded** still use stars interim; banded condo uses luxury path. Keep unbanded condo in those mix tests or split condo-banded cases.

- [ ] **Step 2: Run — expect FAIL** on new banded condo tests.

- [ ] **Step 3: Implement in `ResolveOfficeCondoBand`**

```csharp
static WealthBand ResolveOfficeCondoBand(RoomTypeSO homeType, Random rng)
{
    if (homeType.category == RoomCategory.Office && homeType.luxuryBand != LuxuryBand.None)
    {
        // existing office switch unchanged
    }

    if (homeType.category == RoomCategory.Condo && homeType.luxuryBand != LuxuryBand.None)
    {
        return homeType.luxuryBand switch
        {
            LuxuryBand.Base => WealthBand.Basic,
            LuxuryBand.Mid => WealthBand.Mid,
            LuxuryBand.Upper =>
                string.Equals(homeType.id, CondoLuxury.UpperPenthouseId, StringComparison.Ordinal) ||
                string.Equals(homeType.id, CondoLuxury.UpperCornerId, StringComparison.Ordinal)
                    ? (rng.Next(2) == 0 ? WealthBand.Upper : WealthBand.Premium)
                    : WealthBand.Upper,
            _ => WealthBand.Mid
        };
    }

    // Condo + legacy office without luxuryBand: existing stars mix unchanged
    ...
}
```

- [ ] **Step 4: Re-run `AgentWealthTests`**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 5: Nine condo assets + office color ramp

**Files:**
- Create under `Assets/Resources/Rooms/` and `Assets/ScriptableObjects/Rooms/`:
  - `CondoStudio`, `CondoAlcove`, `CondoBase`, `CondoMidStandard`, `CondoMidLoft`, `CondoMidFamily`, `CondoUpperStandard`, `CondoUpperCorner`, `CondoUpperPenthouse` (+ `.meta` each)
- Remap / retire play use of legacy `condo` / `condo_premium` (keep assets if needed for save remaps; catalog must not require them)
- Modify all nine `Office*.asset` pairs: distinct `placeholderColor` blues

**Interfaces:**
- Spec catalog table + economics table + color requirement

- [ ] **Step 1: Write asset-field assertions** (in `CondoLuxuryTests` or `CondoCatalogTests.cs`)

```csharp
[Test]
public void Condo_catalog_assets_match_spec()
{
    AssertCondo("Rooms/CondoStudio", "condo_studio", LuxuryBand.Base, 0, 4, 1, 35000, 65000);
    AssertCondo("Rooms/CondoAlcove", "condo_alcove", LuxuryBand.Base, 0, 5, 2, 45000, 85000);
    AssertCondo("Rooms/CondoBase", "condo_base", LuxuryBand.Base, 0, 8, 2, 80000, 150000);
    AssertCondo("Rooms/CondoMidStandard", "condo_mid_standard", LuxuryBand.Mid, 2, 10, 3, 120000, 200000);
    AssertCondo("Rooms/CondoMidLoft", "condo_mid_loft", LuxuryBand.Mid, 2, 12, 2, 140000, 230000);
    AssertCondo("Rooms/CondoMidFamily", "condo_mid_family", LuxuryBand.Mid, 2, 14, 4, 160000, 270000);
    AssertCondo("Rooms/CondoUpperStandard", "condo_upper_standard", LuxuryBand.Upper, 3, 12, 3, 180000, 300000);
    AssertCondo("Rooms/CondoUpperCorner", "condo_upper_corner", LuxuryBand.Upper, 3, 14, 4, 220000, 360000);
    AssertCondo("Rooms/CondoUpperPenthouse", "condo_upper_penthouse", LuxuryBand.Upper, 3, 18, 4, 280000, 450000);
}

static void AssertCondo(string path, string id, LuxuryBand band, int stars, int width, int occ, int build, int sale)
{
    var so = Resources.Load<RoomTypeSO>(path);
    Assert.IsNotNull(so, path);
    Assert.AreEqual(id, so.id);
    Assert.AreEqual(RoomCategory.Condo, so.category);
    Assert.AreEqual(band, so.luxuryBand);
    Assert.AreEqual(stars, so.requiredStars);
    Assert.AreEqual(width, so.size.x);
    Assert.AreEqual(1, so.size.y);
    Assert.AreEqual(occ, so.maxOccupants);
    Assert.AreEqual(build, so.buildCost);
    Assert.AreEqual(sale, so.baseIncome);
    Assert.AreEqual(IncomeModel.UpfrontSale, so.incomeModel);
}

[Test]
public void Office_placeholder_colors_are_not_identical()
{
    var colors = new[]
    {
        Resources.Load<RoomTypeSO>("Rooms/OfficeMicro").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeStudio").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeBase").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeMidStandard").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeMidClinic").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeMidTeam").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeUpperStandard").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeUpperCorner").placeholderColor,
        Resources.Load<RoomTypeSO>("Rooms/OfficeUpperFloor").placeholderColor,
    };
    var distinct = new HashSet<Color>();
    foreach (var c in colors)
        distinct.Add(c);
    Assert.GreaterOrEqual(distinct.Count, 7, "Offices should vary placeholderColor within the blue family");
}

[Test]
public void Condo_placeholder_colors_are_not_identical()
{
    // Same idea for the nine condo Resources paths; expect ≥7 distinct greens.
}
```

- [ ] **Step 2: Run — expect FAIL** (assets missing / colors identical).

- [ ] **Step 3: Create condo YAML assets**

Copy structure from `OfficeMicro.asset` / `CondoPremium.asset`. Set `category: 2` (Condo), `incomeModel: 3` (UpfrontSale), `buildFamily` to match other condos (inspect `CondoPremium` / `RoomTypeSO` for condo family enum value — use same as legacy Condo), `noiseSensitivity: 0.8`, `luxuryBand` 1/2/3 for Base/Mid/Upper, `cleanMinutes: 0`.

**Condo green ramp** (hotel-style lightness steps — adjust slightly if needed for visibility):

| Asset | RGB (approx) |
|-------|----------------|
| Studio | (0.72, 0.88, 0.70) |
| Alcove | (0.62, 0.82, 0.58) |
| One Bedroom / Base | (0.50, 0.76, 0.48) |
| Mid Standard | (0.40, 0.70, 0.42) |
| Loft | (0.34, 0.64, 0.40) |
| Family | (0.28, 0.58, 0.36) |
| Upper Standard | (0.22, 0.52, 0.32) |
| Corner | (0.18, 0.46, 0.28) |
| Penthouse | (0.12, 0.38, 0.24) |

**Office blue ramp:**

| Asset | RGB (approx) |
|-------|----------------|
| Micro | (0.55, 0.72, 0.92) |
| Studio | (0.48, 0.66, 0.90) |
| Base | (0.40, 0.60, 0.88) |
| Mid Standard | (0.32, 0.54, 0.84) |
| Clinic | (0.26, 0.50, 0.80) |
| Team Bay | (0.22, 0.46, 0.76) |
| Upper Standard | (0.18, 0.40, 0.72) |
| Corner | (0.14, 0.34, 0.66) |
| Corporate Floor | (0.10, 0.28, 0.58) |

Mirror every Resources asset under ScriptableObjects. Generate `.meta` guids (Unity import or copy+new guid).

Legacy: leave `Condo.asset` / `CondoPremium.asset` in place for remaps but HUD must load the nine new ids. Optionally set legacy ids’ `luxuryBand` for safety if still referenced in tests — update tests that `Resources.Load("Rooms/Condo")` to `CondoBase` / create Resources `CondoBase`.

**Critical:** Create `Assets/Resources/Rooms/CondoBase.asset` (legacy Condo lived only under ScriptableObjects).

- [ ] **Step 4: Re-run catalog color tests**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 6: HUD + selection lines

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs`
- Optionally extend existing UI format tests if present

**Interfaces:**
- Consumes: nine condo Resources paths; `AppendCondoSelectionLines`

- [ ] **Step 1: Replace CondoPremium-only load with nine condo loads**

In `EnsureElevatorAndCatalog`, replace:

```csharp
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoPremium"));
```

with:

```csharp
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoStudio"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoAlcove"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoBase"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoMidStandard"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoMidLoft"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoMidFamily"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoUpperStandard"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoUpperCorner"));
AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoUpperPenthouse"));
```

If `placeableRooms` still includes legacy Condo, dedupe by id in `AddRoomButton` (existing behavior) or remove legacy from the inspector list when convenient.

- [ ] **Step 2: Short labels + glyphs**

Extend `ShortLabel` for the nine display names (Studio, Alcove, One Bedroom, Mid Condo, Loft, Family Condo, Upper Condo, Corner Condo, Penthouse). Keep family chip `Co` / green.

- [ ] **Step 3: `RoomEconomyFormat.AppendCondoSelectionLines`**

```csharp
public static void AppendCondoSelectionLines(List<string> lines, RoomTypeSO type)
{
    if (lines == null || type == null || type.category != RoomCategory.Condo)
        return;
    var band = type.luxuryBand == LuxuryBand.None ? LuxuryBand.Base : type.luxuryBand;
    lines.Add($"Band: {band}");
    lines.Add($"Occupants: {System.Math.Max(1, type.maxOccupants)}");
}
```

Wire call sites next to hotel/office append (selection + build detail).

- [ ] **Step 4: Smoke — compile / existing HUD tests if any**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 7: README + spec status

**Files:**
- Modify: `README.md` (living-room / stars / wealth / deferred bullets)
- Modify: `docs/superpowers/specs/2026-08-05-condo-luxury-catalog-design.md` → **Status: Implemented**
- Modify: office/hotel specs’ follow-up lines that say “condo later” if still open-ended

- [ ] **Step 1: Update README**

- Link condo luxury spec alongside hotel/office.  
- Stars bullet: Mid/Upper **condos** unlock at 2★/3★ like hotels/offices (not “premium Condo”).  
- Wealth bullet: condo buyers use tower mix + acceptance (remove “condo still via stars”).  
- Climate bullet: hotel/office/**condo** fill + luxury pressure.  
- Deferred: remove “condo luxury catalog”; keep demand graph, heatmaps, multi-room firms, households.  
- Example button tag: e.g. `One Bedroom $80k · $150k once`.

- [ ] **Step 2: Mark spec Implemented**

- [ ] **Step 3: Quick grep for stale “condo still 2-tier” / “condo catalog deferred” in docs/README**

- [ ] **Step 4: Commit** (only if user asked)

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| Nine condo types + migration | 5, 6 |
| CondoLuxury acceptance + Premium prefer penthouse | 1, 3 |
| LivingLuxury climate fill + demand bias on Condo | 2, 3 |
| FillCondoVacancies whole-unit sale | 3 |
| Agent.Wealth on residents | 3, 4 |
| Move-in sale / jobs unchanged | 3 (regressions) |
| Green condo colors + blue office fix | 5 |
| HUD / selection | 6 |
| README + success criteria docs | 7 |
| Tests listed in spec §10 | 1–5 |

## Execution

After plan approval, run **Subagent-Driven Development** (user preference — do not ask Inline vs SDD). Ledger under `.superpowers/sdd/` (do not commit).
