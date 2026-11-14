# Build-A-Tower — Collapsible Top Info Bar

**Date:** 2026-08-03  
**Status:** Implemented  
**Depends on:** Economy HUD unlock, Goals unlock, shop visit history, elevator traffic history  
**Touches:** `TowerHudController` top strip

## Goals

Keep the top status strip scannable: **money and stars always visible**, with shop / elevator / tower statistics behind **category dropdowns** only (no duplicate chips on the core row).

## Locked decisions

| Decision | Choice |
|----------|--------|
| Always visible | Brand, Save $ / +income / −expense / avg $/d (when economy unlocked), stars, clock, climate, speed |
| Approach | Category Info buttons (Shops · Elev · Tower) + existing Goals |
| Info mutual exclusion | Shops / Elev / Tower: only one open; Goals independent |
| Unlock gates | Shops & Elev with economy unlock; Tower with goals unlock |
| Selection context | Selecting a shop or elevator does **not** add tower-wide chips to the core row; use Shops / Elev dropdowns (shaft/shop detail stays in Selection) |
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

## Non-goals

- Selection-driven temporary chips on the core row (removed — duplicated dropdown data)
- Sticky pin toggles / second permanent chip row
- Population & staff breakdown (backlog)
- Auto-opening Info dropdowns on selection
