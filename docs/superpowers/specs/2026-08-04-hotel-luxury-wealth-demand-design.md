# Build-A-Tower — Hotel Luxury Catalog + Four Wealth Bands

**Date:** 2026-08-04  
**Status:** Implemented  
**Depends on:** Price tiers + climate; hotel dirty/maid clean; AgentWealth; stars unlocks  
**Parent:** Deeper living-room ladders → demand ledger → amenity heatmaps  
**Follow-ups (tabled):** Proximity/noise/crime heatmaps; SimCity-style Demand graph UI; spa/gym amenities; condo luxury catalog; cyclical office→jobs→condo/hotel demand stocks. **Office catalog:** `2026-08-04-office-luxury-catalog-design.md`

## 1. Goals

1. Replace the thin Hotel Single / Premium pair with a **right-sized hotel catalog** across Base / Mid / Upper luxury.  
2. Introduce tower-wide **four guest/worker wealth bands**: Basic, Mid, Upper, Premium (plus Street for walk-ins).  
3. Wire **climate × luxury** into hotel fill chance and price-tier comfort so budget rooms hold up in recessions and Upper rooms sit empty unless the market is strong.  
4. Leave hooks and explicit revisit notes for **demand graphs**, amenity proximity, and cross-category demand cycles — without shipping those systems in this slice.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Slice scope | Hotel catalog + climate demand + **AgentWealth → 4 bands** (not full demand ledger / graph) |
| Approach | `luxuryBand` on `RoomTypeSO` + explicit `cleanMinutes` |
| Catalog depth | Six named configs + Studio, Accessible, Junior Suite |
| Bed layout variants | Separate buildables **only** when guest cap or footprint differs; otherwise flavor text |
| Star unlocks | Base **0★**, Mid **2★**, Upper **3★** |
| Climate × luxury | Fill chance **and** tighter price-tier tolerance for Mid/Upper in weak climates |
| Wealth in this slice | Migrate living roles to 4 bands; office/condo **room** ladders stay 2-tier until later |
| Default guest mix | Basic 40% · Mid 30% · Upper 20% · Premium 10% (then modifiers) |
| Proximity / noise / crime heatmaps | **Deferred** — revisit when measurement/heatmap slices land |
| Demand graph UI | **Deferred** — SimCity-style R/C/I × wealth visualization later |

## 3. Hotel catalog (v1)

| ID | Display | Band | ★ | Size | Guests | Flavor / notes |
|----|---------|------|---|------|--------|----------------|
| `hotel_base` | Hotel Base | Base | 0 | 3×1 | 2 | 1 queen or 2 twins |
| `hotel_accessible` | Hotel Accessible | Base | 0 | 3×1 | 2 | Same footprint; slightly higher build cost |
| `hotel_mid_standard` | Hotel Mid Standard | Mid | 2 | 4×1 | 4 | 2 queens or 1 king + desk |
| `hotel_mid_extended` | Hotel Mid Extended | Mid | 2 | 6×1 | 6 | 2Q/K + pull-out couch |
| `hotel_studio` | Hotel Studio | Mid | 2 | 5×1 | 3 | Kitchenette |
| `hotel_junior_suite` | Hotel Junior Suite | Mid | 2 | 5×1 | 4 | Open living + sleep (no full split) |
| `hotel_upper_standard` | Hotel Upper Standard | Upper | 3 | 5×1 | 4 | 2Q + seating + larger bath |
| `hotel_upper_king` | Hotel Upper King | Upper | 3 | 5×1 | 2 | King + seating + larger bath |
| `hotel_upper_suite` | Hotel Upper Suite | Upper | 3 | 8×1 | 8 | Connected layout as **one** footprint; 4–8 guests |

### Migration

- `hotel_single` / HotelSingle → **`hotel_base`** (Resources + catalog).  
- `hotel_premium` / HotelPremium → **`hotel_mid_standard`**.  
- Keep old asset ids only if needed for save remaps; play catalog loads the nine new ids.  
- `RoomConditionRules.CleanMinutes` stops using `requiredStars ≥ 2` for hotels; use `cleanMinutes` (fallback: band defaults).

## 4. Schema

### `RoomTypeSO`

- `luxuryBand`: `None | Base | Mid | Upper`  
  - Hotels: Base / Mid / Upper.  
  - Other categories: `None` until their catalog slices.  
- `cleanMinutes`: `float` ≥ 0. Hotels set explicitly.  
- Existing fields unchanged: `requiredStars`, `buildCost`, `baseIncome`, `maxOccupants`, `requiresHousekeeping`, `noiseSensitivity` (noise **unused** this slice).

### Optional later (not required v1)

- `conditionDecayWeight` for Upper maintenance pressure — **optional**; may ship as simple band table instead.

## 5. Economics ladder (Normal climate, Normal tier)

| Type | Build $ | `baseIncome` (nightly) | Clean min |
|------|--------:|----------------------:|----------:|
| Base | 18_000 | 1_800 | 12 |
| Accessible | 22_000 | 1_900 | 14 |
| Mid Standard | 45_000 | 4_000 | 22 |
| Studio | 55_000 | 4_200 | 25 |
| Junior Suite | 60_000 | 4_500 | 28 |
| Mid Extended | 70_000 | 5_500 | 32 |
| Upper Standard | 95_000 | 7_500 | 35 |
| Upper King | 100_000 | 8_000 | 35 |
| Upper Suite | 160_000 | 12_000 | 55 |

Income model remains nightly hotel rent × price-tier multiplier. Dirty-on-checkout and maid pathing unchanged; duration = `cleanMinutes` × housekeeping research multiplier.

## 6. Four wealth bands

### Bands

| Band | Role |
|------|------|
| Street | Walk-in shop visitors only |
| Basic | Budget guests / workers |
| Mid | Standard |
| Upper | Affluent |
| Premium | Top-end |

### Default mix (hotel arrivals / tower guest pool)

Basic **40%** · Mid **30%** · Upper **20%** · Premium **10%**, then apply modifiers and **renormalize**.

### Mix modifiers (hotel v1)

| Signal | Effect |
|--------|--------|
| Stars 0–1 | Boost Basic/Mid; cut Upper/Premium |
| Stars 4–5 | Boost Upper/Premium; trim Basic |
| High average crime | Premium ≈ 0; Upper strongly reduced |
| Recession / Slow | Boost Basic; cut Upper/Premium |
| Strong / Boom | Boost Upper/Premium |

**Amenities** (spa, gym, fine dining access, low elevator stress, parking): **stub hooks only** — weight = 1.0 this slice; document for demand/heatmap follow-up.

### Disposable daily ranges (× climate spend multiplier)

| Band | Range $ |
|------|---------|
| Street | 35–90 |
| Basic | 55–110 |
| Mid | 90–160 |
| Upper | 140–220 |
| Premium | 200–320 |

### Resolve band from home (interim office/condo)

| Home | Rule |
|------|------|
| Hotel Base | Basic |
| Hotel Mid | Mid |
| Hotel Upper (non-suite) | Upper |
| Hotel Upper Suite | 50% Upper / 50% Premium |
| Office / Condo `requiredStars < 2` | 30% Basic / 70% Mid |
| Office / Condo `requiredStars ≥ 2` | 70% Upper / 30% Premium |
| Event visitor | Mid |
| Street visitor | Street |

### Hotel room acceptance

After rolling guest wealth *W*, only claim beds in rooms whose `luxuryBand` is in *W*’s accept set:

| Guest band | Accepts hotel luxury |
|------------|----------------------|
| Basic | Base |
| Mid | Mid |
| Upper | Mid Extended + Upper (all Upper types) |
| Premium | Upper King + Upper Suite (prefer Suite when free) |

If no matching vacant clean room → guest does not check in (no silent downgrade to Base for Premium).

### Shop spend (light touch)

- Fine Dining: soft-prefer Mid+ (Basic may fail afford gate more often).  
- Fast Food / Retail / Restaurant: unchanged afford math using new ranges.  
- No full shop demand ledger this slice.

## 7. Climate × hotel luxury

Reuse `PricePricing` comfort tier + `MarketClimate.ComfortTierOffset`, with a **luxury bias** for hotel demand/payout checks:

| Band | Rec | Slow | Norm | Strong | Boom |
|------|-----|------|------|--------|------|
| Base | +0* | 0 | 0 | 0 | 0 |
| Mid | −1 | 0 | 0 | 0 | 0 |
| Upper | −2 | −1 | 0 | 0 | +1 |

\*Base in Recession: **fill/check-in factor** floored high (e.g. attempt multiplier ≥ 1.0 and demand chance floor ≥ 0.85 when only mildly overpriced).

**Check-in / bed-claim attempt multipliers** (before wealth matching):

| Band | Rec | Slow | Norm | Strong | Boom |
|------|-----|------|------|--------|------|
| Base | 1.1 | 1.0 | 1.0 | 0.95 | 0.9 |
| Mid | 0.55 | 0.8 | 1.0 | 1.05 | 1.1 |
| Upper | 0.2 | 0.5 | 1.0 | 1.15 | 1.25 |

Nightly payout still goes through `PassesDemand` with effective `climateOffset + luxuryBias` for that room’s band.

## 8. HUD / UX

- Hotel family lists all nine types; lock by `requiredStars`.  
- Compact cost · income tags unchanged in spirit.  
- Selection: luxury band, guest cap, clean minutes, climate/wealth hint when useful (e.g. “Upper · weak demand in Recession”).  
- Glyphs: keep short labels (Hb, Ha, Hm, …) or first letters — implementation detail.

## 9. Long-term demand vision (not implemented here)

Target loop (SimCity-like):

- Track **demand** by category × wealth (hotel, office, condo, shops).  
- Positive demand → pressure to build that class; surplus → negative demand.  
- Drivers: stars, crime, amenities, parking, conference/event, elevator stress/population, luxury office jobs ↔ condo/hotel Upper/Premium demand.  
- UI: Demand graph with bands akin to R-$ / R-$$ / R-$$$ (user reference: SimCity 4 Demand window).

This slice only supplies **wealth bands + hotel luxury + climate fill** as prerequisites.

## 10. Revisit later (do not implement in this slice)

1. **Proximity / noise / amenity heatmaps** — Upper rooms prefer quiet + near spa/gym/fine dining; Base tolerates utilities.  
2. **Crime as demand-graph input** — beyond mix weights.  
3. **Demand graph UI** and full demand stocks.  
4. **Condo luxury catalog** (mirror hotel/office ladders). Office catalog: see `2026-08-04-office-luxury-catalog-design.md`.  
5. **Spa, gym, and other amenities** that shift Upper/Premium mix.  
6. **Cyclical** luxury office ↔ high-pay jobs ↔ condo/hotel demand.

## 11. Success criteria

1. Nine hotel types placeable with correct ★ gates, sizes, and guest caps.  
2. Clean duration uses per-type `cleanMinutes`.  
3. `AgentWealth` exposes four living bands (+ Street); hotel guests and office/condo residents resolve via §6.  
4. Recession: Base hotels still fill reasonably; Upper fill and High/Max tiers struggle.  
5. Boom: Upper/Premium mix and Upper fill improve.  
6. High crime reduces Premium (and Upper) hotel guest mix.  
7. EditMode tests cover band resolution, acceptance sets, and climate fill bias.  
8. Spec/README note the deferred demand-graph and heatmap work.

## 12. Non-goals

- Polished room art / tile atlas (separate art track).  
- Saving migration for arbitrary old towers beyond id remap of single/premium.  
- Full RCI demand simulation or Demand graph window.  
- Spa/gym room types.  
- Changing 5★ parking/valet gates.
