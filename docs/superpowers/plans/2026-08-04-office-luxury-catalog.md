# Office Luxury Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship nine office room types with Base/Mid/Upper luxury, shared climate×luxury demand with hotels, and hotel-like wealth roll + desk acceptance for office hiring.

**Architecture:** Extract shared climate/mix helpers from `HotelLuxury` into `LivingLuxury`. Add `OfficeLuxury` for office acceptance + id constants. Fill office desks via a `FillOfficeVacancies` path (mirror hotels) instead of blind SyncHomes fill. Nine `RoomTypeSO` assets with `luxuryBand`; condo stays 2-tier.

**Tech Stack:** Unity 6000.4.x, existing RoomTypeSO / AgentSystem / EconomySystem / AgentWealth / NUnit EditMode tests (net8 hosts OK if Editor busy).

**Spec:** `docs/superpowers/specs/2026-08-04-office-luxury-catalog-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Do not implement condo luxury catalog, multi-room firm tenants, Demand graph UI, spa/gym, or amenity heatmaps
- Do **not** fix parking/lobby bugs in this plan (see Known bugs below) unless the user asks to interrupt
- Prefer Subagent-Driven Development for this multi-task plan
- No parallel-cli
- Economics numbers may be rebalanced later; use spec tables verbatim for v1

## Known bugs (post-slice — do not fix here)

Logged 2026-08-04 for a follow-up after this plan ships:

1. ~~**Lobby expand blocked by lobby-level parking ramp**~~ — **Fixed:** `CanExtendLobby` / `TryExtendLobby` treat parking ramps as lobby-overlapping transit (like stairs/elevators).
2. **Parking contiguous chain** — Code already supported multi-lot same-floor chains; added longer-chain regression tests. If a third lot fails in play, check for a **1+ cell gap** (lots must edge-touch) or lobby span too narrow to place further lots (now fixed by #1).
3. **Above-ground parking** — Hotels/offices offering above-ground lots is a later feature (out of scope).

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Economy/LivingLuxury.cs` (new) | Shared wealth mix roll; climate bias; fill multipliers; demand chance floor |
| `Assets/Scripts/Economy/HotelLuxury.cs` | Thin wrappers → LivingLuxury; keep hotel acceptance + clean minutes + hotel ids |
| `Assets/Scripts/Economy/OfficeLuxury.cs` (new) | Office acceptance; office id constants; Premium desk preference |
| `Assets/Scripts/Economy/EconomySystem.cs` | Apply luxury climate bias + demand floor for **Office** as well as Hotel |
| `Assets/Scripts/Agents/AgentSystem.cs` | Skip blind office fill; `FillOfficeVacancies` + `TryFindOfficeDeskForWorker` |
| `Assets/Scripts/Economy/AgentWealth.cs` | Office `luxuryBand` resolution when Wealth unset; condo interim unchanged |
| `Assets/Scripts/UI/TowerHudController.cs` | Load nine offices; glyphs; drop Premium-only add |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` | Append office band + desk lines |
| `Assets/Resources/Rooms/Office*.asset` (+ ScriptableObjects mirrors) | Nine offices; remap legacy |
| `Assets/Tests/EditMode/HotelLuxuryTests.cs` | Climate/mix still green via wrappers |
| `Assets/Tests/EditMode/OfficeLuxuryTests.cs` (new) | Acceptance + preference |
| `Assets/Tests/EditMode/EconomySystemTests.cs` | Office climate offset |
| `Assets/Tests/EditMode/AgentWealthTests.cs` | Office luxuryBand path |
| `README.md` | Office ladder docs |
| Spec status → Implemented when done |

---

### Task 1: Extract `LivingLuxury` (shared climate + mix)

**Files:**
- Create: `Assets/Scripts/Economy/LivingLuxury.cs` (+ `.meta` via Unity or copy pattern from sibling)
- Modify: `Assets/Scripts/Economy/HotelLuxury.cs`
- Test: `Assets/Tests/EditMode/HotelLuxuryTests.cs` (existing tests must still pass via wrappers)

**Interfaces:**
- Produces:
  - `LivingLuxury.HighCrimeThreshold` (`float`, same `40f`)
  - `LivingLuxury.RollLivingBand(int stars, float averageCrime, int climateStep, Random rng)` → `WealthBand`
  - `LivingLuxury.LuxuryClimateBias(LuxuryBand band, int climateStep)` → `int`
  - `LivingLuxury.CheckInFillMultiplier(LuxuryBand band, int climateStep)` → `float`
  - `LivingLuxury.DemandChanceFloor(LuxuryBand band, int climateStep, int overpriceSteps)` → `float`
- Consumes: existing `HotelLuxury` test expectations unchanged
- Produces wrappers on `HotelLuxury`: `RollGuestBand` / `LuxuryClimateBias` / `CheckInFillMultiplier` / `DemandChanceFloor` delegate to `LivingLuxury`

- [ ] **Step 1: Write failing test that LivingLuxury matches hotel wrapper**

Add to `HotelLuxuryTests.cs`:

```csharp
[Test]
public void LivingLuxury_climate_bias_matches_hotel_wrapper()
{
    Assert.AreEqual(
        HotelLuxury.LuxuryClimateBias(LuxuryBand.Upper, MarketClimate.Recession),
        LivingLuxury.LuxuryClimateBias(LuxuryBand.Upper, MarketClimate.Recession));
}
```

- [ ] **Step 2: Run EditMode / net8 host for `HotelLuxuryTests`**

Expected: FAIL (LivingLuxury missing) or compile error.

- [ ] **Step 3: Move climate + mix implementation into `LivingLuxury`; wrap from `HotelLuxury`**

```csharp
// LivingLuxury.cs — move bodies of RollGuestBand, LuxuryClimateBias,
// CheckInFillMultiplier, DemandChanceFloor, and their private Apply* helpers.
namespace BuildATower
{
    public static class LivingLuxury
    {
        public const float HighCrimeThreshold = 40f;

        public static WealthBand RollLivingBand(int stars, float averageCrime, int climateStep, Random rng)
        {
            // exact body formerly in HotelLuxury.RollGuestBand
        }

        public static int LuxuryClimateBias(LuxuryBand band, int climateStep) { /* same */ }
        public static float CheckInFillMultiplier(LuxuryBand band, int climateStep) { /* same */ }
        public static float DemandChanceFloor(LuxuryBand band, int climateStep, int overpriceSteps) { /* same */ }
    }
}
```

```csharp
// HotelLuxury.cs — keep AcceptsGuest, ResolveCleanMinutes, hotel id constants.
// Replace moved methods with:
public static WealthBand RollGuestBand(int stars, float averageCrime, int climateStep, Random rng) =>
    LivingLuxury.RollLivingBand(stars, averageCrime, climateStep, rng);

public static int LuxuryClimateBias(LuxuryBand band, int climateStep) =>
    LivingLuxury.LuxuryClimateBias(band, climateStep);

public static float CheckInFillMultiplier(LuxuryBand band, int climateStep) =>
    LivingLuxury.CheckInFillMultiplier(band, climateStep);

public static float DemandChanceFloor(LuxuryBand band, int climateStep, int overpriceSteps) =>
    LivingLuxury.DemandChanceFloor(band, climateStep, overpriceSteps);
```

Prefer single source for crime threshold: `LivingLuxury.HighCrimeThreshold`; update any `HotelLuxury.HighCrimeThreshold` call sites to LivingLuxury (or alias constant).

- [ ] **Step 4: Re-run all `HotelLuxuryTests`**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

```bash
git add Assets/Scripts/Economy/LivingLuxury.cs Assets/Scripts/Economy/LivingLuxury.cs.meta Assets/Scripts/Economy/HotelLuxury.cs Assets/Tests/EditMode/HotelLuxuryTests.cs
git commit -m "refactor: extract LivingLuxury shared climate and wealth mix"
```

---

### Task 2: `OfficeLuxury` acceptance

**Files:**
- Create: `Assets/Scripts/Economy/OfficeLuxury.cs` (+ `.meta`)
- Create: `Assets/Tests/EditMode/OfficeLuxuryTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `LuxuryBand`, `WealthBand`
- Produces:
  - Id constants: `MicroId`, `StudioId`, `BaseId`, `MidStandardId`, `MidClinicId`, `MidTeamId`, `UpperStandardId`, `UpperCornerId`, `UpperFloorId` (exact strings from spec)
  - `OfficeLuxury.AcceptsWorker(LuxuryBand roomBand, WealthBand worker, string roomId = null)` → `bool`
  - `OfficeLuxury.PremiumDeskPreferenceRank(WealthBand wealth, string roomId)` → `int` (lower better; only meaningful for Premium)

Acceptance rules (spec §6):

| Worker | Accepts |
|--------|---------|
| Basic | Base only |
| Mid | Mid only |
| Upper | Mid Team Bay (`office_mid_team`) + all Upper |
| Premium | `office_upper_corner` + `office_upper_floor` |

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class OfficeLuxuryTests
    {
        [Test]
        public void Basic_accepts_base_only()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Base, WealthBand.Basic, OfficeLuxury.MicroId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Basic, OfficeLuxury.MidStandardId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Basic, OfficeLuxury.UpperFloorId));
        }

        [Test]
        public void Mid_accepts_mid_only()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Mid, OfficeLuxury.MidClinicId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Base, WealthBand.Mid, OfficeLuxury.BaseId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Mid, OfficeLuxury.UpperStandardId));
        }

        [Test]
        public void Upper_accepts_team_bay_and_all_upper()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Upper, OfficeLuxury.MidTeamId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Upper, OfficeLuxury.MidStandardId));
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Upper, OfficeLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_accepts_corner_and_corporate_only()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Premium, OfficeLuxury.UpperCornerId));
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Premium, OfficeLuxury.UpperFloorId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Premium, OfficeLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_prefers_corporate_over_corner()
        {
            Assert.Less(
                OfficeLuxury.PremiumDeskPreferenceRank(WealthBand.Premium, OfficeLuxury.UpperFloorId),
                OfficeLuxury.PremiumDeskPreferenceRank(WealthBand.Premium, OfficeLuxury.UpperCornerId));
        }
    }
}
```

- [ ] **Step 2: Run `OfficeLuxuryTests`**

Expected: FAIL (type missing).

- [ ] **Step 3: Implement `OfficeLuxury`**

```csharp
using System;

namespace BuildATower
{
    public static class OfficeLuxury
    {
        public const string MicroId = "office_micro";
        public const string StudioId = "office_studio";
        public const string BaseId = "office_base";
        public const string MidStandardId = "office_mid_standard";
        public const string MidClinicId = "office_mid_clinic";
        public const string MidTeamId = "office_mid_team";
        public const string UpperStandardId = "office_upper_standard";
        public const string UpperCornerId = "office_upper_corner";
        public const string UpperFloorId = "office_upper_floor";

        public static bool AcceptsWorker(LuxuryBand roomBand, WealthBand worker, string roomId = null)
        {
            switch (worker)
            {
                case WealthBand.Basic:
                    return roomBand == LuxuryBand.Base;
                case WealthBand.Mid:
                    return roomBand == LuxuryBand.Mid;
                case WealthBand.Upper:
                    if (roomBand == LuxuryBand.Upper) return true;
                    return roomBand == LuxuryBand.Mid &&
                           string.Equals(roomId, MidTeamId, StringComparison.Ordinal);
                case WealthBand.Premium:
                    return string.Equals(roomId, UpperCornerId, StringComparison.Ordinal) ||
                           string.Equals(roomId, UpperFloorId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary>Lower is better. Premium prefers Corporate Floor, then Corner Suite.</summary>
        public static int PremiumDeskPreferenceRank(WealthBand wealth, string roomId)
        {
            if (wealth != WealthBand.Premium) return 0;
            if (string.Equals(roomId, UpperFloorId, StringComparison.Ordinal)) return 0;
            if (string.Equals(roomId, UpperCornerId, StringComparison.Ordinal)) return 1;
            return 2;
        }
    }
}
```

- [ ] **Step 4: Re-run `OfficeLuxuryTests`**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 3: Economy — office climate bias on demand

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` (`EffectiveDemandClimateOffset`, `PassesDemand`)
- Modify: `Assets/Tests/EditMode/EconomySystemTests.cs`

**Interfaces:**
- Consumes: `LivingLuxury.LuxuryClimateBias`, `LivingLuxury.DemandChanceFloor`
- Produces: Hotel **or** Office rooms apply luxury climate bias/floor (same tables)

- [ ] **Step 1: Write failing test**

```csharp
[Test]
public void EffectiveDemandClimateOffset_office_upper_recession_applies_bias()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.category = RoomCategory.Office;
    so.luxuryBand = LuxuryBand.Upper;
    // Match existing hotel climate-offset tests in this file for how Recession is expressed.
    var climateOffset = MarketClimate.Recession - MarketClimate.Normal;
    var effective = EconomySystem.EffectiveDemandClimateOffset(so, climateOffset);
    Assert.AreEqual(
        climateOffset + LivingLuxury.LuxuryClimateBias(LuxuryBand.Upper, MarketClimate.Recession),
        effective);
}
```

- [ ] **Step 2: Run test — expect FAIL** (Office branch missing; only Hotel today).

- [ ] **Step 3: Update `EconomySystem`**

```csharp
public static int EffectiveDemandClimateOffset(RoomTypeSO type, int climateOffset)
{
    var offset = climateOffset;
    if (type != null &&
        (type.category == RoomCategory.Hotel || type.category == RoomCategory.Office))
    {
        var climateStep = Math.Clamp(
            MarketClimate.Normal + climateOffset,
            MarketClimate.Recession,
            MarketClimate.Boom);
        offset += LivingLuxury.LuxuryClimateBias(type.luxuryBand, climateStep);
    }

    return offset;
}

// In PassesDemand, broaden Hotel-only floor block:
if (room?.Type != null &&
    (room.Type.category == RoomCategory.Hotel || room.Type.category == RoomCategory.Office))
{
    var steps = PricePricing.OverpriceSteps(room.PriceTier, currentStars, offset);
    var floor = LivingLuxury.DemandChanceFloor(room.Type.luxuryBand, climateStep, steps);
    if (floor > chance)
        chance = floor;
}
```

- [ ] **Step 4: Run economy tests including new case**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 4: `AgentSystem` — wealth-gated office desk fill

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: extend `Assets/Tests/EditMode/OfficeLuxuryTests.cs` or add `OfficeFillTests.cs` mirroring any existing `TryFindHotelRoomForGuest` tests

**Interfaces:**
- Consumes: `LivingLuxury.RollLivingBand`, `LivingLuxury.CheckInFillMultiplier`, `OfficeLuxury.AcceptsWorker`, `OfficeLuxury.PremiumDeskPreferenceRank`, condo `reservedDesks`
- Produces:
  - `FillOfficeVacancies(...)` (private OK)
  - `TryFindOfficeDeskForWorker(...)` (public if tests need it — mirror `TryFindHotelRoomForGuest`)
- Behavior: SyncHomes **skips** creating OfficeWorkers in the per-room `while (existing < want)` loop (like hotels). Still trims surplus vs reserved. Then calls `FillOfficeVacancies` after reserved desks are computed.

- [ ] **Step 1: Write failing tests for mismatched wealth**

Search Tests for `TryFindHotelRoomForGuest` and mirror. Intent:

```csharp
[Test]
public void TryFindOfficeDesk_rejects_premium_for_micro()
{
    // Grid with one Micro office (luxury Base, 1 desk), empty.
    // Assert TryFindOfficeDeskForWorker(..., WealthBand.Premium, ...) returns false.
}

[Test]
public void TryFindOfficeDesk_accepts_basic_for_micro()
{
    // Same Micro office; Basic wealth → true, slot 0.
}
```

- [ ] **Step 2: Run test — expect FAIL**

- [ ] **Step 3: Implement fill path**

In SyncHomes office branch, stop adding agents in the while-loop:

```csharp
if (role == AgentRole.OfficeWorker)
{
    reservedDesks.TryGetValue(room.InstanceId, out var reservedSlots);
    want = Mathf.Max(0, room.Type.maxOccupants - reservedSlots);
    while (existing > want)
    {
        if (!TryRemoveSurplusOfficeWorker(room))
            break;
        existing--;
    }
    continue; // vacancies filled in FillOfficeVacancies
}
```

Add methods (mirror hotel fill): count vacancies with `maxOccupants - reserved - occupied`; roll `LivingLuxury.RollLivingBand`; `TryFindOfficeDeskForWorker` filters by `OfficeLuxury.AcceptsWorker`, Premium preference, `CheckInFillMultiplier`; set `Agent.Wealth` on hire.

`EffectiveOfficeLuxuryBand`: `luxuryBand == None ? Base : luxuryBand`.

Call `FillOfficeVacancies` from SyncHomes after the living-room loop (pass `reservedDesks`).

- [ ] **Step 4: Run office fill + hotel regression tests**

Expected: PASS. Condo reservation under-fill still leaves reserved empty desks.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 5: `AgentWealth` — office band from `luxuryBand`

**Files:**
- Modify: `Assets/Scripts/Economy/AgentWealth.cs` (`ResolveOfficeCondoBand`)
- Modify: `Assets/Tests/EditMode/AgentWealthTests.cs`

**Interfaces:**
- Consumes: `RoomTypeSO.luxuryBand`, `OfficeLuxury` ids
- Produces: Office with `luxuryBand != None` maps Base→Basic, Mid→Mid, Upper→Upper (Corner/Corporate 50% Premium when resolving without stored Wealth). Condo + legacy unbanded office keep `requiredStars` mix.

- [ ] **Step 1: Failing test**

```csharp
[Test]
public void ResolveBand_office_mid_is_mid()
{
    var so = ScriptableObject.CreateInstance<RoomTypeSO>();
    so.category = RoomCategory.Office;
    so.luxuryBand = LuxuryBand.Mid;
    so.requiredStars = 2;
    var rng = new System.Random(1);
    Assert.AreEqual(WealthBand.Mid, AgentWealth.ResolveBand(AgentRole.OfficeWorker, so, rng));
}
```

- [ ] **Step 2: Run — expect FAIL** (still star-based → Upper/Premium for stars≥2)

- [ ] **Step 3: Implement**

```csharp
static WealthBand ResolveOfficeCondoBand(RoomTypeSO homeType, Random rng)
{
    if (homeType.category == RoomCategory.Office && homeType.luxuryBand != LuxuryBand.None)
    {
        return homeType.luxuryBand switch
        {
            LuxuryBand.Base => WealthBand.Basic,
            LuxuryBand.Mid => WealthBand.Mid,
            LuxuryBand.Upper =>
                string.Equals(homeType.id, OfficeLuxury.UpperFloorId, StringComparison.Ordinal) ||
                string.Equals(homeType.id, OfficeLuxury.UpperCornerId, StringComparison.Ordinal)
                    ? (rng.Next(2) == 0 ? WealthBand.Upper : WealthBand.Premium)
                    : WealthBand.Upper,
            _ => WealthBand.Mid
        };
    }

    if (homeType.requiredStars < 2)
        return rng.NextDouble() < 0.30 ? WealthBand.Basic : WealthBand.Mid;
    return rng.NextDouble() < 0.70 ? WealthBand.Upper : WealthBand.Premium;
}
```

- [ ] **Step 4: Run AgentWealthTests**

Expected: PASS.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 6: Nine office assets + HUD + selection lines

**Files:**
- Create: `Assets/Resources/Rooms/OfficeMicro.asset` … `OfficeUpperFloor.asset` (+ metas)
- Create: matching `Assets/ScriptableObjects/Rooms/Office*.asset` mirrors
- Keep legacy `Office` / `OfficePremium` only if needed for remap; HUD loads nine new ids
- Ensure `OfficeBase` exists under **Resources**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs`

**Asset field checklist** (script guid `00fe1abdabb23524093ed643f4aca030`):

| Asset name | id | displayName | luxuryBand | requiredStars | size | maxOccupants | buildCost | baseIncome |
|------------|-----|-------------|------------|---------------|------|--------------|-----------|------------|
| OfficeMicro | office_micro | Micro Office | 1 Base | 0 | 3×1 | 1 | 12000 | 900 |
| OfficeStudio | office_studio | Studio Office | 1 | 0 | 4×1 | 1 | 16000 | 1100 |
| OfficeBase | office_base | Small Office | 1 | 0 | 6×1 | 2 | 28000 | 2200 |
| OfficeMidStandard | office_mid_standard | Mid Office | 2 | 2 | 9×1 | 4 | 55000 | 5000 |
| OfficeMidClinic | office_mid_clinic | Professional Suite | 2 | 2 | 10×1 | 6 | 75000 | 7200 |
| OfficeMidTeam | office_mid_team | Team Bay | 2 | 2 | 12×1 | 8 | 95000 | 9600 |
| OfficeUpperStandard | office_upper_standard | Upper Office | 3 | 3 | 12×1 | 6 | 110000 | 9000 |
| OfficeUpperCorner | office_upper_corner | Corner Suite | 3 | 3 | 14×1 | 8 | 140000 | 13000 |
| OfficeUpperFloor | office_upper_floor | Corporate Floor | 3 | 3 | 18×1 | 12 | 200000 | 20000 |

Common fields:

```yaml
category: 1
incomeModel: 1
hasActiveHours: 1
activeHoursStart: 9
activeHoursEnd: 17
allowAboveGround: 1
allowBasement: 0
requiresHousekeeping: 0
cleanMinutes: 0
buildFamily: 1
```

Unique `.meta` guids per asset (do not reuse hotel guids).

- [ ] **Step 1: Create all Resources + ScriptableObjects YAML assets**

- [ ] **Step 2: HUD — load all nine offices**

Replace `OfficePremium`-only add with Micro → UpperFloor loads. Update glyphs (Om, Os, Ob, MS, Cl, Tb, Uo, Uc, Cf) and `ShortLabel` as needed.

- [ ] **Step 3: `RoomEconomyFormat.AppendOfficeSelectionLines`**

```csharp
public static void AppendOfficeSelectionLines(List<string> lines, RoomTypeSO type)
{
    if (lines == null || type == null || type.category != RoomCategory.Office)
        return;
    var band = type.luxuryBand == LuxuryBand.None ? LuxuryBand.Base : type.luxuryBand;
    lines.Add($"Band: {band}");
    lines.Add($"Desks: {Math.Max(1, type.maxOccupants)}");
}
```

Wire into TowerHudController selection builders next to hotel lines.

- [ ] **Step 4: Play Mode smoke (manual)** — nine types; Mid locked @ 2★; Upper @ 3★; Micro places 3×1.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 7: README + spec Implemented

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-08-04-office-luxury-catalog-design.md` — Status: **Implemented**

- [ ] **Step 1: Update README**

Document nine-type Office Base/Mid/Upper ladder, star gates, hotel-like hiring, condo still 2-tier, deferred multi-room firms / demand graph. Do not claim parking bugs fixed.

- [ ] **Step 2: Mark office spec Implemented**

- [ ] **Step 3: Run EditMode filters**

`HotelLuxuryTests`, `OfficeLuxuryTests`, `AgentWealthTests`, `EconomySystemTests`.

Expected: PASS.

- [ ] **Step 4: Commit** (only if user asked)

```bash
git commit -m "docs: mark office luxury catalog implemented"
```

---

## Spec coverage self-check

| Spec section | Task |
|--------------|------|
| §1–2 Goals / locked decisions | All tasks (constraints) |
| §3 Catalog + migration | Task 6 |
| §4 Schema / LivingLuxury | Tasks 1–2 |
| §5 Economics | Task 6 assets |
| §6 Wealth + acceptance + climate | Tasks 2–5 |
| §7 Systems touchpoints | Tasks 3–6 |
| §8 HUD | Task 6 |
| §9 Testing | Tasks 1–5, 7 |
| §10 Deferred + Known bugs | Global constraints |
| §11 Success criteria | Task 7 |

## Placeholder / consistency check

- Id strings match spec (`office_micro` … `office_upper_floor`).
- Climate tables live in `LivingLuxury`; hotel wrappers preserved.
- Office fill respects condo reserved desks.
- No condo catalog or parking-bug fixes in any task.
