# Build-A-Tower — Collapsible Top Info Bar

**Date:** 2026-08-03  
**Status:** Implemented  
**Depends on:** Economy HUD unlock, Goals unlock, shop visit history, elevator traffic history  
**Touches:** `TowerHudController` top strip

## Goals

Keep the top status strip scannable: **money and stars always visible**, with shop / elevator / tower statistics behind **category dropdowns** or temporary **selection context chips**.

## Locked decisions

| Decision | Choice |
|----------|--------|
| Always visible | Brand, Save $ / +income / −expense / avg $/d (when economy unlocked), stars, clock, climate, speed |
| Approach | Category Info buttons (Shops · Elev · Tower) + existing Goals |
| Info mutual exclusion | Shops / Elev / Tower: only one open; Goals independent |
| Unlock gates | Shops & Elev with economy unlock; Tower with goals unlock |
| Selection context | Selecting a shop or elevator adds temporary **tower-wide** chips on the core row; deselect clears; does **not** auto-open dropdowns |
| Shaft detail | Remains in Selection panel only |

## UI behavior

### Core row
Fixed height (same as today). Does **not** grow when dropdowns open (overlay below the bar).

### Buttons (right cluster)
- **Shops** / **Elev** / **Tower** / **Goals**
- Toggle same Info button again → close that panel
- Opening one of Shops/Elev/Tower closes the other two

### Dropdown contents
- **Shops:** `Shops yday N`, `Shops ~N/d`
- **Elev:** `El yday N`, `El ~N/d`, `Wait yday Xm`, `Wait ~Xm`
- **Tower:** Pop, Stress, Crime, condo jobs line when any assigned
- **Goals:** unchanged next-★ checklist

### Selection context chips
- Shop selected (`RoomCategory.Commercial` / `BuildFamily.Shops`): tower shop chips after money group, before stars
- Elevator selected (`isElevatorShaft`): tower elevator chips in the same slot
- Cleared when selection is null or another room type

## Non-goals

- Sticky pin toggles / second permanent chip row
- Population & staff breakdown (backlog)
- Auto-opening Info dropdowns on selection
