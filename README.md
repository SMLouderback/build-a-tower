# Build-A-Tower

SimTower-inspired 2D side-cutaway skyscraper simulation (Unity).

See `docs/superpowers/specs/2026-07-27-build-a-tower-slice1-design.md` for Slice #1 design.

## Play Slice #1

1. Open this folder in Unity **6000.4.7f1**.
2. Open `Assets/Scenes/TowerSandbox.unity`.
3. Press **Play**.
4. You should see:
   - A dark **HUD panel in the top-left** with funds + how-to text
   - Brown **dirt** underground, a dark **ground line**, and a **yellow Floor 1** strip
5. **Drag left → right on the yellow Floor 1 band** to place the Lobby.
6. Click a room button (Office / Condo / Hotel / Retail), then click above the lobby.
7. Right/middle-drag pans; scroll zooms; Bulldoze removes non-lobby rooms.

### Important: Game view Scale

Keep the Game tab **Scale slider at 1x** (or Scale to Fit). If Scale is above 1x (e.g. 1.4x), Unity zooms the Game view and **crops the menu** so you only see a corner of it.

On Play, the project tries to reset Scale to 1x automatically. You can also use menu **Build-A-Tower → Reset Game View Scale to 1x**.
