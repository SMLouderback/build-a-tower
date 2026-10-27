# Build-A-Tower — Weather & Seasons

**Date:** 2026-10-07  
**Status:** Approved (brainstorming)  
**Depends on:** `GameClock` calendar; `DayNightSky`; `ParallaxBackdrop` tree strip; `AgentSystem` street visitors; tower snapshot save/load  
**Engine target:** Unity (2D), desktop/Editor-first  
**Related (not this):** `MarketClimate` remains **economic** climate only — do not merge with weather

## 1. Goals

Add a sim-owned weather/season system that:

1. Drives street-visitor traffic and mildly adjusts shop spend.
2. Presents season + weather via sky tint, precip/thunder FX, seasonal parallax trees, and a small HUD season tile.
3. Persists across save/load mid-storm.

### Success criteria

In Play Mode a player can:

1. See the season HUD square match the calendar season.
2. See parallax trees hard-swap on season change (winter bare, spring flowers, summer current, fall mixed warm leaves).
3. See sky/FX change for cloudy, rain/snow, thunderstorm, blizzard.
4. Observe street traffic drop under wet weather, hit zero in severe weather, and follow blizzard hangover rules.
5. Reload a save mid-storm and keep the same weather segment / hangover state.
6. EditMode tests cover season mapping, weight buckets, precip kind by season, durations, hangover multipliers, and snapshot round-trip.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Architecture | Approach 1 — `WeatherSystem` + thin presenters |
| Scope | Full package: sim multipliers + sky tint + FX + seasonal trees + season HUD tile |
| Traffic effect | Street visitors hard; mild same-day shop spend / walk-in disposable hit |
| Seasons | Classic N. Hemisphere: Spr Mar–May, Sum Jun–Aug, Fall Sep–Nov, Win Dec–Feb |
| Season blend | None — hard cut on month boundaries |
| HUD | One small season square (static image per season); no weather text label |
| Wet precip | Rain outside winter; snow in winter |
| Severe | Thunderstorm outside winter; blizzard in winter |
| Fair weather | ~75% Clear / PartlyCloudy (max street traffic) |
| Cloudy | ~10% (slight street reduction) |
| Wet | ~10% (drastic street drop) |
| Severe | ~5% (street traffic 0) |
| Cadence | Reevaluate every ~2–4 game hours (rolled) |
| Thunderstorm length | 1–2 game hours |
| Blizzard length | Usually rest of calendar day |
| Blizzard hangover | Remainder of blizzard day crushed; next day ~0.5× street; then normal |
| Out of scope v1 | Seasonal grass/skyline; news ticker; lightning damage; umbrella cosmetics; fade blends; condo/office vacancy weather |

## 3. Core model

### 3.1 Ownership

`TowerSimulation` owns `WeatherSystem` (parallel to `MarketClimate`). Advanced from `GameClock` time advancement. Presenters and `AgentSystem` **read** multipliers; they do not roll weather.

### 3.2 Season

```
Season FromMonth(int month) // 1–12
  Dec–Feb → Winter
  Mar–May → Spring
  Jun–Aug → Summer
  Sep–Nov → Fall
```

Derived from `GameClock.CalendarDate.Month`. No stored season required except for convenience in snapshots.

### 3.3 Weather kinds

| Kind | Notes |
|------|-------|
| `Clear` | Fair — full street traffic |
| `PartlyCloudy` | Fair bucket sibling — same traffic as Clear; slightly softer sky |
| `Cloudy` | Overcast |
| `Rain` | Wet, non-winter |
| `Snow` | Wet, winter |
| `Thunderstorm` | Severe, non-winter |
| `Blizzard` | Severe, winter |

Fair bucket for weighting = `Clear` + `PartlyCloudy` combined (~75%). When fair is rolled, split ~60/40 Clear vs PartlyCloudy (tunable constant).

### 3.4 Weighting (independent of season for bucket; kind maps by season)

| Bucket | Weight | Maps to |
|--------|-------:|---------|
| Fair | 0.75 | Clear / PartlyCloudy |
| Cloudy | 0.10 | Cloudy |
| Wet | 0.10 | Rain or Snow |
| Severe | 0.05 | Thunderstorm or Blizzard |

### 3.5 Cadence & duration

- Default segment length: uniform random **2–4 game hours**.
- If new roll is `Thunderstorm`: duration clamped to **1–2 game hours**.
- If new roll is `Blizzard`: segment ends at **next local midnight** (rest of day), unless already near midnight then allow a short minimum (≥1 hour) so the event is visible.
- While a segment is active, do not re-roll until `segmentEnd` (absolute game minute or dayIndex+minute).
- After blizzard ends: enter hangover state for **the next calendar day** at street mult `0.5`, then clear.

### 3.6 Multipliers

| Condition | StreetTrafficMultiplier | ShopSpendMultiplier |
|-----------|------------------------:|--------------------:|
| Clear / PartlyCloudy | 1.00 | 1.00 |
| Cloudy | 0.85 | 0.95 |
| Rain / Snow | 0.35 | 0.80 |
| Thunderstorm / Blizzard (active) | 0.00 | 0.60 |
| Blizzard hangover (next day) | 0.50 | 0.90 |
| Blizzard remainder-of-day after storm ends same day | 0.15 | 0.70 |

Street `0` means skip street visitor spawns entirely.

Shop spend multiplier stacks with existing `_climate?.SpendMultiplier` (multiply).

## 4. Gameplay hooks

### 4.1 Street visitors

In `AgentSystem.UpdateStreetTraffic`, multiply spawn chance by `weather.StreetTrafficMultiplier`. If multiplier ≤ 0, continue without spawning.

### 4.2 Shop spend

Where street-origin visitors roll disposable income / spend (path already using economic `SpendMultiplier`), also multiply by `weather.ShopSpendMultiplier`.

### 4.3 Explicit non-hooks (v1)

- Hotel / office / condo vacancy fill
- Delivery / curb vehicles
- Indoor leisure dwell schedules

## 5. Presentation

### 5.1 Sky

`DayNightSky` remains the time-of-day base. Weather applies a multiply/lerp overlay:

- Clear — identity  
- PartlyCloudy — slight desat / soft grey  
- Cloudy — flatter grey-blue  
- Rain/Snow — darker, desaturated  
- Thunderstorm/Blizzard — heavy grey; thunderstorm may pulse brief flash toward white for lightning beats  

### 5.2 FX overlay

Camera-aligned (or screen-space) overlay, not part of parallax:

- Rain streaks for `Rain` / `Thunderstorm` (heavier for thunder)
- Snow flakes for `Snow` / `Blizzard` (denser for blizzard)
- Occasional lightning flash for `Thunderstorm`

Placeholder particles acceptable first; swap to authored sheets without API change.

### 5.3 Parallax trees

Hard-swap tree strip resource by season (no crossfade):

| Season | Tree plate |
|--------|------------|
| Winter | Bare branches |
| Spring | Foliage + flowers |
| Summer | Current summer trees |
| Fall | Mixed orange / yellow / brown / red |

`ParallaxBackdrop` listens to season changes (or polls on day/month) and rebinds the trees strip sprite(s).

### 5.4 HUD

One small top-bar **season square** (four static icons). Calendar date + precip FX + trees communicate weather; no separate weather caption required in v1.

## 6. Persistence

Add `WeatherSnapshotV1` (or fields on tower snapshot) including:

- Current weather kind  
- Segment end (`dayIndex`, `minuteOfDay`)  
- Hangover: enum/flags (`None`, `BlizzardSameDayCleanup`, `BlizzardNextDayHalf`) + hangover end day  
- Optional: last roll seed not required if state is fully explicit  

Restore must re-derive multipliers from restored state without immediately re-rolling.

## 7. File / type sketch

| Unit | Responsibility |
|------|----------------|
| `WeatherSystem` | Season, rolls, durations, multipliers, hangover, snapshot |
| `WeatherKind` / `Season` enums | Shared vocabulary |
| `WeatherSnapshotV1` | Save DTO |
| `WeatherFx` (MonoBehaviour or static driver) | Particles + lightning flash |
| `DayNightSky` (extend) | Weather tint helper |
| `ParallaxBackdrop` (extend) | Season tree plate swap |
| `TowerHudController` (extend) | Season tile |
| `AgentSystem` (extend) | Apply street + spend multipliers |
| `TowerSimulation` / `TowerSnapshotMapper` | Own, tick, persist |

## 8. Tests

EditMode:

1. `SeasonFromMonth` table for all 12 months.  
2. Weighted bucket frequencies over N≥5000 rolls ≈ 75/10/10/5 (± tolerance).  
3. Wet/severe map to rain/thunder vs snow/blizzard by season.  
4. Thunderstorm duration ∈ [60, 120] minutes.  
5. Blizzard ends at midnight boundary; hangover next-day street = 0.5.  
6. Snapshot round-trip preserves kind, segment end, hangover.  
7. Street spawn chance respects multiplier 0 (no spawn attempts succeed when forced).

## 9. Art deliverables

| Asset | Notes |
|-------|-------|
| 4× season HUD icons | Square, readable at ~32–48px |
| 4× tree parallax plates | Match current tree strip dimensions/style |
| Rain / snow / thunder FX | Particles or sprite sheets |

Placeholders may ship behind the API; replace without changing sim contracts.
