# Build-A-Tower

SimTower-inspired 2D side-cutaway skyscraper simulation (Unity).

- Slice #1: `docs/superpowers/specs/2026-07-27-build-a-tower-slice1-design.md`
- Slice #3: `docs/superpowers/specs/2026-07-28-build-a-tower-slice3-design.md`
- Slice #4: `docs/superpowers/specs/2026-07-29-build-a-tower-slice4-design.md`
- Selector + elevator maintenance resize: `docs/superpowers/specs/2026-07-29-selector-elevator-maintenance-design.md`
- SimTower behavior reference (tower-together): `docs/reference/tower-together/`
- Slice #3 elevators checklist: `docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md`

## Play (Slice #1 + #2 + #3 + #4)

1. Open this folder in Unity **6000.4.7f1**.
2. Open `Assets/Scenes/TowerSandbox.unity`.
3. Press **Play**.
4. HUD (top-left): funds, **clock**, **agents / stress**, tools.
5. **Drag left → right on Floor G** (lobby / ground / 1st floor — same level) to place the Lobby.
6. Place **Office / Condo / Hotel / Retail** above the lobby on floors 1+ (no overhangs). Basement rooms go on B1 (−1) and below.
7. Place **Stairs** (**2×2**, two floors). From Floor G, stairs reach **B1** (origin at −1) or **floor 1** (origin at 0). Stair run is bottom-left → top-right. Stack the next flight one floor up on the same columns (connecting floor shares landings; roles 1 and 4 cannot overlap).
8. Watch **office workers** commute in the morning and **hotel guests** after 4pm (clock runs ~1 game minute per real second).
9. Trips farther than **3 floors** via stairs use elevators when a shaft serves both floors; otherwise they fail and raise stress.
10. Bulldoze under occupied floors leaves scaffolding; RMB/MMB pan, scroll zoom.
11. Select **Elevator** and click to place a **1×2** shaft through supported floors (the initial cost is two floors).
12. Use **Selector** to click any built room for a HUD summary. Selected elevators show **top/bottom edge handles** — drag to extend (charges new floors) or shorten under the rules below.
13. After an extension you have **10 real-time seconds** to shrink back toward the previous height without maintenance (quick mistake undo). Extending never requires maintenance.
14. Outside that window, open **Enter Maintenance** on the selected elevator. Existing queues/passengers finish; new agents will not board. When status is **Ready to shorten**, drag an edge inward (no refund; min height 2 floors), then **Exit Maintenance**.
15. Elevator shafts span at most **30 floors** and cannot overlap stairs. Watch the gold car marker move as agents call and ride the elevator. Waiting agents form a **visible line beside the shaft** (on the side they walked in from), so long queues are easy to spot; they only enter the shaft once they board.
16. (Optional) With the Elevator place tool still selected, drag vertically from a shaft cell to extend — same as edge handles.
17. HUD (top-left): **Stars (0/2)**, **population**, **average stress**, and **Last Net** (yesterday income / expense summary).
18. Each **midnight**, occupied offices and hotels pay daily rent; each elevator shaft costs **$10,000/day** upkeep — watch funds and Last Net change.
19. When a **condo** first gains a resident, you receive a **one-time sale** payout (no repeat if they move out).
20. Every **90 game days**, a quarterly star check runs: start at **0★**; earn **1★** with ≥10 population, ≤40 avg stress, and a lobby; earn **2★** with ≥30 population, ≤25 avg stress, and at least one elevator. Failing the current tier can **demote** a star.
21. **Elevators** need **1★**; premium Office / Hotel / Condo variants need **2★** — locked buttons show the star requirement and stay grey until earned.
22. Time presets under the clock: **|| · 1x · 2x · 5x · 10x · 60x** (pause or fast-forward; 60x helps reach midnight and quarterly checks).
23. Pan with the **bottom horizontal** and **right vertical scrollbars** (RMB/MMB drag and scroll zoom still work) to build and inspect tall, wide towers.

### Important: Game view Scale

Keep the Game tab **Scale slider at 1x** (or Scale to Fit). Scale &gt; 1x crops the HUD.

On Play, the project tries to reset Scale to 1x. Menu: **Build-A-Tower → Reset Game View Scale to 1x**.
