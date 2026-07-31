# Build-A-Tower — Commercial Visit Traffic (E1)

**Date:** 2026-07-31  
**Status:** Approved  
**Depends on:** Slice #4 economy, price tiers HUD, build-grace refund  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → more transit → evaluation/heatmaps → polish

## 1. Goals

Make `TrafficVariable` shops earn money from **real visits**: tower residents (office / hotel / condo) and **street visitors** from Outside. Income settles in the existing **midnight** economy sweep.

### Success criteria

In Play Mode a player can:

1. Place **Fast Food**, **Restaurant**, and **Retail** (nested under Shops → Food / Retail).
2. See office workers take a midday commercial trip to an open reachable shop, dwell, and return.
3. See hotel guests and condo residents make at most one commercial trip per day on their MVP windows.
4. See street visitors spawn Outside, enter via lobby, visit a shop, then leave the tower.
5. At midnight, watch shop income appear in Last Net / unit contribution (no longer “traffic inactive $0”).
6. See selection report today’s visit count and expected income model for a shop.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Visit model | Real pathfinding trips (not formula-only) |
| Internal visitors | Office + hotel + condo |
| External visitors | Street traffic from Outside → lobby → shop → leave |
| Shop catalog this pass | Fast Food · Restaurant · Retail |
| Income timing | Batched at **midnight** |
| Price tiers on shops | Out of scope for E1 |
| Architecture | Home-agent commercial trips + ephemeral street visitor agents |

## 3. Shop catalog

Three placeable `RoomTypeSO` assets (`IncomeModel.TrafficVariable`):

| Id (suggested) | Display | Subgroup | Concurrent slots | Dwell (game minutes) | Pay / completed visit | Active hours |
|----------------|---------|----------|------------------|----------------------|----------------------|--------------|
| `shop_food_fast` | Fast Food | Food | 4 | 15–25 | $40 | 11:00–21:00 |
| `shop_food_restaurant` | Restaurant | Food | 6 | 40–60 | $120 | 11:00–22:00 |
| `shop_retail` | Retail | Retail | 5 | 20–40 | $80 | 10:00–20:00 |

Notes:

- Replace / retarget the existing single commercial asset (`RetailFastFood` / id `retail`) so the catalog matches Food vs Retail nesting.
- Exact dollars are first-pass tuneables; keep them as SO fields (`baseIncome` = pay per visit) plus optional authored active hours / max occupants for slots.
- Prefer **omit until asset exists** for future Fancy Restaurant / Boutique — not in this slice.
- Shop must be **reachable from lobby** (same transit rules as other rooms) to accept visits; otherwise skipped.

## 4. Visit flow

### 4.1 Internal (home agents)

| Role | When (MVP) | Trip |
|------|------------|------|
| Office worker | Midday window (~11:30–13:30), once per day while Working / at office | Office → shop → back to office (continue work day) |
| Hotel guest | Evening while Staying (~18:00–21:00), once per stay day | Room → shop → back to room |
| Condo resident | Daytime after moved-in (~12:00–17:00), once per day | Home → shop → home |

Rules:

- At most **one completed commercial trip attempt per agent per calendar day** (MVP).
- Choose a random open shop with free capacity and a valid path; if none, skip (no stress spike required this pass).
- On arrival: enter `VisitingShop` (or equivalent phase), dwell for type range, then resume return path.
- Completed visit increments that shop’s **day visit counter**.

### 4.2 Street visitors

- New role: `StreetVisitor` (ephemeral; not counted in star population).
- Spawn from Outside near lobby exit during daytime when ≥1 open reachable shop exists.
- Spawn rate: light constant × (1 + stars) with a **hard concurrent cap** (e.g. 8) so elevators stay readable.
- Path: Outside → lobby → shop → Outside; despawn when they leave.
- Completed visit increments the same day counter as internal visits.

### 4.3 Capacity & hours

- Concurrent visitors at a shop ≤ slot count (`maxOccupants` or dedicated field).
- Outside active hours: shop is closed (not selectable as destination).
- Full shop: try another open shop; if none, skip.

## 5. Economy

- **During day:** do **not** credit wallet per visit; only tally visits (+ bump `LifetimeIncome` at midnight when paid, for grace clawback consistency).
- **Midnight (`EconomySystem.OnNewDay`):** for each `TrafficVariable` room,  
  `amount = visitsToday × payPerVisit` (payPerVisit = `baseIncome`), add to wallet / Last Income / last-room maps / lifetime income, then **reset** `visitsToday`.
- Selection / format: replace “Traffic income inactive ($0)” with status like `Visits today: N` and income line `Income: $X / visit (batched at midnight)`.
- Button tags: show cost · `$X/visit` (or abbreviated).

## 6. Systems / files (expected)

| Area | Change |
|------|--------|
| Room assets / SO | Fast Food, Restaurant, Retail; active hours; slots; pay |
| `AgentRole` / phases | `StreetVisitor`; visiting / return phases as needed |
| `AgentSystem` | Schedule commercial trips; street spawn/despawn; dwell |
| Shop visit tally | On `RoomInstance` or small commercial service |
| `EconomySystem` | Midnight traffic payout from tallies |
| `RoomEconomyFormat` / HUD | Live visit status; no longer inactive |
| `BuildCatalog` / HUD list | Ensure three shops appear under Food/Retail |
| Population | Street visitors excluded from star population (like condo buyers pre-move-in) |
| Tests | Visit tally → midnight pay; closed/unreachable skip; street spawn cap; catalog grouping |
| README | Play steps for shops + visits |

## 7. Out of scope

- Shop price tiers / comfort demand  
- Noise penalties from commercial neighbors  
- Happy hour / daypart payout curves beyond simple active hours  
- Fancy Restaurant, boutiques, grocery, etc.  
- Stress from failed lunch (optional later)  
- Multi-stop shopping trips in one day  

## 8. Verification

- EditMode: completed visit increments counter; midnight pays `N × baseIncome` and clears counter; closed hours / unreachable / full capacity skip without pay.  
- EditMode: street visitors excluded from population; concurrent street cap respected.  
- EditMode: catalog groups Fast Food + Restaurant under Food, Retail under Retail.  
- Play Mode: place Fast Food near lobby + offices; at lunch see agents path in; overnight Last Net includes shop income; street visitors appear when shops are open.

## 9. Roadmap note

This is **deeper economy E1**. After this: higher stars (3–5), more transit, evaluation/heatmaps, polish.
