# Build-A-Tower — Main Menu & Difficulty Shell

**Date:** 2026-08-05  
**Status:** Implemented  
**Depends on:** Existing `TowerSandbox` scene, UIToolkit HUD (`TowerHudController`), `BuildController` / wallet spend paths, game clock pause  
**Parent:** Numeric rebalance / difficulty roadmap (menu first; economy curves next)  
**Follow-ups:** Difficulty economy multipliers + Normal rebalance; Save/Load persistence; demand/climate graphs + heatmaps; visual polish; above-ground parking (lower priority)

## 1. Goals

1. Ship a dedicated **Main Menu** as the app entry point.  
2. Let the player start a **New Game** by choosing a **difficulty**.  
3. Wire **Sandbox** so building is **free** (no wallet spend) for idea-testing.  
4. Provide **Contact Us** and **About** with real studio info.  
5. Allow return to the menu via **Esc / pause** (abandon unsaved tower).  
6. Keep Save/Load visible but not functional yet (“Not available yet”).

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Delivery order | Menu shell **before** full numeric rebalance |
| Scene model | Separate `MainMenu` scene (Build Settings index 0); `TowerSandbox` index 1 |
| UI stack | UIToolkit (same as HUD) |
| Session carrier | `GameSession` (static or DontDestroyOnLoad) holding `Difficulty` |
| Difficulties | Sandbox, Easy, Normal, Hard, Extreme |
| This-slice economy | **Sandbox = free builds**; Easy/Normal/Hard/Extreme = **today’s** costs/economy |
| Star gates | **Unchanged** on all difficulties |
| Save / Load | Visible; click → “Not available yet” dialog |
| Contact Us | Email + website (open system mail / browser) |
| About | Game blurb + `Application.version` + copyright line |
| Copyright | `© 2026 Escape Productions. All rights reserved.` |
| Contact email | `escapemobileproductions@gmail.com` |
| Website | `https://escapeproductions.biz/` |
| Return to menu | Esc (and HUD Menu) → pause overlay → confirm abandon → load MainMenu |
| Editor Play on TowerSandbox | If `GameSession` unset, use **Normal**; if already set in this Editor play, keep it |
| Look | Readable placeholder atmosphere; final visual polish later |

## 3. Scene flow & session

### Build Settings

1. `Assets/Scenes/MainMenu.unity` — entry  
2. `Assets/Scenes/TowerSandbox.unity` — play

### `GameSession`

- Holds at least: `Difficulty` enum (`Sandbox`, `Easy`, `Normal`, `Hard`, `Extreme`).  
- Default if missing when tower boots: **`Normal`**.  
- **New Game:** set difficulty from submenu → `SceneManager.LoadScene("TowerSandbox")`.  
- **Main Menu from pause:** load `MainMenu`; tower state discarded (no persistence in this slice).  
- Survives the MainMenu → TowerSandbox load (static or DontDestroyOnLoad). Returning to MainMenu may reset ephemeral run state but keeps nothing saved.

### Direct Editor play

- Playing `TowerSandbox` without going through the menu must still run. If difficulty was never set, use **Normal**.

## 4. Main menu UI

### Root screen

- Brand-forward title: **Build-A-Tower** (hero-level on the first viewport).  
- Buttons:
  - **New Game** → difficulty submenu  
  - **Save Game** → dialog “Not available yet.”  
  - **Load Game** → dialog “Not available yet.”  
  - **Contact Us** → contact panel  
  - **About** → about panel  

### New Game submenu

- Title: **Choose difficulty**  
- Buttons: **Sandbox**, **Easy**, **Normal**, **Hard**, **Extreme**  
- One-line hints:
  - Sandbox — free builds; test layouts without money pressure  
  - Easy / Normal / Hard / Extreme — “economy tuning coming soon” (or equivalent); Normal is the default feel today  
- **Back** → root menu  

### Contact Us panel

- Show email and website.  
- Activating email opens the system mail client to `escapemobileproductions@gmail.com`.  
- Activating the URL opens the system browser to `https://escapeproductions.biz/`.  
- **Back** → root menu  

### About panel

- Short game description (SimTower-inspired tower builder; build, house tenants, grow stars).  
- Version from `Application.version`.  
- Copyright: `© 2026 Escape Productions. All rights reserved.`  
- **Back** → root menu  

### Visual baseline

- UIToolkit layout; atmospheric background (gradient or subtle pattern — not flat single grey).  
- No requirement for final marketing art in this slice.

## 5. In-game pause & HUD

### Pause overlay

- **Esc** or HUD **Menu**: if pause closed → open pause; if pause open (and not on confirm) → Resume.  
- Overlay actions: **Resume** · **Main Menu**.  
- **Main Menu** requires confirm: “Leave tower? Progress is not saved.”  
  - Yes → load `MainMenu`  
  - No (or Esc on confirm) → stay on pause overlay  
- While paused, simulation clock is paused (reuse existing pause / time-scale controls where present).

### Difficulty chip

- HUD shows current `GameSession.Difficulty` (e.g. `Sandbox`, `Normal`).

## 6. Sandbox free-build behavior

When `Difficulty == Sandbox`:

- Room place, lobby place/extend, elevator shaft grow/edge adjust, and scaffold place **do not debit** the wallet and **do not require** affordability checks.  
- Demolish, grace refunds, income simulation, agents, crime, and **star gates** behave as today.  
- Wallet may still exist and display; income can still accrue (no requirement to freeze economy beyond free placement).

When `Difficulty` is Easy / Normal / Hard / Extreme:

- All spend/afford paths use **current** costs and economy (no multipliers yet).

## 7. Non-goals (this slice)

- Save / load persistence  
- Easy/Hard/Extreme starting funds or cost/income multipliers  
- Rebalancing Normal room costs / fill / star *feel* via economy numbers (next slice; star *thresholds* stay fixed)  
- Demand/climate graphs and heatmaps  
- Final visual / audio polish  
- Above-ground parking  

## 8. Acceptance criteria

1. App/Editor build entry opens **MainMenu**, not an empty tower.  
2. New Game → each difficulty → tower loads; HUD shows that difficulty.  
3. Sandbox: placing rooms / scaffold / lobby extend does not reduce funds.  
4. Normal (and Easy/Hard/Extreme): placement still spends as today.  
5. Save Game / Load Game show “Not available yet.”  
6. Contact Us opens or presents mail + website correctly; About shows version + copyright.  
7. Esc / Menu → confirm → returns to MainMenu; Resume continues the run.  
8. Existing TowerSandbox PlayMode smoke still runnable (Normal default when session unset).

## 9. Implementation sketch (non-binding)

| Piece | Responsibility |
|-------|----------------|
| `GameDifficulty` enum + `GameSession` | Session state across scenes |
| `MainMenuController` + UXML/USS | Root, submenu, Contact, About, stub dialogs |
| `MainMenu.unity` | Entry scene + UIDocument |
| `PauseMenuController` (or HUD extension) | Esc overlay, confirm, clock pause |
| `BuildController` (+ related spend sites) | Skip spend/afford when Sandbox |
| Build Settings / README | Entry scene + how to start a run |
| Tests | Sandbox free place; session difficulty default; optional menu smoke |

## 10. Roadmap reminder

After this slice: **numeric rebalance** (difficulty multipliers + Normal tune for too-rich / uneven rooms / star feel; star gates fixed) → **demand/climate + heatmaps** → **visual polish**. Above-ground parking remains lower priority.
