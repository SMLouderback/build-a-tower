# Build-A-Tower — Office Luxury Catalog

**Date:** 2026-08-04  
**Status:** Approved  
**Depends on:** Hotel luxury + four wealth bands (`luxuryBand`, `HotelLuxury` / climate bias, `Agent.Wealth`); price tiers + climate; office desk sync  
**Parent:** Deeper living-room ladders → demand ledger → amenity heatmaps  
**Follow-ups (tabled):** Condo luxury catalog; multi-room firm tenants; demand graph UI; proximity/noise/amenity heatmaps; cyclical office→jobs→condo/hotel demand stocks; numeric rebalance of build/rent

## 1. Goals

1. Replace the thin Office / Premium Office pair with a **~9-type ladder** across Base / Mid / Upper, sized for solo practices through large floorplates.  
2. **Share** hotel climate × luxury fill and price-tier pressure with offices via generalized living-luxury helpers.  
3. Hire office workers with **hotel-like wealth roll + room acceptance** (no silent downgrade).  
4. Leave **condo** on the existing 2-tier ladder; multi-room “firms” remain player flavor only (no firm entity this slice).

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Slice scope | **Office catalog only** (condo later) |
| Catalog depth | Hotel-parity **~9 types**; variants when desks or footprint differ |
| Multi-room firms | **Flavor only** — players may place several rooms; no company/lease object |
| Approach | Shared living-luxury helpers + `luxuryBand` on office `RoomTypeSO`s |
| Star unlocks | Base **0★**, Mid **2★**, Upper **3★** (match hotels) |
| Climate × luxury | **Mirror hotels** — fill attempt multipliers + demand bias/floors |
| Worker wealth | **Hotel-like** — roll from tower mix, then accept into matching offices; store on `Agent.Wealth` |
| Condo / demand graph / heatmaps | **Deferred** |
| Economics numbers | V1 ladder below; **rebalance later if needed** |

## 3. Office catalog (v1)

| ID | Display | Band | ★ | Size | Desks | Flavor |
|----|---------|------|---|------|------:|--------|
| `office_micro` | Micro Office | Base | 0 | 3×1 | 1 | Private detective / solo CPA |
| `office_studio` | Studio Office | Base | 0 | 4×1 | 1 | Sole prop + tiny meeting nook |
| `office_base` | Small Office | Base | 0 | 6×1 | 2 | 2-person practice |
| `office_mid_standard` | Mid Office | Mid | 2 | 9×1 | 4 | General mid firm |
| `office_mid_clinic` | Professional Suite | Mid | 2 | 10×1 | 6 | Doctors / specialists |
| `office_mid_team` | Team Bay | Mid | 2 | 12×1 | 8 | Lawyers / software pod |
| `office_upper_standard` | Upper Office | Upper | 3 | 12×1 | 6 | Prestige practice |
| `office_upper_corner` | Corner Suite | Upper | 3 | 14×1 | 8 | Partner floor / exec bay |
| `office_upper_floor` | Corporate Floor | Upper | 3 | 18×1 | 12 | Large floorplate |

Large firms may also be **roleplayed** by renting multiple Mid/Upper rooms; that is not simulated as one tenant.

### Migration

- `office` / Office → **`office_base`** (Resources + catalog).  
- `office_premium` / OfficePremium → **`office_mid_standard`**.  
- Keep old asset ids only if needed for save remaps; play catalog loads the nine new ids.  
- Ensure every playable office exists under `Resources/Rooms` (base Office historically may live only under ScriptableObjects).

## 4. Schema

### `RoomTypeSO`

- Reuse existing `luxuryBand`: `None | Base | Mid | Upper`.  
  - Offices: Base / Mid / Upper.  
  - Condos: stay `None` (interim wealth still via `requiredStars`) until condo catalog.  
- `maxOccupants` = desk capacity.  
- `cleanMinutes` unused for offices (no dirty/maid loop).  
- Existing fields unchanged: `requiredStars`, `buildCost`, `baseIncome`, `incomeModel` (daily rent), active hours, etc.

### Shared helpers

- Extract hotel climate bias, demand floors, and fill multipliers into a shared module (e.g. `LivingLuxury`), with thin hotel wrappers so existing hotel tests keep working.  
- Office acceptance / id constants live beside or in that module (office-specific accept sets).  
- Mixing tower wealth weights (stars / crime / climate) stay shared with hotel guest rolls where practical.

## 5. Economics ladder (Normal climate, Normal tier)

| Type | Build $ | `baseIncome` / day |
|------|--------:|-------------------:|
| Micro Office | 12_000 | 900 |
| Studio Office | 16_000 | 1_100 |
| Small Office | 28_000 | 2_200 |
| Mid Office | 55_000 | 5_000 |
| Professional Suite | 75_000 | 7_200 |
| Team Bay | 95_000 | 9_600 |
| Upper Office | 110_000 | 9_000 |
| Corner Suite | 140_000 | 13_000 |
| Corporate Floor | 200_000 | 20_000 |

Income model remains **daily office rent × price-tier multiplier**. Overpriced units may skip payout via existing demand; Mid/Upper get hotel-style climate pressure through shared helpers.

Legacy reference: Office was ~$40k / $3k (2 desks); Premium ~$60k / $5k (4 desks, 9×1). Small + Mid replace those roles with the footprints above.

## 6. Wealth mix and office acceptance

### Tower worker mix

Reuse hotel guest mix defaults and modifiers:

Basic **40%** · Mid **30%** · Upper **20%** · Premium **10%**, then stars / crime / climate modifiers and **renormalize**.

### Office acceptance

After rolling worker wealth *W*, only claim desks in offices whose type is in *W*’s accept set:

| Worker band | Accepts |
|-------------|---------|
| Basic | Base only (`office_micro`, `office_studio`, `office_base`) |
| Mid | Mid only (`office_mid_standard`, `office_mid_clinic`, `office_mid_team`) |
| Upper | `office_mid_team` + all Upper |
| Premium | `office_upper_corner` + `office_upper_floor` (prefer Corporate Floor when free) |

If no matching vacant desk → worker does not take a desk (no silent downgrade to Base for Premium).

### Agent wealth storage

- On successful desk assign, set `Agent.Wealth` to the rolled band (same pattern as hotel check-in).  
- Disposable income prefers stored `Agent.Wealth` when set.  
- **Condo** (and any remaining legacy office without band) keeps interim `AgentWealth.ResolveOfficeCondoBand` via `requiredStars` until condo catalog.

### Climate × office luxury

Same tables as hotel luxury (shared helpers):

**Luxury bias** on demand/payout climate offset:

| Band | Rec | Slow | Norm | Strong | Boom |
|------|-----|------|------|--------|------|
| Base | +0* | 0 | 0 | 0 | 0 |
| Mid | −1 | 0 | 0 | 0 | 0 |
| Upper | −2 | −1 | 0 | 0 | +1 |

\*Base in Recession: demand chance floor remains high when only mildly overpriced (hotel rule).

**Desk-fill attempt multipliers** (before wealth matching):

| Band | Rec | Slow | Norm | Strong | Boom |
|------|-----|------|------|--------|------|
| Base | 1.1 | 1.0 | 1.0 | 0.95 | 0.9 |
| Mid | 0.55 | 0.8 | 1.0 | 1.05 | 1.1 |
| Upper | 0.2 | 0.5 | 1.0 | 1.15 | 1.25 |

`EconomySystem.PassesDemand` / `EffectiveDemandClimateOffset` apply shared luxury bias for **Office** as well as Hotel.

## 7. Systems touchpoints

1. **Assets** — nine Resources + ScriptableObjects mirrors; HUD catalog loads all nine.  
2. **`AgentSystem.SyncHomes` (office path)** — climate fill attempt → roll wealth → acceptance → assign desk + `Agent.Wealth`; respect condo desk reservation under-fill.  
3. **`EconomySystem`** — office rooms use shared luxury climate effective offset.  
4. **`AgentWealth`** — office resolution prefers stored wealth / `luxuryBand` acceptance path; remove reliance on `requiredStars` for new office ids.  
5. **HUD / Selection** — luxury band, desk cap, climate hint; short glyphs for the nine types.  
6. **README** — document office ladder; note condo still 2-tier; deferred multi-room firms and demand graph.

## 8. HUD / UX

- Office family lists all nine types; lock by `requiredStars`.  
- Compact cost · income tags unchanged in spirit.  
- Selection: luxury band, desks, climate/wealth hint when useful (e.g. “Upper · weak demand in Recession”).  
- Glyphs: short labels (Om, Os, Ob, MS, Cl, Tb, Uo, Uc, Cf) or equivalent — implementation detail.

## 9. Testing

EditMode (and net8 hosts if Editor busy):

1. Office acceptance matrix (Basic↛Upper, Premium↛Micro, Upper accepts Team Bay + Upper, Premium prefers Corporate).  
2. Shared climate bias/floors apply when category is Office.  
3. Desk fill refuses mismatched wealth.  
4. Asset fields: band, desks, stars, costs for each id.  
5. Hotel tests remain green after helper extract.  
6. Condo fill/wealth path unchanged (regression).

## 10. Revisit later (do not implement in this slice)

1. **Condo luxury catalog** (mirror this ladder).  
2. **Multi-room firm tenants** — company objects leasing N rooms.  
3. **Demand graph UI** and full demand stocks.  
4. **Proximity / noise / amenity heatmaps**.  
5. **Cyclical** luxury office ↔ high-pay jobs ↔ condo/hotel demand.  
6. **Numeric rebalance** of build cost / rent.

## 11. Success criteria

1. Nine office types placeable with correct ★ gates, sizes, and desk caps.  
2. Shared climate × luxury affects office rent payout checks and desk-fill attempts.  
3. Hiring uses tower wealth mix + acceptance; wealth stored on the agent.  
4. Condo behavior and 2-tier catalog unchanged.  
5. Legacy `office` / `office_premium` remapped; play catalog uses new ids.  
6. EditMode tests cover acceptance, climate-on-office, and hotel regression.  
7. README notes office ladder and deferred condo / firms / demand graph.

## 12. Non-goals

- Condo catalog expansion.  
- Firm / company leasing simulation.  
- Polished room art / tile atlas.  
- Full RCI demand simulation or Demand graph window.  
- Spa/gym room types.  
- Changing 5★ parking/valet gates or hotel catalog.
