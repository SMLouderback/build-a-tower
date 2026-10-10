# Build-A-Tower — Economy Foundation (VPSF + Hybrid Sim Loop)

**Date:** 2026-10-09  
**Status:** Approved (brainstorming)  
**Depends on:** `EconomySystem`, `BuildEconomy`, `DifficultyProfile`, `MarketClimate`, Office/Hotel/Condo luxury bands, star unlocks, elevator wait metrics  
**Engine target:** Unity (2D) C#  
**Related rules:** `.cursor/rules/economy-systems.mdc`

## 1. Goals

Replace SO-authored `buildCost` / `baseIncome` money paths with a central **Value-Per-Square-Foot (VPSF)** engine, add a **weekly Market Pulse** beside monthly climate, wire the **108 living matrix** (type × tier × class) with class upkeep and clash/elev rules, and apply **soft vertical zoning** with placement/HUD warnings — SimTower cash/ops cadence + SimCity-style demand/value pulse.

### Success criteria

1. All living/work and commercial place costs + midnight income/upkeep come from `EconomicBalancingManager` (not SO money fields).
2. 108 Office/Hotel/Condo variants resolve to (Family, Tier 1–9, Class Lower/Mid/Upper) with class upkeep ≈40% / baseline / ≈15%.
3. Weekly pulse advances and persists; stacks with monthly `MarketClimate` under documented clamps.
4. Floor-fit soft-multiplies income; bad fits show dismissible warning copy (no hard placement ban).
5. Upper class takes elev-wait stress after **15s**; class-clash penalties apply near Lower / noisy services.
6. Five difficulties (Sandbox…Extreme) still scale costs/income via existing mult hooks.
7. EditMode tests cover VPSF samples, floor-fit, clash, elev threshold, pulse snapshot.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Feel | Hybrid SimTower ops + SimCity demand/value pulse |
| Stars | Keep star-gated unlocks; stars widen prestige floor band + slight VPSF ceiling |
| Vertical zoning | Soft fit + HUD/placement warnings (not hard gates) |
| VPSF vs SO money | Full replace — SO `buildCost`/`baseIncome` unused for economy |
| 108 matrix | Full this slice (upkeep + clash + Upper elev 15s) |
| Pulse cadence | Weekly Market Pulse **plus** monthly climate layer |
| Architecture | Central VPSF engine + thin adapters |

### Follow-ups (explicit backlog — not this slice)

- Value heatmap + per-building-type toggles (reads same VPSF / floor-fit APIs)
- Progressive **land section** purchase (sideways expand more expensive each buy)
- New-player tutorial / dismissible tips + **account setting** to disable for future towers
- Casino mini-game; bowling arcade mini-game
- Hard floor bans; deep transport↔economy API beyond elev wait + existing metro mults

## 3. Architecture & money path

| Module | Role |
|--------|------|
| `VpsfCatalog` | Tables: family base $/cell, tier curves, class mults, upkeep ratios, non-living family bases |
| `EconomicBalancingManager` | Pure: `BuildCost`, `PeriodIncome` / sale, `PeriodUpkeep` from identity + cells + difficulty + climate + pulse + floor-fit |
| `MacroEconomicController` | Owns weekly pulse state; **reads** `MarketClimate`; exposes factors to balancer |
| `FloorValueRules` | Soft `FloorFit01` + warning reason string |
| `ClassClashRules` | Penalty when Upper near Lower / noisy service-leisure |
| `EconomySystem` / `BuildEconomy` / HUD formatters | Call balancer only for money |

**Flow:** Afford/place → `BuildCost`. Midnight → income/upkeep from balancer. Logistics wait minutes feed Upper stress only as float metrics into existing stress path.

**Living identity:** Map each room type id → `(EconomicFamily, Tier, TenantClass)`. `LuxuryBand` maps to Lower/Mid/Upper (Base→Mid).

## 4. Vertical zoning

**Bands (lobby floor = 0; edges in `FloorValueRules`, star-tunable):**

- **Street/podium** (~0–3): food, retail, leisure/amenities favored (street traffic + tower pop).
- **Mid** (~4…star-scaled mid-cap): flexible Mid living/work; shops OK but weaker street pull higher up.
- **High/prestige** (above mid-cap): Upper / high-tier Mid Office/Hotel/Condo favored; street-reliant F&B/retail/leisure soft-penalized.

`FloorFit01` ≈ 0.55…1.0 multiplies income (and may nudge fill). Warnings on catalog hover, placement ghost, and selected-room info when below threshold — never block place.

## 5. Matrix, class, pulse

**Upkeep ratios (of rent):** Lower ≈40%, Mid baseline, Upper ≈15%.  
**Density:** Lower higher pop/cell where applicable; Upper lower density / footprint bias.  
**Class clash:** Upper near Lower living/work or noisy service/leisure (same floor / adjacent) → stress/fill penalty (`ClassClashRules`, EditMode-tested).  
**Elev wait:** Upper stress if wait > **15 seconds** game time; Lower resilient; Mid between.  
**Weekly pulse:** ~every 7 game days; signed demand/value factor for living vs commercial; snapshot; stack with monthly climate (multiply/add with clamp).  
**Shops/services/leisure:** VPSF family tables (not 108); street/pop + floor-fit soft income.

## 6. Migration

- Stop reading SO money in `BuildEconomy`, `EconomySystem`, `RoomEconomyFormat`, `ShopVisitRules` spend bases, afford checks.
- Leave SO fields on assets for Unity compatibility; do not use them for sim money.
- Scaffold/elevator special cases go through balancer or explicit VPSF infrastructure family rows (no ad-hoc magic in place code beyond catalog constants inside `VpsfCatalog`).

## 7. Verification

### EditMode

- Golden VPSF outputs for several (family, tier, class, cells, difficulty) samples  
- Floor-fit mult + warning non-empty when bad  
- Class-clash penalty present/absent  
- Upper elev wait threshold 15s  
- Pulse advance + snapshot round-trip  
- Difficulty mult still applied  

### Manual

- Low shop vs high Upper condo: warning + income differ  
- Pulse drifts over a game week  
- Stars still gate unlocks  

## 8. Out of scope

See §2 follow-ups. No casino/bowling games. No value map UI this slice.
