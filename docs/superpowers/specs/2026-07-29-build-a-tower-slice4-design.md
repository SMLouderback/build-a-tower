# Build-A-Tower — Slice #4 Design

**Date:** 2026-07-29  
**Status:** Approved (implement)  
**Depends on:** Slice #3 (elevators, TransitRouter, agents, Floor G lobby, selector)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Approach:** Lightweight `EconomySystem` + `StarSystem` on the existing sim (not full tower-together ledgers)

## 1. Goals

Make the tower **pay for itself** and **progress through stars** so build options open over time.

### Slice #4 success criteria

In Play mode a player can:

1. See funds change at **midnight** from recurring room income and elevator upkeep.
2. Receive a **one-time condo sale** when a condo first gains a resident.
3. Start at **0★**, earn **1★** and **2★** on **quarterly** checks (population + average stress + facilities).
4. Lose a star on a failed quarterly check for the current tier.
5. Be **blocked** from placing elevators (and other `requiredStars ≥ 1` content) until 1★; blocked from 2★ premium variants until 2★.
6. See ★, population, average stress, and last income/expense summary on the HUD.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Architecture | Lightweight economy + star systems (Approach A) |
| Recurring income | **Daily midnight** sweep |
| One-time income | **Event-driven** (condo sale on first resident) |
| Star range this slice | **0 → 1 → 2** only |
| Star check cadence | Every **90 game days** (quarterly) |
| Star inputs | Population + avg agent stress + required facilities |
| Unlock content 1★ | Elevators (+ optional fast-food / low retail stubs if assets exist) |
| Unlock content 2★ | Premium office / hotel / condo variants (higher cost + `baseIncome`) |
| Stars 3–5 | Spec roadmap only — no gameplay |
| Multi-car / express / research / parking / casinos | Out of scope |
| Full income/expense ledgers / rent-tier UI | Out of scope |
| Retail `TrafficVariable` payouts | Out of scope (rooms may exist but pay $0) |

## 3. Economy

### 3.1 Units

Keep dollar integers on `FundsWallet` as today (no cash-unit conversion this slice).

### 3.2 Room income models (existing enum)

| `IncomeModel` | Slice #4 behavior |
|---------------|-------------------|
| `None` | No payout |
| `QuarterlyRent` | Treated as **daily rent** at midnight if the room is economically active |
| `NightlyRate` | **Daily** payout at midnight while hotel room has a staying guest / assigned agent |
| `UpfrontSale` | One-time payout on first resident; no repeat sale |
| `TrafficVariable` | No payout this slice |

`RoomTypeSO.baseIncome` is the daily amount for rent/nightly models, and the sale price for `UpfrontSale`.

### 3.3 Economically active rooms

A room is active for midnight rent when:

- **Office (`QuarterlyRent`):** at least one agent has this room as `HomeRoom` and is not permanently Outside without a home link (all synced office agents count as leasing).
- **Hotel (`NightlyRate`):** at least one hotel guest has this room as home and `Phase` is `Staying` (or `Moving`/`Waiting`/`Riding` toward/from stay — MVP: any hotel agent with this `HomeRoom` and `Visible` or `Staying`).
- **Condo:** sale is event-only; optional daily post-sale income is **none** this slice unless `baseIncome` is set on a follow-up — default: sale only.

Exact MVP rule (simpler, testable):

- Midnight rent: sum `baseIncome` for every room whose `incomeModel` is `QuarterlyRent` or `NightlyRate` and that has **≥ 1 agent** with `HomeRoom == room`.
- Condo `UpfrontSale`: when `SyncHomes` first creates an agent for a condo room that has not yet flagged `Sold`, pay `baseIncome` once and mark sold on the room instance (or a side set of sold instance ids).

### 3.4 Upkeep

At the same midnight sweep, debit:

- **Elevator upkeep:** `$10,000` per elevator shaft room per day (constant; mirrors tower-together’s standard carrier spirit, daily instead of 3-day).

No stairs upkeep. No refunds of prior upkeep on demolish.

### 3.5 Condo sale event

- Trigger: first agent assigned to a condo `HomeRoom` in `AgentSystem.SyncHomes`.
- Action: `Wallet.Add(condo.Type.baseIncome)` once; record sold so demolish+rebuild can sell again (new instance id).
- Tune assets: set condo `baseIncome` to a meaningful sale price (e.g. `$200,000`–`$500,000`).

### 3.6 Wiring

- `EconomySystem` owns last sweep day index, last net income line for HUD.
- `TowerSimulation` detects `GameClock` day rollover and calls `EconomySystem.OnNewDay(...)`.
- Uses existing `FundsWallet` on `BuildController`.

## 4. Stars

### 4.1 State

- `StarSystem.CurrentStars` ∈ `{0, 1, 2}` for MVP (type may allow 0–5 for future).
- `LastEvaluationDay`, optional last fail/pass reason string for HUD/help.

### 4.2 Population

Population = count of agents that have a non-null `HomeRoom` (office workers + hotel guests + condo residents). Condo residents count even if always AtHome.

### 4.3 Stress

Use existing `AgentSystem.AverageStress` (0–100). Lower is better.

### 4.4 Quarterly evaluation

Every time `DayIndex` crosses a multiple of **90** (days 90, 180, …), evaluate **target star = CurrentStars** for retention, and whether **CurrentStars + 1** can be earned (cap 2).

**Earn / keep 1★**

| Metric | Threshold (tunable constants) |
|--------|--------------------------------|
| Population | ≥ **10** |
| Average stress | ≤ **40** |
| Facilities | Lobby present (`Grid.HasLobby`) |

**Earn / keep 2★**

| Metric | Threshold |
|--------|-----------|
| Population | ≥ **30** |
| Average stress | ≤ **25** |
| Facilities | CurrentStars ≥ 1 **and** ≥ 1 elevator shaft exists **and** ≥ 1 room with `requiredStars ≥ 1` that is a premium accommodation **or** any placed room tagged as 1★ unlock content beyond elevators — MVP concrete rule: ≥ 1 elevator shaft **and** ≥ 1 hotel or condo or office room with `requiredStars == 1` **or** simply ≥ 1 elevator + population/stress (if no 1★ room variants ship). |

**Locked concrete MVP facility rule for 2★:**

- Lobby present
- At least one `isElevatorShaft` room in `Grid.Rooms`
- Population / stress as above

(If premium 1★ room assets are added, prefer also requiring one of them; otherwise elevator + thresholds suffice for 2★.)

**Promotion:** if currently at N and pass criteria for N+1, set stars to N+1 (max 2).  
**Retention:** if currently at N (N ≥ 1) and fail criteria for N, demote to N−1.  
**Order per quarter:** check demotion for current tier first, then promotion once.

### 4.5 Unlock gating

- Add `RoomTypeSO.requiredStars` (int, default 0).
- Set **ElevatorNormal** (and Resources copy) to `requiredStars = 1`.
- Add or duplicate **premium** office/hotel/condo assets with `requiredStars = 2` and higher `buildCost` / `baseIncome`.
- `BuildController.TryPlaceSelected` / elevator tool / HUD buttons: reject or hide when `StarSystem.CurrentStars < type.requiredStars`.
- Existing placed rooms remain if demoted; player simply cannot place more of that tier.

### 4.6 Starting state

New sandbox starts at **0★**. Elevators are locked until 1★ — early game is lobby + rooms + stairs only (matches Slice 2 vertical limit until stars unlock elevators).

## 5. Roadmap: stars 3–5 (not implemented)

Documented intent for later slices (facility gates will apply):

| Stars | Example unlocks |
|------:|-----------------|
| 3 | Express elevators, fine dining, conference, security, research facility, basic above-ground parking |
| 4 | Penthouse hotel, escalators (≤6 floors low stress), freight/HK elevators, multi-floor condo/office, garbage/recycling (basement), underground valet parking, casinos |
| 5 | Hotel events, ballrooms, clubs, spas, celebrity restaurants, luxury condo/retail, indoor arenas, wedding chapels |

Research facility (3★+) later enables paid upgrades (safety, max height, elevator speed, economy helpers).

## 6. HUD

Show:

- Funds (existing)
- **Stars** (`★ ★☆` style or `Stars: 1/2`)
- Population count
- Average stress
- Last midnight net (`+$X / −$Y` or single net)

Help text when a locked tool is clicked: `Needs N★`.

## 7. Files (expected)

| File | Role |
|------|------|
| `Assets/Scripts/Economy/EconomySystem.cs` | Midnight sweep + sale API |
| `Assets/Scripts/Economy/StarSystem.cs` | Quarterly stars |
| `Assets/Scripts/Data/RoomTypeSO.cs` | `requiredStars` |
| `Assets/Scripts/Core/RoomInstance.cs` | Optional `CondoSold` flag |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Wire day/quarter hooks |
| `Assets/Scripts/Build/BuildController.cs` | Gate placement |
| `Assets/Scripts/UI/TowerHudController.cs` | ★ / pop / stress / net |
| `Assets/Scripts/Agents/AgentSystem.cs` | Notify condo sale |
| Tests under `Assets/Tests/EditMode/` | Economy + stars + gate |
| Spec / README | This doc + play steps |

## 8. Testing

- EditMode: midnight income for occupied office; elevator upkeep; condo sale once only.
- EditMode: day 90 grants 1★ with pop/stress/lobby; fails without; demotes when stress too high.
- EditMode: cannot place elevator at 0★; can at 1★.
- PlayMode smoke: advance clock across midnight → balance changes; optional star bump when thresholds forced in test setup.

## 9. Non-goals

- Retail traffic income, happy hour, F&B curves  
- Rent tier dialogs  
- 3-day SimTower ledger UI  
- Stars 3–5 gameplay  
- Multi-car shafts, express, escalators, research tree  
- Evaluation overlays (Slice #5 territory)

## 10. Open tunables (constants, not blockers)

| Constant | Initial value |
|----------|---------------|
| 1★ population | 10 |
| 1★ max avg stress | 40 |
| 2★ population | 30 |
| 2★ max avg stress | 25 |
| Elevator daily upkeep | 10_000 |
| Quarter length days | 90 |
