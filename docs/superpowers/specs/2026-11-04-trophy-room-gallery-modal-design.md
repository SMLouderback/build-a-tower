# Build-A-Tower — Trophy Room cutaway scale + gallery modal

**Date:** 2026-11-04  
**Status:** Approved (brainstorm sections)  
**Related:** `docs/superpowers/specs/2026-11-03-reward-challenge-trophy-design.md` §9 Trophy Room

## Problem

The placed Trophy Room dollhouse plate reads too zoomed-in on the tower cutaway relative to peer rooms. Players need a tower-scale shell in the building, plus a closable close-up to inspect trophies.

## Goals

1. Tower cutaway uses a **wide-shot static gallery shell** at the same scale/perspective as other 6×1 dollhouse rooms.
2. Cutaway still shows **tiny medal hints** that update from `TrophyCase` (empty silhouette vs filled metal).
3. A **floating close-up gallery window** shows a larger interior with readable pedestals; Close + Esc dismiss it.
4. Open paths: Info **View trophies**; **double-click** a Trophy Room; **single-click** when that room is **already selected**.
5. Opening the modal **does not force pause** — leave current pause/speed unchanged.
6. While open, block world build input (same spirit as other HUD modals).

## Non-goals

- Sim buffs from Trophy Room (still vanity-only).
- Changing trophy claim / Reward Book / metal progression rules.
- Auto-opening on first trophy (tutorial modal stays separate).
- Camera zoom onto the tower tile as the “popup.”
- Replacing the modal with the Reward Book Trophies tab.

## Approach

**Dual art + IMGUI gallery modal:** retarget cutaway art to a wide shell with small chips; modal uses a larger gallery plate + full pedestal sprites from existing `TrophyRoomDisplay` keys.

## Tower cutaway

- Footprint remains **6×1** (`service_trophy_room`).
- Resource leaf stays `trophy_room_6x1` (or a clearly named wide sibling if crop/replace is cleaner — one leaf mapped in `RoomDollhouseArt`).
- Shell art: warm architectural dollhouse gallery; magenta `#FF00FF` key plate only; not pink/toy.
- Overlay **small medal chips** on fixed UV/world anchors per `ChallengeSubject` pedestal, driven by `TrophyRoomDisplay.BuildPedestals(TrophyCase)`.
- Multiple placed rooms mirror the **same** case.
- Metal-upgrade **burst FX** still anchors to placed Trophy Room centers; news/badge/tutorial unchanged.
- Demolishing the room does not clear trophies.

## Gallery modal

- New UI helper (e.g. `TrophyRoomGalleryModal`): `IsModalOpen`, `TryOpen`, `Dismiss`, Esc handling.
- Visual: large gallery interior + readable pedestals (subject + metal art; empty = silhouette). Title “Trophy Room”; **Close** button + **Esc**.
- Wired into `TowerHudController.BlocksWorldInput` when open.
- **No** calls to pause/speed APIs on open or close.
- Selection may remain after close; modal state alone clears.

### Open rules

| Input | Result |
|-------|--------|
| Info panel **View trophies** while selected room is Trophy Room | Open modal |
| Select tool **double-click** on a placed Trophy Room | Select (if needed) + open modal |
| Select tool **single-click** on a Trophy Room that is already `SelectedRoom` | Open modal |
| Select tool **first** single-click on a Trophy Room | Select only; fill Info; do **not** open |

## Architecture

| Piece | Role |
|-------|------|
| Dollhouse / paint path | Wide shell + small chips in cutaway |
| `TrophyRoomDisplay` | Pedestal mapping + sprite keys (shared cutaway chips + modal) |
| `TrophyRoomGalleryModal` | Open/close/Esc; draw body |
| `TowerHudController` | Draw modal; Info button; BlocksWorldInput |
| `BuildController` | Detect double-click / second-click on Trophy Room → request open |

**Data:** read-only `TrophyCase` via existing rewards system. No new save fields.

## Testing (EditMode)

1. Modal open/close + Esc clears `IsModalOpen`.
2. When modal open, HUD BlocksWorldInput is true (or modal reports blocking).
3. Open rules: Info path; double-click; second single-click on already selected; first click does not open.
4. Pedestal mapping still matches `TrophyCase` metals.
5. Opening modal does not change clock pause/speed (assert no pause mutation in modal API / gate).

## Success criteria

- Tower Trophy Room no longer dominates as an over-cropped close-up.
- Players can inspect trophies in a closable window from Info or Select-tool clicks as specified.
- Cutaway chips and modal pedestals stay in sync with claims/upgrades.
- Burst FX and vanity-only economy behavior remain intact.
