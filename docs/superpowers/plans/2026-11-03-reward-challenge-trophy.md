# Reward Challenges, Claim Book & Trophy Room Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a full hybrid reward system: data-driven challenges, claim-only Reward Book, subject trophies (Bronze→Galactic), rotating contracts, vanity Trophy Room, news+badge, first-trophy pause tutorial, Claim All with `$` rain, and trophy-room burst FX.

**Architecture:** `ChallengeCatalog` + condition evaluators feed `ChallengeTracker`. Completions enqueue `ClaimLedger` pending entries (never auto-pay). Claims credit `FundsWallet`, apply `RewardExtras`, and upgrade `TrophyCase`. `ContractBoard` rotates offers. `TowerSimulation` owns runtime state; optional `RewardsSnapshotV1` persists it (schema stays 1). HUD hosts Reward Book UI/FX; dollhouse shows Trophy Room pedestals from the same case.

**Tech Stack:** Unity 6000.4.7f1, C#, EditMode NUnit under `Assets/Tests/EditMode`, IMGUI HUD patterns from `TowerHudController` / `ResearchHudPanel`, `TowerNews.Push`, `StarCelebrationPauseGate`.

**Spec:** `docs/superpowers/specs/2026-11-03-reward-challenge-trophy-design.md`

## Global Constraints

- Payout is **claim-only**. Meeting a challenge → pending + news + badge; wallet unchanged until `TryClaim` / `ClaimAll`.
- Trophy metals: `None < Bronze < Silver < Gold < Platinum < Diamond < Rainbow < Galactic`. Upgrade on claim only; never downgrade.
- Subjects: Population, Stars, Construction, Demolition, Operations, Infamy, Commerce, Transit, Time, Quirks.
- Hybrid: permanent milestones/quirks always tracked; contracts only while on the board (3–5 active).
- First claim that awards any metal pauses once (`rewardTutorialSeen`); afterward pulse badge + news only — no reward pauses.
- Claim All + `$` rain on bottom-bar `Save $…` chip; Trophy Room light/star burst when metal rises.
- Trophy Room: vanity only — no happiness/demand/rent buffs. Demolish room ≠ lose trophies.
- Goals dropdown stays next-star only; Reward Book is a **separate** badged command-bar control.
- Save schema stays **1**. Absent `RewardsSnapshotV1` → empty ledger, no trophies, tutorial false, empty board/extras.
- Magenta `#FF00FF` is key plate only. Trophy Room / menu icons: warm architectural dollhouse, not pink/toy.
- Commit with `git commit --only` on task files. Do not `git add -A`. `TowerSimulation.cs` and `TowerHudController.cs` may have unrelated unstaged edits — copy aside, checkout HEAD, apply only this task, `git commit --only`, restore dirty copy + re-apply task edit.
- Unity EditMode runs without `-quit`. If the editor already has this project open, do not close it. Report BLOCKED.
- Prefer small focused files under `Assets/Scripts/Rewards/` (new folder) rather than bloating `EconomySystem`.

## File map

| Path | Role |
|------|------|
| `Assets/Scripts/Rewards/TrophyMetal.cs` | Metal enum + compare helpers |
| `Assets/Scripts/Rewards/ChallengeSubject.cs` | Subject enum |
| `Assets/Scripts/Rewards/ChallengeKind.cs` | Milestone / Contract / Quirk |
| `Assets/Scripts/Rewards/ChallengeDef.cs` | Definition record (id, copy, subject, metal, kind, condition, cash, extras) |
| `Assets/Scripts/Rewards/ChallengeCatalog.cs` | Static/table load of ~40–60 defs |
| `Assets/Scripts/Rewards/ChallengeCondition.cs` | Threshold / DurationHold / Counter / Flag models |
| `Assets/Scripts/Rewards/ChallengeTracker.cs` | Progress + complete → pending |
| `Assets/Scripts/Rewards/ClaimLedger.cs` | Pending/claimed; TryClaim; ClaimAll |
| `Assets/Scripts/Rewards/TrophyCase.cs` | Subject → metal + titles/ribbons |
| `Assets/Scripts/Rewards/ContractBoard.cs` | Active offers, refresh, expiry |
| `Assets/Scripts/Rewards/RewardExtras.cs` | Apply / inventory tokens |
| `Assets/Scripts/Rewards/RewardsNews.cs` | `TowerNewsItem` builders |
| `Assets/Scripts/Rewards/RewardsSystem.cs` | Facade owned by sim (tracker+ledger+case+board+extras) |
| `Assets/Scripts/Persistence/SaveContracts.cs` | `RewardsSnapshotV1` + fields on `TowerSnapshotV1` |
| `Assets/Scripts/Persistence/TowerSnapshotMapper.cs` | Capture / restore rewards |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Construct, tick day hooks, expose `Rewards` |
| `Assets/Scripts/UI/RewardBookHud.cs` | Tabs, claim, Claim All, badge helpers |
| `Assets/Scripts/UI/RewardClaimFx.cs` | `$` rain toward money chip rect |
| `Assets/Scripts/UI/RewardTutorialGate.cs` | First-trophy pause modal using pause gate |
| `Assets/Scripts/UI/TowerHudController.cs` | Rewards button, badge pulse, bind FX/tutorial |
| `Assets/Scripts/Data` + `Resources/Rooms/TrophyRoom.asset` | Room type |
| `Assets/Scripts/Rendering/*` | Trophy pedestal art + room burst |
| `Assets/Tests/EditMode/Rewards*.cs` | EditMode coverage |

---

### Task 1: Core enums, defs, empty catalog API

**Files:**
- Create: `Assets/Scripts/Rewards/TrophyMetal.cs`, `ChallengeSubject.cs`, `ChallengeKind.cs`, `ChallengeDef.cs`, `ChallengeCondition.cs`, `ChallengeCatalog.cs`
- Test: `Assets/Tests/EditMode/ChallengeCatalogTests.cs`

- [ ] **Step 1: Write failing tests** — catalog `All` non-empty after Task 9; for Task 1 assert `TrophyMetalCompare.Max(Bronze, Gold) == Gold`, `ChallengeCatalog.TryGet("missing", out _)` false, and a **seed stub** of at least one def `pop_100` exists so later tasks compile. Prefer adding `pop_100` stub in catalog now (Threshold pop ≥ 100, Population, Bronze, cash 5000).

- [ ] **Step 2: Run EditMode filter `ChallengeCatalogTests`** — expect fail (types missing). Unity path:

```
"C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" -runTests -testPlatform EditMode -testFilter ChallengeCatalogTests -testResults "C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\TestResults\rewards-task1.xml" -logFile "C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\TestResults\rewards-task1.log"
```

If lockfile/editor open → BLOCKED (do not kill Unity).

- [ ] **Step 3: Implement enums + `ChallengeDef` + `ChallengeCondition` + catalog with stub `pop_100`**

- [ ] **Step 4: Re-run tests → green**

- [ ] **Step 5: Commit**

```
git commit --only Assets/Scripts/Rewards/TrophyMetal.cs Assets/Scripts/Rewards/ChallengeSubject.cs Assets/Scripts/Rewards/ChallengeKind.cs Assets/Scripts/Rewards/ChallengeDef.cs Assets/Scripts/Rewards/ChallengeCondition.cs Assets/Scripts/Rewards/ChallengeCatalog.cs Assets/Tests/EditMode/ChallengeCatalogTests.cs -m "feat(rewards): add challenge catalog stub and trophy metal enums"
```

(Include `.meta` files Unity generates if present.)

---

### Task 2: TrophyCase + ClaimLedger (no auto-pay)

**Files:**
- Create: `Assets/Scripts/Rewards/TrophyCase.cs`, `ClaimLedger.cs`
- Test: `Assets/Tests/EditMode/ClaimLedgerTests.cs`

**Behavior:**
- `EnqueuePending(id)` ignores duplicates and already-claimed.
- `TryClaim(id, wallet, case, extras, def)` pays `def.Cash` via `wallet.Add`, upgrades case to `def.Metal` if higher, marks claimed; returns struct `{ ok, cashPaid, metalRaised, firstTrophyOnTower }`.
- `ClaimAll` iterates pending snapshot; sums cash; `firstTrophyOnTower` if any claim set first metal.
- Second claim same id → false / 0 cash.

- [ ] **Step 1: Failing tests** for pending→claim, double-claim, ClaimAll sum, metal upgrade / no downgrade, firstTrophy flag.

- [ ] **Step 2: Run `ClaimLedgerTests` → fail**

- [ ] **Step 3: Implement TrophyCase + ClaimLedger** (extras arg may be no-op interface stub until Task 7)

- [ ] **Step 4: Tests green → commit**

```
git commit --only ... -m "feat(rewards): claim ledger pays only on claim and upgrades trophies"
```

---

### Task 3: ChallengeTracker thresholds

**Files:**
- Create: `Assets/Scripts/Rewards/ChallengeTracker.cs`
- Test: `Assets/Tests/EditMode/ChallengeTrackerTests.cs`

**Behavior:**
- `EvaluateThresholds(RewardsMetrics metrics, ClaimLedger ledger, ChallengeCatalog catalog)` for Milestone/Quirk Threshold defs not claimed/pending: if metric ≥ value → `EnqueuePending`.
- `RewardsMetrics` plain struct/class: Population, Stars, RoomCounts, DemolishLifetime, CondemnCount, QuarantineEver, DayIndex, etc. (fill what thresholds need; expand in later tasks).

- [ ] **Step 1: Test** pop ≥ 100 enqueues `pop_100`; wallet not touched; already pending not duplicated.

- [ ] **Step 2–4: Implement, green, commit**

```
git commit --only ... -m "feat(rewards): track threshold challenges into pending claims"
```

---

### Task 4: DurationHold + lifetime counters

**Files:**
- Modify: `ChallengeTracker.cs`, `ChallengeCondition.cs` as needed
- Test: extend `ChallengeTrackerTests.cs`

**Behavior:**
- DurationHold: each day, if metric in band → streak++; else streak = 0. When streak ≥ N → pending.
- Counter conditions for contracts armed on board only (hook in Task 6); lifetime demolish/build counters live on tracker/metrics updated from sim events.

- [ ] **Step 1: Tests** — 7 days healthy completes; breach on day 3 resets; demolish lifetime threshold.

- [ ] **Step 2–4: Implement, green, commit**

```
git commit --only ... -m "feat(rewards): duration holds and lifetime counters for challenges"
```

---

### Task 5: RewardsSystem facade + sim day hooks + news

**Files:**
- Create: `Assets/Scripts/Rewards/RewardsSystem.cs`, `RewardsNews.cs`
- Modify: `TowerSimulation.cs` (careful with dirty file protocol)
- Test: `Assets/Tests/EditMode/RewardsSystemTests.cs`

**Behavior:**
- `RewardsSystem` owns tracker, ledger, case, (empty board stub), tutorial flag.
- Sim: create on awake/init; each day roll call `OnDayRolled(metrics, news)`; on grid/pop/star changes call `Evaluate(metrics, news)`.
- On new pending: `news.Push(RewardsNews.Completed(def))`.

- [ ] **Step 1: Test** Evaluate with pop 100 → pending count 1 + news text non-empty (can use a list spy or real `TowerNews`).

- [ ] **Step 2–4: Wire sim minimally, green, commit with dirty-file protocol for `TowerSimulation.cs`**

```
git commit --only ... -m "feat(rewards): wire rewards system into simulation day and event hooks"
```

---

### Task 6: ContractBoard

**Files:**
- Create: `Assets/Scripts/Rewards/ContractBoard.cs`
- Modify: `RewardsSystem.cs`, catalog contract defs (few stubs OK until Task 9)
- Test: `Assets/Tests/EditMode/ContractBoardTests.cs`

**Behavior:**
- Maintain 3–5 slots; offer contract-kind defs not claimed; track progress/expiry day; success → ledger pending + remove; expire → news fail, no pending; weekly refresh fills empty slots.

- [ ] **Step 1–4: TDD, green, commit**

```
git commit --only ... -m "feat(rewards): rotating contract board with success and expiry"
```

---

### Task 7: RewardExtras

**Files:**
- Create: `Assets/Scripts/Rewards/RewardExtras.cs`
- Modify: `ClaimLedger.cs` to call apply on claim
- Test: `Assets/Tests/EditMode/RewardExtrasTests.cs`

**Behavior:**
- Instant title/ribbon → TrophyCase metadata.
- Tokens: build credit (next placement cost 0 up to credit $ or one room — pick **one rule**: subtract min(credit, room cost) on next successful spend path via flag on build controller; document in code), demand bump (multiplier + expiry day), crew day (bool charges).
- Inventory in snapshot later.

- [ ] **Step 1–4: TDD core apply + inventory, green, commit**

```
git commit --only ... -m "feat(rewards): apply claim extras and token inventory"
```

---

### Task 8: Save snapshot

**Files:**
- Modify: `SaveContracts.cs`, `TowerSnapshotMapper.cs`, `RewardsSystem.cs`
- Test: `Assets/Tests/EditMode/RewardsSnapshotTests.cs`

**Behavior:**
- Optional `RewardsSnapshotV1` on `TowerSnapshotV1`.
- Round-trip pending, claimed, trophies, tutorial flag, contracts, extras.
- Null snapshot → defaults (mirror cleanliness absent behavior).

- [ ] **Step 1–4: TDD, green, commit**

```
git commit --only ... -m "feat(rewards): persist claim ledger and trophies in schema v1"
```

---

### Task 9: Fat catalog (~40–60 defs)

**Files:**
- Modify: `ChallengeCatalog.cs` (or `ChallengeCatalogData.cs` partial)
- Test: `ChallengeCatalogTests.cs` — assert count ≥ 40, unique ids, every subject has ≥ 1, every metal used at least once across catalog, contracts ≥ 8 in pool

Include ladders: population, stars, construction families, demolish, health/clean holds, quarantine/condemn, commerce/transit/time, quirky/infamy one-shots. Cash scales with metal (document table in catalog comments).

- [ ] **Step 1–4: Expand data, tests green, commit**

```
git commit --only ... -m "feat(rewards): ship full challenge catalog across subjects and metals"
```

---

### Task 10: Reward Book HUD + badge pulse + Claim All

**Files:**
- Create: `Assets/Scripts/UI/RewardBookHud.cs`
- Modify: `TowerHudController.cs` (dirty-file protocol)
- Test: `Assets/Tests/EditMode/RewardBookHudTests.cs` for pure helpers (badge count, tab labels, claim-all ordering) — keep IMGUI draw thin

**Behavior:**
- Command button near Goals; lobby gate OR pending > 0.
- Pulse while pending > 0 (sin/time unscaled alpha or scale).
- Tabs Ready / Catalog / Contracts / Trophies; Claim + Claim All call `RewardsSystem`.

- [ ] **Step 1–4: Implement, tests for helpers, commit**

```
git commit --only ... -m "feat(rewards): add reward book HUD with claim all and pending badge"
```

---

### Task 11: First-trophy tutorial pause

**Files:**
- Create: `Assets/Scripts/UI/RewardTutorialGate.cs`
- Modify: `TowerHudController.cs` / `RewardsSystem` claim path to raise `NeedsTutorial` once
- Test: `Assets/Tests/EditMode/RewardTutorialGateTests.cs`

**Behavior:**
- On claim result `firstTrophyOnTower && !rewardTutorialSeen` → capture pause gate, show modal, on dismiss restore + set flag.
- Second metal upgrade never pauses.

- [ ] **Step 1–4: TDD flag logic, commit**

```
git commit --only ... -m "feat(rewards): pause once to introduce the reward book and trophies"
```

---

### Task 12: `$` rain claim FX

**Files:**
- Create: `Assets/Scripts/UI/RewardClaimFx.cs`
- Modify: `TowerHudController.cs` — pass bottom money chip rect; start FX on claim
- Test: optional helper test for particle spawn count / duration; Play Mode smoke manual

**Behavior:**
- Short burst of `$` glyphs moving toward / erupting from `Save $` chip; duration ~0.8–1.2s; works for Claim All (one burst scaled by cash or count).

- [ ] **Step 1–3: Implement FX + wire; commit**

```
git commit --only ... -m "feat(rewards): pour dollar signs into the savings chip on claim"
```

---

### Task 13: Trophy Room placeable + pedestal bind

**Files:**
- Create room asset `Assets/Resources/Rooms/TrophyRoom.asset` (+ menu icon placeholder under `Art/Menu/`)
- Dollhouse placeholder color or simple plate under `Art/Dollhouse/` (warm gallery; no magenta as scene color)
- Modify: build catalog listing in `TowerHudController` / room loader paths matching other prestige/service rooms
- Create: `Assets/Scripts/Rendering/TrophyRoomDisplay.cs` (map `TrophyCase` → pedestal slots)
- Test: `Assets/Tests/EditMode/TrophyRoomDisplayTests.cs`

**Behavior:**
- ~4×1 or 6×1; mid cost; 0 income; lobby required to build; vanity.
- Empty pedestals for None; metal sprite key per subject with metal.
- Multiple rooms same case; demolish keeps case.

- [ ] **Step 1–4: TDD display mapping + register room, commit**

```
git commit --only ... -m "feat(rewards): add vanity trophy room that shows the trophy case"
```

---

### Task 14: Trophy Room burst FX + shared trophy art keys

**Files:**
- Create: trophy metal sprites/placeholders `Assets/Resources/Art/Trophies/` (bronze…galactic)
- Modify: `TrophyRoomDisplay` / view sync on `RewardsSystem.OnTrophyUpgraded`
- Create: `Assets/Scripts/Rendering/TrophyRoomBurstFx.cs`
- Test: light unit test that burst is requested once per metal-up event

- [ ] **Step 1–4: Wire burst on metal raise; commit**

```
git commit --only ... -m "feat(rewards): burst light and stars from trophy rooms on upgrade"
```

---

### Task 15: Extras spend hooks + polish pass

**Files:**
- Modify: build spend path for build credit; economy/demand read for timed bump; housekeeping/repair for crew day if hooks exist
- News copy pass for expire/quirk lines
- Ensure badge pulse + Claim All + tutorial + save round-trip still green (`RewardsSystemTests`, `RewardsSnapshotTests`, `ClaimLedgerTests`, `ContractBoardTests`, `ChallengeCatalogTests`)

- [ ] **Step 1: Wire token consumption at real spend sites (minimal, documented)**

- [ ] **Step 2: Run EditMode filter `Rewards|ClaimLedger|ChallengeCatalog|ContractBoard|TrophyRoom|RewardBook|RewardTutorial`**

- [ ] **Step 3: Fix failures**

- [ ] **Step 4: Commit**

```
git commit --only ... -m "feat(rewards): spend reward tokens and polish news and HUD wiring"
```

---

## Manual Play Mode checklist (after Task 15)

1. Reach pop 100 → news + badge; money unchanged until claim.
2. Claim / Claim All → `$` rain + balance up; Trophies tab shows Bronze Population.
3. First trophy → pause modal once; second challenge claim no pause.
4. Place Trophy Room → pedestals match case; claim upgrade → burst.
5. Save / load → pending, claimed, trophies, tutorial flag, contracts intact.
6. Contract succeed / expire behaviors match news + ledger rules.

## Execution note

After this plan is committed, implement via **subagent-driven-development** task-by-task (spec already approved; do not re-brainstorm).
