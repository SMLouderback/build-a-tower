# Build-A-Tower — Utility Building Services (Mail, Recycling, Loading Dock)

**Date:** 2026-10-03  
**Status:** Approved (brainstorming)  
**Depends on:** Utility family (Housekeeping / Maintenance / Security / Research); `ParkingStalls` ramp + parking accessibility; `AmenitySystem` proximity relief; pictorial Utility strip + dollhouse pipeline  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Parent roadmap:** Leisure amenities/attractions → **utility/ops building services** → prestige venues / retail ladder (later) → numeric rebalance → heatmaps → visual polish  
**Backlog (not this pass):** Medical/daycare; fire station; garbage-truck / mail-courier agents; trash meters; freight scheduling

## 1. Goals

Add three **Utility** building-service rooms — **Mail Room**, **Recycling Center**, **Loading Dock** — with catalog data, dollhouse + menu art, vehicle-access placement for Recycling/Loading Dock (mirroring underground parking), and thin proximity hooks via `AmenitySystem`. No full trash/mail/freight simulation.

### Success criteria

In Play Mode a player can:

1. Place all three rooms from Build → **Utility** when star gates allow.
2. See Recycling / Loading Dock reject (red ghost) without vehicle access; accept when access matches parking rules (Loading Dock also via adjacent accessible parking).
3. Get Mail / Recycling once-per-day stress relief for eligible agents in range; Loading Dock gives a small shop/hotel demand nudge when accessible.
4. See dollhouse overlays and square menu icons matching Utility industrial cutaway style.
5. EditMode tests cover catalog, vehicle access, amenity hooks, and art mapping.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Scope | Exactly three rooms: Mail, Recycling, Loading Dock |
| Depth | Catalog + art + light hooks (Approach 1 — extend `AmenitySystem`) |
| Family | Existing `BuildFamily.Utility` — no new family |
| Income | `IncomeModel.None` — no visit spend this pass |
| Vehicle access | Recycling + Loading Dock required; Mail none |
| Loading Dock extra | May also satisfy access by edge-adjacency to an **accessible** parking lot |
| Agents / meters | Out of scope — access is placement/ops gate only |
| Stars | Mail 1★; Recycling / Loading Dock 2★ — no new promotion criteria |
| Art | Magenta contact → crop; warm industrial Utility materials |

## 3. Catalog

| id | Display | ★ | Size | Build $ | Noise | Zone | Vehicle access |
|----|---------|---|------|--------:|------:|------|----------------|
| `service_mail` | Mail Room | 1 | 4×1 | 38,000 | 0.15 | Above or basement | None |
| `service_recycling` | Recycling Center | 2 | 6×1 | 72,000 | 0.40 | Basement preferred; above allowed | Required (parking rules) |
| `service_loading_dock` | Loading Dock | 2 | 8×1 | 95,000 | 0.55 | Basement only | Required (parking rules **or** adjacent accessible parking) |

**Shared**

- `buildFamily = Utility`; `category = Service`; `incomeModel = None`; `baseIncome = 0`.
- Placeholder colors: muted industrial Utility teal/amber variants.
- Broken rooms: no light hooks; do not count as vehicle-access seeds/links.
- Star ladder: Mail (1) → Recycling / Loading Dock (2).

## 4. Vehicle access

Generalize the underground-parking accessibility model in `ParkingStalls` (or a thin sibling helper used by both parking and these rooms).

A vehicle-access room counts as **accessible** when:

1. It is on **B1 (−1)**, or  
2. It **edge-touches** a ramp that reaches B1 or Lobby via the existing ramp flood, or  
3. It is linked through **same-floor edge-adjacent** peers of the vehicle-access set (Recycling ↔ Recycling, Loading Dock ↔ Loading Dock; Loading Dock may also bridge through **accessible parking** lots).

**Placement:** `CanPlace` / ghost validation fails when Recycling or Loading Dock would not be accessible after place (same spirit as parking lot usefulness). Mail ignores this check.

**Runtime hooks:** Recycling and Loading Dock amenity/demand effects apply only while accessible and not broken.

## 5. Light hooks (`AmenitySystem`)

**Range:** ±2 floors, ≤12 cells horizontal, origin-to-origin (same as leisure amenities).

| Room | Beneficiaries | Effect |
|------|---------------|--------|
| Mail Room | Condo residents + office workers (home/desk in range) | Once/day stress relief **−1** |
| Recycling Center | Hotel guests + condo residents in range | Once/day stress relief **−2** (if vehicle-accessible) |
| Loading Dock | Shops + hotels in range | **+5%** demand/pay nudge constant (if vehicle-accessible); not stress relief |

**Rules**

- Daily stress relief still picks the **strongest** in-range amenity (leisure + Mail + Recycling compete).
- Loading Dock nudge is separate from stress relief (shop visit pay or hotel stay preference — one shared constant in plan).
- Optional Selection line “Vehicle access: OK / blocked” is nice-to-have, not required for v1.

## 6. Art, HUD, audio

### 6.1 Dollhouse

| Room id | Resource leaf | Brief |
|---------|---------------|-------|
| `service_mail` | `mail_4x1` | Sorting benches, mail slots, carts |
| `service_recycling` | `recycling_6x1` | Bins, baler, roll-up door hint |
| `service_loading_dock` | `loading_dock_8x1` | Raised bay, truck apron, crates |

Shared: side cutaway; dark structural frame; warm industrial materials (concrete, steel, muted amber); magenta key plate only; no painted-in agents. Review order: Mail → Recycling → Loading Dock.

### 6.2 Menu / HUD

- Square `Art/Menu/{id}` icons; English tooltips secondary.
- Utility strip lists the three with ★ grey-out and red ghost on failed vehicle access.

### 6.3 Audio

- `SoundProfileMap` → existing `SoundProfile.Utility`.

## 7. Architecture / files

| Area | Change |
|------|--------|
| `Resources/Rooms/*` | New SOs for three rooms |
| `ParkingStalls` or sibling | Vehicle-access API for Recycling / Loading Dock (+ parking reuse) |
| `TowerGrid` / `BuildController` | Placement validation for access |
| `AmenitySystem` (+ hotel/shop demand call sites) | Mail/Recycling relief; Loading Dock nudge |
| `RoomDollhouseArt` / menu icons | Map leaves + HUD load |
| `TowerHudController` | `TryAddRoomButton` for three rooms |
| Tests | Catalog, access graphs, amenity hooks, art mapping |

## 8. Out of scope

- Garbage truck / mail courier / freight agents  
- Trash meters, pickup schedules, dock queues  
- Medical, daycare, fire station, prestige venues, retail ladder  
- New star promotion criteria  
- Dedicated Utility ambience WAVs (optional follow-up)

## 9. Spec self-review

- No TBDs on catalog sizes/stars/costs/access.  
- Vehicle access explicitly reuses parking semantics; Loading Dock parking-bridge called out.  
- Amenity vs dock nudge separated so leisure daily-relief path stays coherent.  
- Scope stays catalog + art + light hooks — no full logistics sim.
