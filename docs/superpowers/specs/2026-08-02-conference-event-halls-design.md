# Build-A-Tower — Conference / Event Halls, 4★ & Tower News

**Date:** 2026-08-02  
**Status:** Implemented (Play Mode pending) — plan (`docs/superpowers/plans/2026-08-02-conference-event-halls.md`)  
**Depends on:** Stars3 ops (Conference placeholder); Security + crime; hotel dirty/check-in; offices + agents; elevators; shops / street visitors; HUD top bar  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Deeper economy → higher stars → more transit → **venue economy + news** → evaluation/heatmaps → polish  
**Prior slice:** Hybrid stairs + elevator (`docs/superpowers/specs/2026-08-02-hybrid-stairs-elevator-design.md`)

## 1. Goals

Turn the placeholder **Conference Room** into a real venue economy: **daily meetings** driven by **office workers**, and **major multi-day events** (Comic-con style) in large **Event Halls** that flood the tower with foot traffic. Raise the earnable star cap to **4★**. Add a **tower news** surface (event banner + scrolling ticker) for majors and ops alerts.

### Success criteria

In Play Mode a player can:

1. Unlock / place **Conference** at **3★** and see **daily meeting income** scale with **office worker population**.
2. Earn **4★** (higher pop + tighter stress + **staffed Security**) and place an **Event Hall**.
3. Experience a **major event** that books Event Hall(s), pays **hotel guests × stars × hall capacity**, and **pauses daily income only on booked halls**.
4. See **EventVisitor** agents (day crowd + hotel-backed fraction) stress elevators, shops, crime, and hotels.
5. Get **banner** alerts for upcoming / live / ending majors, and a **ticker** with events, serious ops, and quirky lines.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Approach | **Full event sim** (attendees as agents) + `ConferenceSystem` + `TowerNews` |
| Daily demand | **Office worker population** |
| Daily venues | **Conference** halls (Event Halls idle for daily unless later tuned) |
| Major payout | **Hotel guest pop × stars × booked hall capacity** (+ event mult / climate nudge) |
| During major | Daily paused **only on halls used by the event** (other Conferences keep daily) |
| Attendees | **Hybrid**: day visitors + fraction book vacant hotels |
| Halls | **Two sizes**: Conference @ **3★**, Event Hall @ **4★** |
| Earnable stars | **`MaxStars = 4`** (HUD slots remain 5) |
| 4★ gate | Higher pop + tighter stress **and** ≥1 **staffed Security Post** |
| News | **Banner + scrolling ticker** (events + serious + quirks) |
| Hall staffing | **No** staff hire on Conference/Event Hall MVP |
| Event Hall hours | **8:00–22:00** visitor / open window |
| Post-event ops | Event Hall queues **360 maid-minutes** (2×3h) + **1 handyman × 3h**; maids work a shared clean pool in ≤30m shifts (hotels prioritized) |
| Post-meeting ops | Conference queues **30 maid-minutes** after a day with meeting income |

## 3. Rooms

| Asset | Id | Unlock | Size (target) | Notes |
|-------|-----|--------|---------------|-------|
| Conference | `service_conference` | **3★** | enlarge from 4×1 → ~**8×1** (tunable) | Daily meetings; capacity field |
| Event Hall | `service_event_hall` (new) | **4★** | ~**12×2** | Majors prefer/require; higher capacity; higher build cost |

- Category / family: Service + Utility (same pattern as Research/Security).  
- `baseIncome` stays 0 on the SO; payouts come from `ConferenceSystem` via wallet credits.  
- **Capacity**: integer on SO or derived from footprint; used for daily share + major payout + attendee caps.  
- Broken / unusable halls skip income and booking.

## 4. Stars (4★)

- `StarSystem.MaxStars = 4`.  
- **4★** `MeetsCriteria`: lobby; `population ≥ FourStarPopulation` (proposed **100**); `averageStress ≤ FourStarMaxStress` (proposed **15**); **and** at least one Security Post with `StaffedWorkers ≥ 1` and not broken.  
- Keep 1–3★ rules (elevator @2, HK+Maint @3, existing pop/stress).  
- Goals HUD / `FormatNextStarGoal` must describe 4★.  
- Unlock Event Hall via `requiredStars = 4`.

## 5. Daily meetings

- Each **midnight** (alongside rent): for each Conference that is **not** booked by an active major:  
  `payout += MeetingRate(officeWorkerPop, hall.Capacity, CurrentStars, climate)`.  
- Office worker pop = count of `AgentRole.OfficeWorker` agents that exist for the tower (same population notion used elsewhere, or in-tower only — prefer **assigned office agents**, not Outside-only).  
- Demand is shared across eligible Conferences (diminishing returns if many empty halls).  
- Event Halls: **no daily meeting payout** in MVP (majors only).  
- Credit wallet; surface in Selection as estimated daily meetings $.

## 6. Major events

- `ConferenceSystem` schedules majors when ≥1 placeable Event Hall exists (and 4★ content reachable). Cadence: e.g. every **14–21** days with foreshadow **2 days** ahead (constants in plan).  
- Duration **1–3** days; books one or more Event Halls (fill highest capacity first).  
- **Revenue:** `hotelGuestPop × CurrentStars × sum(bookedCapacity) × EventPayMult` (climate SpendMultiplier may apply). Prefer **lump sum at event start** plus optional small daily while live.  
- Booked halls: **no daily meeting income** until event ends.  
- News: enqueue upcoming / live / ended MajorEvent items (banner + ticker).

### Attendees

- Role: `AgentRole.EventVisitor` (ephemeral).  
- **Day crowd:** spawn from lobby/Outside → Event Hall dwell → shops (commercial rules) → leave Outside. Raise elevator load, shop traffic, crime congestion like street visitors / hotel occupancy where applicable.  
- **Hotel fraction:** when vacant hotel beds exist, some visitors check in (reuse hotel guest patterns lightly or temporary HotelGuest-like stay for event nights); checkout marks Dirty as usual.  
- Spawn count scales with booked capacity; hard caps (e.g. concurrent visitors) to protect performance.  
- Despawn when event ends (force leave if still present).

## 7. Tower news

### Model

`TowerNews` holds a capped queue of items:

- `Category`: MajorEvent | OpsSerious | Quirk  
- `Priority`, `Text`, `CreatedDay` / expiry  
- Push API used by ConferenceSystem, crime, HK dirty backlog, stress/elevator pressure, broken/low condition

### HUD

- **Banner:** top-of-screen auto popup for high-priority MajorEvent (upcoming / live / ending); auto-dismiss after several seconds; optional click-to-dismiss; does not pause time.  
- **Ticker:** scrolling strip near top bar; cycles items with MajorEvent/OpsSerious preferred over Quirk.

### Example lines (illustrative)

- Event: “TowerCon opens tomorrow in the Event Hall.”  
- Serious: “Housekeeping backlog: dirty rooms may miss afternoon check-ins.” / “Crime spike on floor 7.” / “Elevator waits are stressing tenants.”  
- Quirk: “Someone held an elevator for a sandwich run on floor 3.”

## 8. Components

| Component | Responsibility |
|-----------|----------------|
| `ConferenceSystem` | Schedule, booking, daily/event payouts, attendee spawn targets |
| `TowerNews` | Feed queue + priority |
| `StarSystem` | 4★ criteria |
| Room SOs + tests | Conference resize; Event Hall asset |
| `AgentSystem` | EventVisitor lifecycle; hotel-backed subset |
| `EconomySystem` / sim tick | Midnight credits; wire ConferenceSystem |
| `TowerHudController` | Banner + ticker; Selection hall stats |
| EditMode tests | Stars, payout math, booking pause, news enqueue |

## 9. Non-goals

- Player-authored event calendar UI  
- Staffed Conference/Event Hall workers  
- Full newspaper archive / history panel  
- Raising earnable stars beyond 4 in this slice  
- Evaluation heatmaps (later roadmap)

## 10. Rollout

1. Spec approved → implementation plan → Subagent-Driven Development on `feature/conference-event-halls`.  
2. Order: stars 4★ + room assets → daily math → majors + booking → attendees → news HUD.  
3. Play Mode checklist against §1 success criteria; tune capacity, pay mults, spawn caps.
