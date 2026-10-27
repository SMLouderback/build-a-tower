# Economy Foundation (VPSF) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route all sim money through a VPSF balancer with 108 living matrix, weekly market pulse + monthly climate, soft floor-fit warnings, and class clash / Upper elev-wait rules.

**Architecture:** Pure `VpsfCatalog` + `EconomicBalancingManager`; `MacroEconomicController` owns weekly pulse and reads `MarketClimate`; `FloorValueRules` / `ClassClashRules` feed soft multipliers; `BuildEconomy` / `EconomySystem` / HUD stop using SO `buildCost`/`baseIncome`.

**Tech Stack:** Unity 6 C#, EditMode NUnit, existing `GameDifficulty` / `MarketClimate` / luxury room catalogs.

**Spec:** `docs/superpowers/specs/2026-10-09-economy-foundation-vpsf-design.md`

## Global Constraints

- Never hardcode rents/costs/sizes outside `VpsfCatalog` (or named constants inside it).
- All economic money changes loop through `EconomicBalancingManager`.
- Preserve 5 difficulties: Sandbox, Easy, Normal, Hard, Extreme.
- Living matrix: 3 families × 9 tiers × 3 classes; Lower upkeep ≈40% of rent, Upper ≈15%, Mid baseline.
- Upper elev wait stress threshold: **15 seconds** game time.
- Vertical zoning: soft fit + warnings only (no hard placement bans).
- Weekly Market Pulse + keep monthly `MarketClimate`.
- SO `buildCost` / `baseIncome` unused for sim money after wire-up.
- Out of scope: value map UI, land purchases, tutorial tips, casino/bowling minigames.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/EconomicFamily.cs` | Family + `TenantClass` enums |
| `Assets/Scripts/Economy/VpsfCatalog.cs` | Tables + room-id → identity map |
| `Assets/Scripts/Economy/EconomicBalancingManager.cs` | BuildCost / income / upkeep API |
| `Assets/Scripts/Economy/FloorValueRules.cs` | FloorFit01 + warning copy |
| `Assets/Scripts/Economy/ClassClashRules.cs` | Clash penalty |
| `Assets/Scripts/Economy/MacroEconomicController.cs` | Weekly pulse + snapshot |
| `Assets/Scripts/Build/BuildEconomy.cs` | Afford/cost via balancer |
| `Assets/Scripts/Economy/EconomySystem.cs` | Midnight via balancer |
| `Assets/Scripts/UI/RoomEconomyFormat.cs` + HUD placement | Display + warnings |
| `Assets/Scripts/Agents/AgentSystem.cs` | Upper 15s wait + clash stress hooks |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Own/tick macro controller |
| `Assets/Tests/EditMode/Vpsf*Tests.cs` etc. | EditMode coverage |

Unity EditMode (editor often `6000.4.7f1`):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" -runTests -testPlatform EditMode -testFilter "VpsfCatalogTests|EconomicBalancingManagerTests|FloorValueRulesTests|ClassClashRulesTests|MacroEconomicControllerTests" -testResults "TestResults\economy-vpsf.xml" -logFile "unity-economy-vpsf.log" -nographics
```

---

### Task 1: Identity enums + VpsfCatalog mapping (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/EconomicFamily.cs` (+ meta) — `EconomicFamily`, `TenantClass`
- Create: `Assets/Scripts/Economy/VpsfCatalog.cs` (+ meta) — base rates, tier/class curves, `TryIdentity(RoomTypeSO, out family, out tier, out class)`, cell count from `type.size`
- Create: `Assets/Tests/EditMode/VpsfCatalogTests.cs` (+ meta)

**Interfaces:**
- Produces: `VpsfCatalog.TryIdentity`, `Cells(RoomTypeSO)`, table accessors used by Task 2

- [ ] **Step 1: Failing tests** — map known office/hotel/condo ids to tier+class; unknown id false; cells = w×h.
- [ ] **Step 2: Implement catalog + LuxuryBand→TenantClass map (None/Base→Mid).**
- [ ] **Step 3: Pass; commit** `feat: add VPSF catalog and living economic identity`

---

### Task 2: EconomicBalancingManager core (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/EconomicBalancingManager.cs` (+ meta)
- Create: `Assets/Tests/EditMode/EconomicBalancingManagerTests.cs` (+ meta)

**Interfaces:**
- Consumes: `VpsfCatalog`
- Produces:
```csharp
public static class EconomicBalancingManager
{
    public static int BuildCost(RoomTypeSO type, GameDifficulty difficulty);
    public static int PeriodIncome(RoomTypeSO type, PriceTier tier, GameDifficulty difficulty,
        float climateSpendMult, float pulseMult, float floorFit01);
    public static int PeriodUpkeep(RoomTypeSO type, GameDifficulty difficulty, float pulseMult);
}
```
Income for sale-model condos = sale payout once (existing income model), still derived from VPSF.

- [ ] **Step 1: Failing golden tests** for Normal Mid office tier sample; Lower upkeep ~40%; Upper ~15%; difficulty scales.
- [ ] **Step 2: Implement.**
- [ ] **Step 3: Pass; commit** `feat: add EconomicBalancingManager VPSF money API`

---

### Task 3: FloorValueRules (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/FloorValueRules.cs` (+ meta)
- Create: `Assets/Tests/EditMode/FloorValueRulesTests.cs` (+ meta)

**Interfaces:**
- Produces: `float Fit01(EconomicFamily family, TenantClass cls, int floorY, int stars)`, `bool TryWarning(..., out string reason)`

- [ ] **Step 1: Failing tests** — street shop high fit on 0–3; Upper condo higher fit on prestige floors; warning when fit below threshold.
- [ ] **Step 2: Implement band edges + star-scaled mid-cap.**
- [ ] **Step 3: Pass; commit** `feat: add soft floor-value fit and warning copy`

---

### Task 4: ClassClashRules + Upper elev wait helper (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/ClassClashRules.cs` (+ meta)
- Create: `Assets/Tests/EditMode/ClassClashRulesTests.cs` (+ meta)
- Optionally add `TenantClassStress.cs` for `ElevWaitStress(TenantClass, float waitSeconds)` with 15s Upper threshold

- [ ] **Step 1: Failing tests** — Upper vs adjacent Lower clash; Mid no clash; Upper wait 16s stresses, 14s does not.
- [ ] **Step 2: Implement.**
- [ ] **Step 3: Pass; commit** `feat: add class clash and Upper elevator wait stress rules`

---

### Task 5: MacroEconomicController weekly pulse (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/MacroEconomicController.cs` (+ meta)
- Create: `Assets/Tests/EditMode/MacroEconomicControllerTests.cs` (+ meta)
- Modify: `TowerSimulation` / snapshot mapper to own + persist pulse

**Interfaces:**
- Produces: `float LivingPulseMult`, `float CommercialPulseMult`, `Tick(GameClock)`, snapshot DTO

- [ ] **Step 1: Failing tests** — advances after 7 game days; snapshot round-trip; clamp range.
- [ ] **Step 2: Implement + wire sim tick + save/load.**
- [ ] **Step 3: Pass; commit** `feat: add weekly market pulse controller`

---

### Task 6: Wire BuildEconomy + EconomySystem + HUD money

**Files:**
- Modify: `BuildEconomy.cs`, `EconomySystem.cs`, `RoomEconomyFormat.cs`, afford/place paths, `ShopVisitRules` base spend if tied to `baseIncome`

- [ ] **Step 1: Replace SO money reads with balancer (+ floor-fit/pulse/climate args from sim).**
- [ ] **Step 2: EditMode / compile; smoke existing economy tests still meaningful or update expectations.**
- [ ] **Step 3: Commit** `feat: route build and midnight economy through VPSF balancer`

---

### Task 7: Placement/HUD floor-fit warnings + agent stress hooks

**Files:**
- Modify: `TowerHudController` (catalog/selection/ghost warning lines)
- Modify: `AgentSystem` (Upper elev wait 15s; apply clash penalty to fill/stress where cheap)

- [ ] **Step 1: Show warning string when `FloorValueRules.TryWarning`.**
- [ ] **Step 2: Hook elev wait + clash into stress/fill.**
- [ ] **Step 3: Commit** `feat: surface floor-fit warnings and class stress hooks`

## Spec coverage

| Spec | Task |
|------|------|
| VPSF identity/catalog | 1 |
| Balancer money API | 2 |
| Soft vertical zoning | 3, 7 |
| Class clash + Upper 15s | 4, 7 |
| Weekly pulse + climate stack | 5, 6 |
| SO money unused | 6 |
| Warnings HUD | 7 |
