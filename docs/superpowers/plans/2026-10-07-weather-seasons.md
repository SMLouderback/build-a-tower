# Weather & Seasons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a sim-owned weather/season system that throttles street traffic and shop spend, persists mid-storm, and presents sky tint, precip FX, seasonal parallax trees, and a HUD season tile.

**Architecture:** `WeatherSystem` owned by `TowerSimulation` (parallel to `MarketClimate`). Presenters read season/weather; `AgentSystem` applies multipliers. Optional `WeatherSnapshotV1` on `TowerSnapshotV1` (null = default Clear).

**Tech Stack:** Unity 6 C#, EditMode NUnit, Resources art (`.bytes` / PNG), existing IMGUI HUD.

**Spec:** `docs/superpowers/specs/2026-10-07-weather-seasons-design.md`

## Global Constraints

- Do not merge with `MarketClimate` (economy stays separate).
- Weights: Fair 75% / Cloudy 10% / Wet 10% / Severe 5%.
- Seasons: Dec–Feb Winter, Mar–May Spring, Jun–Aug Summer, Sep–Nov Fall; hard cuts.
- Wet→Rain/Snow and Severe→Thunderstorm/Blizzard by season.
- Thunderstorm 1–2h; blizzard rest-of-day; hangover: same-day cleanup then next-day 0.5× street.
- Street visitors hard; shop spend mild stack with climate spend mult.
- HUD: season square only; no weather caption.
- Trees: hard swap four plates; no crossfade.
- Placeholders OK for art behind stable resource paths.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Simulation/Season.cs` | Season enum + `FromMonth` |
| `Assets/Scripts/Simulation/WeatherKind.cs` | Weather kind enum |
| `Assets/Scripts/Simulation/WeatherSystem.cs` | Rolls, durations, multipliers, hangover, snapshot |
| `Assets/Scripts/Persistence/SaveContracts.cs` | `WeatherSnapshotV1` + field on `TowerSnapshotV1` |
| `Assets/Scripts/Persistence/TowerSnapshotMapper.cs` | Capture/restore weather |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Own, tick, expose `Weather` |
| `Assets/Scripts/Agents/AgentSystem.cs` | Street + spend multipliers |
| `Assets/Scripts/Rendering/DayNightSky.cs` | Weather tint helper |
| `Assets/Scripts/Rendering/WeatherFx.cs` | Rain/snow/thunder overlay |
| `Assets/Scripts/Rendering/ParallaxBackdrop.cs` | Season tree plate swap |
| `Assets/Scripts/UI/TowerHudController.cs` | Season HUD tile |
| `Assets/Resources/Art/Parallax/near_trees_*.bytes` | Seasonal tree plates |
| `Assets/Resources/Art/Hud/season_*.bytes` | Season HUD icons |
| `Assets/Tests/EditMode/WeatherSystemTests.cs` | Core sim tests |
| `Assets/Tests/EditMode/WeatherSnapshotTests.cs` | Persistence tests |

---

### Task 1: Season + WeatherKind + WeatherSystem core (TDD)

**Files:**
- Create: `Assets/Scripts/Simulation/Season.cs`
- Create: `Assets/Scripts/Simulation/WeatherKind.cs`
- Create: `Assets/Scripts/Simulation/WeatherSystem.cs`
- Create: `Assets/Tests/EditMode/WeatherSystemTests.cs`

**Interfaces:**
- `Season SeasonUtil.FromMonth(int month)` — month 1–12
- `WeatherSystem` ctor with optional `Random`
- `void AdvanceTo(int dayIndex, int minuteOfDay)` — advance/roll segments
- `Season CurrentSeason { get; }`
- `WeatherKind Kind { get; }`
- `float StreetTrafficMultiplier { get; }`
- `float ShopSpendMultiplier { get; }`
- Constants for weights and multiplier table per spec §3.4–3.6

- [ ] **Step 1: Write failing tests** — all 12 months→season; 5000 rolls ≈75/10/10/5; winter wet=Snow severe=Blizzard; non-winter Rain/Thunderstorm; thunder duration 60–120; blizzard ends at midnight; next-day hangover street 0.5; fair Clear/PartlyCloudy same street 1.0.
- [ ] **Step 2: Run EditMode WeatherSystemTests — expect fail.**
- [ ] **Step 3: Implement enums + `WeatherSystem` per spec (no Unity scene deps).**
- [ ] **Step 4: Run — expect pass.**
- [ ] **Step 5: Commit** `feat: add WeatherSystem season rolls and multipliers`

---

### Task 2: Wire TowerSimulation tick + AgentSystem multipliers

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Extend: `Assets/Tests/EditMode/WeatherSystemTests.cs` (or `WeatherGameplayTests.cs`) for multiplier application helpers if pure; otherwise integration-style unit test of spawn chance formula with injected weather.

**Behavior:**
- Create `_weather` alongside `_climate`; `public WeatherSystem Weather => _weather`.
- Each sim tick after clock advance: `_weather.AdvanceTo(clock.DayIndex, clock.MinuteOfDay)`.
- `UpdateStreetTraffic`: `chance *= weather.StreetTrafficMultiplier`; skip spawn if ≤0.
- Street disposable roll: multiply by `weather.ShopSpendMultiplier` in addition to climate spend.

- [ ] **Step 1: Failing test** that documents street chance uses weather mult (extract pure helper if needed: `StreetSpawnChance(stars, atriumMult, weatherMult)`).
- [ ] **Step 2: Wire simulation + agent system.
- [ ] **Step 3: Tests pass; commit** `feat: apply weather multipliers to street traffic and shop spend`

---

### Task 3: Snapshot persistence

**Files:**
- Modify: `Assets/Scripts/Persistence/SaveContracts.cs` — add `WeatherSnapshotV1` + `public WeatherSnapshotV1 weather;` on `TowerSnapshotV1` (nullable/optional; old saves null → Clear)
- Modify: `Assets/Scripts/Persistence/TowerSnapshotMapper.cs`
- Modify: `TowerSimulation` restore path to call `_weather.RestoreSnapshot`
- Create: `Assets/Tests/EditMode/WeatherSnapshotTests.cs`

**WeatherSnapshotV1 fields:**
- `string kind` (enum name)
- `int segmentEndDayIndex`
- `int segmentEndMinuteOfDay`
- `string hangover` (`None` / `BlizzardSameDayCleanup` / `BlizzardNextDayHalf`)
- `int hangoverEndDayIndex`

- [ ] **Step 1: Failing round-trip test** mid-thunderstorm and mid-blizzard hangover.
- [ ] **Step 2: Implement capture/restore; null weather on old saves defaults to Clear fair segment.**
- [ ] **Step 3: Pass; commit** `feat: persist weather state in tower snapshots`

---

### Task 4: Sky tint + WeatherFx

**Files:**
- Modify: `Assets/Scripts/Rendering/DayNightSky.cs` — add `Color ApplyWeather(Color baseColor, WeatherKind kind, float lightningPulse01 = 0)`
- Create: `Assets/Scripts/Rendering/WeatherFx.cs` — ensure-in-scene component; reads `TowerSimulation.Weather`; drives simple particle/line rain/snow + lightning pulse
- Modify: sky update call site (wherever `DayNightSky.ColorAt` sets camera background) to pass weather
- Create/extend tests for tint identity on Clear and darkened on Rain (pure color math)

- [ ] **Step 1: Failing tint tests.**
- [ ] **Step 2: Implement tint + FX placeholders.**
- [ ] **Step 3: Pass; commit** `feat: weather sky tint and precip FX overlay`

---

### Task 5: Seasonal parallax trees

**Files:**
- Modify: `Assets/Scripts/Rendering/ParallaxBackdrop.cs` — swap trees resource by season:  
  `Art/Parallax/near_trees` (summer / fallback),  
  `Art/Parallax/near_trees_winter`,  
  `Art/Parallax/near_trees_spring`,  
  `Art/Parallax/near_trees_fall`
- Add placeholder `.bytes`/PNG (copy summer plate tinted or silhouetted as interim if art not ready)
- Poll/bind when season changes

- [ ] **Step 1: Document resource keys; failing test optional if sprite load hard in EditMode — prefer PlayMode smoke or pure path-selection unit test `TreeResourceFor(Season)`.**
- [ ] **Step 2: Implement hard swap + placeholders.**
- [ ] **Step 3: Commit** `feat: seasonal parallax tree plate swap`

---

### Task 6: HUD season tile + art placeholders

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` — draw ~36px season square near clock/date
- Resources: `Art/Hud/season_spring`, `season_summer`, `season_fall`, `season_winter` (`.bytes`)
- Load via small helper or `Resources.Load` / TextAsset pattern used by menu icons

- [ ] **Step 1: Wire draw using `simulation.Weather.CurrentSeason` (or FromMonth from clock).**
- [ ] **Step 2: Add four placeholder icons.**
- [ ] **Step 3: Commit** `feat: add season HUD tile`

---

### Task 7: Art polish pass (optional same PR or follow-up)

**Files:** authored tree plates + season icons + FX sheets replacing placeholders.

- [ ] **Step 1: Drop final art into resource paths; verify Play Mode seasons/weather.**
- [ ] **Step 2: Commit** `art: seasonal trees HUD icons and weather FX`

---

## Spec coverage checklist

| Spec section | Task |
|--------------|------|
| §3 Core model / weights / durations / hangover | Task 1 |
| §4 Street + shop spend | Task 2 |
| §6 Persistence | Task 3 |
| §5.1–5.2 Sky + FX | Task 4 |
| §5.3 Trees | Task 5 |
| §5.4 HUD | Task 6 |
| §9 Art polish | Task 7 |
