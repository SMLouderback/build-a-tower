# Build-A-Tower

SimTower-inspired 2D side-cutaway skyscraper simulation (Unity).

- Slice #1: `docs/superpowers/specs/2026-07-27-build-a-tower-slice1-design.md`
- Slice #3: `docs/superpowers/specs/2026-07-28-build-a-tower-slice3-design.md`
- Slice #4: `docs/superpowers/specs/2026-07-29-build-a-tower-slice4-design.md`
- Selector + elevator maintenance resize: `docs/superpowers/specs/2026-07-29-selector-elevator-maintenance-design.md`
- Star goals + economy HUD retune: `docs/superpowers/specs/2026-07-29-star-goals-economy-hud-design.md`
- Price tiers + progressive HUD: `docs/superpowers/specs/2026-07-30-price-tiers-progressive-hud-design.md`
- Commercial visit traffic (E1): `docs/superpowers/specs/2026-07-31-commercial-visit-traffic-design.md`
- Smart elevator routing: `docs/superpowers/specs/2026-07-31-smart-elevator-routing-design.md`
- Hybrid stairs + elevator pathing: `docs/superpowers/specs/2026-08-02-hybrid-stairs-elevator-design.md`
- Conference / Event Halls + tower news: `docs/superpowers/specs/2026-08-02-conference-event-halls-design.md`
- Collapsible top info bar (Shops / Elev / Tower dropdowns): `docs/superpowers/specs/2026-08-03-collapsible-top-info-bar-design.md`
- 5★ + underground parking & valet: `docs/superpowers/specs/2026-08-03-five-star-parking-valet-design.md`
- Parking ramp (deep basement access): `docs/superpowers/specs/2026-08-04-parking-ramp-design.md`
- Population & staff statistics HUD (backlog): `docs/superpowers/specs/2026-08-03-population-staff-statistics-design.md`
- Condo commute & stair capacity: `docs/superpowers/specs/2026-08-03-condo-commute-stair-capacity-design.md`
  - Plan: `docs/superpowers/plans/2026-08-03-condo-commute-stair-capacity.md` (implemented)
- Shop visit history (yesterday + 7-day avg): `docs/superpowers/specs/2026-08-03-shop-visit-history-design.md`
- Elevator traffic history (yesterday + 7-day avg passengers/wait): `docs/superpowers/specs/2026-08-03-elevator-traffic-history-design.md`
- Disposable income & market climate: `docs/superpowers/specs/2026-07-31-agent-disposable-income-climate-design.md`
- 3★ ops rooms & service workers: `docs/superpowers/specs/2026-07-31-stars3-ops-services-design.md`
- SimTower behavior reference (tower-together): `docs/reference/tower-together/`
- Slice #3 elevators checklist: `docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md`

## Play (Slice #1 + #2 + #3 + #4 + E1 shops)

1. Open this folder in Unity **6000.4.7f1**.
2. Open `Assets/Scenes/TowerSandbox.unity`.
3. Press **Play**.
4. HUD **core strip** (top-left): funds (+ income / expense / avg when economy unlocks), stars, **Gregorian date + time** (starts **Sat 01 Jan 2000**), **Climate**, **speed presets**, and help. Optional stats sit behind **Shops · Elev · Tower** dropdowns (and **Goals**); selecting a shop or elevator also shows temporary tower-wide shop/elevator chips on the strip. Expand **Build** (open by default), then later **Goals** (after lobby) and **Economy** (after first midnight / income).
5. **Drag left → right on Floor G** (lobby / ground / 1st floor — same level) to place the Lobby. Basement demolish restores the brown dirt fill (no sky holes) inside the starter dirt band.
6. In **Build**, open a family (**Office / Hotel / Condo / Shops / Transit**) and pick a variant. Place rooms above the lobby on floors 1+ (no overhangs). Basement rooms go on B1 (−1) and below. **Utility** appears only when support-room assets exist. At **4★**, **Transit** also includes **Underground Parking** (6 stalls), **Valet**, and **Parking Ramp** (3×2); ~25% of arrivals may enter via parking when Valet + free stalls exist. **B1** parking always counts; **B2+** stalls only count when a ramp chain reaches B1 or Lobby.
7. Place **Stairs** under **Transit** (**2×2**, two floors). From Floor G, stairs reach **B1** (origin at −1) or **floor 1** (origin at 0). Stair run is bottom-left → top-right. Stack the next flight one floor up on the same columns (connecting floor shares landings; roles 1 and 4 cannot overlap).
8. Watch **office workers** commute in the morning and **hotel guests** from **4pm–7pm** (skewed toward **4pm**). Guests stay overnight and check out between **6am–11am** next day (skewed toward **11am**). Clock runs ~1 game minute per real second.
9. Trips of **≤ 3 floors** use **stairs** when a valid stairs path exists (comfort band). When a goal is **above or below** an elevator's served range, agents **ride to the closest shaft floor** then finish on stairs (or stairs first, then elevator, going the other way). Longer stair climbs (**4+ floors**) add **stress per extra floor**; at **100 stress** agents **refuse** another over-cap floor and replan. When a shaft serves both start and goal, agents still prefer the elevator under normal waits. Each car holds up to **10** passengers; agents **waiting in line may switch shafts** when another is clearly better.
10. Bulldoze under occupied floors leaves scaffolding; RMB/MMB pan, scroll zoom.
11. Within **10 real-time seconds** of placing a room, bulldoze refunds build cost minus that room’s net earnings/upkeep; after the window, demolish stays **$0**.
12. Select **Elevator** (under Transit) and click to place a **1×2** shaft through supported floors (the initial cost is two floors).
13. Use **Selector** to click any built room — expand **Selection** for identity, economy lines, and (for offices/hotels/condos) **Low · Normal · High · Max** price tiers with a market hint. Selected elevators show **top/bottom edge handles** — drag to extend (charges new floors) or shorten under the rules below.
14. After an extension you have **10 real-time seconds** to shrink back toward the previous height without maintenance (quick mistake undo). Extending never requires maintenance.
15. Outside that window, open **Enter Maintenance** on the selected elevator. Existing queues/passengers finish; new agents will not board. When status is **Ready to shorten**, drag an edge inward (no refund; min height 2 floors), then **Exit Maintenance**.
16. Elevator shafts span at most **30 floors** and cannot overlap stairs. Watch the gold car marker move as agents call and ride the elevator. Waiting agents form a **visible line beside the shaft** (on the side they walked in from), so long queues are easy to spot; they only enter the shaft once they board. Selection shows **today / yesterday / 7-day** passengers and avg wait per shaft; the **Elev** top-bar dropdown (and selecting an elevator) shows tower-wide `El yday` / `El ~N/d` and `Wait yday` / `Wait ~Xm` (board events; wait at board; 7-day wait is passenger-weighted).
17. (Optional) With the Elevator place tool still selected, drag vertically from a shaft cell to extend — same as edge handles.
18. After the lobby exists, expand **Goals** for the **Next ★** checklist. After the first midnight or condo sale, expand **Economy** for **population**, **average stress**, and **Last Net**.
19. Each **midnight**, occupied offices and hotels pay daily rent **scaled by price tier** (Low 70% · Normal 100% · High 130% · Max 160%); overpriced units vs the star comfort band may skip that day’s payout. Each elevator shaft costs **$3,000/day** upkeep.
20. A new **condo** stays vacant until it has a valid route from the lobby. Buyers then travel to it (spawn chance also respects overpricing); you receive the **one-time sale** (tier-scaled) only when the first resident arrives (no payout for inaccessible condos). Moved-in residents leave for work weekdays: **≥50%** commute **Outside** (lobby → Outside, 15–60 min one-way ~30 bias, then ~8h work, return); the rest claim **reserved vacant office desks** (SyncHomes under-fills offices for that pool — never displaces office workers). HUD shows **Condo jobs: N in-tower / M outside** once assigned. Each **stairs** room holds at most **5** agents at once (shared up/down); extras wait and gain a little stress.
21. Stars are **earned as soon as requirements are met** (start at **0★**, cap **5★**; HUD shows **5** slots): **1★** needs ≥10 population, ≤40 avg stress, and a lobby; **2★** needs ≥30 population, ≤25 avg stress, and at least one elevator; **3★** needs ≥60 population, ≤20 avg stress, lobby + elevator + **Housekeeping** + **Maintenance** (facility rooms that are **Broken** do not count); **4★** needs ≥100 population, ≤15 avg stress, plus a **staffed Security Post**; **5★** needs ≥150 population, ≤12 avg stress, plus a **Valet** and **≥6 underground parking stalls**. A star can only be **lost** at the **90-day quarterly review** if the current tier no longer qualifies.
22. **Elevators** need **1★**; premium Office / Hotel / Condo variants and **Housekeeping / Maintenance** need **2★**; **Security Post**, **Research Lab**, **Conference Room**, and **Fine Dining** need **3★**; **Event Hall**, **Underground Parking**, **Valet**, and **Parking Ramp** need **4★** — locked buttons show the star requirement and stay grey until earned.
23. Under **Utility**, place **Housekeeping** and **Maintenance** at **2★** (each auto-hires **1** worker on placement). Select either room and use the **Staff 0–4** stepper to hire or release maids / handymen. Both ops rooms are required for **3★**. **Security Post** unlocks at **3★** (same **Staff 0–4** pattern) and is required staffed for **4★**.
24. **Condition** (0–100, default 100) drops **−1 per midnight** on degradable rooms (living, shops, utility — not lobby, elevators, stairs, or parking ramps). **≥70** normal; **40–69** raises stress; **1–39** pauses that room’s income; **0** marks the room **Broken** (dark desaturated tint) — no income, no new occupants, handymen skip it; **bulldoze + rebuild** only.
25. Hotel rooms go **Dirty** (brownish tint) on guest checkout and block new check-ins until a **maid** cleans them. Basic hotels: **15** game minutes; Premium hotels: **30** game minutes. Maids pathfind from Housekeeping to the oldest dirty room with a valid route.
26. **Handymen** pathfind from Maintenance to the lowest-**Condition** degradable room (1–99, not Broken), work **60** game minutes, then restore **+10** condition (cap 100) before taking the next job or idling home.
27. Each midnight debits service wages tower-wide: **$200/day per maid**, **$300/day per handyman** (sum of all hired staff). Selection shows **Condition**, **Dirty / Broken / OK**, and staff count for HK/Maint rooms.
28. Time presets under the clock: **|| · 1x · 2x · 5x · 10x · 60x** (pause or fast-forward; 60x helps reach midnight and quarterly checks).
29. Pan with the **bottom horizontal** and **right vertical scrollbars** (RMB/MMB drag and scroll zoom still work) to build and inspect tall, wide towers.
30. Room buttons show a compact **cost · income** tag (e.g. `Office $40k · $3k/d`, `Condo $80k · $150k once`, `Elevator $100k/fl · -$3k/d`); Build tool detail and Selection spell out full cost, income at the current tier, and any daily upkeep.
31. Under **Shops**, place **Fast Food** / **Restaurant** / **Fine Dining** (Food) or **Retail** — each needs a reachable path from the lobby. Office workers take a midday lunch trip; hotel guests and condo residents make at most one commercial trip per day; street visitors arrive from Outside when shops are open.
32. **Market climate** starts at **Normal** (Recession · Slow · Normal · Strong · Boom) and may shift on the **1st of each Gregorian month**. Climate scales daily disposable spend and how many rent/sale price tiers the market tolerates (see price-tier market hints).
33. Agents roll a **daily disposable budget** by home band — **Basic** (standard Office / Hotel / Condo): ~$40–$100; **Premium** (`*Premium` living rooms): ~$90–$200; **Street** visitors: ~$20–$60 — then multiply by the current climate. Price tier on the unit does **not** change the band.
34. Shop visits spend **random dollars** (`1 … min(shop list price, remaining budget)`); agents skip shops they cannot afford (e.g. Basic budget vs Restaurant). Shop income is **batched at midnight** from **dollars spent that day**, not flat `visits × list price`. Selection shows today’s visits, **yesterday**, and a **7-day average**; the **Shops** top-bar dropdown (and selecting a shop) shows tower-wide yesterday + 7-day avg shop visits.
35. Under **Utility**, place **Conference Room** at **3★** (~8×1). Each midnight, eligible Conference halls earn **daily meeting income** scaled by **office worker population**, hall capacity, stars, and climate. Select a Conference to see **Est. daily meetings: $…** in Selection.
36. Earn **4★** to unlock **Event Hall** (~12×2). Event Halls do not earn daily meetings; they host **major events** when at least one is placeable.
37. **Major events** schedule every ~2–3 weeks: foreshadow **2 days** ahead, run **1–3 days**, book Event Hall(s), pay a **lump sum at start** (hotel guest pop × stars × booked capacity), and **pause daily meetings only on booked halls**. A **banner** pops for upcoming / live / ending majors; a **scrolling ticker** above the top dashboard cycles events, serious ops alerts, and quirky lines.
38. **Event visitors** spawn during majors: day crowd paths lobby → Event Hall → shops → Outside; some book vacant hotel rooms for the event. They add elevator load, shop traffic, and crime pressure like street visitors.

### Important: Game view Scale

Keep the Game tab **Scale slider at 1x** (or Scale to Fit). Scale &gt; 1x crops the HUD.

On Play, the project tries to reset Scale to 1x. Menu: **Build-A-Tower → Reset Game View Scale to 1x**.
