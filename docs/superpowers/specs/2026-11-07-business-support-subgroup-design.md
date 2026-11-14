# Build-A-Tower — Utility Business Support subgroup

**Date:** 2026-11-07  
**Status:** Approved (brainstorm sections)  
**Related:** `docs/superpowers/specs/2026-11-03-utility-subgroups-lobby-merge-design.md`

## Problem

Conference Room and Event Hall are Utility rooms with `buildSubgroup = None`. After Utility was nested into Health / Power / Water / Services only, those venues never appear in the build popout. Mail and Loading Dock sit under Services but belong with conference/event as business-support logistics.

## Goals

1. Restore Conference and Event Hall in the Utility build menu.
2. Add Utility subgroup **Business Support** containing: Mail, Loading Dock, Conference, Event Hall.
3. Keep Security, Maintenance, Research Lab, Trophy Room under **Services**.
4. Match existing Utility subgroup folder UX (folder chrome + themed icon).

## Non-goals

- Conference / event economy or booking changes.
- New room types or renaming the Utility family.
- Moving Health / Power / Water rooms.

## Catalog

| Item | Value |
|------|--------|
| Enum | `BuildSubgroup.BusinessSupport` |
| Label | `Business Support` |
| Utility order | Health/Sanitation → Power → Water → **Business Support** → Services |
| Rooms | `service_mail`, `service_loading_dock`, `service_conference`, `service_event_hall` |

Tag `buildSubgroup` on those four assets. `ResolvedBuildSubgroup()` id fallback maps the same ids when untagged. Remove mail / loading dock from Services fallback.

## HUD / art

- `MenuIconIdForSubgroup` → `subgroup_business`
- Glyph `Bs`; folder tooltip style unchanged
- Icon: warm dollhouse business-support vignette; magenta `#FF00FF` key plate only

## Testing

EditMode covers: Utility subgroup order includes Business Support; four rooms bucket correctly; Conference/Event Hall no longer orphaned; Services excludes mail/loading dock; labels and icon id resolve.

## Success

Player opens Utility → Business Support and can place Mail, Loading Dock, Conference, and Event Hall. Services no longer lists mail/dock.
