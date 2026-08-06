# Demand/Climate Graph & Tower Heatmaps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Maps top-bar mode with climate/demand Graph plus per-cell Crime, Noise, Traffic, and Economic heatmaps (with Traffic Today/30-day and Economic Profit/Demand/Blend toggles).

**Architecture:** `TowerMapAnalytics` owns rolling daily samples and current 0–1 cell scores. Agent/transit/crime/economy systems feed samples. `TilemapTowerView` paints a heatmap Tilemap. `TowerHudController` owns Maps UI + graph IMGUI panel. Rebuild scores on an interval and at midnight.

**Tech Stack:** Unity 6000.4.x, existing Tilemaps, IMGUI HUD, NUnit EditMode / net8 hosts.

**Spec:** `docs/superpowers/specs/2026-08-05-demand-climate-heatmaps-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Prefer SDD; inline if quota exhausted
- No parallel-cli
- One Maps mode at a time; do not retune economy/star balance for maps
- Future amenity noise = hooks only

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Maps/TowerMapMode.cs` | Off/Graph/Crime/Noise/Traffic/Economic |
| `Assets/Scripts/Maps/EconomicMapView.cs` | Profit/Demand/Blend |
| `Assets/Scripts/Maps/TrafficMapWindow.cs` | Today / Avg30 |
| `Assets/Scripts/Maps/TowerMapAnalytics.cs` | Samples + score maps |
| `Assets/Scripts/Maps/NoiseEmitterWeights.cs` | Category/id noise weights |
| `Assets/Scripts/Rendering/TilemapTowerView.cs` | Heatmap tilemap paint/clear |
| `Assets/Scripts/UI/TowerHudController.cs` | Maps dropdown, toggles, graph, legend |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Tick analytics / midnight archive |
| `Assets/Scripts/Agents/AgentSystem.cs` (minimal hooks) | Optional traversal/wait recording API |
| `Assets/Scenes/TowerSandbox.unity` | Heatmap Tilemap child if needed |
| Tests under `Assets/Tests/EditMode/` | Score helpers |
| README + spec Implemented |

---

### Task 1: Enums + analytics skeleton + blend/normalize tests

**Files:**
- Create: `TowerMapMode.cs`, `EconomicMapView.cs`, `TrafficMapWindow.cs`, `TowerMapAnalytics.cs` (+ metas)
- Create: `Assets/Tests/EditMode/TowerMapAnalyticsTests.cs`

**Interfaces:**
- `TowerMapAnalytics.SetScore(mapKind, cell, float01)` / `GetScore(...)`
- `TowerMapAnalytics.Blend(profit, demand, wProfit=0.5f)`
- `TowerMapAnalytics.Clamp01`
- Ring buffer length **30** for traffic daily maps; climate history **90**

- [x] **Step 1: Failing tests** — Blend 0.5/0.5; Clamp; empty GetScore=0
- [x] **Step 2: Implement skeleton**
- [x] **Step 3: PASS**
- [ ] **Step 4: Commit** (only if asked)

---

### Task 2: Traffic scores + capacity stress

**Files:**
- Extend `TowerMapAnalytics` with `RecordTraversal(cell)`, `RecordWait(cell, weight)`, `ArchiveTrafficDay()`, `RebuildTraffic(TrafficMapWindow)`
- `TrafficCapacity.StressForShaft(occupancy, capacity, researchMult) -> float` (testable static)
- Hook from agent path steps / elevator wait if accessible; else approximate from elevator queue counts + stair occupancy each tick

- [x] **Step 1: Tests** — capacity stress rises near full; research mult lowers stress
- [x] **Step 2: Implement rebuild Today vs Avg30**
- [x] **Step 3: Wire sampling from simulation tick** (best-effort hooks)
- [ ] **Step 4: Commit** (only if asked)

---

### Task 3: Crime scores

**Files:**
- `RebuildCrime(grid, trafficScores, criminals, patrolCoverage, eventBoostCells)`
- Formula: `clamp01(traffic*0.45 + criminal*0.4 + event*0.25 - patrol*0.35)`

- [x] **Step 1: Tests** — criminal nearby raises; patrol lowers
- [x] **Step 2: Implement + wire**
- [ ] **Step 3: Commit** (only if asked)

---

### Task 4: Noise scores + night residential boost

**Files:**
- `NoiseEmitterWeights.Emit(roomType, occupied, crimeActive, minuteOfDay) -> float`
- `RebuildNoise(..., traffic, isNight)` — residential bother *= night factor

- [x] **Step 1: Tests** — shop > lobby emit; hotel cell hotter at night than day for same emit
- [x] **Step 2: Implement**
- [ ] **Step 3: Commit** (only if asked)

---

### Task 5: Economic Profit / Demand / Blend

**Files:**
- Rebuild from `EconomySystem` last room income/expense + vacancy/overprice proxies on room cells

- [x] **Step 1: Tests** — Blend midpoint; profit-only ignores demand
- [x] **Step 2: Implement three views**
- [ ] **Step 3: Commit** (only if asked)

---

### Task 6: Heatmap Tilemap painting

**Files:**
- `TilemapTowerView`: `heatmapTilemap` serialize; `PaintHeatmap(IEnumerable<(cell,score)>)`, `ClearHeatmap()`
- Color ramp cool→hot via existing colored tile factory
- Scene: add Heatmap Tilemap under Grid (sorting above rooms)

- [x] **Step 1: Paint/clear API**
- [x] **Step 2: Scene wiring** (Editor or YAML)
- [ ] **Step 3: Commit** (only if asked)

---

### Task 7: HUD Maps + Graph panel

**Files:**
- `TowerHudController`: Maps button/dropdown; mode state; Traffic/Economic toggles; legend; graph panel (climate history + spend mult + demand proxy sparklines)
- `TowerSimulation` / climate: append midnight history samples into analytics

- [x] **Step 1: Maps UI**
- [x] **Step 2: Graph history**
- [x] **Step 3: Manual smoke checklist in report**
- [ ] **Step 4: Commit** (only if asked)

---

### Task 8: README + spec Implemented

- [x] Document Maps controls
- [x] Spec → Implemented

---

## Execution

Start SDD immediately after plan save. Inline if subagents unavailable.
