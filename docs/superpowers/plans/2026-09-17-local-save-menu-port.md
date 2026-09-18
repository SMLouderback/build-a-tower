# Local Save Menu Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the working local `.batsave` foundation from `feature/cloud-save-accounts` onto the current game branch and wire Save/Load into the pause menu and main menu so players can manually persist and resume towers offline.

**Architecture:** Copy the Persistence layer and menu presenters from the cloud-save worktree, merge restore hooks into current `TowerGrid` / `GameSession` / HUD / main menu, keep schema v1 with middle-ground fields added only where current systems already expose capture/restore. Agents and elevator queues still regenerate after load.

**Tech Stack:** Unity 6000.4.7f1, C#, `JsonUtility`, GZip, SHA-256, UIToolkit main menu, IMGUI pause menu, NUnit EditMode/PlayMode

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-17-cloud-save-accounts-design.md` (slices 1–2 only).
- Port source: `Build-A-Tower/.worktrees/cloud-save-accounts` on `feature/cloud-save-accounts`.
- Target Windows desktop; game remains playable offline with no account.
- Schema version starts at `1`.
- Persist room types by `RoomTypeSO.id` only.
- Restore exact `RoomInstance.InstanceId` values; do not spend funds or fire normal construction callbacks during restore.
- Local save = current file + exactly three recovery copies; atomic temp→replace writes.
- Corrupt/unsupported saves preserved and reported; never silently overwritten.
- Manual saves only (no timed autosave in this plan).
- Regenerate agents, elevator cars/queues after structural restore.
- No AccountClient, SyncCoordinator, API, or cloud UI in this plan.
- Follow-on plans (same spec): API+accounts, 3-slot sync, conflict/recovery/export/deletion.

## File Map

**Copy from worktree (create on current branch):**

- `Assets/Scripts/Persistence/SaveContracts.cs`
- `Assets/Scripts/Persistence/SaveSerializer.cs`
- `Assets/Scripts/Persistence/RoomTypeRegistry.cs`
- `Assets/Scripts/Persistence/TowerSnapshotMapper.cs`
- `Assets/Scripts/Persistence/LocalSaveRepository.cs`
- `Assets/Scripts/Persistence/SaveCoordinator.cs`
- `Assets/Scripts/UI/LocalSaveMenuPresenter.cs` (includes `PauseSaveService`, `LocalSaveMenuPresenter`, `RestoreFallbackPresenter`)
- EditMode tests: `SaveSerializerTests`, `RoomTypeRegistryTests`, `RoomStateSnapshotTests`, `TowerGridRestoreTests`, `TowerSnapshotMapperTests`, `LocalSaveRepositoryTests`, `GameSessionLoadTests`, `MainMenuLoadFlowTests`
- PlayMode: `TowerSaveLoadSmokeTests`

**Merge carefully (already diverge on current branch):**

- `Assets/Scripts/Core/GameSession.cs` — pending load, dirty flags, restore fallback notice
- `Assets/Scripts/Core/RoomInstance.cs` — room state capture/restore
- `Assets/Scripts/Core/TowerGrid.cs` — structural import / exact IDs
- `Assets/Scripts/Core/FundsWallet.cs` — trusted restore
- `Assets/Scripts/Time/GameClock.cs` — clock capture/restore
- `Assets/Scripts/Economy/StarSystem.cs` — `ForceStars` restore path
- `Assets/Scripts/Build/BuildController.cs` — restore before sim construction; repaint
- `Assets/Scripts/Simulation/TowerSimulation.cs` — restore clock/stars; complete pending load
- `Assets/Scripts/UI/TowerHudController.cs` — pause Save/Load + dirty quit warning
- `Assets/Scripts/UI/MainMenuController.cs` + `MainMenu.uxml` + `MainMenu.uss` — Load panel; remove stubs

---

### Task 1: Copy Persistence core + serializer tests

**Files:**
- Create: all six `Assets/Scripts/Persistence/*.cs` (+ `.meta` from worktree or let Unity regenerate)
- Create: `Assets/Tests/EditMode/SaveSerializerTests.cs`
- Create: `Assets/Tests/EditMode/RoomTypeRegistryTests.cs`

**Interfaces:**
- Produces: `SaveSchema.CurrentVersion == 1`
- Produces: `SaveSerializer.Serialize` / `Deserialize`
- Produces: `RoomTypeRegistry` lookup by `RoomTypeSO.id`
- Produces: `LocalSaveRepository`, `SaveCoordinator`, `TowerSnapshotMapper` types (wired in later tasks)

- [ ] **Step 1: Copy Persistence scripts from the worktree**

Worktree root:

`c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\.worktrees\cloud-save-accounts`

```powershell
$src = "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\.worktrees\cloud-save-accounts"
$dst = "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower"
New-Item -ItemType Directory -Force -Path "$dst\Assets\Scripts\Persistence" | Out-Null
Copy-Item "$src\Assets\Scripts\Persistence\*" "$dst\Assets\Scripts\Persistence\" -Force
Copy-Item "$src\Assets\Tests\EditMode\SaveSerializerTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
Copy-Item "$src\Assets\Tests\EditMode\RoomTypeRegistryTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
```

- [ ] **Step 2: Run EditMode filter for serializer + registry**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" `
  -runTests -testPlatform EditMode `
  -testFilter "SaveSerializerTests|RoomTypeRegistryTests" `
  -testResults ".superpowers/sdd/save-task1.xml" `
  -logFile ".superpowers/sdd/save-task1.log" -quit
```

Expected: PASS (or compile errors if Persistence references missing restore APIs — if so, stub-compile only after Task 2/3; do not weaken tests).

If Unity Editor has the project open, close it first or use Pipeline `unity command` once available.

- [ ] **Step 3: Commit**

```powershell
git add Assets/Scripts/Persistence Assets/Tests/EditMode/SaveSerializerTests.cs* Assets/Tests/EditMode/RoomTypeRegistryTests.cs*
git commit -m "feat: port local save Persistence contracts and serializer"
```

---

### Task 2: Port GameSession pending-load + dirty tracking

**Files:**
- Modify: `Assets/Scripts/Core/GameSession.cs`
- Create: `Assets/Tests/EditMode/GameSessionLoadTests.cs` (from worktree)

**Interfaces:**
- Produces: `GameSession.PrepareLoad(TowerSnapshotV1)` / `CompleteLoad` / `FailLoad` / `HasPendingLoad` / `TakePendingLoad`
- Produces: `GameSession.IsSaveDirty` / `MarkGameplaySaveDirty` / `MarkSaveClean` / `MarkSaveDirty`
- Produces: `GameSession.StartNewGame` clears pending load
- Produces: restore-fallback notice peek/consume helpers used by HUD

- [ ] **Step 1: Copy `GameSessionLoadTests` and fail them**

```powershell
Copy-Item "$src\Assets\Tests\EditMode\GameSessionLoadTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
```

Run filter `GameSessionLoadTests`. Expected: FAIL (missing APIs).

- [ ] **Step 2: Merge worktree `GameSession` into current**

Diff worktree `GameSession.cs` against current. Keep current difficulty APIs. Add pending-load fields, dirty flags, and restore-fallback notice exactly as worktree tests require. Ensure `StartNewGame` and `ResetForTests` clear pending load + dirty state.

- [ ] **Step 3: Re-run `GameSessionLoadTests`**

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/Core/GameSession.cs Assets/Tests/EditMode/GameSessionLoadTests.cs*
git commit -m "feat: add pending load and save dirty tracking to GameSession"
```

---

### Task 3: Port room/grid/wallet/clock/stars capture-restore

**Files:**
- Modify: `RoomInstance.cs`, `TowerGrid.cs`, `FundsWallet.cs`, `GameClock.cs`, `StarSystem.cs`
- Create tests from worktree: `RoomStateSnapshotTests`, `TowerGridRestoreTests`, `TowerSnapshotMapperTests`

**Interfaces:**
- Produces: `RoomInstance` capture/restore of fields in `RoomSnapshotV1`
- Produces: `TowerGrid` import that restores exact instance IDs without spending funds
- Produces: `FundsWallet` trusted balance restore
- Produces: `GameClock` capture/restore matching `ClockSnapshotV1`
- Produces: `StarSystem.ForceStars` (or equivalent) for restore
- Produces: `TowerSnapshotMapper.Capture` / `ValidateForRestore`

- [ ] **Step 1: Copy the three EditMode test files; run to see failures**

```powershell
Copy-Item "$src\Assets\Tests\EditMode\RoomStateSnapshotTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
Copy-Item "$src\Assets\Tests\EditMode\TowerGridRestoreTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
Copy-Item "$src\Assets\Tests\EditMode\TowerSnapshotMapperTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
```

- [ ] **Step 2: Port restore APIs from worktree into current types**

Three-way merge carefully: current branch has leisure/shop/amenity fields. For any new room fields present on current `RoomInstance` that affect resume (shop earnings, visit history, condo/hotel state already in worktree snapshot), keep them in `RoomSnapshotV1` / capture/restore. Do **not** yet add research/demand pool blobs unless tests require them for middle-ground (Task 7).

- [ ] **Step 3: Run the three test filters**

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/Core/RoomInstance.cs Assets/Scripts/Core/TowerGrid.cs Assets/Scripts/Core/FundsWallet.cs Assets/Scripts/Time/GameClock.cs Assets/Scripts/Economy/StarSystem.cs Assets/Tests/EditMode/RoomStateSnapshotTests.cs* Assets/Tests/EditMode/TowerGridRestoreTests.cs* Assets/Tests/EditMode/TowerSnapshotMapperTests.cs*
git commit -m "feat: restore tower structure and room state from snapshots"
```

---

### Task 4: Wire SaveCoordinator into BuildController + TowerSimulation

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Create: `Assets/Tests/EditMode/LocalSaveRepositoryTests.cs` (from worktree)

**Interfaces:**
- Consumes: `GameSession` pending load; `SaveCoordinator.SaveCurrent` / `PrepareLocalLoad`
- Produces: On sandbox boot, if pending load → restore grid/wallet/clock/stars before normal construction spend paths; repaint rooms; `CompleteLoad` or `FailLoad` with fallback notice

- [ ] **Step 1: Copy `LocalSaveRepositoryTests`; ensure repository round-trip PASS**

```powershell
Copy-Item "$src\Assets\Tests\EditMode\LocalSaveRepositoryTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
```

Run filter `LocalSaveRepositoryTests`. Expected: PASS.

- [ ] **Step 2: Port worktree restore bootstrap from `BuildController` + `TowerSimulation`**

Diff those two files on the worktree against current. Apply only save/load bootstrap hunks. After restore, agents/elevators boot empty and refill via normal sim — do not serialize them.

- [ ] **Step 3: Manual compile check / focused EditMode that construct coordinator**

If PlayMode cannot run yet, at least ensure no compile errors via Editor or batchmode empty test run.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/Build/BuildController.cs Assets/Scripts/Simulation/TowerSimulation.cs Assets/Tests/EditMode/LocalSaveRepositoryTests.cs*
git commit -m "feat: restore pending local saves during tower bootstrap"
```

---

### Task 5: Main menu Load panel (replace stubs)

**Files:**
- Modify: `Assets/Scripts/UI/MainMenuController.cs`
- Modify: `Assets/Scripts/UI/MainMenu.uxml`
- Modify: `Assets/Scripts/UI/MainMenu.uss`
- Create: `Assets/Scripts/UI/LocalSaveMenuPresenter.cs`
- Create: `Assets/Tests/EditMode/MainMenuLoadFlowTests.cs`

**Interfaces:**
- Consumes: `LocalSaveMenuPresenter.RefreshLocalSaves` / `TryPrepareLoad` / `TryPrepareRecovery`
- Produces: Load panel listing local saves; selecting a save prepares pending load and loads `TowerSandbox`
- Produces: Save button on main menu either disabled with tooltip (`SaveDisabledTooltip`) or opens explanation that saves are created from pause — match worktree behavior
- Removes: `ShowDialog("Not available yet.")` for Load

- [ ] **Step 1: Copy presenter + `MainMenuLoadFlowTests`; port UXML/USS load panel from worktree**

```powershell
Copy-Item "$src\Assets\Scripts\UI\LocalSaveMenuPresenter.cs*" "$dst\Assets\Scripts\UI\" -Force
Copy-Item "$src\Assets\Tests\EditMode\MainMenuLoadFlowTests.cs*" "$dst\Assets\Tests\EditMode\" -Force
```

Three-way merge `MainMenu.uxml` / `.uss` / `MainMenuController.cs` so current About/Contact/difficulty panels remain.

- [ ] **Step 2: Run `MainMenuLoadFlowTests`**

Expected: PASS.

- [ ] **Step 3: Commit**

```powershell
git add Assets/Scripts/UI/LocalSaveMenuPresenter.cs* Assets/Scripts/UI/MainMenuController.cs Assets/Scripts/UI/MainMenu.uxml Assets/Scripts/UI/MainMenu.uss Assets/Tests/EditMode/MainMenuLoadFlowTests.cs*
git commit -m "feat: enable main menu local save load panel"
```

---

### Task 6: Pause menu Save + Load

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Create (if not already via presenter): pause Load UI state
- Create: `Assets/Tests/PlayMode/TowerSaveLoadSmokeTests.cs` (from worktree; adapt scene names)

**Interfaces:**
- Produces: Pause options include **Save** and **Load** beside Resume / Options / Main Menu
- Produces: `SaveFromPause()` via `PauseSaveService` → `SaveCoordinator.SaveCurrent`
- Produces: Pause Load lists local saves (reuse presentation helpers); confirm then `PrepareLocalLoad` + reload `TowerSandbox` (or in-place restore if worktree did in-place — match worktree)
- Produces: Main Menu confirm uses dirty warning when `IsLocalSaveDirty`
- Produces: `ConfigureLocalSave` / `BindPauseSave` called from tower bootstrap

- [ ] **Step 1: Diff worktree `TowerHudController` pause/save hunks into current HUD**

Current HUD is large and newer (leisure, pictorial menu). Port only pause-state additions, Save/Load buttons, load list overlay, dirty quit string, and grid-changed dirty marking. Do not regress build menu.

- [ ] **Step 2: Wire `BindPauseSave` during tower HUD/sim startup** (same call site as worktree)

- [ ] **Step 3: Copy/adapt PlayMode smoke test; run if Editor available**

```powershell
Copy-Item "$src\Assets\Tests\PlayMode\TowerSaveLoadSmokeTests.cs*" "$dst\Assets\Tests\PlayMode\" -Force
```

Expected: save → reload → matching wallet/stars/day/room count.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/UI/TowerHudController.cs Assets/Tests/PlayMode/TowerSaveLoadSmokeTests.cs*
git commit -m "feat: add pause menu save and load for local towers"
```

---

### Task 7: Middle-ground snapshot extensions (research / progression)

**Files:**
- Modify: `SaveContracts.cs` (`TowerSnapshotV1` fields as needed — keep `schemaVersion = 1` if additive and old files still deserialize with defaults; if breaking, bump to `2` with explicit migration in `SaveSerializer`)
- Modify: `TowerSnapshotMapper.cs`
- Modify: restore path in `TowerSimulation` / `ResearchSystem` as required
- Test: extend `TowerSnapshotMapperTests` + one new EditMode test for research round-trip

**Interfaces:**
- Produces: capture/restore of research branch completion / in-progress state required to resume the same unlocks
- Produces: any other progression flags already authoritative on `TowerSimulation` that would otherwise reset (star-related already covered)
- Still excludes: agent list, elevator queue runtime, demand pool transient counters unless trivial and already on rooms

- [ ] **Step 1: Write failing mapper test for research (or documented progression) round-trip**

- [ ] **Step 2: Implement minimal capture/restore**

- [ ] **Step 3: Run mapper + smoke filters**

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git commit -m "feat: persist research progression in local tower snapshots"
```

---

### Task 8: Slice 1–2 verification + docs touch

**Files:**
- Modify: `README.md` only if it still says Save/Load unavailable
- Optional: short note in `.superpowers/sdd/` progress

- [ ] **Step 1: Run EditMode filters**

`SaveSerializerTests|RoomTypeRegistryTests|GameSessionLoadTests|RoomStateSnapshotTests|TowerGridRestoreTests|TowerSnapshotMapperTests|LocalSaveRepositoryTests|MainMenuLoadFlowTests`

Expected: all PASS.

- [ ] **Step 2: Play smoke (Editor): New Game → build → Esc Save → Main Menu → Load → tower matches**

- [ ] **Step 3: Update README stub copy if present; commit**

```powershell
git commit -m "docs: note local save and load are available from pause and main menu"
```

---

## Follow-on plans (same spec; do not implement in this plan)

1. **API + accounts** — ASP.NET Identity + Postgres Compose on Harvester; AccountClient; Credential Manager  
2. **3-slot sync** — SyncCoordinator; upload/download with revision precondition  
3. **Conflict / recovery / export / deletion** — conflict UI; 30-day revisions; export; account deletion  

---

## Plan self-review

| Spec slice 1–2 requirement | Task |
|----------------------------|------|
| Port local foundation onto current branch | 1–4 |
| Pause Save/Load | 6 |
| Main menu Load (replace stubs) | 5 |
| Manual saves; recovery copies; atomic writes | 1, 4 (repository) |
| Middle-ground research/progression | 7 |
| Regenerate agents/elevators | 4 (explicit non-persist) |
| Dirty quit warning | 6 |
| No cloud/API in this plan | Global constraints |

No TBD placeholders. Cloud slices deferred to follow-on plans by design.
