# Price Tiers & Progressive HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Low/Normal/High/Max price tiers with light demand, a progressive accordion HUD, and a nested Build catalog (Office/Hotel/Condo/Shops/Utility/Transit).

**Architecture:** Pure helpers (`PricePricing`) for multipliers and star comfort; `PriceTier` on `RoomInstance`; economy/agent systems apply demand; `RoomTypeSO.buildFamily` / `buildSubgroup` drive nested IMGUI catalog in `TowerHudController`.

**Tech Stack:** Unity 6000.4.7f1, C#, IMGUI HUD, NUnit EditMode tests

## Global Constraints

- Discrete tiers only (slider after MVP)
- Omit empty Utility / future shop buttons until assets exist
- Prefer omit-until-asset for catalog placeholders
- Keep Build available from start; soft-unlock Goals (lobby) and Economy (first income/midnight)

---

### Task 1: PricePricing + RoomInstance.PriceTier + economy payouts

**Files:**
- Create: `Assets/Scripts/Economy/PricePricing.cs`
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: `Assets/Scripts/UI/RoomEconomyFormat.cs`
- Test: `Assets/Tests/EditMode/PricePricingTests.cs`, update `EconomySystemTests.cs`

- [ ] Failing tests for multipliers (0.7/1/1.3/1.6) and comfort max tier by stars
- [ ] Implement `PricePricing` + `PriceTier` (default 1)
- [ ] Apply rounded payout in `OnNewDay` / `TrySellCondo`
- [ ] Format effective income at current tier in selection lines
- [ ] Commit

### Task 2: Light demand vs comfort band

**Files:**
- Modify: `Assets/Scripts/Economy/PricePricing.cs` (retention chance)
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (condo buyer spawn gate)
- Test: demand / spawn tests

- [ ] Deterministic chance table: at/under comfort = 1.0; +1 = 0.4; +2 or more = 0.1
- [ ] Recurring rooms skip income when demand roll fails (seeded RNG on EconomySystem)
- [ ] Condo SyncHomes skips buyer create when spawn roll fails (seeded RNG)
- [ ] Commit

### Task 3: Build family metadata + catalog grouping

**Files:**
- Create: `Assets/Scripts/Data/BuildFamily.cs` (enums)
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs`
- Create: `Assets/Scripts/UI/BuildCatalog.cs`
- Update room `.asset` YAML for family/subgroup where needed
- Test: `BuildCatalogTests.cs`

- [ ] Enums `BuildFamily`, `BuildSubgroup`
- [ ] Fields on `RoomTypeSO`; infer from category/id if unset
- [ ] `BuildCatalog.Group(rooms)` → families → optional subgroups → types
- [ ] Commit

### Task 4: Progressive HUD + price buttons + nested Build

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` (`HasRecordedIncome` unlock flag)
- Modify: `Assets/Scripts/Build/BuildController.cs` if needed for SetPriceTier
- Modify: `README.md`

- [ ] Core strip: funds, time/speeds, stars, help
- [ ] Foldouts: Goals (lobby), Economy (income event), Build (always, start expanded), Selection (when selected)
- [ ] Selection: Low/Normal/High/Max + market hint
- [ ] Nested Build families; Shops → Food/Retail; omit empty Utility
- [ ] Tools flat: Selector, Extend Lobby, Bulldoze (Transit under family)
- [ ] README play steps
- [ ] Commit

### Task 5: Closeout

- [ ] Roslyn typecheck Scripts + EditMode
- [ ] Final commit if needed / push when asked
