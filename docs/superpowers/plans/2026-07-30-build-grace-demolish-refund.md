# Build-Grace Demolish Refund Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Within 10 real-time seconds of placing a demolishable room, bulldoze refunds `ConstructionSpent − (LifetimeIncome − LifetimeExpense)` so mistaken builds undo cleanly without income farming.

**Architecture:** Ledger fields live on `RoomInstance`. `BuildController` stamps place/extension spend and pays the grace refund on demolish. `EconomySystem` bumps lifetime income/expense when rooms pay or cost upkeep. Elevator resize recreates the `RoomInstance` with the same id — ledger must be copied onto the replacement.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode tests, IMGUI selection hint

## Global Constraints

- Timer: `Time.realtimeSinceStartup`, duration **10** seconds from **original place** only
- Eligible: all demolishable rooms (not lobby, not scaffolding)
- Extensions add to `ConstructionSpent` but **do not** refresh `PlacedAtRealtime`
- After window: **$0** refund (current behavior)
- Spec: `docs/superpowers/specs/2026-07-30-build-grace-demolish-refund-design.md`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Core/RoomInstance.cs` | Ledger fields + grace/refund helpers |
| `Assets/Scripts/Core/TowerGrid.cs` | Copy ledger when elevator resize replaces instance |
| `Assets/Scripts/Build/BuildController.cs` | Stamp spend on place/extend; refund on demolish; selection hint |
| `Assets/Scripts/Economy/EconomySystem.cs` | Bump `LifetimeIncome` / `LifetimeExpense` |
| `Assets/Scripts/UI/TowerHudController.cs` | Show undo line in Selection (optional if summary carries it) |
| `Assets/Tests/EditMode/BuildGraceRefundTests.cs` | Formula, window, elevator extend, condo clawback |
| `README.md` | One play bullet for undo refund |

---

### Task 1: RoomInstance ledger + pure helpers

**Files:**
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Test: `Assets/Tests/EditMode/BuildGraceRefundTests.cs`

**Interfaces:**
- Produces: `const float BuildGraceSeconds = 10f`; `float PlacedAtRealtime`; `int ConstructionSpent`; `int LifetimeIncome`; `int LifetimeExpense`; `void RecordConstructionSpend(int amount, float nowRealtime, bool isInitialPlace)`; `void RecordLifetimeIncome(int amount)`; `void RecordLifetimeExpense(int amount)`; `bool IsInBuildGrace(float nowRealtime)`; `int GraceRefundAmount()`; `void CopyBuildGraceLedgerFrom(RoomInstance source)`; `static bool IsGraceRefundEligible(RoomTypeSO type)`

- [x] **Step 1: Write failing tests for formula and eligibility**

```csharp
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildGraceRefundTests
    {
        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.buildCost = 40_000;
            so.incomeModel = IncomeModel.QuarterlyRent;
            so.baseIncome = 3000;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            return so;
        }

        [Test]
        public void GraceRefundAmount_is_spend_minus_lifetime_net()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, nowRealtime: 100f, isInitialPlace: true);
            room.RecordLifetimeIncome(150_000);
            room.RecordLifetimeExpense(3_000);
            // 40000 - (150000 - 3000) = -107000
            Assert.AreEqual(-107_000, room.GraceRefundAmount());
        }

        [Test]
        public void IsInBuildGrace_only_within_ten_realtime_seconds_of_place()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, nowRealtime: 50f, isInitialPlace: true);
            Assert.IsTrue(room.IsInBuildGrace(59.9f));
            Assert.IsFalse(room.IsInBuildGrace(60.1f));
        }

        [Test]
        public void Extension_spend_does_not_refresh_grace_deadline()
        {
            var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
            room.RecordConstructionSpend(40_000, nowRealtime: 10f, isInitialPlace: true);
            room.RecordConstructionSpend(20_000, nowRealtime: 15f, isInitialPlace: false);
            Assert.AreEqual(60_000, room.ConstructionSpent);
            Assert.IsFalse(room.IsInBuildGrace(20.1f)); // placed at 10 → expires 20
        }

        [Test]
        public void Lobby_and_scaffolding_are_not_grace_eligible()
        {
            var lobby = ScriptableObject.CreateInstance<RoomTypeSO>();
            lobby.isLobby = true;
            var scaffold = ScriptableObject.CreateInstance<RoomTypeSO>();
            scaffold.isScaffolding = true;
            Assert.IsFalse(RoomInstance.IsGraceRefundEligible(lobby));
            Assert.IsFalse(RoomInstance.IsGraceRefundEligible(scaffold));
            Assert.IsTrue(RoomInstance.IsGraceRefundEligible(Office()));
        }
    }
}
```

- [x] **Step 2: Run tests — expect compile/fail on missing API**

Roslyn typecheck or Unity EditMode filter `BuildGraceRefundTests`. Expected: missing members.

- [x] **Step 3: Implement ledger on `RoomInstance`**

```csharp
public const float BuildGraceSeconds = 10f;

public float PlacedAtRealtime { get; private set; } = -1f;
public int ConstructionSpent { get; private set; }
public int LifetimeIncome { get; private set; }
public int LifetimeExpense { get; private set; }

public void RecordConstructionSpend(int amount, float nowRealtime, bool isInitialPlace)
{
    if (amount < 0) return;
    ConstructionSpent += amount;
    if (isInitialPlace)
        PlacedAtRealtime = nowRealtime;
}

public void RecordLifetimeIncome(int amount)
{
    if (amount > 0) LifetimeIncome += amount;
}

public void RecordLifetimeExpense(int amount)
{
    if (amount > 0) LifetimeExpense += amount;
}

public bool IsInBuildGrace(float nowRealtime) =>
    PlacedAtRealtime >= 0f &&
    nowRealtime < PlacedAtRealtime + BuildGraceSeconds;

public int GraceRefundAmount() =>
    ConstructionSpent - (LifetimeIncome - LifetimeExpense);

public void CopyBuildGraceLedgerFrom(RoomInstance source)
{
    if (source == null) return;
    PlacedAtRealtime = source.PlacedAtRealtime;
    ConstructionSpent = source.ConstructionSpent;
    LifetimeIncome = source.LifetimeIncome;
    LifetimeExpense = source.LifetimeExpense;
}

public static bool IsGraceRefundEligible(RoomTypeSO type) =>
    type != null && !type.isLobby && !type.isScaffolding;
```

- [x] **Step 4: Re-run tests — expect PASS**

- [x] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/RoomInstance.cs Assets/Tests/EditMode/BuildGraceRefundTests.cs Assets/Tests/EditMode/BuildGraceRefundTests.cs.meta
git commit -m "feat: add RoomInstance build-grace ledger helpers"
```

---

### Task 2: Economy lifetime bumps + elevator resize ledger copy

**Files:**
- Modify: `Assets/Scripts/Economy/EconomySystem.cs`
- Modify: `Assets/Scripts/Core/TowerGrid.cs` (`TryResizeElevator`)
- Test: `Assets/Tests/EditMode/BuildGraceRefundTests.cs`, update `EconomySystemTests.cs` if useful

**Interfaces:**
- Consumes: `RoomInstance.RecordLifetimeIncome` / `RecordLifetimeExpense` / `CopyBuildGraceLedgerFrom`
- Produces: midnight + condo sale update lifetime; elevator resize preserves ledger

- [x] **Step 1: Failing test — condo sale bumps lifetime; resize copies ledger**

```csharp
[Test]
public void Condo_sale_increments_LifetimeIncome()
{
    var condo = ScriptableObject.CreateInstance<RoomTypeSO>();
    condo.incomeModel = IncomeModel.UpfrontSale;
    condo.baseIncome = 150_000;
    var room = new RoomInstance(1, condo, Vector2Int.zero, Vector2Int.one);
    var economy = new EconomySystem();
    var wallet = new FundsWallet(0);
    Assert.IsTrue(economy.TrySellCondo(room, wallet));
    Assert.AreEqual(150_000, room.LifetimeIncome);
}

[Test]
public void Elevator_resize_preserves_build_grace_ledger()
{
    var grid = new TowerGrid();
    // place lobby + elevator using existing test helpers pattern from ElevatorTests
    // after RecordConstructionSpend on shaft, TryExtendElevator/TryResizeElevator
    // find shaft by same InstanceId and assert ConstructionSpent and PlacedAtRealtime preserved
}
```

Implement the elevator test using the same lobby/elevator helpers as `ElevatorTests.cs` (copy minimal `Lobby()` / `Elevator()` factories).

- [x] **Step 2: Run — expect fail (LifetimeIncome still 0 / ledger wiped)**

- [x] **Step 3: Wire `EconomySystem`**

In `OnNewDay`, when assigning elevator upkeep / room income, also:

```csharp
room.RecordLifetimeExpense(ElevatorDailyUpkeep);
// and
room.RecordLifetimeIncome(amount);
```

In `TrySellCondo` after computing `amount`:

```csharp
room.RecordLifetimeIncome(amount);
```

- [x] **Step 4: Wire `TowerGrid.TryResizeElevator` ledger copy**

Before `RemoveRoom(shaft)`, capture ledger via locals or keep reference. After `PlaceElevator(...)` returns the new instance, call `elevator.CopyBuildGraceLedgerFrom(shaft)` (shaft object still holds old field values even after removal from grid).

```csharp
var previous = shaft;
// ... RemoveRoom(shaft); ...
var elevator = PlaceElevator(...);
elevator.CopyBuildGraceLedgerFrom(previous);
```

Change `TryResizeElevator` to use the `PlaceElevator` return value (today it discards it).

- [x] **Step 5: Tests PASS — Commit**

```bash
git commit -m "feat: track lifetime income/expense; preserve ledger on elevator resize"
```

---

### Task 3: Stamp spend on place/extend + grace refund on demolish

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Test: `Assets/Tests/EditMode/BuildGraceRefundTests.cs` (controller-level via place/demolish helpers if exposed, or thin public test hooks)

**Interfaces:**
- Consumes: ledger helpers; `Time.realtimeSinceStartup`
- Produces: `TryPlaceSelected` / elevator grow paths stamp spend; `TryDemolishAt` pays grace refund

- [x] **Step 1: Failing integration-style tests**

Prefer testing through `BuildController` if EditMode can construct it with a stub view; if MonoBehaviour wiring is heavy, extract a small static/helper used by controller:

```csharp
public static int ComputeDemolishWalletDelta(RoomInstance removed, float nowRealtime)
{
    if (!RoomInstance.IsGraceRefundEligible(removed?.Type)) return 0;
    if (!removed.IsInBuildGrace(nowRealtime)) return 0;
    return removed.GraceRefundAmount();
}
```

Put helper on `BuildController` as `public static` or a tiny `BuildGraceRefund` static class in `Assets/Scripts/Build/BuildGraceRefund.cs`.

Tests:

```csharp
[Test]
public void Demolish_within_grace_refunds_construction_spend()
{
    var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
    room.RecordConstructionSpend(40_000, 0f, isInitialPlace: true);
    Assert.AreEqual(40_000, BuildGraceRefund.WalletDelta(room, nowRealtime: 5f));
}

[Test]
public void Demolish_after_grace_refunds_zero()
{
    var room = new RoomInstance(1, Office(), Vector2Int.zero, Vector2Int.one);
    room.RecordConstructionSpend(40_000, 0f, isInitialPlace: true);
    Assert.AreEqual(0, BuildGraceRefund.WalletDelta(room, nowRealtime: 11f));
}
```

- [x] **Step 2: Implement `BuildGraceRefund.WalletDelta` + wire controller**

After successful `Grid.TryDemolishAt` in `TryDemolishAt`, before visuals:

```csharp
var delta = BuildGraceRefund.WalletDelta(removed, Time.realtimeSinceStartup);
if (delta > 0) Wallet.Add(delta);
else if (delta < 0) Wallet.Subtract(-delta); // Add() ignores negatives; Subtract floors at 0
```

- [x] **Step 3: Stamp construction on place**

In `TryPlaceSelected` after successful `TryPlace`:

```csharp
room.RecordConstructionSpend(cost, Time.realtimeSinceStartup, isInitialPlace: true);
```

In `TryExtendElevator` / growing branch of `TryResizeElevator` after success, find current shaft by `instanceId` and:

```csharp
shaft.RecordConstructionSpend(cost, Time.realtimeSinceStartup, isInitialPlace: false);
```

(Do **not** stamp lobby place/extend for refund eligibility.)

- [x] **Step 4: Tests PASS — Commit**

```bash
git commit -m "feat: grace demolish refund and construction spend stamping"
```

---

### Task 4: Selection / help hint + README

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs` (`GetSelectionSummary` and/or `RefreshHelpText`)
- Modify: `Assets/Scripts/UI/TowerHudController.cs` only if summary is insufficient
- Modify: `README.md`

- [x] **Step 1: When selected room in grace, append undo line**

In `GetSelectionSummary`, after condition line:

```csharp
var now = Time.realtimeSinceStartup;
if (RoomInstance.IsGraceRefundEligible(room.Type) && room.IsInBuildGrace(now))
{
    var secs = room.PlacedAtRealtime + RoomInstance.BuildGraceSeconds - now;
    return /* existing */ + $"\nUndo refund {secs:0.0}s (${room.GraceRefundAmount():N0})";
}
```

Bulldoze help when a room is selected is optional; at minimum Selection shows the line.

- [x] **Step 2: README play bullet**

Add after bulldoze / place steps: within **10 real-time seconds** of placing a room, bulldoze refunds build cost minus that room’s net earnings/upkeep; after the window, demolish stays $0.

- [x] **Step 3: Roslyn typecheck Scripts + EditMode — Commit**

```bash
git commit -m "docs: build-grace undo hint and README"
```

---

### Task 5: Closeout

- [x] Roslyn typecheck clean (exclude `UnityEngine.dll` when referencing modules)
- [x] Focused EditMode coverage: formula, timer, extension non-refresh, lifetime bumps, resize copy, wallet delta
- [x] Final commit if needed; push only when asked

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| 10s realtime from original place | 1, 3 |
| All demolishable except lobby | 1 eligibility + 3 |
| Formula spend − (income − expense) | 1, 3 |
| Extension spend, no timer refresh | 1, 3 |
| Economy lifetime bumps | 2 |
| Elevator resize preserves ledger | 2 |
| Selection undo hint | 4 |
| README | 4 |
| After window $0 | 1, 3 |
