# Utility Subgroups & Lobby Merge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nest Utility rooms into Health/Sanitation, Power, Water, and Services subgroups; keep stairs/elevators in Transit only; merge Lobby and Sky Lobby into one floor-aware tool.

**Architecture:** Extend `BuildSubgroup` and teach `BuildCatalog.Group` to nest Utility like Shops. Tag room assets (with id fallbacks). Slim the tools strip to Select / Lobby / Scaffold / Bulldoze; Lobby placement picks ground vs sky from floor.

**Tech Stack:** Unity 6000.4.7f1, C#, EditMode NUnit under `Assets/Tests/EditMode`.

**Spec:** `docs/superpowers/specs/2026-11-03-utility-subgroups-lobby-merge-design.md`

## Global Constraints

- One Utility strip family; four subgroups in order: HealthSanitation → Power → Water → Services.
- Room bucketing: Health/Sanitation = Clinic, Public Restroom, Office Restroom, Trash, Recycling, Housekeeping; Power = service_power; Water = service_pump, service_treatment; Services = Security, Mail, Loading Dock, Maintenance, Research Lab, Trophy Room.
- Tools strip: Select · Lobby · Scaffold · Bulldoze only. No Sky Lobby, Stairs, or Elevator tool icons.
- Stairs and all elevator shafts appear under Transit catalog only.
- One Lobby tool: `LobbyFloor` → ground lobby (`isLobby`); above → sky lobby (`isSkyLobby`); sky numeric rules unchanged.
- Keep distinct lobby/sky room types and flags in sim/save; menu merges only.
- Magenta `#FF00FF` is key plate only. Commit with `git commit --only` on task files. Do not `git add -A`. `TowerHudController.cs` may have unrelated dirty WIP — use dirty-file protocol (copy aside, checkout HEAD, apply only this task, commit --only, restore WIP + re-apply).
- Unity EditMode without `-quit`. If editor has project open, do not kill it; report BLOCKED.

## File map

| Path | Role |
|------|------|
| `Assets/Scripts/Data/BuildFamily.cs` | Add Utility subgroups to `BuildSubgroup` |
| `Assets/Scripts/Data/RoomTypeSO.cs` | Fallback `ResolvedBuildSubgroup` for Utility ids |
| `Assets/Scripts/UI/BuildCatalog.cs` | Nest Utility subgroups; labels |
| `Assets/Resources/Rooms/*.asset` | Tag buildFamily/buildSubgroup on listed rooms |
| `Assets/Scripts/UI/TowerHudController.cs` | Tools strip; Utility subgroup popout (mirror shops) |
| Build controller / lobby select (find `SelectSkyLobbyTool`) | Floor-aware Lobby; hide sky tool |
| `Assets/Tests/EditMode/BuildCatalog*.cs` or new tests | Grouping + lobby floor resolution |

---

### Task 1: BuildSubgroup enums + catalog nesting

**Files:**
- Modify: `Assets/Scripts/Data/BuildFamily.cs`
- Modify: `Assets/Scripts/UI/BuildCatalog.cs`
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs` (fallback mapping)
- Test: `Assets/Tests/EditMode/BuildCatalogUtilitySubgroupTests.cs` (create)

**Produces:** `BuildSubgroup.HealthSanitation|Power|Water|Services`; `BuildCatalog.Group` nests Utility; `SubgroupLabel` strings match spec; `ResolvedBuildSubgroup` id fallbacks.

- [ ] **Step 1: Write failing tests** — Utility catalog has four subgroups; clinic under HealthSanitation; power under Power; pump/treatment under Water; trophy under Services; stairs under Transit.

- [ ] **Step 2: Run EditMode filter `BuildCatalogUtilitySubgroupTests` → fail**

- [ ] **Step 3: Implement enums + Group nesting + fallbacks** (assets may still be None; fallbacks make tests pass)

- [ ] **Step 4: Green → commit**

```
git commit --only ... -m "feat(build): nest utility rooms into health power water services subgroups"
```

---

### Task 2: Tag room assets

**Files:**
- Modify: relevant `Assets/Resources/Rooms/*.asset` YAML `buildFamily` / `buildSubgroup` fields for the locked room list
- Test: extend catalog tests to assert asset-resolved subgroup (not only fallback) where practical

- [ ] **Step 1–4: Tag assets, verify catalog still green, commit**

```
git commit --only ... -m "feat(build): tag utility room assets with subgroup families"
```

---

### Task 3: HUD Utility popout like Shops

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (dirty-file protocol)
- Ensure expanding Utility shows subgroup strip then rooms (reuse shop subgroup expansion pattern: `_expandedShopSubgroup` or generalize to utility subgroups)

- [ ] **Step 1: Mirror Shops subgroup UI for Utility** (labels from `BuildCatalog.SubgroupLabel`)

- [ ] **Step 2: Manual/compile sanity; EditMode for any pure helpers**

- [ ] **Step 3: Commit**

```
git commit --only ... -m "feat(build): show utility subgroups in the build menu popout"
```

---

### Task 4: Tools strip + floor-aware Lobby

**Files:**
- Modify: `TowerHudController.DrawToolIcons` — remove Sky Lobby; ensure no Stairs/Elevator tools
- Modify: build selection (`SelectLobbyTool` / placement) so floor picks ground vs sky type
- Modify: tutorial highlight targets that referenced Sky Lobby → Lobby
- Test: `Assets/Tests/EditMode/LobbyToolFloorTests.cs` (create) for floor → type resolution helper

**Produces:** Pure helper e.g. `LobbyPlacement.ResolveType(floor, groundType, skyType)` used by build code.

- [ ] **Step 1: Failing tests for LobbyFloor → ground, floor+1 → sky**

- [ ] **Step 2–4: Implement helper + wire UI/placement; remove sky strip tool; green; commit**

```
git commit --only ... -m "feat(build): merge lobby and sky lobby into one floor-aware tool"
```

---

### Task 5: Transit-only stairs/elevators polish + regression

**Files:**
- Verify `CollectMenuRoomButtons` still feeds stairs/elevators into catalog Transit only
- Confirm tools strip has no transit room tools
- Run: `BuildCatalogUtilitySubgroupTests`, `LobbyToolFloorTests`, existing `MenuCatalogTests` / shop subgroup tests

- [ ] **Step 1: Fix any gaps**

- [ ] **Step 2: EditMode green**

- [ ] **Step 3: Commit if needed**

```
git commit --only ... -m "fix(build): keep stairs and elevators under transit catalog only"
```

---

## Manual Play Mode checklist

1. Utility → four subgroups with correct rooms.
2. Transit → stairs + elevators placeable; tools strip has no Sky/Stairs/Elev.
3. Lobby on G extends ground lobby; Lobby above G places sky lobby.
4. Shops Food/Retail unchanged.

## Execution note

After this plan is committed, implement via **subagent-driven-development** task-by-task.
