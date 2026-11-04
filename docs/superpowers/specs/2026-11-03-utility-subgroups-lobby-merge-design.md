# Build-A-Tower — Utility Subgroups & Lobby Merge

**Date:** 2026-11-03  
**Status:** Approved (brainstorming, section by section)  
**Depends on:** `BuildFamily` / `BuildSubgroup`, `BuildCatalog`, build dock HUD, lobby / sky lobby placement tools, Transit room catalog (stairs, elevators, parking)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Out of scope:** New subgroup-header art; changing sky-lobby height/spacing rules; numeric rebalance; splitting Utility into separate strip families

---

## 1. Goals

Make the build menu clearer for ops rooms and lobbies:

1. Keep a single **Utility** strip icon, but nest rooms into **Health/Sanitation**, **Power**, **Water**, and **Services** subgroups (same pattern as Shops → Food / Retail).
2. Put **stairs and elevators under Transit** in the catalog (not as tools-strip icons).
3. Merge **Lobby** and **Sky Lobby** into one Lobby tool that places ground lobby on floor G / 1st and sky lobby on floors above.

### Success criteria

In Play Mode a player can:

1. Open Utility and see the four subgroups with the rooms listed below.
2. Open Transit and place Stairs / Normal / Express / Service elevators from that popout (no Stairs/Elevator/Sky Lobby on the tools strip).
3. Select Lobby once; drag on G to extend ground lobby; place above G to get a sky lobby under existing sky rules.
4. Still use Select, Scaffold, and Bulldoze on the tools strip.

EditMode tests cover Utility subgroup membership, Transit containing stairs/elevators, and floor-aware lobby tool selection.

---

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Utility strip | One family icon (not four top-level families) |
| Utility nesting | Subgroups like Shops Food/Retail |
| Subgroup order | Health/Sanitation → Power → Water → Services |
| Lobby menu | Single Lobby tool; auto ground vs sky by floor |
| Stairs / elevators | Transit catalog only; remove from tools strip if present |
| Tools strip | Select · Lobby · Scaffold · Bulldoze |
| Sky rules | Unchanged (≥15 up, spacing, etc.) |
| Sim flags | Keep distinct `isLobby` / `isSkyLobby` room types under the hood |

---

## 3. Strip & popout layout

### 3.1 Family strip

Unchanged set: Office, Hotel, Condo, Shops, Leisure, **Utility**, Transit.

### 3.2 Utility subgroups

| Subgroup | Rooms |
|----------|--------|
| Health / Sanitation | Clinic, Public Restroom, Office Restroom, Trash, Recycling, Housekeeping |
| Power | Breaker / Power (`service_power`) |
| Water | Pump (`service_pump`), Water Treatment (`service_treatment`) |
| Services | Security, Mail, Loading Dock, Maintenance, Research Lab, Trophy Room |

Empty subgroups are omitted. Room icons remain square Art/Menu assets; English tooltips secondary.

### 3.3 Transit

Contains Stairs, Elevator Normal / Express / Service, Parking (above/underground as today), Metro, Valet, and other transit-tagged rooms. Chosen from the Transit popout only.

### 3.4 Tools strip

| Tool | Notes |
|------|--------|
| Select | Unchanged |
| Lobby | Merged; see §4 |
| Scaffold | Unchanged |
| Bulldoze | Unchanged |

**Removed from tools strip:** Sky Lobby, Stairs, Elevator (if any tool icons still exist).

---

## 4. Lobby auto ground / sky

One Lobby tool. On place/drag:

| Floor under cursor | Result |
|--------------------|--------|
| Ground / 1st (`TowerGrid.LobbyFloor`) | Ground lobby (`isLobby`) — extend/widen as today |
| Any floor above | Sky lobby (`isSkyLobby`) — existing sky placement rules |
| Basement / invalid | Reject with existing invalid-placement feedback |

Tooltip covers both: lobby on G; sky lobby on floors above. Tutorial highlights that targeted Sky Lobby retarget to Lobby. Save/sim keep separate room types and flags; only the menu entry merges.

---

## 5. Catalog data & wiring

### 5.1 Enums

Extend `BuildSubgroup`:

- `HealthSanitation`
- `Power`
- `Water`
- `Services`

Keep `Food` and `Retail` for shops.

### 5.2 `BuildCatalog.Group`

When family is `Utility`, nest subgroups like Shops (add each non-empty subgroup in §3.2 order). Labels:

| Enum | Label |
|------|--------|
| HealthSanitation | Health / Sanitation |
| Power | Power |
| Water | Water |
| Services | Services |

### 5.3 Room tagging

Set `buildFamily = Utility` and the matching `buildSubgroup` on each listed room asset. Fallback in `ResolvedBuildSubgroup()` (or catalog helper): if Utility and subgroup is `None`, map known `id`s (e.g. `service_clinic` → HealthSanitation, `service_power` → Power, `service_pump` / `service_treatment` → Water, `service_trophy_room` / security / mail / … → Services) so untagged assets still sort.

Transit rooms: ensure stairs and elevator shafts resolve to `BuildFamily.Transit` and appear only in that family’s popout.

### 5.4 Lobby tool code

- Placement / `SelectLobbyTool` resolves ground vs sky room type from floor.
- Remove or stop drawing `SelectSkyLobbyTool` on the strip.
- Retarget tutorial `SkyLobby` highlights to Lobby where applicable.

---

## 6. Testing

EditMode (representative):

1. `BuildCatalog.Group` Utility entry has four subgroups with expected room ids.
2. Stairs and elevator types appear under Transit, not as `BuildMenuTool` strip entries.
3. Lobby tool selection on `LobbyFloor` uses ground lobby type; above uses sky lobby type.
4. Shops Food/Retail nesting still works (no regression).

---

## 7. Non-goals (restate)

No new strip families for Power/Water/Health. No change to sky lobby numeric rules. No requirement for new subgroup header art in this slice.
