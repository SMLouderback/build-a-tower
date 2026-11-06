# Pictorial Build Menu Design

**Status:** Implemented  
**Date:** 2026-09-13

## 1. Goal

Clean up and modernize the build menu so it is pictorial and SimTower-like:

1. **Remove** legacy **Office**, **Hotel**, and **Condo** from the menu (first `Of` / `Ho` / `Co` entries — old 9×1 / purple 4×1 / green 16×1).
2. **Replace** text glyphs with **dedicated square icons** for tools, family folders, shop subgroups, and every placeable room.
3. **Reshape** the build panel into a denser **2-column icon strip** (tools + top-level facilities), then a variant pane for Office / Hotel / Condo / shop choices.
4. Icons should read **without English**; tooltips keep English names for detail.

## 2. Layout

Build panel (SimTower-inspired, still IMGUI):

```
┌─────────────┐
│ Tools 2-col │  Selector | Lobby
│             │  Sky Lobby | Scaffold
│             │  Bulldoze  | (empty / future)
├─────────────┤
│ Families    │  Office | Hotel
│ 2-col       │  Condo  | Shops
│             │  Utility| Transit
├─────────────┤
│ Variants    │  When a family (or Shops
│ grid        │  subgroup) is selected —
│             │  rooms in that bucket as
│             │  square icons (scroll OK)
└─────────────┘
```

- Click family → expand variants (Shops: Food / Retail row, then rooms).
- Click room → select build tool + type (same as today).
- Selection outline on active tool / family / room.
- Hover tooltip: English name, cost, size.
- Help text may sit above or below the strip.

## 3. Art

- **Style:** Warm dollhouse / SimTower mini-interior icons matching Build-A-Tower (not raw 1995 pixels). Clear silhouette at small size.
- **Format:** Square sources (64×64 or 128×128), magenta key optional; **no English text on the image**.
- **Location:** `Assets/Resources/Art/Menu/` with stable names (`tool_select`, `family_office`, `office_micro`, …).
- **Coverage:** Tools, family folders, shop subgroups (Food/Retail), every catalog room (9 offices + 9 hotels + 9 condos + shops + utility + transit).
- **Missing icon:** Color-tile fallback; prefer shipping icons for all catalog entries in this pass.
- **Excluded:** Legacy Office / Hotel / Condo assets are not used for icons and leave the menu.

## 4. Wiring

- Clear / stop loading legacy Office, Hotel, Condo from `placeableRooms` (and any other path that adds them to the catalog).
- Keep loading luxury 9+9+9 + shops/utility/transit via `EnsureElevatorAndCatalog`.
- Add `MenuIconArt` (or similar): load `Art/Menu/{id}` → Texture for IMGUI.
- Rewrite build-panel draw in `TowerHudController`: tools → families → variants; textures instead of 2-letter glyphs.
- `BuildCatalog` remains the data model; presentation only changes.
- Tests: legacy types absent from catalog; icon loader true when asset present / false when missing.

## 5. Out of scope

- Redesigning the entire HUD chrome (stars, money, pause) beyond the build catalog strip.
- Localizing tooltip strings.
- Regenerating dollhouse tower-view art.
- Changing placement / economy rules for rooms.

## 6. Success criteria

1. Menu shows 9 offices, 9 hotels, 9 condos (no legacy trio).
2. Tools, families, subgroups, and rooms use pictorial icons.
3. Layout is a readable 2-column SimTower-like strip + variants.
4. Missing icon does not break the HUD.
5. Focused EditMode tests for catalog membership + icon loading pass.
