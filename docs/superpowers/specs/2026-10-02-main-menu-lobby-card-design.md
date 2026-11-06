# Build-A-Tower — Main Menu Lobby Card Polish

**Date:** 2026-10-02  
**Status:** Approved (brainstorming)  
**Depends on:** Existing `MainMenu.uxml` / `MainMenu.uss` / `MainMenuController`  
**Engine target:** Unity UIToolkit  

## 1. Goals

Polish the main menu into a **lobby plaque** composition: brand and every root action live **inside** one bordered card. Fix overflow so options never sit outside the gold border. Elevate atmosphere without changing gameplay flows.

### Success criteria

1. Root menu: title, subtitle, and all root buttons render inside a single bordered plaque.
2. No button or label clips outside the plaque border at 1920×1080 and common laptop sizes (~1366×768).
3. Subpanels (difficulty, load, account, feedback, about, dialogs) reuse the same plaque chrome.
4. Soft lobby-atmosphere background (not flat single color); 2–3 intentional motions (hover + panel transition).
5. Existing button names / controller callbacks unchanged; EditMode menu flow tests still pass.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Direction | **B — Lobby card** |
| Brand placement | Hero **inside** the plaque (not above it) |
| Outside plaque | Atmosphere background only |
| Overflow | Shrink stack / internal scroll — never spill past border |
| Subpanels | Same plaque chrome |
| Scope | Visual / layout only — no new menu items or API work |
| Filigree | Tasteful gold border; avoid heavy ornate overload from mockup |

## 3. Layout

### Root (`panel-root`)

Structure (conceptual):

```
screen [lobby atmosphere]
  plaque / panel-root
    brand-title
    subtitle
    buttons…
```

Move `brand-title` and `subtitle` from siblings of `panel-root` into the plaque hierarchy (or wrap brand + panel in one `plaque` element that owns the border). Preferred: one `VisualElement name="plaque"` with border styles; root and other panels share class `plaque` / `panel`.

### Density

- Root has 8 buttons — reduce default button height (e.g. ~40px) and margins so the full stack fits with padding inside the plaque at 1080p.
- If still overflowing on short screens: `ScrollView` inside the plaque for the button column only; brand stays pinned at top of plaque.
- Plaque `max-height` ~90% of screen; horizontal max-width ~480–520px.

### Subpanels

Keep current panel switching. Apply the same plaque background, border, padding, title styles. Difficulty buttons remain compact two-line layout.

## 4. Visual language

| Token | Direction |
|-------|-----------|
| Background | Warm dark lobby atmosphere — gradient + subtle soft architecture (USS gradient and/or quiet texture); not neon, not purple glow |
| Plaque fill | Dark wood / charcoal (`rgba` near current panel) |
| Border | Warm gold (`~rgb(242, 214, 140)` family), 1–2px, modest corner radius |
| Brand | Gold, large, bold, centered — hero of the plaque |
| Subtitle | Muted warm gray-blue, smaller |
| Buttons | Slightly lighter fill; gold-tint border on hover |
| Motion | Button hover brightness; panel show/hide short opacity/translate (UIToolkit transitions or class toggles) |

Avoid: pink/magenta scene colors; purple neon; dashboard card grids; floating badges on the hero.

## 5. Files

| File | Change |
|------|--------|
| `Assets/Scripts/UI/MainMenu.uxml` | Nest brand/subtitle inside plaque; optional plaque wrapper |
| `Assets/Scripts/UI/MainMenu.uss` | Atmosphere, plaque, density, hover, transitions |
| `Assets/Scripts/UI/MainMenuController.cs` | Only if compact-header helpers need updating for nested brand |
| Optional | Soft background texture under `Resources` if gradient alone is weak |
| Tests | `PlaytestMenuFlowTests` / load-flow — still find buttons; add layout assert if cheap |

## 6. Out of scope

- New menu entries or reordering product features  
- Replacing account/cloud behavior  
- Full 3D lobby scene  
- Custom font asset pack (use Unity built-in / existing unless already in project)
