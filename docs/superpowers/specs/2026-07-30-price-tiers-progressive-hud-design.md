# Build-A-Tower — Price Tiers & Progressive HUD

**Date:** 2026-07-30  
**Status:** Approved  
**Depends on:** Slice #4 + star goals / unit economy / condo move-in  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first

## 1. Goals

Give players **control over room pricing** without a full continuous slider yet, and shrink the HUD so the game starts simple and grows as capabilities unlock.

### Success criteria

In Play Mode a player can:

1. Select a priced room (office / hotel / condo, including premiums) and set **Low / Normal / High / Max**.
2. See payout and demand change with tier (and with tower stars via the comfort band).
3. See a compact **core HUD** (funds, time, stars, help) that does not dump every panel at once.
4. Expand **Goals / Economy / Build / Selection** sections; sticky open/closed for the session.
5. Only see soft-unlocked sections after their gates (lobby → Goals; first midnight/income → Economy).
6. Still place rooms via Build from the start.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Pricing UI | Discrete tiers now; continuous slider **after MVP** |
| HUD layout | Hybrid: always-on core + expandable / unlockable sections |
| Demand this pass | Light occupancy / buyer-spawn response for office, hotel, condo |
| Commercial traffic visits | Out of scope (still inactive; field reserved) |
| Quality levers beyond stars | Out of scope (stress/amenities/crime later) |

## 3. Price tiers

### 3.1 Storage

Each priced `RoomInstance` stores `PriceTier` ∈ `{0,1,2,3}`:

| Value | Label | Payout vs `baseIncome` |
|------:|-------|-------------------------|
| 0 | Low | 70% |
| 1 | Normal (default) | 100% |
| 2 | High | 130% |
| 3 | Max | 160% |

Applies to: Office, Premium Office, Hotel, Premium Hotel, Condo, Premium Condo.  
Restaurant / Retail: may store the field later; selection still shows traffic income inactive.

Payout amounts are integers: `round(baseIncome * multiplier)` (or truncate consistently — prefer round-to-nearest, document in code).

### 3.2 Demand (this pass)

**Office / Hotel (`QuarterlyRent` / `NightlyRate`)**  
- Each day (or on occupancy check), acceptance/retention chance falls as tier rises above the star comfort band.  
- Vacant / no guest → no recurring income that day.  
- Occupied income uses tier multiplier.

**Condo (`UpfrontSale`)**  
- When a condo becomes reachable, buyer spawn chance falls at higher tiers / overpriced bands.  
- Sale payout = `baseIncome × multiplier` paid only on move-in (existing condo sale timing).  
- Inaccessible condos still spawn no buyers and pay nothing.

**Stars comfort band** (ready for 0–5★ even if only 0–2★ are earnable now):

| Stars | Comfortably supports up to |
|------:|----------------------------|
| 0★ | Low |
| 1★ | Low–Normal |
| 2★ | Normal |
| 3★ | Normal–High |
| 4★ | High |
| 5★ | High–Max |

Pricing above comfort is allowed; demand drops steeply (exact curve is a tunable constant table).

### 3.3 Selection UI

When a priced room is selected:

- Four buttons: **Low · Normal · High · Max** (active tier highlighted).
- One-line market hint, e.g. `Market: OK for 1★` / `Overpriced for 1★`.
- Existing unit economy lines (built cost, income model, status, last contribution) remain.

No global “set all rents” panel this slice.

## 4. Progressive HUD

### 4.1 Always-on core strip

- Funds  
- Time + speed presets  
- Stars (`N/Max`)  
- One-line help / status  

Keep this short so the first viewport stays readable.

### 4.2 Expandable sections

| Section | Contents | Soft unlock |
|---------|----------|-------------|
| **Goals** | Next ★ checklist | Lobby exists |
| **Economy** | Last Net, population, avg stress | After first midnight sweep **or** first condo sale / income event |
| **Build** | Room buttons + tools | Always (required to play) |
| **Selection** | Identity, economy, price tiers, elevator maintenance | Something selected |

Behavior:

- Collapsed by default except as needed for first-run play (Build may start expanded).  
- Open/closed state sticky for the Play session.  
- Hidden entirely until unlock gate trips (do not show locked empty headers).  
- Price controls live only under Selection for priced rooms.

### 4.3 Implementation sketch

- `TowerHudController`: core strip + foldout section drawing; session bools for expanded state; unlock predicates from sim/build.  
- Prefer IMGUI foldouts / toggle headers consistent with current HUD (no new UI framework this pass).

## 5. Systems / files (expected)

| Area | Change |
|------|--------|
| `RoomInstance` | `PriceTier` property |
| `EconomySystem` | Apply tier multiplier to recurring income and condo sale |
| `AgentSystem` / occupancy | Light demand checks for office/hotel retention and condo buyer spawn |
| `StarSystem` or small helper | Comfort-band + market hint string |
| `TowerHudController` | Progressive sections + selection price buttons |
| `RoomEconomyFormat` | Show effective income at current tier |
| Tests | Tier payout math; overpriced demand; HUD unlock predicates if testable |
| README | Price tiers + HUD sections |

## 6. Out of scope

- Continuous rent / price slider  
- Restaurant vs retail visit schedules and traffic income  
- Global rent board for all rooms at once  
- Stress / amenities / crime as pricing power  
- Full 3–5★ unlock content (band table only)  
- Build-grace demolish refunds (noted for later economy C)

## 7. Roadmap note

Parent roadmap remains: deeper economy → higher stars → more transit → evaluation/heatmaps → polish.  
This slice is a **UI + light demand** piece of deeper economy; visit-based commercial (E1) can follow.

## 8. Verification

- EditMode: tier multipliers on rent and condo sale; inaccessible condo still pays $0; overpriced reduces occupancy/buyer chance under controlled RNG or deterministic thresholds.  
- Play Mode: select office → change tier → see status/hint; collapse Goals/Economy; Build still usable; core strip stays visible at all speeds.
