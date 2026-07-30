# Build-A-Tower — Build-Grace Demolish Refund

**Date:** 2026-07-30  
**Status:** Approved  
**Depends on:** Slice #4 economy + price tiers / unit income tracking  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Follows with:** Commercial visit traffic (E1)

## 1. Goals

Let players undo a mistaken placement within a short wall-clock window and restore cash as if the room had never paid (or cost upkeep), without enabling midnight/condo income farming via place → collect → demolish → rebuild.

### Success criteria

In Play Mode a player can:

1. Place an office (or other demolishable room), bulldoze it within **10 real-time seconds**, and recover approximately the build cost (minus any net the room already produced).
2. Place a condo, sell it via move-in within the window, then bulldoze and see the sale clawed back as part of the refund math.
3. After the window expires, bulldoze with **$0** refund (current behavior).
4. Extend an elevator during the original place’s grace window, then demolish the shaft and recover **initial + extension** spend (still only if the shaft’s **original** place is within 10s).
5. See a short selection / help hint when a selected room is still in the undo window.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Timer basis | **Real-time seconds** (`Time.realtimeSinceStartup`), same idea as elevator correction window |
| Duration | **10 seconds** from **original place** of that instance |
| Eligible rooms | All **demolishable** rooms (office / hotel / condo / shops / stairs / elevator). **Not** lobby |
| Extension spend | Counts toward refundable construction spend |
| Extension vs timer | Extensions **do not** refresh the grace deadline; only first create stamps `PlacedAtRealtime` |
| Refund formula | `ConstructionSpent − (LifetimeIncome − LifetimeExpense)` |
| After window | **$0** refund (unchanged) |
| Architecture | Per-room ledger fields on `RoomInstance` |

## 3. Ledger on `RoomInstance`

| Field | Meaning |
|-------|---------|
| `PlacedAtRealtime` | `Time.realtimeSinceStartup` at first create; used for grace check |
| `ConstructionSpent` | Sum of wallet spends for this instance (place + paid extensions) |
| `LifetimeIncome` | Sum of income credited to this instance (daily rent, condo sale, …) |
| `LifetimeExpense` | Sum of expenses charged to this instance (elevator daily upkeep, …) |

Helpers (pure or on instance):

- `bool IsInBuildGrace(float nowRealtime)` → `nowRealtime < PlacedAtRealtime + 10`
- `int GraceRefundAmount()` → `ConstructionSpent - (LifetimeIncome - LifetimeExpense)` (can be negative if the room somehow over-earned vs spend; wallet must still apply the delta)

Wire points:

- **Place room / stairs / elevator:** set `PlacedAtRealtime`, add cost to `ConstructionSpent`.
- **Elevator / lobby extension that spends:** add cost to `ConstructionSpent` only (no timer refresh). Lobby remains non-demolishable / no grace refund path.
- **`EconomySystem`:** when recording room income or elevator upkeep, also bump `LifetimeIncome` / `LifetimeExpense` on that `RoomInstance` (in addition to existing last-day maps).
- **`BuildController.TryDemolishAt`:** after successful grid demolish, if room is eligible and in grace, `Wallet.Add(GraceRefundAmount())`.

Condo note: sale already sets `CondoSold` and pays once; clawback via `LifetimeIncome` means grace demolish after sale returns spend minus the sale (player does not keep the sale + rebuild loop).

## 4. UI / feedback

- When Selector has a room in grace: one line such as `Undo refund Xs` (seconds remaining).
- Optional bulldoze help text when hovering an in-grace room: same idea.
- No new HUD section; reuse Selection / help.

## 5. Out of scope

- Game-minute timers  
- Lobby demolish / lobby-cell refunds  
- Selling scaffolding  
- Partial elevator shorten refunds outside the existing correction-window rules (shorten still no cash refund; this slice is **full demolish** only)  
- Continuous rent slider / E1 traffic income (next deeper-economy slice after this)

## 6. Verification

- EditMode: place office → immediate grace demolish restores spend; after simulated `+11s` realtime, demolish restores $0.  
- EditMode: room with recorded lifetime income/expense → refund matches formula.  
- EditMode: elevator place + paid extend within grace → demolish refunds sum of both spends (assuming no upkeep yet).  
- EditMode: condo sale recorded then grace demolish → net ≈ pre-build cash.  
- Play Mode: place → undo within 10s; place → wait → bulldoze keeps $0.

## 7. Roadmap note

Parent roadmap: deeper economy → higher stars → more transit → evaluation/heatmaps → polish.  
This is **economy C** (anti money-loop undo). Next deeper-economy slice: **E1 commercial visit traffic**.
