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
- Disposable income & market climate: `docs/superpowers/specs/2026-07-31-agent-disposable-income-climate-design.md`
- SimTower behavior reference (tower-together): `docs/reference/tower-together/`
- Slice #3 elevators checklist: `docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md`

## Play (Slice #1 + #2 + #3 + #4 + E1 shops)

1. Open this folder in Unity **6000.4.7f1**.
2. Open `Assets/Scenes/TowerSandbox.unity`.
3. Press **Play**.
4. HUD **core strip** (top-left): funds, stars, **Gregorian date + time** (starts **Sat 01 Jan 2000**), **Climate** (market step), **speed presets**, and help. Expand **Build** (open by default), then later **Goals** (after lobby) and **Economy** (after first midnight / income).
5. **Drag left → right on Floor G** (lobby / ground / 1st floor — same level) to place the Lobby.
6. In **Build**, open a family (**Office / Hotel / Condo / Shops / Transit**) and pick a variant. Place rooms above the lobby on floors 1+ (no overhangs). Basement rooms go on B1 (−1) and below. **Utility** appears only when support-room assets exist.
7. Place **Stairs** under **Transit** (**2×2**, two floors). From Floor G, stairs reach **B1** (origin at −1) or **floor 1** (origin at 0). Stair run is bottom-left → top-right. Stack the next flight one floor up on the same columns (connecting floor shares landings; roles 1 and 4 cannot overlap).
8. Watch **office workers** commute in the morning and **hotel guests** after 4pm (clock runs ~1 game minute per real second).
9. Trips of **≤ 3 floors** use **stairs** when a valid stairs path exists. Longer trips pick the **best serving elevator** (lowest walk + queue + load score among shafts that span both floors); if none qualify, the trip fails and raises stress. Each car holds up to **10** passengers; agents **waiting in line may switch shafts** when another is clearly better.
10. Bulldoze under occupied floors leaves scaffolding; RMB/MMB pan, scroll zoom.
11. Within **10 real-time seconds** of placing a room, bulldoze refunds build cost minus that room’s net earnings/upkeep; after the window, demolish stays **$0**.
12. Select **Elevator** (under Transit) and click to place a **1×2** shaft through supported floors (the initial cost is two floors).
13. Use **Selector** to click any built room — expand **Selection** for identity, economy lines, and (for offices/hotels/condos) **Low · Normal · High · Max** price tiers with a market hint. Selected elevators show **top/bottom edge handles** — drag to extend (charges new floors) or shorten under the rules below.
14. After an extension you have **10 real-time seconds** to shrink back toward the previous height without maintenance (quick mistake undo). Extending never requires maintenance.
15. Outside that window, open **Enter Maintenance** on the selected elevator. Existing queues/passengers finish; new agents will not board. When status is **Ready to shorten**, drag an edge inward (no refund; min height 2 floors), then **Exit Maintenance**.
16. Elevator shafts span at most **30 floors** and cannot overlap stairs. Watch the gold car marker move as agents call and ride the elevator. Waiting agents form a **visible line beside the shaft** (on the side they walked in from), so long queues are easy to spot; they only enter the shaft once they board.
17. (Optional) With the Elevator place tool still selected, drag vertically from a shaft cell to extend — same as edge handles.
18. After the lobby exists, expand **Goals** for the **Next ★** checklist. After the first midnight or condo sale, expand **Economy** for **population**, **average stress**, and **Last Net**.
19. Each **midnight**, occupied offices and hotels pay daily rent **scaled by price tier** (Low 70% · Normal 100% · High 130% · Max 160%); overpriced units vs the star comfort band may skip that day’s payout. Each elevator shaft costs **$3,000/day** upkeep.
20. A new **condo** stays vacant until it has a valid route from the lobby. Buyers then travel to it (spawn chance also respects overpricing); you receive the **one-time sale** (tier-scaled) only when the first resident arrives (no payout for inaccessible condos).
21. Stars are **earned as soon as requirements are met** (start at **0★**): **1★** needs ≥10 population, ≤40 avg stress, and a lobby; **2★** needs ≥30 population, ≤25 avg stress, and at least one elevator. A star can only be **lost** at the **90-day quarterly review** if the current tier no longer qualifies.
22. **Elevators** need **1★**; premium Office / Hotel / Condo variants need **2★** — locked buttons show the star requirement and stay grey until earned.
23. Time presets under the clock: **|| · 1x · 2x · 5x · 10x · 60x** (pause or fast-forward; 60x helps reach midnight and quarterly checks).
24. Pan with the **bottom horizontal** and **right vertical scrollbars** (RMB/MMB drag and scroll zoom still work) to build and inspect tall, wide towers.
25. Room buttons show a compact **cost · income** tag (e.g. `Office $40k · $3k/d`, `Condo $80k · $150k once`, `Elevator $100k/fl · -$3k/d`); Build tool detail and Selection spell out full cost, income at the current tier, and any daily upkeep.
26. Under **Shops**, place **Fast Food** / **Restaurant** (Food) or **Retail** — each needs a reachable path from the lobby. Office workers take a midday lunch trip; hotel guests and condo residents make at most one commercial trip per day; street visitors arrive from Outside when shops are open.
27. **Market climate** starts at **Normal** (Recession · Slow · Normal · Strong · Boom) and may shift on the **1st of each Gregorian month**. Climate scales daily disposable spend and how many rent/sale price tiers the market tolerates (see price-tier market hints).
28. Agents roll a **daily disposable budget** by home band — **Basic** (standard Office / Hotel / Condo): ~$40–$100; **Premium** (`*Premium` living rooms): ~$90–$200; **Street** visitors: ~$20–$60 — then multiply by the current climate. Price tier on the unit does **not** change the band.
29. Shop visits spend **random dollars** (`1 … min(shop list price, remaining budget)`); agents skip shops they cannot afford (e.g. Basic budget vs Restaurant). Shop income is **batched at midnight** from **dollars spent that day**, not flat `visits × list price`. Selection shows today’s visit count and earnings.

### Important: Game view Scale

Keep the Game tab **Scale slider at 1x** (or Scale to Fit). Scale &gt; 1x crops the HUD.

On Play, the project tries to reset Scale to 1x. Menu: **Build-A-Tower → Reset Game View Scale to 1x**.
