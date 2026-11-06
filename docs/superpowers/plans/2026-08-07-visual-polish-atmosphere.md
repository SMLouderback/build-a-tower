# Visual Polish Atmosphere — Implementation Plan

**Spec:** `docs/superpowers/specs/2026-08-07-visual-polish-atmosphere-design.md`  
**Approach:** A (approved)

## Tasks

1. Nudge `DayNightSky.Day`; wire controller on TowerSandbox Main Camera + runtime ensure in `TowerSimulation.Awake`.
2. Add `TowerLookPalette` + `InteriorLighting` (+ metas).
3. Update `TilemapTowerView` paint path, lighting bucket repaint, dirt color; `BuildController` ghosts + `RepaintAllRooms`.
4. EditMode tests + README note.
5. Mark spec Implemented when done.
