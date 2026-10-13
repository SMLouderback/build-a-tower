# Build-A-Tower — New Player Tutorial (Guided Coach)

**Date:** 2026-10-13  
**Status:** Approved design  
**Depends on:** `GameSession` new vs load; `BuildController` lobby drag / place; build menu Office/Hotel/Condo/Stairs/Elevator; `StarSystem.CanBuild`; Pause Options; IMGUI `TowerHudController`  
**Parent:** Economy backlog — tutorial tips + prefs toggle  
**Follow-ups:** Localized copy; advanced coaches (metro/security/maps); cloud sync of prefs

## 1. Goals

1. Guide new players through lobby → living room → stairs → elevator place/extend.  
2. Auto-start on **new** Sandbox / Easy / Normal games only (not loads; not Hard/Extreme).  
3. Non-blocking soft coach: pulse + arrow + card; player may ignore and still advance when goals are met.  
4. Separate Options prefs: **Guided tutorial** and **Hover tips**.  
5. Skip ends coach for the current run only.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Architecture | Step machine + HUD coach overlays |
| Difficulties | Sandbox, Easy, Normal only |
| Re-run | Every eligible new game while Guided pref is on |
| Load save | Never auto-start |
| Ignore | Soft coach; objectives complete however achieved |
| Skip | Ends this run; prefs unchanged |
| Prefs | Two toggles: Guided tutorial, Hover tips (default both on) |
| Attention | Pulse + arrow + coach card (no full-screen dim) |
| Living step | Any Office **or** Condo **or** Hotel |
| Elevator | Wait until 1★ unlock (or shaft already exists), then place + extend |

## 3. Architecture

| Module | Role |
|--------|------|
| `TutorialPrefs` | `PlayerPrefs` for guided + hover tips |
| `TutorialDirector` | Steps, evaluate/advance, Skip, run-active flag |
| `TutorialHighlight` | Target + copy for HUD |
| `TowerHudController` | Coach UI, pulse/arrow, Options toggles, tip gating |
| `GameSession` / boot | One-shot start flag on `StartNewGame`; cleared on load |

## 4. Steps & completion

| Step | Complete when |
|------|----------------|
| LobbyTool | Lobby tool selected or lobby exists |
| PlaceLobby | `HasLobby` |
| PlaceLiving | ≥1 Office/Condo/Hotel room |
| PlaceStairs | ≥1 stairs room |
| WaitElevator | Stars ≥ 1 or any elevator shaft exists |
| PlaceElevator | ≥1 elevator shaft |
| ExtendElevator | Any shaft spans ≥2 floors (or grew vs step snapshot) |
| Done | Brief ack then hide |

Soft advance: when a later objective is already true, skip ahead when that step becomes current. No input locks.

## 5. UI

- Coach card: short copy + **Skip tutorial**; non-modal.  
- Pulse outline on UI/world target; chevron toward target.  
- Hover tips only if `HoverTipsEnabled`.  
- Pause → Options: two toggles, persist immediately.

## 6. Persistence

| Key | Default |
|-----|---------|
| `bat.tutorial.guided` | on |
| `bat.tutorial.hoverTips` | on |

Run step state is not saved in tower snapshots.

## 7. Out of scope

Hard/Extreme tutorial; cloud pref sync; i18n/voice; metro/research/security coaches; forced tool locking; economy changes for tutorial.

## 8. Verification

### EditMode
1. Start flag: Sandbox/Easy/Normal + guided on → true; Hard/Extreme, guided off, load → false.  
2. Completion predicates for lobby/living/stairs/elevator/extend.  
3. Skip → inactive; prefs unchanged.  
4. HoverTips gates tooltip helper.

### Play Mode
New Easy game walks the sequence; Skip works; Options toggles persist; load does not show coach.
