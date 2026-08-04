# Build-A-Tower — Shop Visit History (Yesterday + 7-Day Avg)

**Date:** 2026-08-03  
**Status:** Implemented  
**Depends on:** Shop `VisitsToday` / midnight `ResetVisitsToday`; Economy HUD unlock  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  

## 1. Goals

Show shop foot traffic for **yesterday** and a **rolling 7-day average**, both **per shop** (Selection) and **tower-wide** (Shops dropdown / selection context). Keep storage tiny (7 ints per shop + 7 ints tower).

### Success criteria

1. Selecting a shop shows visits today, yesterday, and 7-day average.  
2. **Shops** top-bar dropdown (and selecting a shop) shows tower yesterday shop visits and 7-day average when economy is unlocked.  
3. Average uses only days that have been recorded (no zero-padding before history fills).  
4. At most 7 days of visit counts retained per shop and for the tower.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | **Both** per-shop + tower-wide |
| Window | Last **7** completed midnights |
| Average | Mean over recorded days only (1…7), not forced /7 with leading zeros |
| Storage | Approach 1 — ring on `RoomInstance` + tower ring on `EconomySystem` |
| Push timing | Midnight in `EconomySystem.OnNewDay`, **before** `ResetVisitsToday` |

## 3. Data model

### Per shop (`RoomInstance`)

- Keep `VisitsToday`.  
- Add ring buffer of 7 ints (oldest→newest or head index).  
- On midnight for each shop: `PushDailyVisits(VisitsToday)` then `ResetVisitsToday()`.  
- API: `VisitsYesterday`, `AverageVisitsLast7Days` (float).

### Tower (`EconomySystem`)

- Each midnight: `towerDay = Σ VisitsToday` over shops (before reset).  
- Push `towerDay` into a 7-int ring.  
- Expose `LastShopVisitsYesterday`, `AverageShopVisitsLast7Days`.

## 4. UI

- **Selection** (`RoomEconomyFormat` TrafficVariable):  
  - Visits today (existing)  
  - Visits yesterday: N  
  - Avg visits (7d): X.X  
- **Top bar**: **Shops** dropdown (and temporary chips while a shop is selected) show `Shops yday N` and `Shops ~X/d` when economy is unlocked.

## 5. Out of scope

- Earnings history  
- Per-visitor breakdown / charts  
- Persist across save (no save system yet)

## 6. Tests

- Push 1 day → yesterday that count; avg = that count.  
- Push 7 then 8th → oldest dropped; avg over latest 7.  
- Tower sum matches sum of shop day totals for that midnight.
