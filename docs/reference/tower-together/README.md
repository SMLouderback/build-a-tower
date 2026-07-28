# tower-together reference (SimTower reverse-engineering)

**Upstream:** [phulin/tower-together](https://github.com/phulin/tower-together) (MIT)  
**Live game:** [towers.world](https://towers.world)  
**Pulled:** 2026-07-28 for Build-A-Tower design reference  

These docs are **not** original SimTower source. They are reverse-engineered notes and a tick-oriented reimplementation. Prefer their **trace tests / specs** as behavior oracles; do not paste TypeScript into Unity wholesale.

## How we use this

| Use | Do |
|-----|-----|
| Rules | Port concepts into C# on `TowerGrid` / agents / transit |
| Elevators (Slice #3) | Start from `specs/ELEVATORS.md` + `specs/ROUTING.md` |
| People / schedules | `specs/PEOPLE.md`, `specs/TIME.md`, facility docs |
| Licensing | MIT — attribute upstream in docs when porting algorithms |

## Contents (subset)

- `AGENTS.md` — map of the upstream pure sim core (`apps/worker/src/sim`)
- `specs/` — subsystem specs (elevators, routing, people, time, economy, …)
- `specs/facility/` — lobby, office, hotel, condo, commercial, parking, metro, …

## Build-A-Tower mapping

See [SLICE3-ELEVATORS-CHECKLIST.md](SLICE3-ELEVATORS-CHECKLIST.md) for how elevators map onto our Unity stack and what stays stairs-only until then.

## Also noted (not mirrored here)

- [fabianschuiki/OpenSkyscraper](https://github.com/fabianschuiki/OpenSkyscraper) — C++/SFML, **GPL-2.0**, older; useful for ideas, awkward to copy into a non-GPL project.
