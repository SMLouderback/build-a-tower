# Main Menu & Difficulty Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a UIToolkit `MainMenu` entry scene with New Game → difficulty, Contact/About, Save/Load stubs, Esc pause → Main Menu, HUD difficulty chip, and Sandbox free-build (other difficulties keep today’s economy).

**Architecture:** `GameSession` holds `GameDifficulty` across scene loads. `MainMenuController` drives the entry UI; selecting a difficulty sets the session and loads `TowerSandbox`. `BuildController` skips wallet spend/afford when Sandbox. Pause overlay on the HUD pauses `TowerSimulation` clock and can return to `MainMenu`.

**Tech Stack:** Unity 6000.4.x, UIToolkit, existing `BuildController` / `TowerHudController` / `TowerSimulation.SetSpeedPreset`, NUnit EditMode (+ PlayMode smoke update).

**Spec:** `docs/superpowers/specs/2026-08-05-main-menu-difficulty-shell-design.md`

## Global Constraints

- Do not commit unless the user asks
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Prefer Subagent-Driven Development; if quota exhausted, implement inline
- No parallel-cli
- Star gates unchanged; Easy/Normal/Hard/Extreme use today’s economy (no multipliers yet)
- Sandbox: free placement only (lobby/room/elevator grow/scaffold); income/agents/stars unchanged
- Contact: `escapemobileproductions@gmail.com`, `https://escapeproductions.biz/`
- Copyright: `© 2026 Escape Productions. All rights reserved.`
- Save/Load → “Not available yet.” dialog only

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Core/GameDifficulty.cs` | Enum Sandbox…Extreme |
| `Assets/Scripts/Core/GameSession.cs` | Static difficulty; EnsureDefault; StartNewGame |
| `Assets/Scripts/Build/BuildController.cs` | Sandbox skip spend/afford |
| `Assets/Scripts/UI/MainMenu.uxml` / `MainMenu.uss` | Menu layout + styles |
| `Assets/Scripts/UI/MainMenuController.cs` | Root / difficulty / Contact / About / stubs |
| `Assets/Scenes/MainMenu.unity` | Entry scene + UIDocument |
| `ProjectSettings/EditorBuildSettings.asset` | MainMenu index 0, TowerSandbox index 1 |
| `Assets/Scripts/UI/TowerHudController.cs` (+ uxml/uss as needed) | Difficulty chip, Menu button, pause overlay |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Already has `SetSpeedPreset` — pause uses it |
| `Assets/Tests/EditMode/GameSessionTests.cs` | Session defaults + StartNewGame |
| `Assets/Tests/EditMode/SandboxBuildTests.cs` | Free place when Sandbox |
| `Assets/Tests/PlayMode/TowerSandboxBuildSmokeTests.cs` | Still works with unset → Normal |
| `README.md` | How to launch via MainMenu |
| Spec → Implemented |

---

### Task 1: GameSession + difficulty enum

**Files:**
- Create: `Assets/Scripts/Core/GameDifficulty.cs` (+ `.meta` via Unity or copy GUID pattern)
- Create: `Assets/Scripts/Core/GameSession.cs`
- Create: `Assets/Tests/EditMode/GameSessionTests.cs` (+ `.meta`)

**Interfaces:**
- Produces: `enum GameDifficulty { Sandbox, Easy, Normal, Hard, Extreme }`
- Produces: `GameSession.Difficulty` (get/set), `GameSession.EnsureDefault()`, `GameSession.StartNewGame(GameDifficulty difficulty)`, `GameSession.ResetForTests()`
- Consumes: nothing

- [x] **Step 1: Write failing tests**

```csharp
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class GameSessionTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        [TearDown]
        public void TearDown() => GameSession.ResetForTests();

        [Test]
        public void EnsureDefault_sets_Normal_when_unset()
        {
            GameSession.EnsureDefault();
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.IsTrue(GameSession.HasDifficulty);
        }

        [Test]
        public void StartNewGame_sets_difficulty()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            Assert.AreEqual(GameDifficulty.Sandbox, GameSession.Difficulty);
        }

        [Test]
        public void EnsureDefault_does_not_overwrite_existing()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            GameSession.EnsureDefault();
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
        }
    }
}
```

- [x] **Step 2: Run — expect FAIL** (types missing)

Run EditMode filter `GameSessionTests` (Unity batchmode or Editor Test Runner).

- [x] **Step 3: Implement**

`GameDifficulty.cs`:

```csharp
namespace BuildATower
{
    public enum GameDifficulty
    {
        Sandbox = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3,
        Extreme = 4
    }
}
```

`GameSession.cs`:

```csharp
namespace BuildATower
{
    public static class GameSession
    {
        static bool _hasDifficulty;
        static GameDifficulty _difficulty;

        public static bool HasDifficulty => _hasDifficulty;

        public static GameDifficulty Difficulty
        {
            get
            {
                EnsureDefault();
                return _difficulty;
            }
            set
            {
                _difficulty = value;
                _hasDifficulty = true;
            }
        }

        public static bool IsSandbox => Difficulty == GameDifficulty.Sandbox;

        public static void EnsureDefault()
        {
            if (_hasDifficulty) return;
            _difficulty = GameDifficulty.Normal;
            _hasDifficulty = true;
        }

        public static void StartNewGame(GameDifficulty difficulty)
        {
            _difficulty = difficulty;
            _hasDifficulty = true;
        }

        public static void ResetForTests()
        {
            _hasDifficulty = false;
            _difficulty = GameDifficulty.Normal;
        }
    }
}
```

- [x] **Step 4: Re-run — expect PASS**

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 2: Sandbox free-build in BuildController

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Create: `Assets/Tests/EditMode/SandboxBuildTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `GameSession.IsSandbox`
- Produces: spend/afford helpers that no-op cost when Sandbox; public place APIs unchanged

- [x] **Step 1: Write failing tests**

```csharp
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SandboxBuildTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        [TearDown]
        public void TearDown() => GameSession.ResetForTests();

        [Test]
        public void Sandbox_TrySpendForBuild_does_not_debit()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            var wallet = new FundsWallet(1000);
            Assert.IsTrue(BuildEconomy.TrySpendForBuild(wallet, 500));
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Normal_TrySpendForBuild_debits()
        {
            GameSession.StartNewGame(GameDifficulty.Normal);
            var wallet = new FundsWallet(1000);
            Assert.IsTrue(BuildEconomy.TrySpendForBuild(wallet, 500));
            Assert.AreEqual(500, wallet.Balance);
        }

        [Test]
        public void Sandbox_CanAffordBuild_always_true()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            var wallet = new FundsWallet(0);
            Assert.IsTrue(BuildEconomy.CanAffordBuild(wallet, 99999));
        }
    }
}
```

- [x] **Step 2: Run — expect FAIL**

- [x] **Step 3: Implement `BuildEconomy` + wire BuildController**

Create `Assets/Scripts/Build/BuildEconomy.cs`:

```csharp
namespace BuildATower
{
    public static class BuildEconomy
    {
        public static bool CanAffordBuild(FundsWallet wallet, int cost)
        {
            if (GameSession.IsSandbox) return true;
            return wallet != null && wallet.CanAfford(cost);
        }

        public static bool TrySpendForBuild(FundsWallet wallet, int cost)
        {
            if (GameSession.IsSandbox) return true;
            return wallet != null && wallet.TrySpend(cost);
        }

        public static void RefundBuild(FundsWallet wallet, int cost)
        {
            if (GameSession.IsSandbox) return;
            wallet?.Add(cost);
        }
    }
}
```

In `BuildController`, replace build-placement spend/afford/refund-on-failed-place paths with `BuildEconomy.*` (scaffold, lobby place/extend, room place, elevator grow/edge). **Do not** change demolish refund / grace paths.

Ghost validity checks that use `Wallet.CanAfford` for **placement** must use `BuildEconomy.CanAffordBuild`.

At `BuildController` / simulation start (Awake or Start), call `GameSession.EnsureDefault()`.

- [x] **Step 4: Re-run SandboxBuildTests + existing placement tests — expect PASS**

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 3: MainMenu UI + scene + Build Settings

**Files:**
- Create: `Assets/Scripts/UI/MainMenu.uxml`
- Create: `Assets/Scripts/UI/MainMenu.uss`
- Create: `Assets/Scripts/UI/MainMenuController.cs`
- Create: `Assets/Scenes/MainMenu.unity` (UIDocument + EventSystem/camera as needed)
- Modify: `ProjectSettings/EditorBuildSettings.asset` — MainMenu first, TowerSandbox second
- Create: `Assets/Tests/EditMode/MainMenuControllerTests.cs` — pure logic helpers if extracted; otherwise manual Play Mode checklist in task report

**Interfaces:**
- Consumes: `GameSession.StartNewGame`, `Application.version`, `Application.OpenURL`
- Produces: `MainMenuController` wired to buttons; `StartTower(GameDifficulty)` loads `"TowerSandbox"`

- [x] **Step 1: Author UXML/USS**

UXML structure (names must match controller queries):

- `panel-root` — New Game, Save Game, Load Game, Contact Us, About  
- `panel-difficulty` — hidden by default; Sandbox/Easy/Normal/Hard/Extreme + Back  
- `panel-contact` — email button, website button, Back  
- `panel-about` — blurb label, version label, copyright label, Back  
- `panel-dialog` — message label + OK (for Save/Load stub)

USS: full-bleed dark teal/slate gradient background; large **Build-A-Tower** title; stacked buttons (~48px height); readable sans (project may use UIToolkit default — avoid Inter-as-brand if adding a font asset is easy; otherwise system is OK for this placeholder slice). Brand title must dominate the first viewport.

- [x] **Step 2: Implement MainMenuController**

```csharp
// Key behaviors:
void OnNewGame() => ShowOnly(panelDifficulty);
void OnDifficulty(GameDifficulty d) {
  GameSession.StartNewGame(d);
  SceneManager.LoadScene("TowerSandbox");
}
void OnSave() / OnLoad() => ShowDialog("Not available yet.");
void OnContact() => ShowOnly(panelContact);
void OnAbout() {
  versionLabel.text = $"Version {Application.version}";
  copyrightLabel.text = "© 2026 Escape Productions. All rights reserved.";
  ShowOnly(panelAbout);
}
void OnEmail() => Application.OpenURL("mailto:escapemobileproductions@gmail.com");
void OnWebsite() => Application.OpenURL("https://escapeproductions.biz/");
```

Difficulty hints (label under buttons or button tooltip text):

- Sandbox: “Free builds — test layouts without money pressure.”
- Easy/Normal/Hard/Extreme: “Economy tuning coming soon.” (Normal remains default feel today.)

- [x] **Step 3: Create MainMenu scene + Build Settings**

- Empty scene, orthographic/camera optional (UIToolkit PanelSettings can overlay).  
- GameObject `MainMenu` with `UIDocument` (assign MainMenu.uxml + reuse or clone `TowerHudPanelSettings`) + `MainMenuController`.  
- Save as `Assets/Scenes/MainMenu.unity`.  
- EditorBuildSettings enabled scenes:

```
0: Assets/Scenes/MainMenu.unity
1: Assets/Scenes/TowerSandbox.unity
```

- [x] **Step 4: Manual smoke**

Play from MainMenu → New Game → Normal → tower loads. Save/Load show dialog. Contact/About work. Back navigation works.

- [ ] **Step 5: Commit** (only if user asked)

---

### Task 4: Pause overlay + HUD difficulty chip

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Scripts/UI/TowerHud.uxml` and/or draw IMGUI overlay consistent with existing HUD patterns (HUD currently mixes UIToolkit shell + IMGUI chips — follow the **existing** pattern for pause: IMGUI overlay is OK if that matches speed controls)
- Modify: `Assets/Scripts/Build/BuildController.cs` if input must ignore build clicks while paused

**Interfaces:**
- Consumes: `GameSession.Difficulty`, `TowerSimulation.SetSpeedPreset`, `SceneManager`
- Produces: pause state; Menu / Esc behavior per spec §5

- [x] **Step 1: Add pause state machine**

States: `Playing` | `Paused` | `ConfirmQuit`

- Esc / HUD **Menu** when Playing → Paused (store prior clock speed; `simulation.SetSpeedPreset(priorSpeed, paused: true)` or `SetSpeedPreset(0, true)` matching existing pause preset).  
- Esc / Resume when Paused → Playing (restore prior speed, unpaused).  
- Main Menu when Paused → ConfirmQuit.  
- Yes → `SceneManager.LoadScene("MainMenu")`.  
- No / Esc on ConfirmQuit → Paused.

While `Paused` or `ConfirmQuit`: `BuildController` must not place/demolish (early-out in Update click handlers when `hud.IsPaused` or a shared `GamePause.IsPaused` flag).

- [x] **Step 2: HUD UI**

- Difficulty chip text: `GameSession.Difficulty.ToString()` (e.g. `Sandbox`).  
- **Menu** control near time presets.  
- Pause panel copy: Resume / Main Menu; confirm: “Leave tower? Progress is not saved.” Yes / No.

- [x] **Step 3: Manual smoke**

Sandbox run: Esc → Resume; Esc → Main Menu → confirm → MainMenu. Difficulty chip correct.

- [ ] **Step 4: Commit** (only if user asked)

---

### Task 5: README, PlayMode smoke, spec status

**Files:**
- Modify: `README.md`
- Modify: `Assets/Tests/PlayMode/TowerSandboxBuildSmokeTests.cs` — call `GameSession.ResetForTests()` / `EnsureDefault()` in setup so Normal applies
- Modify: `docs/superpowers/specs/2026-08-05-main-menu-difficulty-shell-design.md` → **Status: Implemented**

- [x] **Step 1: README**

Document:

1. Open project; Play starts on **MainMenu** (Build Settings).  
2. New Game → choose difficulty → tower.  
3. Esc / Menu for pause; Sandbox builds are free.  
4. Direct play of `TowerSandbox` still OK (defaults to Normal).

- [x] **Step 2: Update PlayMode smoke setup**

```csharp
[SetUp]
public void SetUp()
{
    GameSession.ResetForTests();
    GameSession.EnsureDefault(); // Normal
}
```

Ensure scene load still finds BuildController; funds still debit under Normal.

- [x] **Step 3: Mark spec Implemented**

- [x] **Step 4: Final checklist vs spec §8**

- [ ] **Step 5: Commit** (only if user asked)

---

## Execution

After plan approval, start Subagent-Driven Development immediately (user preference). If subagent quota is exhausted, continue inline task-by-task with the same gates.
