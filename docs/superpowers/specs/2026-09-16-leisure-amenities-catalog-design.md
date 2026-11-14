# Build-A-Tower — Leisure Amenities Catalog

**Date:** 2026-09-16  
**Status:** Approved  
**Depends on:** Shop catalog + visit pipeline; pictorial build menu; dollhouse art pipeline; `RoomTypeSO.requiredStars`  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Amenities/attractions (this) → prestige venues → retail ladder → utility/ops → transit leftovers  

## 1. Goals

Add five leisure rooms — **Gym, Spa, Pool, Bowling Alley, Movie Theater** — as a new **Leisure** build family with matching dollhouse + menu art, sensible footprints and star gates, visit-based income, and a thin **AmenitySystem** for proximity comfort. Full systems (showtimes, memberships, lane booking) are deferred.

### Success criteria

In Play Mode a player can:

1. Place all five rooms from Build → **Leisure** when tower stars meet each gate.
2. See residents and street visitors visit leisure rooms (path → dwell → spend → midnight credit).
3. See hotel guests / condo residents near leisure rooms get a small once-per-day stress relief (spa strongest).
4. See hotels near spa/gym get a light occupancy / stay preference nudge.
5. See dollhouse overlays and square menu icons that match existing cutaway style and room cell sizes.
6. EditMode tests cover footprints, stars, visit eligibility, amenity radius math, and art mapping.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | All five rooms + thin AmenitySystem + art/HUD |
| Depth | Catalog + light amenity hooks (not full leisure sim) |
| Catalog | New `BuildFamily.Leisure` (not under Shops or Utility) |
| Category / income | `RoomCategory.Commercial`, `IncomeModel.TrafficVariable` |
| Visits | Same commercial trip / afford / dwell / midnight path as shops; leisure tagged by family |
| Light hooks | ±2 floors, ≤12 cells horizontal; once-per-day stress relief; spa/gym hotel nudge |
| Art | Whole-room magenta contact sheet → crop; warm architectural dollhouse; square menu icons |
| Stars | `requiredStars` build gates only — no new star promotion criteria |
| Out of scope | Showtimes, memberships, lane queues, staffing, prestige pack, retail ladder, garbage, escalators/metro |

## 3. Leisure catalog

| id | Display | ★ | Size | Slots | Pay cap | Build cost | Dwell (min) | Hours | Noise | Street weight |
|----|---------|---|------|-------|---------|------------|-------------|-------|-------|---------------|
| `leisure_gym` | Gym | 2 | 6×1 | 8 | $40 | $205,000 | 30–50 | 6:00–22:00 | 0.45 | 1 |
| `leisure_spa` | Spa | 5 | 6×1 | 6 | $90 | $310,000 | 45–75 | 10:00–21:00 | 0.25 | 1 |
| `leisure_pool` | Pool | 3 | 8×2 | 12 | $35 | $340,000 | 40–70 | 8:00–21:00 | 0.55 | 1 |
| `leisure_bowling` | Bowling Alley | 3 | 10×1 | 10 | $45 | $275,000 | 50–80 | 11:00–23:00 | 0.70 | 1.25 |
| `leisure_theater` | Movie Theater | 4 | 8×2 | 16 | $55 | $385,000 | 90–130 | 12:00–23:00 | 0.40 | 1.5 |

**Shared rules**

- `buildFamily = Leisure`; `baseIncome` = pay cap per visit.
- Basement allowed for gym, pool, bowling, theater; **spa above-ground only**.
- Multi-floor rooms (pool, theater): one `RoomInstance`; visits enter at `Origin` (bottom-left); upper floors visual/capacity/crime load only (same as department store).
- Broken rooms skip visits and amenity bonuses.

**Star ladder:** Gym (2) → Pool / Bowling (3) → Theater (4) → Spa (5).

## 4. Visits & AmenitySystem

### 4.1 Visits

- Extend commercial trip destination picking so Leisure rooms compete with shops when open, affordable, reachable, and not at slot capacity.
- Split traffic helpers: `IsTrafficVenue` = `IncomeModel.TrafficVariable`; `IsShop` = traffic venue **and** `ResolvedBuildFamily() == Shops`. Leisure must **not** consume Food/Retail `ShopDemandSystem` pools (bypass `CanServe` / `TryConsume` for Leisure, or always allow).
- Reuse dwell / spend / `ShopEarningsToday` midnight credit for all traffic venues.
- Selection HUD shows today’s visits / earnings like shops.
- Add leisure dwell ranges in `ShopVisitRules` (by `leisure_*` id).

### 4.2 AmenitySystem (thin)

- Evaluate for hotel guests and condo residents (assigned home room).
- Eligible amenity: Leisure family, not broken, within **±2 floors** and **horizontal distance ≤ 12 cells** of home origin (or home center — pick origin consistently and test).
- **Once per agent per day:** apply stress relief from the strongest in-range amenity:

| Amenity | Stress relief |
|---------|---------------|
| Spa | −5 |
| Pool | −4 |
| Gym | −3 |
| Theater | −3 |
| Bowling | −2 |

- **Hotel nudge:** if spa or gym in range, apply a small constant occupancy / stay preference bonus (table in plan; no new UI this pass).

### 4.3 Noise / crime

- Reuse commercial noise emission path; catalog noise values as above.
- Crime floor loads treat concurrent leisure visitors like shop visitors across occupied floors of the footprint.

## 5. Art, HUD, audio

### 5.1 Dollhouse

| Room id | Resource leaf | Brief |
|---------|---------------|-------|
| `leisure_gym` | `gym_6x1` | Weights, mats, mirrors; cool-neutral lighting |
| `leisure_spa` | `spa_6x1` | Loungers, soft lamps, calm finishes |
| `leisure_pool` | `pool_8x2` | Two-floor: deck + water volume |
| `leisure_bowling` | `bowling_10x1` | Lanes + seats + pins end |
| `leisure_theater` | `theater_8x2` | Lobby band + dark screen house |

Shared: side cutaway; dark structural frame; warm architectural materials (no toy-pink); magenta key plate only; no painted-in agents. Art review order: Gym → Spa → Bowling → Pool → Theater.

### 5.2 Menu / HUD

- Add `BuildFamily.Leisure` and family icon `family_leisure`.
- Square `Art/Menu/{id}` icons for each room; English tooltips secondary.
- Build strip: Leisure expands to five variants with ★ lock grey-out.

### 5.3 Audio

- Map via `SoundProfileMap` to nearest existing profiles this pass (e.g. spa/gym → softer beds; bowling/theater → busier). Dedicated ambience WAVs optional follow-up.

## 6. Architecture / files

| Area | Change |
|------|--------|
| `BuildFamily` | Add `Leisure` |
| `TowerHudController` / menu icons | Family strip + Leisure variants |
| `Assets/Resources/Rooms/` | Five new `RoomTypeSO` assets |
| Commercial trip picker | Include Leisure destinations |
| `AmenitySystem` (new) | Proximity stress relief + hotel nudge |
| `RoomDollhouseArt` | Five leaf mappings |
| `Art/Dollhouse/`, `Art/Menu/` | PNGs (+ `.bytes` where convention requires) |
| `SoundProfileMap` | Nearest-neighbor leisure mappings |
| EditMode tests | Catalog, visits, amenity math, art load |

## 7. Testing

- Catalog: size, `requiredStars`, family, basement flags, pay caps.
- Visits: leisure eligible alongside shops; afford / slots respected.
- AmenitySystem: radius/floor window; once-per-day; spa magnitude > bowling; broken rooms ignored.
- Art: `IsMapped` + Resources load for all five leaves and menu icons.

## 8. Out of scope

- Showtimes, memberships, lane capacity, leisure staffing  
- Atrium, chapel, casino, ballroom, dance club, arena  
- Retail specialty ladder; garbage/recycling/grounds; escalators; metro; above-ground parking  
- New ★ promotion criteria requiring leisure rooms  
- Dedicated leisure ambience WAVs (optional later)  
