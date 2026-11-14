# Build-A-Tower — Leisure Attractions Art (Casino, Nightclub, Chapel, Atrium)

**Date:** 2026-10-01  
**Status:** Approved (brainstorming)  
**Depends on:** Leisure attractions gameplay (`2026-09-30-leisure-attractions-casino-atrium-design.md`), existing `RoomDollhouseArt` / `MenuIconArt` pipelines  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  

## 1. Goals

Replace placeholder colors for the four new Leisure rooms with painterly **dollhouse cutaways** and **square menu icons**, matching Gym/Spa/Pool/Bowling/Theater language.

### Success criteria

1. Each of the four room types loads dollhouse art via `RoomDollhouseArt` (no flat placeholder when mapped).
2. Each appears as a square pictorial icon in the Leisure build strip via `MenuIconArt` (`Resources/Art/Menu/{room.id}`).
3. Art uses magenta chroma plates; warm architectural/industrial hotel materials (not pink toy dollhouse). Magenta is key plate only.
4. Production: contact sheet → human pick → crop → wire → EditMode load tests; one room fully done before the next.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Scope | Menu icons + dollhouse for Casino, Nightclub, Chapel, Atrium |
| Pipeline | Magenta contact sheets → crop → pick winner → map |
| Cadence | Per room: dollhouse sheet → pick → menu sheet → pick → wire/tests |
| Order | Casino → Nightclub → Chapel → Atrium |
| Family icon | `family_leisure` unchanged this pass |
| Gameplay | Unchanged |
| People in art | None |

## 3. Asset mapping

| Room id | Dollhouse leaf | Aspect | Menu id |
|---------|----------------|--------|---------|
| `leisure_casino` | `casino_10x1` | 10:1 | `leisure_casino` |
| `leisure_nightclub` | `nightclub_8x1` | 8:1 | `leisure_nightclub` |
| `leisure_chapel` | `chapel_6x1` | 6:1 | `leisure_chapel` |
| `leisure_atrium` | `atrium_8x3` | 8:3 (one sprite, three floors) | `leisure_atrium` |

Paths:

- Dollhouse: `Assets/Resources/Art/Dollhouse/<leaf>.png` + `.bytes` + `.meta`
- Menu: `Assets/Resources/Art/Menu/<room.id>.png` + `.bytes` + `.meta`

## 4. Content briefs

| Room | Dollhouse | Menu icon |
|------|-----------|-----------|
| Casino | Gaming floor: felt tables, chip racks, subdued gold/green, carpet, ceiling cans — classy hotel casino | Table + chips, warm wood/green |
| Nightclub | Dance floor, DJ booth, booths, bar; dark walls, restrained amber/purple accents | DJ booth / speakers / floor |
| Chapel | Pews/chairs, simple altar, soft wood, subtle stained-glass glow | Steeple / pew / soft light |
| Atrium | Three open levels, escalators/stairs, plants, seating, glass rails, soft daylight | Open well / escalators / greenery |

Shared: dark structural frame; warm spot lighting; readable at tower zoom; style refs `gym_6x1`, `theater_8x2`, other leisure strips.

## 5. Pipeline (per room)

1. Generate dollhouse contact sheet: hot magenta plate; 2–3 full strips at target aspect; magenta gutters between strips.
2. Human picks strip (or regenerate).
3. Crop → Dollhouse PNG/bytes/meta; map `RoomDollhouseArt.LeafByTypeId`.
4. Generate square menu contact sheet: 2–3 variants; subject fills square; magenta plate.
5. Human picks → Menu PNG/bytes/meta.
6. EditMode: mapped + Resources load; brief Play Mode check; next room.

Recreate/adapt crop helper under `.superpowers/sdd/` if missing (same chroma-band approach as shop dollhouse plan).

## 6. Code / tests

| Area | Change |
|------|--------|
| `RoomDollhouseArt.cs` | Four `LeafByTypeId` entries |
| `Assets/Resources/Art/Dollhouse/` | Four leaves |
| `Assets/Resources/Art/Menu/` | Four icons keyed by room id |
| EditMode tests | Extend dollhouse/menu catalog tests for new ids |

No economy, pathing, or footprint changes.

## 7. Out of scope

- Regenerating existing leisure art
- Updating `family_leisure` family icon
- Ambient sound profiles
- Backlog leisure units (Arcade, Observation Deck, etc.)
