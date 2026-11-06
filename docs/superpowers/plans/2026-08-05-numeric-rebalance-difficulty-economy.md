# Numeric Rebalance & Difficulty Economy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retune Normal room costs/incomes (~40–50% squeeze, ~$1.125M start) and apply Easy/Hard/Extreme start funds + build/income multipliers via `DifficultyProfile`, without changing star gates.

**Architecture:** `DifficultyProfile` supplies start $, build-cost mult, and income mult from `GameSession.Difficulty`. `BuildEconomy` charges `ceil(nominal * costMult)` (Sandbox free). `EconomySystem` (and condo sale / shop visit credit paths) multiply player income by income mult. RoomTypeSO assets get a Normal catalog pass; scaffold cost → 750.

**Tech Stack:** Unity 6000.4.x, existing `GameSession` / `BuildEconomy` / `EconomySystem` / `PricePricing`, NUnit EditMode / net8 hosts.

**Spec:** `docs/superpowers/specs/2026-08-05-numeric-rebalance-difficulty-economy-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Prefer SDD; if quota exhausted, implement inline
- No parallel-cli
- Star gates / `requiredStars` / population / stress thresholds unchanged
- Expenses/upkeep stay **nominal** (no expense mult) unless tests prove need
- Sync Resources + ScriptableObjects room assets

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Core/DifficultyProfile.cs` | Start funds + cost/income mults |
| `Assets/Scripts/Build/BuildEconomy.cs` | Apply cost mult; recorded spend = charged |
| `Assets/Scripts/Build/BuildController.cs` | Wallet from profile at Awake |
| `Assets/Scripts/Economy/EconomySystem.cs` | Income mult on rent/sale/visit credits |
| `Assets/Scripts/Core/TowerGrid.cs` | `ScaffoldBuildCost = 750` |
| Room `.asset` catalogs | Normal retune |
| `Assets/Scripts/UI/MainMenu.uxml` | Difficulty hint copy |
| `Assets/Tests/EditMode/DifficultyProfileTests.cs` | Profile + spend/income mult |
| README + spec → Implemented |

---

### Task 1: DifficultyProfile + BuildEconomy cost mult + wallet start

**Files:**
- Create: `Assets/Scripts/Core/DifficultyProfile.cs` (+ meta)
- Modify: `Assets/Scripts/Build/BuildEconomy.cs`
- Modify: `Assets/Scripts/Build/BuildController.cs` (Awake wallet)
- Create: `Assets/Tests/EditMode/DifficultyProfileTests.cs` (+ meta)

**Interfaces:**
- `DifficultyProfile.StartingFunds(GameDifficulty d) -> int`
- `DifficultyProfile.BuildCostMultiplier(GameDifficulty d) -> float`
- `DifficultyProfile.IncomeMultiplier(GameDifficulty d) -> float`
- `BuildEconomy.EffectiveBuildCost(int nominal) -> int`
- `BuildEconomy.ApplyIncome(int nominal) -> int`

Exact profile values from spec §3.

- [ ] **Step 1: Write failing tests** (profile table + Sandbox cost 0 / free spend + Hard cost > Normal + Easy income > Normal)

- [ ] **Step 2: Implement DifficultyProfile + extend BuildEconomy**

```csharp
// EffectiveBuildCost: Sandbox → 0; else Max(0, CeilToInt(nominal * BuildCostMult))
// TrySpendForBuild spends EffectiveBuildCost (if 0, success no debit)
// RecordedSpend / RefundBuild use the charged amount
// ApplyIncome: Max(0, Round(nominal * IncomeMult)); Sandbox uses 1.0
```

- [ ] **Step 3: BuildController Awake** — `Wallet = new FundsWallet(DifficultyProfile.StartingFunds(GameSession.Difficulty));` after `EnsureDefault()`.

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit** (only if asked)

---

### Task 2: Income mult in EconomySystem (+ any direct sale helpers)

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: shop/condo credit sites if they add to wallet outside EconomySystem (grep `Wallet.Add` / income credits)
- Extend: `DifficultyProfileTests` or `EconomyIncomeMultTests`

- [ ] **Step 1: Find all player income credit paths** (OnNewDay rent, upfront sale, traffic variable, conference queued income if player-facing)

- [ ] **Step 2: Route amounts through `BuildEconomy.ApplyIncome` (or `DifficultyProfile` helper) before `Wallet.Add`**

- [ ] **Step 3: Test** — with Normal base amount 1000, Easy → 1250, Hard → 800, Extreme → 650

- [ ] **Step 4: Commit** (only if asked)

---

### Task 3: Scaffold 750 + Normal catalog asset pass

**Files:**
- Modify: `TowerGrid.ScaffoldBuildCost` → 750; update tests that assert 500
- Modify: all living/commercial/structure room assets under Resources + ScriptableObjects per payback guide
- Update: `ScaffoldPlacementTests` expected cost 750

**Method:** For each income-bearing room, set new buildCost/baseIncome toward medium targets; structure/transit/parking/ops ~+45% cost; leave requiredStars/luxuryBand/size alone.

- [ ] **Step 1: Scaffold constant + test updates**
- [ ] **Step 2: Scripted or careful bulk retune of assets** (document sample before/after in task report)
- [ ] **Step 3: Spot-check payback math** on 2 offices, 2 hotels, 2 condos, 1 shop
- [ ] **Step 4: Commit** (only if asked)

---

### Task 4: Menu hints + README + spec Implemented

**Files:**
- `MainMenu.uxml` difficulty `diff-hint` labels
- `README.md` — note difficulty economy
- Spec status → Implemented

- [ ] **Step 1: Update hints**
- [ ] **Step 2: README + spec**
- [ ] **Step 3: Manual smoke** — Easy vs Hard start $ and one build cost; Sandbox still free
- [ ] **Step 4: Commit** (only if asked)

---

## Execution

Start SDD immediately after plan save (user preference). Inline if subagent quota exhausted.
