# Build-A-Tower — Condo Luxury Catalog

**Date:** 2026-08-05  
**Status:** Implemented  
**Depends on:** Hotel + office luxury catalogs; `LivingLuxury`; four wealth bands; condo sale / move-in / jobs  
**Parent:** Deeper living-room ladders → demand ledger → amenity heatmaps  
**Sibling:** `2026-08-04-office-luxury-catalog-design.md`  
**Follow-ups (tabled):** Multi-unit household entities; demand graph UI; proximity/noise/amenity heatmaps; cyclical office→jobs→condo/hotel demand stocks; numeric rebalance of build/sale

## 1. Goals

1. Replace the thin Condo / Premium Condo pair with a **~9-type ladder** across Base / Mid / Upper, with real footprint and occupant diversity.  
2. Sell condos with **hotel/office-like wealth roll + room acceptance** (no silent downgrade), reusing `LivingLuxury` climate × luxury.  
3. Give each condo type a **distinct `placeholderColor`** (green ramp); **also fix office colors** (blue ramp) missed in the office slice.  
4. Leave move-in, `CondoSold`, in-tower/outside jobs, and desk reservation behavior intact aside from the fill/acceptance path.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Slice scope | **Condo catalog** + office placeholder-color fix |
| Catalog depth | Hotel/office-parity **~9 types**; variants when size or occupant cap differs |
| Approach | `CondoLuxury` + `luxuryBand` on condo `RoomTypeSO`s; reuse `LivingLuxury` |
| Star unlocks | Base **0★**, Mid **2★**, Upper **3★** |
| Climate × luxury | **Mirror hotels/offices** — fill attempt multipliers + demand bias/floors |
| Buyer wealth | **Office-shaped acceptance**; store on `Agent.Wealth` at assign |
| Differentiation | **Size + occupants** (not reskins of one 16×1 slab) |
| Placeholder colors | Hotel-style per-type ramp within family hue (condos green; offices blue) |
| Households / demand graph / heatmaps | **Deferred** |
| Economics numbers | V1 ladder below; **rebalance later if needed** |

## 3. Condo catalog (v1)

| ID | Display | Band | ★ | Size | Occ | Flavor |
|----|---------|------|---|------|----:|--------|
| `condo_studio` | Studio | Base | 0 | 4×1 | 1 | Micro loft |
| `condo_alcove` | Alcove Studio | Base | 0 | 5×1 | 2 | Sleeping nook |
| `condo_base` | One Bedroom | Base | 0 | 8×1 | 2 | Standard starter |
| `condo_mid_standard` | Mid Condo | Mid | 2 | 10×1 | 3 | General mid |
| `condo_mid_loft` | Loft | Mid | 2 | 12×1 | 2 | Open-plan / creative |
| `condo_mid_family` | Family Condo | Mid | 2 | 14×1 | 4 | Larger Mid; Upper buyers can take it |
| `condo_upper_standard` | Upper Condo | Upper | 3 | 12×1 | 3 | Prestige unit |
| `condo_upper_corner` | Corner Condo | Upper | 3 | 14×1 | 4 | View / corner |
| `condo_upper_penthouse` | Penthouse | Upper | 3 | 18×1 | 4 | Top floorplate; Premium prefers this |

### Migration

- `condo` / Condo → **`condo_base`** (Resources + catalog).  
- `condo_premium` / CondoPremium → **`condo_mid_standard`**.  
- Keep old asset ids only if needed for save remaps; play catalog loads the nine new ids.  
- Ensure every playable condo exists under `Resources/Rooms` (base Condo historically may live only under ScriptableObjects).

## 4. Schema

### `RoomTypeSO`

- Set `luxuryBand`: Base / Mid / Upper on all nine condo types.  
- `maxOccupants` = household capacity (all slots fill when the unit sells, same as today).  
- `incomeModel` remains **`UpfrontSale`**; `baseIncome` = sale price.  
- `cleanMinutes` unused (no dirty/maid loop).  
- Existing fields unchanged: `requiredStars`, `buildCost`, active hours, noise, etc.

### `CondoLuxury` (new)

- Id constants for the nine types.  
- `AcceptsBuyer(LuxuryBand roomBand, WealthBand buyer, string roomId = null)` — office-shaped matrix.  
- `PremiumUnitPreferenceRank(WealthBand wealth, string roomId)` — lower better; Penthouse then Corner.

### Shared helpers

- Reuse `LivingLuxury` for wealth mix roll, climate bias, fill multipliers, demand floors.  
- No further extract required unless duplication appears during implementation.

## 5. Economics ladder (Normal climate, Normal tier)

| Type | Build $ | Sale $ (`baseIncome`) | Occ |
|------|--------:|----------------------:|----:|
| Studio | 35_000 | 65_000 | 1 |
| Alcove Studio | 45_000 | 85_000 | 2 |
| One Bedroom | 80_000 | 150_000 | 2 |
| Mid Condo | 120_000 | 200_000 | 3 |
| Loft | 140_000 | 230_000 | 2 |
| Family Condo | 160_000 | 270_000 | 4 |
| Upper Condo | 180_000 | 300_000 | 3 |
| Corner Condo | 220_000 | 360_000 | 4 |
| Penthouse | 280_000 | 450_000 | 4 |

Sale still pays once via `EconomySystem.TrySellCondo` on first move-in. Price-tier scaling unchanged.

Legacy reference: Condo ~$80k / $150k (2 occ, 16×1); Premium ~$120k / $200k (4 occ, 16×1). One Bedroom + Mid Condo replace those roles with smaller footprints.

## 6. Wealth mix and condo acceptance

### Tower buyer mix

Same defaults as hotel/office:

Basic **40%** · Mid **30%** · Upper **20%** · Premium **10%**, then stars / crime / climate modifiers and **renormalize**.

### Condo acceptance

After rolling buyer wealth *W*, only sell into units whose type is in *W*’s accept set:

| Buyer band | Accepts |
|------------|---------|
| Basic | Base only (`condo_studio`, `condo_alcove`, `condo_base`) |
| Mid | Mid only (`condo_mid_standard`, `condo_mid_loft`, `condo_mid_family`) |
| Upper | `condo_mid_family` + all Upper |
| Premium | `condo_upper_corner` + `condo_upper_penthouse` (prefer Penthouse when free) |

If no matching vacant unsold unit → no sale this attempt (no silent downgrade).

### Agent wealth storage

- On successful condo assign, set every resident in that unit’s `Agent.Wealth` to the rolled band.  
- Disposable income prefers stored `Agent.Wealth` when set.  
- `AgentWealth.ResolveOfficeCondoBand`: condo path uses `luxuryBand` (like offices); remove stars-only interim for new condo ids.

### Climate × condo luxury

Same tables as hotel/office via `LivingLuxury`:

**Luxury bias** on demand/payout climate offset:

| Band | Rec | Slow | Norm | Strong | Boom |
|------|-----|------|------|--------|------|
| Base | +0* | 0 | 0 | 0 | 0 |
| Mid | −1 | 0 | 0 | 0 | 0 |
| Upper | −2 | −1 | 0 | 0 | +1 |

\*Base in Recession: demand chance floor remains high when only mildly overpriced.

**Sale-fill attempt multipliers** (before wealth matching):

| Band | Rec | Slow | Norm | Strong | Boom |
|------|-----|------|------|--------|------|
| Base | 1.1 | 1.0 | 1.0 | 0.95 | 0.9 |
| Mid | 0.55 | 0.8 | 1.0 | 1.05 | 1.1 |
| Upper | 0.2 | 0.5 | 1.0 | 1.15 | 1.25 |

`EconomySystem.EffectiveDemandClimateOffset` / `PassesDemand` apply shared luxury bias for **Condo** as well as Hotel and Office. Condo pre-sale gate (`PassesCondoDemand` or successor) must use the same effective offset + floors.

## 7. Systems touchpoints

1. **Assets** — nine Resources + ScriptableObjects mirrors; HUD catalog loads all nine.  
2. **`AgentSystem.SyncHomes` (condo path)** — stop blind fill of unsold condos; add `FillCondoVacancies` mirroring offices: climate fill gate → roll wealth → acceptance → spawn up to `maxOccupants` with `Agent.Wealth`; keep reachability (`CanReachCondoFromLobby`) and sold-unit skip.  
3. **Sale / move-in** — first `AtHome` still triggers `TrySellCondo`; jobs / desk reservation unchanged.  
4. **`EconomySystem`** — Condo rooms use shared luxury climate effective offset + demand floors.  
5. **`AgentWealth`** — condo resolution prefers stored wealth / `luxuryBand` path.  
6. **HUD / Selection** — luxury band, occupants, climate/wealth hint; short glyphs for the nine types.  
7. **Placeholder colors** — distinct per condo type (green ramp); **recolor all nine office assets** (blue ramp, hotel-style lightness steps).  
8. **README** — document condo ladder; note office color fix; deferred households / demand graph.

## 8. Placeholder colors

Stay in the family’s hue; step lightness/saturation so types are readable on the grid (hotel purple pattern):

- **Condos** — greens: lighter Base → mid greens → deeper Upper / Penthouse.  
- **Offices (fix)** — blues: lighter Micro/Studio/Base → mid Mid types → deeper Upper / Corporate Floor.  

Exact RGB values are implementation detail; requirement is **visibly different within the family**, not identical copies.

## 9. HUD / UX

- Condo family lists all nine types; lock by `requiredStars`.  
- Compact cost · sale tags unchanged in spirit.  
- Selection: luxury band, occupants, sold/unsold, climate/wealth hint when useful.  
- Glyphs: short labels (e.g. St, Al, 1b, Md, Lf, Fm, Uc, Cn, Ph) — implementation detail.

## 10. Testing

EditMode (and net8 hosts if Editor busy):

1. Condo acceptance matrix (Basic↛Upper, Premium↛Studio, Upper accepts Family + Upper, Premium prefers Penthouse).  
2. Shared climate bias/floors apply when category is Condo.  
3. Sale fill refuses mismatched wealth; all occupants in a unit share the rolled wealth.  
4. Asset fields: band, size, occ, stars, build, sale for each id.  
5. Office + hotel tests remain green.  
6. Office placeholder colors are no longer identical across the nine types (spot-check or asset assert).  
7. Move-in sale + condo job reservation regressions stay green.

## 11. Revisit later (do not implement in this slice)

1. **Multi-resident household entities** (couples/families as one lease object).  
2. **Demand graph UI** and full demand stocks.  
3. **Proximity / noise / amenity heatmaps**.  
4. **Cyclical** luxury office ↔ high-pay jobs ↔ condo/hotel demand.  
5. **Numeric rebalance** of build cost / sale price.  
6. Polished room art / tile atlas.

## 12. Success criteria

1. Nine condo types placeable with correct ★ gates, sizes, and occupant caps.  
2. Shared climate × luxury affects condo sale demand and fill attempts.  
3. Buying uses tower wealth mix + acceptance; wealth stored on residents.  
4. Move-in sale, jobs, and desk reservation still work.  
5. Legacy `condo` / `condo_premium` remapped; play catalog uses new ids.  
6. Condo and office placeholder colors vary within their families.  
7. EditMode tests cover acceptance, climate-on-condo, sale fill, and regressions.  
8. README notes condo ladder and deferred items.

## 13. Non-goals

- Household / family simulation objects.  
- Changing hotel catalog or office desk ladder (except office colors).  
- Firm / company leasing.  
- Spa/gym room types.  
- Full RCI demand simulation or Demand graph window.  
- Changing 5★ parking/valet gates.
