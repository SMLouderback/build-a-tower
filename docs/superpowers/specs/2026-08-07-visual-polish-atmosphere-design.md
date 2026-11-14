# Build-A-Tower — Visual Polish Atmosphere (Approach A)

**Date:** 2026-08-07  
**Status:** Implemented  
**Depends on:** `DayNightSky` / `DayNightSkyController`; `TilemapTowerView`; `RoomTypeSO` category + luxury; `BuildController` / `TowerSimulation`; dirt / lobby starter guides  
**Parent:** Roadmap visual polish (after demand/climate heatmaps)  
**Follow-ups:** Real cutaway room art / furniture modules (later slice); economy retune out of scope

## 1. Goals

1. Day / night / sunrise / sunset **sky works again in play**.  
2. Rooms use **cutaway-inspired category / luxury palettes** (procedural colored tiles only — no furniture art).  
3. Soft **interior lighting** multiply by time of day; basements / parking get weaker exterior influence.  
4. **Lobby / dirt / structure** more readable against sky and rooms.  
5. Later slice owns real room art — **out of scope** here.

## 2. Locked decisions (Approach A)

| Decision | Choice |
|----------|--------|
| Delivery | Atmosphere + palette + soft TOD lighting on existing procedural tiles |
| Sky | Restore `DayNightSkyController` on Main Camera (scene + runtime ensure) |
| Day sky | Brighter blue in range ~R 0.42–0.55, G 0.70–0.78, B 0.92–0.95 |
| Room colors | Paint-time `TowerLookPalette.ForRoom` (category + luxury + id hints); do not mass-rewrite `.asset` colors |
| Lighting | Multiply/shift base paint by TOD; subterranean weaker sky, more fluorescent |
| Repaint | Bucket every 15 game minutes → full room repaint |
| Ghosts | Place preview uses palette `ForRoom` |
| Room art | Deferred |

## 3. Day / night sky

- Keep `DayNightSky` key times and night / sunrise / sunset / day transitions.  
- Nudge `Day` toward a brighter cutaway blue within the locked RGB band.  
- Wire `DayNightSkyController` on TowerSandbox **Main Camera** (GameObject `1093245804`): script guid `672448db8cf8a2f46b424f7b5b0a42d6`, `targetCamera` = Camera `1093245805`, `simulation` may be null (Find at runtime).  
- Runtime ensure: if `Camera.main` has no controller, `AddComponent` and assign refs (Awake Find). Prefer **scene + runtime**.

## 4. TowerLookPalette

`Assets/Scripts/Rendering/TowerLookPalette.cs` — static `Color ForRoom(RoomTypeSO type)`:

| Kind | Look |
|------|------|
| Lobby | Warm stone cream |
| Office | Cool blue; Base / Mid / Upper steps |
| Hotel | Lavender → deeper purple by luxury |
| Condo | Soft purple-green family (distinct from hotel purple) |
| Commercial | Warm wood / orange-red; dining ids warmer |
| Security | Cool grey-blue |
| Maintenance / housekeeping | Workshop amber / grey |
| Parking / valet / ramp | Dark concrete |
| Stairs | Mid shaft grey |
| Elevator | Darker shaft blue-grey |
| Scaffold | Temporary dull amber (≠ lobby) |
| Conference / Event | Muted accent |

Unknown → `type.placeholderColor`.

## 5. Interior lighting

`InteriorLighting.Apply(Color base, int minuteOfDay, bool subterranean)`:

- Night: cooler + dimmer.  
- Sunrise / sunset: warmer.  
- Day: near-neutral.  
- Subterranean (`y < 0` or parking): weaker exterior influence, more constant fluorescent.  
- Key times aligned with `DayNightSky`.

## 6. TilemapTowerView / BuildController

- `RoomPaintColor`: palette → broken / dirty washes → lighting (needs minute; instance method or pass minute).  
- Hold refs to `BuildController` / `TowerSimulation` (Find in Awake).  
- `Update`: when lighting bucket changes (15 game minutes), `RepaintAllRooms()` from `build.Grid`.  
- Ghosts: palette when placing a room type.  
- `DirtBand.Color`: richer earth brown for contrast.  
- Lobby guide strip stays gold; lobby paint uses warm stone from palette.

## 7. Tests & docs

- Existing `DayNightSkyTests` still pass.  
- `TowerLookPaletteTests`: office cooler than lobby; hotel purple-ish; parking dark.  
- `InteriorLightingTests`: night darker than day for same base.  
- README short note under visuals / maps: day-night restored; atmosphere palettes (full room art later).

## 8. Non-goals

- No commit from this slice unless asked.  
- No `.superpowers/sdd` or `_Recovery` edits.  
- No economy / numeric retune.  
- No furniture or real room art sprites.
