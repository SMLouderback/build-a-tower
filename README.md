# Build-A-Tower

SimTower-inspired 2D side-cutaway skyscraper simulation (Unity).

- Slice #1: `docs/superpowers/specs/2026-07-27-build-a-tower-slice1-design.md`
- Slice #2: `docs/superpowers/specs/2026-07-28-build-a-tower-slice2-design.md`
- SimTower behavior reference (tower-together): `docs/reference/tower-together/`
- Slice #3 elevators checklist: `docs/reference/tower-together/SLICE3-ELEVATORS-CHECKLIST.md`

## Play (Slice #1 + #2)

1. Open this folder in Unity **6000.4.7f1**.
2. Open `Assets/Scenes/TowerSandbox.unity`.
3. Press **Play**.
4. HUD (top-left): funds, **clock**, **agents / stress**, tools.
5. **Drag left → right on Floor G** (lobby / ground / 1st floor — same level) to place the Lobby.
6. Place **Office / Condo / Hotel / Retail** above the lobby on floors 1+ (no overhangs). Basement rooms go on B1 (−1) and below.
7. Place **Stairs** (**2×2**, two floors). From Floor G, stairs reach **B1** (origin at −1) or **floor 1** (origin at 0). Stair run is bottom-left → top-right. Stack the next flight one floor up on the same columns (connecting floor shares landings; roles 1 and 4 cannot overlap).
8. Watch **office workers** commute in the morning and **hotel guests** after 4pm (clock runs ~1 game minute per real second).
9. Trips farther than **3 floors** via stairs fail and raise stress (elevators come in Slice #3).
10. Bulldoze under occupied floors leaves scaffolding; RMB/MMB pan, scroll zoom.

### Important: Game view Scale

Keep the Game tab **Scale slider at 1x** (or Scale to Fit). Scale &gt; 1x crops the HUD.

On Play, the project tries to reset Scale to 1x. Menu: **Build-A-Tower → Reset Game View Scale to 1x**.
