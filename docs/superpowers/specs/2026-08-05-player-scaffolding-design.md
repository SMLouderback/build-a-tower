# Build-A-Tower — Player-Built Scaffolding

**Date:** 2026-08-05  
**Status:** Implemented  
**Depends on:** Existing demolish scaffolding (`isScaffolding`, load-bearing lock); `StairsPathfinder` walkable = occupied cells; Build tools (Select / Bulldoze / PlaceRoom)  
**Parent:** Tower structure / floor planning UX  
**Follow-ups (tabled):** Scaffold refund on bulldoze; auto-collapse if support removed; corridor art; star unlock; daily upkeep

## 1. Goals

1. Let the player **place scaffolding** to fill empty cells so upper floors can be built with **gaps between rooms** on lower floors.  
2. Scaffolding is **cheap structural fill that agents can walk through** (hallway / corridor use).  
3. Expose it as an **always-visible tool** (not buried in a room family), with **click or drag-paint** placement.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Role | Structural support **and** walkable corridor |
| Cost | **$500** per 1×1 cell (cheap, not free) |
| Catalog UX | **Dedicated tool** outside families (with Selector / Bulldoze) |
| Placement | **Click or drag-paint**; charge per successful cell |
| Demolish | Keep load-bearing lock; non-load-bearing may bulldoze; rooms still rebuild over scaffolding |
| Refund on bulldoze | **None** in v1 |
| Auto scaffolding from demolish | Unchanged — same type / look |
| Star gate / upkeep | **None** |

## 3. Tool and asset

### `BuildTool.Scaffold`

- New enum value beside `Select` / `Bulldoze` / `PlaceRoom`.  
- HUD: always-visible button on the core tool strip (glyph **Sc** / label **Scaffold**).  
- Selecting it clears any selected room type and enters scaffold paint mode.  
- Hint text: e.g. “Scaffold ($500): click or drag empty supported cells. Walkable fill; supports floors above.”

### `RoomTypeSO` scaffolding

- Prefer a single shared type used by demolish auto-fill **and** player place (runtime `CreateScaffoldingType` today, or a Resources asset — implementation may promote to Resources for HUD cost display; behavior identical).  
- Fields: `id = scaffolding`, `isScaffolding = true`, `category = Structure`, `size = 1×1`, `buildCost = 500`, `incomeModel = None`, `maxOccupants = 0`, allow above-ground + basement, existing tan `placeholderColor`.  
- `CanPlace` currently **rejects** `isScaffolding` — player place uses a dedicated path (`CanPlaceScaffold` / `TryPlaceScaffold`), not the normal room `CanPlace` gate.

## 4. Placement rules

Valid cell when all hold:

1. Has lobby.  
2. Cell empty (no room; not Floor G / lobby floor).  
3. In tower X bounds.  
4. Floor allowed for structure (above-ground and basement OK; not lobby floor).  
5. `HasSupportFromAdjacentLevel` — same support chain as rooms (grow from lobby / dirt / existing structure).  
6. Funds ≥ $500 for that cell (checked per cell while painting).

Invalid cells during a drag are skipped (no charge). Valid cells: place 1×1 scaffold instance, debit $500, paint structure tilemap.

Grace refund window: scaffolding is **not** a grace-refundable room in v1 (or treat like structure with no refund — pick simplest: no grace refund).

## 5. Walkability and systems

- `StairsPathfinder` already marks **all occupied cells** walkable — player scaffolding participates automatically after `TransitRouter.Rebuild` / pathfinder rebuild on place.  
- `AgentSystem.SyncHomes` already skips `isScaffolding` — no residents.  
- No income, condition decay, or service jobs on scaffolding.  
- Scaffolding continues to count for `HasSupportFromAdjacentLevel` / load-bearing checks via `_cells`.

## 6. Demolish / replace

Unchanged rules:

- Cannot bulldoze scaffolding that is still **load-bearing** (`IsLoadBearingCell`).  
- Non-load-bearing scaffolding may be bulldozed ($0 refund).  
- Placing a normal room on scaffolding clears those studs and registers the room (existing path).  
- Demolishing a room under occupied floors may still **spawn** auto-scaffolding as today.

## 7. HUD / UX

- Core strip: Selector · Scaffold · Bulldoze (order flexible; Scaffold near Bulldoze is fine).  
- Drag ghost: highlight valid cells green-ish / invalid red-ish consistent with place-room ghosts.  
- Selection: clicking scaffolding in Select tool may show “Scaffolding · $500 build · structural / walkable” (optional but useful).  
- README: note player-built scaffolding tool and $500 cost.

## 8. Testing

EditMode (net8 hosts OK if Editor busy):

1. `CanPlaceScaffold` / `TryPlaceScaffold` — empty supported cell succeeds; unsupported / occupied / lobby floor fails.  
2. Placing scaffold under a gap allows a room on the floor above that column.  
3. Pathfinder: two rooms separated by scaffolding cells are connected horizontally.  
4. Load-bearing scaffold cannot demolish; isolated scaffold can.  
5. Wallet decrements $500 per placed cell; drag of N valid cells costs `N * 500`.  
6. Auto demolish-scaffolding regression still green.

## 9. Success criteria

1. Player can click/drag-place scaffolding from an always-visible tool.  
2. Each cell costs $500 when placed.  
3. Scaffolding supports building above and is walkable.  
4. Load-bearing demolish lock and room-over-scaffold replace still work.  
5. README documents the tool.

## 10. Non-goals

- Scaffold refunds or grace refund.  
- Auto-collapse / cascading demolish when support removed.  
- Multi-cell prefab sizes (only 1×1 paint).  
- Separate “hallway” room type with art.  
- Star unlock or daily upkeep.  
- Changing condo/office/hotel catalogs.
