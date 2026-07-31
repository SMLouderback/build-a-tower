# Build-A-Tower — 3★ Stars, Ops Rooms & Service Workers

**Date:** 2026-07-31  
**Status:** Approved  
**Depends on:** StarSystem 0–2; agents + transit; economy midnight; commercial visits; disposable income / climate (optional)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → **higher stars** → more transit → evaluation/heatmaps → polish

## 1. Goals

Raise the star cap to **3★** with a focused unlock pack, and add a **visible ops loop**: room **condition** decay, **broken** rooms that must be rebuilt, **hotel dirtiness** after checkout, and **maid / handyman agents** hired from Housekeeping / Maintenance rooms.

### Success criteria

In Play Mode a player can:

1. Earn **3★** with population, stress, lobby, elevator, **Security**, **Housekeeping**, and **Maintenance**.
2. Build **Housekeeping** and **Maintenance** at **2★** (auto-hire 1 worker); build **Security**, **Research Lab**, **Conference**, **Fine Dining** at **3★**.
3. See hotel rooms go **Dirty** on checkout and stay unrentable until a **maid** finishes cleaning (15 / 30 game minutes).
4. See eligible rooms lose **1 condition / day**; handymen restore **+10 per game hour**; at **0** the room shows **broken** and must be bulldozed (no handyman repair).
5. Pay daily wages for hired maids/handymen at midnight; see condition / dirty / staff on Selection.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | **One combined slice**: 3★ + unlock pack + condition + maids/handymen |
| Unlock pack | Fine Dining, Conference, Security, Research Lab (3★); Housekeeping, Maintenance (2★) |
| 3★ facilities | Security + Housekeeping + Maintenance (plus lobby + elevator) |
| 3★ pop / stress | ≥ **60** pop, ≤ **20** avg stress |
| Ops workers | **Visible agents** pathfinding (not formula-only) |
| HK/Maint on place | **Auto-hire 1** worker; Selection can set **0–4** |
| Wages | Maid **$200/day**, Handyman **$300/day** per hired worker |
| Clean times | Basic hotel **15** min; Premium hotel **30** min |
| Repair | **+10** condition per **60** game minutes of handyman work |
| Pressure | Dirty blocks hotel check-in; condition **&lt;70** stress/eval; **&lt;40** pauses income; **0** broken → bulldoze only |
| Non-degrading | Lobby, elevators, stairs |
| Architecture | StarSystem 3★ + room SOs + RoomInstance condition/dirty/broken + service agents/jobs + economy wages |

## 3. Stars

### 3.1 Cap

- `StarSystem.MaxStars = 3` (update HUD `Stars: x/3` and `ForceStars` clamp).

### 3.2 Keep 1★ / 2★

Unchanged from current implementation:

| Tier | Population | Max avg stress | Facilities |
|------|------------|----------------|------------|
| 1★ | ≥ 10 | ≤ 40 | Lobby |
| 2★ | ≥ 30 | ≤ 25 | Lobby + ≥1 elevator |

### 3.3 Earn / keep 3★

| Metric | Threshold |
|--------|-----------|
| Population | ≥ **60** |
| Average stress | ≤ **20** |
| Facilities | Lobby + ≥1 elevator + ≥1 **Security** + ≥1 **Housekeeping** + ≥1 **Maintenance** |

Facility rooms that are **Broken** do **not** count toward the 3★ facility gate until rebuilt.

### 3.4 Cadence

- Promotion: `TryPromote` as today (no demotion on promote path).
- Demotion: quarterly `EvaluateQuarterly` (90 game days) as today.
- `FormatNextStarGoal` lists 3★ pop/stress + Security / Housekeeping / Maintenance checks.

## 4. Room catalog

### 4.1 New placeables

| Id (suggested) | Display | Unlock | Family | Notes |
|----------------|---------|--------|--------|-------|
| `service_housekeeping` | Housekeeping | **2★** | Utility | Staff 0–4 maids; auto 1 on place |
| `service_maintenance` | Maintenance | **2★** | Utility | Staff 0–4 handymen; auto 1 on place |
| `service_security` | Security Post | **3★** | Utility | Facility gate only this slice |
| `service_research` | Research Lab | **3★** | Utility | Placeable; upgrades out of scope |
| `service_conference` | Conference Room | **3★** | Utility (or Office service) | Placeable; light/no income MVP OK |
| `shop_food_fine` | Fine Dining | **3★** | Shops → Food | `TrafficVariable`; higher $/visit than Restaurant (e.g. **$200**) |

Sizes / costs: first-pass tuneables on SOs (suggest 2–4 wide utility rooms; Fine Dining similar to Restaurant).

### 4.2 Existing

- Premium Office / Hotel / Condo remain **2★**.
- Basic living / shops / stairs remain **0★**; Elevator **1★**.

### 4.3 HUD catalog

Wire new SOs into Resources + Build catalog / `TowerHudController` nested buttons; grey-out by `requiredStars`.

## 5. Condition, dirty, broken

### 5.1 Condition

- Field on `RoomInstance`: `Condition` int **0–100**, default **100** on place.
- Midnight: for each room that **can degrade**, `Condition = max(0, Condition - 1)`.
- **Does not degrade:** lobby, elevator shafts, stairs.
- **Does degrade:** living, shops, utility (including HK/Maint/Security/Research/Conference), hotels, offices, condos, etc.

### 5.2 Thresholds

| Condition | Effect |
|-----------|--------|
| ≥ 70 | Normal |
| 40–69 | Soft: raise stress and/or lower evaluation for agents tied to that room |
| 1–39 | Income **paused** for that room (rent/nightly midnight; shop day earnings not paid / not accepted—prefer: no economic payout; visits may still be blocked if broken only) |
| 0 | **Broken** (see below) |

Condo already-sold stays sold; paused means no further recurring payouts from that unit.

### 5.3 Broken

- At **0**, set `Broken = true` (or derive from Condition == 0).
- Visual: distinct broken tint / overlay on room tiles (darker desaturated).
- Effects: no income; no new occupants / hotel check-in / office-condo sync into that room; handymen **skip** the room.
- Recovery: **bulldoze + rebuild only** (grace refund rules unchanged).

### 5.4 Hotel dirty

- On hotel guest **checkout**, set `Dirty = true` on that hotel room.
- While Dirty: **no new check-in** / no new hotel agent assignment to that room.
- Maid job clears `Dirty` after dwell work time:
  - Basic hotel (`requiredStars < 2` / non-premium): **15** game minutes
  - Premium hotel: **30** game minutes
- Dirty is independent of Condition (a cleaned room can still be low condition).

### 5.5 Selection

Show Condition, Dirty, Broken; for HK/Maint show hired staff count with stepper 0–4.

## 6. Service workers (visible agents)

### 6.1 Hiring

- Placing Housekeeping or Maintenance sets **StaffedWorkers = 1**.
- Selection adjusts **0–4**; changing count spawns/despawns service agents based at that room.
- Midnight wages (tower-wide sum):  
  `maidsHired * 200 + handymenHired * 300`  
  Debited in `EconomySystem.OnNewDay` as expense.

### 6.2 Roles

| Role | Home | Job selection | Work |
|------|------|---------------|------|
| Maid | Housekeeping room | Oldest Dirty hotel with path | Travel → work 15/30 min → clear Dirty → next or idle home |
| Handyman | Maintenance room | Lowest Condition among rooms with 1≤Condition≤99, not Broken, degradable type | Travel → work 60 min → Condition += 10 (cap 100) → next or idle |

- Concurrent active jobs ≤ hired workers of that type.
- Use existing `TransitRouter` / elevator waiting like other agents.
- Distinct agent colors in `AgentView`.
- Service agents excluded from star **population** (like street visitors) — they are staff, not residents.

### 6.3 Sync with living agents

- Hotel update / `SyncHomes`: skip Dirty and Broken hotel rooms for new guests.
- Office/condo sync: skip Broken rooms; optionally skip Condition &lt; 40 for new fills (recommended: skip Broken only for offices/condos; low condition still occupied but stressed).

## 7. Systems / files (expected)

| Area | Change |
|------|--------|
| `StarSystem.cs` | MaxStars 3; 3★ criteria; goals text; facility helpers |
| New `RoomTypeSO` assets + Resources | Six new rooms |
| `BuildCatalog` / HUD | Nest Utility + Fine Dining; staff UI |
| `RoomInstance.cs` | Condition, Dirty, Broken; staffed count on service rooms |
| `TilemapTowerView` / paint | Dirty/broken tints |
| `AgentEnums` / `Agent` / `AgentSystem` | Maid, Handyman phases/jobs |
| `EconomySystem` | Wages; income pause when Condition &lt; 40 or Broken |
| `AgentSystem` hotel checkout | Set Dirty |
| Tests | 3★ gate; dirty clean timing; decay; +10 repair; broken no repair; auto-hire 1; wages |
| README | 3★ goals; ops loop |

## 8. Out of scope

- Stars **4–5** and their unlock lists  
- Research **upgrades** (lab is placeable only)  
- Security gameplay beyond facility presence  
- Conference meeting simulation / Fine Dining special AI beyond shop visits  
- Formula-only cleaning without agents  
- Condition UI heatmaps (later evaluation slice)

## 9. Verification

- EditMode: MaxStars 3; MeetsCriteria(3) requires Security+HK+Maint; Broken facility fails gate.  
- EditMode: checkout sets Dirty; maid clear after 15/30; check-in blocked while Dirty.  
- EditMode: midnight −1 condition; handyman +10 after 60 min; Condition 0 → Broken; handyman ignores Broken.  
- EditMode: place HK → StaffedWorkers == 1; wage expense counts hired.  
- Play Mode: visible maids/handymen; broken tint; 3★ goals in HUD.

## 10. Roadmap note

This is the first **higher stars** slice (3★) plus the ops backbone for later 4–5★ content and evaluation/heatmaps.
