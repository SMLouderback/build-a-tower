# Leisure Attractions (Casino, Nightclub, Chapel, Atrium) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Casino, Nightclub, Chapel, and a 3-floor Atrium under Leisure with visit/amenity hooks, atrium transit + marketing, and EditMode coverage.

**Architecture:** Four new `RoomTypeSO` assets. Casino/Nightclub/Chapel reuse `IncomeModel.TrafficVariable` leisure visits; crime load scaled in `CrimeFloorLoads`. Atrium is `IncomeModel.None` + `isAtrium`: vertical walk edges in `StairsPathfinder`, transfer floors in `TowerGrid`, no stair stress on atrium climbs, wider amenity range, and non-stacking `AtriumMarketing` bonuses wired into hotel fill, condo demand, and street spawn.

**Tech Stack:** Unity 6000.x, C#, NUnit EditMode, existing Leisure / Amenity / Stairs / AgentSystem patterns

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-30-leisure-attractions-casino-atrium-design.md`
- No gambling minigame; atrium is not a traffic venue
- Atrium proximity **±6 / 24**; other leisure **±2 / 12**
- Marketing: hotel +0.05, condo +0.05, street spawn ×1.15; boolean ≥1 atrium
- Unity: prefer Pipeline `unity command` when Editor is open; else batchmode EditMode
- TDD: failing EditMode tests before implementation
- Commit after each task; do not stage unrelated dirty files

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Data/RoomTypeSO.cs` | Add `isAtrium` |
| `Assets/Resources/Rooms/LeisureCasino.asset` (+ Nightclub, Chapel, Atrium) | Catalog assets |
| `Assets/Scripts/UI/TowerHudController.cs` | Wire Resources.Load for four rooms |
| `Assets/Scripts/Economy/AmenitySystem.cs` | Relief ids + atrium range |
| `Assets/Scripts/Economy/CrimeFloorLoads.cs` | Crime load multipliers by leisure id |
| `Assets/Scripts/Economy/ShopVisitRules.cs` | Dwell ranges for casino/nightclub/chapel |
| `Assets/Scripts/Economy/AtriumMarketing.cs` | Non-stacking marketing helpers |
| `Assets/Scripts/Transit/StairsPathfinder.cs` | Vertical edges on atrium cells |
| `Assets/Scripts/Core/TowerGrid.cs` | Atrium floors as transfer floors |
| `Assets/Scripts/Agents/AgentSystem.cs` | Marketing wire-up; atrium stair stress skip |
| `Assets/Tests/EditMode/LeisureCatalogTests.cs` | Extend catalog assertions |
| `Assets/Tests/EditMode/AmenitySystemTests.cs` | Atrium range + new relief |
| `Assets/Tests/EditMode/AtriumTransitTests.cs` | Pathing, transfer, stress |
| `Assets/Tests/EditMode/AtriumMarketingTests.cs` | Non-stacking bonuses |
| `Assets/Tests/EditMode/CrimeFloorLoadsTests.cs` | Multipliers |

---

### Task 1: Catalog assets + HUD + dwell rules

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs` (add `public bool isAtrium;`)
- Create: `Assets/Resources/Rooms/LeisureCasino.asset`, `LeisureNightclub.asset`, `LeisureChapel.asset`, `LeisureAtrium.asset` (+ `.meta` via Unity import or mirror existing Leisure `.meta` pattern)
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (`CollectMenuRoomButtons` TryAdd list)
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs` (dwell ranges)
- Modify: `Assets/Tests/EditMode/LeisureCatalogTests.cs`

**Interfaces:**
- Consumes: existing `RoomTypeSO` YAML / `Resources.Load("Rooms/…")`
- Produces: four loadable assets; atrium `incomeModel = None` (0), `isAtrium = 1`, `maxOccupants = 0`; casino/nightclub/chapel `TrafficVariable` (4)

- [ ] **Step 1: Write failing catalog tests**

Extend `LeisureCatalogTests`:

```csharp
[Test]
public void New_leisure_attractions_match_spec()
{
    AssertRoom(Load("LeisureCasino"), "leisure_casino", 10, 1, 4, true);
    AssertRoom(Load("LeisureNightclub"), "leisure_nightclub", 8, 1, 3, true);
    AssertRoom(Load("LeisureChapel"), "leisure_chapel", 6, 1, 2, true);

    var atrium = Load("LeisureAtrium");
    Assert.IsNotNull(atrium);
    Assert.AreEqual("leisure_atrium", atrium.id);
    Assert.AreEqual(new Vector2Int(8, 3), atrium.size);
    Assert.AreEqual(3, atrium.requiredStars);
    Assert.AreEqual(IncomeModel.None, atrium.incomeModel);
    Assert.IsTrue(atrium.isAtrium);
    Assert.AreEqual(0, atrium.maxOccupants);
    Assert.IsFalse(ShopVisitRules.IsTrafficVenue(atrium));
}

[Test]
public void Casino_nightclub_chapel_noise_and_street_weights()
{
    var casino = Load("LeisureCasino");
    Assert.AreEqual(0.85f, casino.noiseOutput, 0.001f);
    Assert.AreEqual(1.5f, casino.streetVisitWeight, 0.001f);
    var club = Load("LeisureNightclub");
    Assert.AreEqual(0.90f, club.noiseOutput, 0.001f);
    Assert.AreEqual(1.75f, club.streetVisitWeight, 0.001f);
    var chapel = Load("LeisureChapel");
    Assert.AreEqual(0.15f, chapel.noiseOutput, 0.001f);
}
```

Update `AssertRoom` for TrafficVariable rooms only (keep atrium assertions separate as above).

Add dwell assertions in the same file or a small test:

```csharp
Assert.AreEqual((40, 70), ShopVisitRules.DwellRangeForTests("leisure_casino"));
```

If no public dwell helper exists, assert via `PickDwellMinutes` bounds over many rolls, or extract:

```csharp
public static (int min, int max) DwellRangeForId(string id) { /* existing private switch made testable */ }
```

- [ ] **Step 2: Run tests — expect FAIL** (missing assets / `isAtrium`)

- [ ] **Step 3: Implement**

1. Add `public bool isAtrium;` on `RoomTypeSO` (near `isStairs`).
2. Create four YAML assets mirroring `LeisureTheater.asset` / `LeisureGym.asset` with spec fields:

| Asset | id | size | stars | cost | baseIncome | maxOccupants | noise | street | hours | incomeModel | isAtrium |
|-------|-----|------|-------|------|------------|--------------|-------|--------|-------|-------------|----------|
| Casino | leisure_casino | 10×1 | 4 | 420000 | 80 | 14 | 0.85 | 1.5 | 720–120 (wrap) | 4 | 0 |
| Nightclub | leisure_nightclub | 8×1 | 3 | 295000 | 55 | 12 | 0.90 | 1.75 | 1200–240 | 4 | 0 |
| Chapel | leisure_chapel | 6×1 | 2 | 185000 | 25 | 8 | 0.15 | 1.0 | 480–1200 | 4 | 0 |
| Atrium | leisure_atrium | 8×3 | 3 | 450000 | 0 | 0 | 0.20 | 1 | hasActiveHours 0 | 0 | 1 |

`buildFamily: 7`, `category: 4`, `allowAboveGround: 1`, basement true for casino/nightclub/chapel; atrium above-ground only (`allowBasement: 0`).

3. In `TowerHudController.CollectMenuRoomButtons`, after existing leisure loads:

```csharp
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureCasino"));
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureNightclub"));
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureChapel"));
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureAtrium"));
```

4. In `ShopVisitRules` dwell switch, add casino / nightclub / chapel ranges from spec.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/RoomTypeSO.cs Assets/Resources/Rooms/LeisureCasino.asset Assets/Resources/Rooms/LeisureNightclub.asset Assets/Resources/Rooms/LeisureChapel.asset Assets/Resources/Rooms/LeisureAtrium.asset Assets/Resources/Rooms/LeisureCasino.asset.meta Assets/Resources/Rooms/LeisureNightclub.asset.meta Assets/Resources/Rooms/LeisureChapel.asset.meta Assets/Resources/Rooms/LeisureAtrium.asset.meta Assets/Scripts/UI/TowerHudController.cs Assets/Scripts/Economy/ShopVisitRules.cs Assets/Tests/EditMode/LeisureCatalogTests.cs
git commit -m "feat: add Casino Nightclub Chapel Atrium leisure catalog"
```

---

### Task 2: Amenity relief + atrium range

**Files:**
- Modify: `Assets/Scripts/Economy/AmenitySystem.cs`
- Modify: `Assets/Tests/EditMode/AmenitySystemTests.cs`

**Interfaces:**
- Consumes: `RoomTypeSO.isAtrium`, room ids
- Produces:
  - `AmenitySystem.AtriumMaxFloorDelta = 6`
  - `AmenitySystem.AtriumMaxHorizontalCells = 24`
  - `ReliefForId` returns casino 3, nightclub 2, chapel 4, atrium 5
  - `IsInRange` uses atrium constants when amenity `isAtrium` (or id starts with `leisure_atrium`)

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Atrium_relief_uses_wider_range()
{
    var grid = new TowerGrid();
    // place lobby + condo at (0,1), atrium origin far but within 24 cells / 6 floors
    // assert MaxReliefInRange == 5 inside range
    // move condo to 25 cells or 7 floors → relief 0 from atrium
}

[Test]
public void Other_leisure_still_uses_tight_range()
{
    // spa at delta floor 3 → no relief (still MaxFloorDelta 2)
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement** in `AmenitySystem`:

```csharp
public const int AtriumMaxFloorDelta = 6;
public const int AtriumMaxHorizontalCells = 24;

public static float ReliefForId(string id)
{
    // existing + 
    if (id.StartsWith("leisure_casino", …)) return 3f;
    if (id.StartsWith("leisure_nightclub", …)) return 2f;
    if (id.StartsWith("leisure_chapel", …)) return 4f;
    if (id.StartsWith("leisure_atrium", …)) return 5f;
}

static bool IsInRange(RoomInstance amenity, Vector2Int homeOrigin)
{
    var atrium = amenity.Type != null &&
        (amenity.Type.isAtrium || amenity.Type.id.StartsWith("leisure_atrium", …));
    var maxFloor = atrium ? AtriumMaxFloorDelta : MaxFloorDelta;
    var maxHoriz = atrium ? AtriumMaxHorizontalCells : MaxHorizontalCells;
    …
}
```

Update `IsEligibleAmenity` / `IsInRange` call sites accordingly (pass amenity type into range check).

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit** `feat: atrium amenity relief range and new leisure relief values`

---

### Task 3: Crime load multipliers

**Files:**
- Modify: `Assets/Scripts/Economy/CrimeFloorLoads.cs`
- Create: `Assets/Tests/EditMode/CrimeFloorLoadsTests.cs`

**Interfaces:**
- Produces: `CrimeFloorLoads.VisitorCrimeWeight(RoomTypeSO type)` → 1.75 casino, 1.5 nightclub, 0.5 chapel, else 1
- `ShopLoadByFloor` multiplies `ConcurrentVisitors` by that weight

- [ ] **Step 1: Failing test**

```csharp
[Test]
public void Casino_busy_load_is_scaled()
{
    // place casino with ConcurrentVisitors = 4 → floor load 7f (4 * 1.75)
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement** weight helper + multiply in `ShopLoadByFloor`

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit** `feat: scale crime load for casino nightclub chapel`

---

### Task 4: AtriumMarketing (non-stacking)

**Files:**
- Create: `Assets/Scripts/Economy/AtriumMarketing.cs`
- Create: `Assets/Tests/EditMode/AtriumMarketingTests.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (hotel fill, `PassesCondoDemand`, `UpdateStreetTraffic`)

**Interfaces:**
- Produces:

```csharp
public static class AtriumMarketing
{
    public const float HotelFillBonus = 0.05f;
    public const float CondoDemandBonus = 0.05f;
    public const float StreetSpawnMultiplier = 1.15f;
    public static bool HasAtrium(TowerGrid grid);
    public static float HotelFillBonusFor(TowerGrid grid) => HasAtrium(grid) ? HotelFillBonus : 0f;
    public static float CondoDemandBonusFor(TowerGrid grid) => HasAtrium(grid) ? CondoDemandBonus : 0f;
    public static float StreetSpawnMultiplierFor(TowerGrid grid) => HasAtrium(grid) ? StreetSpawnMultiplier : 1f;
}
```

`HasAtrium`: any room with `Type.isAtrium` (or id `leisure_atrium`) and `!IsBroken`.

Wire:
- Hotel: `fill = Clamp01(fill + AmenitySystem.HotelDemandBonus(...) + AtriumMarketing.HotelFillBonusFor(grid));`
- Condo: `chance = Clamp01(chance + AtriumMarketing.CondoDemandBonusFor(grid));` inside `PassesCondoDemand` (needs `grid` — pass grid into method or use field already on AgentSystem)
- Street: `chance = Clamp01(StreetSpawnBaseChance * (1 + stars) * AtriumMarketing.StreetSpawnMultiplierFor(grid));`

- [ ] **Step 1: Failing unit tests** for 0/1/2 atriums on `HasAtrium` / bonus getters (use runtime `RoomTypeSO` + `TowerGrid.TryPlace`)

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement helper + AgentSystem wire-up**

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit** `feat: non-stacking atrium marketing for hotel condo street`

---

### Task 5: Atrium pathfinding + transfer floors + stair stress skip

**Files:**
- Modify: `Assets/Scripts/Transit/StairsPathfinder.cs`
- Modify: `Assets/Scripts/Core/TowerGrid.cs` (`GetLobbyFloors`, `IsTransferLobbyFloor`)
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (`TryApplyStairFloorCrossing` / step movement stair wait)
- Optional helper: `Assets/Scripts/Transit/StairSpanMath.cs` for comfort span between transfer floors
- Create: `Assets/Tests/EditMode/AtriumTransitTests.cs`

**Interfaces:**
- `StairsPathfinder.Rebuild`: cells of `isAtrium` rooms join `_stairsCells` **or** a parallel `_atriumCells` set; `TryAddVertical` allows atrium↔atrium same-x vertical neighbors
- Prefer separate `_atriumCells` so atrium is not treated as demolishable stairs shaft elsewhere
- `TowerGrid.GetLobbyFloors`: for each atrium room, add every `y` from `Origin.y` … `Origin.y + Size.y - 1`
- `IsTransferLobbyFloor`: true if any atrium occupies that floor
- Stress: when stepping vertically and both cells are atrium (or current/target room `isAtrium`), do **not** increment `StairsFloorsCrossedThisLeg` stress / do not apply stair wait for atrium-only congestion if occupancy rules don't apply — minimum: skip `TryApplyStairFloorCrossing` stress when the floor crossing is atrium-internal
- Stair comfort reset: when computing over-cap for a stairs path that passes a transfer floor, use max segment between transfer floors (include atrium). Add `StairSpanMath.MaxSegmentSpan(startY, goalY, transferFloorsSorted)` used by `TransitRouter` affordability / penalty where `stairSpan = Abs(a-b)` today for pure stairs that cross atrium floors

**Concrete pathing test setup:**
- Lobby G, pads/offices on floors 1–5, atrium 8×3 at floor 1 (occupies 1–3), stairs from floor 3 up to 5 beside atrium
- Assert path from atrium bottom cell to atrium top cell exists without external stairs
- Assert `IsTransferLobbyFloor` true for atrium Y levels
- Assert agent crossing atrium floors does not gain over-cap stress for those crossings
- Assert stairs from atrium top + 2 floors is treated as span ≤2 for comfort (not 3 atrium + 2)

- [ ] **Step 1: Write failing `AtriumTransitTests`**

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement pathfinder, grid transfer, stress skip, span math + router hook**

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit** `feat: atrium vertical transit transfer floors and zero stair stress`

---

### Task 6: Human Play Mode smoke

**Files:** none required (manual)

- [ ] Place Casino, Nightclub, Chapel, Atrium from Leisure menu
- [ ] Confirm agents can change floor inside atrium
- [ ] Confirm casino/nightclub noisy hours feel right (optional)
- [ ] Optional: publish playtest zip only if user requests

- [ ] **Commit:** none (or docs note only if user asks)

---

## Spec coverage check

| Spec requirement | Task |
|------------------|------|
| Four placeable leisure units | 1 |
| Visit income casino/nightclub/chapel | 1 (TrafficVariable) |
| Atrium not traffic venue | 1 |
| Dwell ranges | 1 |
| Amenity relief values + atrium ±6/24 | 2 |
| Crime multipliers | 3 |
| Marketing non-stack hotel/condo/street | 4 |
| Atrium vertical pathing | 5 |
| Zero atrium stair stress | 5 |
| All 3 floors transfer / stair reset | 5 |
| EditMode tests | 1–5 |
| Play Mode smoke | 6 |
