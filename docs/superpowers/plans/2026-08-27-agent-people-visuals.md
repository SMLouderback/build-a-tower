# Agent People Visuals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace role-colored agent dots with painterly walk-cycle sprites scaled to ~70% cell height, keyed by role × gender × dress tier (or staff uniform / criminal outfit).

**Architecture:** Add `AgentGender` on `Agent` (assigned at spawn). New `AgentSpriteArt` maps `(AgentRole, AgentGender, WealthBand)` → Resources sheet key, loads horizontal 4-frame strips from `Art/Agents/`, slices walk frames. `AgentView` pools `SpriteRenderer`s, animates while moving, flips X from travel direction, scales to target height. No economy rule changes.

**Tech Stack:** Unity Built-in RP, Resources `.bytes` + `.png`, NUnit EditMode, painterly cutaway art style (128 PPU).

**Spec:** `docs/superpowers/specs/2026-08-27-agent-people-visuals-design.md`

## Global Constraints

- **1 cell = 1 world unit = 128 px** for room art; agent target **rendered height ≈ 0.70 cells** (~90 px).
- **4 walk frames** per sheet; horizontal strip; **flip X** for left/right — no mirrored art.
- **Dress tiers (render-only):** Street/Basic → `basic`, Mid → `mid`, Upper/Premium → `upper`. Staff + Criminal ignore wealth tier.
- **Gender:** `Male` / `Female`; ~50/50 at spawn; stable for agent lifetime.
- **Sheet naming:** economy `{roleSlug}_{male|female}_{basic|mid|upper}`; staff `{roleSlug}_{male|female}_uniform`; criminal `criminal_{male|female}`.
- **Role slugs:** `office_worker`, `hotel_guest`, `condo_resident`, `street_visitor`, `event_visitor`, `maid`, `handyman`, `security`.
- Path: `Assets/Resources/Art/Agents/`; ship `.png` + identical `.bytes`; bytes-first load (match `RoomDollhouseArt`).
- **No dots in shipping path** when sheet loads; dev fallback dot only if sheet missing.
- **sortingOrder 20** (below elevator cars 30).
- Do not commit `.superpowers/sdd/*`, `_Recovery/`, `*.wip`.
- PowerShell: use `;` not `&&`.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Agents/AgentEnums.cs` | Add `AgentGender` |
| `Assets/Scripts/Agents/Agent.cs` | Add `Gender` property |
| `Assets/Scripts/Agents/AgentSystem.cs` | Assign gender in all spawn paths |
| `Assets/Scripts/Rendering/AgentSpriteArt.cs` | Dress tier, sheet key, load/cache, slice frames |
| `Assets/Scripts/Agents/AgentView.cs` | Sprite pool, walk anim, flip, scale |
| `Assets/Tests/EditMode/AgentSpriteArtTests.cs` | Resolver + slice tests |
| `Assets/Tests/EditMode/AgentGenderTests.cs` | Spawn assigns gender |
| `Assets/Resources/Art/Agents/*` | 38 walk sheets + metas |
| `.superpowers/sdd/agent-sprite-normalize.ps1` | Validate dimensions / generate metas (do not commit sdd unless asked — keep script inline in task or under `Assets/Editor` if needed) |

**Sheet inventory (38)**

| Group | Count | Keys |
|-------|-------|------|
| Economy | 30 | 5 roles × 2 genders × 3 tiers |
| Staff uniform | 6 | maid, handyman, security × 2 genders |
| Criminal | 2 | male, female |

---

### Task 1: AgentGender on sim model + spawn assignment

**Files:**
- Modify: `Assets/Scripts/Agents/AgentEnums.cs`
- Modify: `Assets/Scripts/Agents/Agent.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs` (all `new Agent(...)` sites)
- Create: `Assets/Tests/EditMode/AgentGenderTests.cs`

**Interfaces:**
```csharp
public enum AgentGender { Male, Female }

// Agent.cs
public AgentGender Gender { get; set; }

// AgentSystem.cs — internal helper
static AgentGender RollGender(Random rng) =>
    rng.Next(2) == 0 ? AgentGender.Male : AgentGender.Female;
```

- [ ] **Step 1: Failing test** — spawn office worker via existing test harness (or minimal grid fixture); assert `Gender` is Male or Female; spawn 100 agents with fixed seed → both genders appear.

```csharp
[Test]
public void SpawnOfficeWorker_AssignsGender()
{
    var rng = new Random(42);
    var genders = new HashSet<AgentGender>();
    for (var i = 0; i < 50; i++)
        genders.Add(RollGender(rng));
    Assert.That(genders, Does.Contain(AgentGender.Male));
    Assert.That(genders, Does.Contain(AgentGender.Female));
}
```

- [ ] **Step 2: Run EditMode tests** — `Unity -runTests -testFilter AgentGenderTests` (or project test runner). Expected: FAIL (enum/property missing).

- [ ] **Step 3: Implement** — add enum, property, call `RollGender(_rng)` in every agent constructor initializer block (office, hotel, condo, service staff, street, event, criminal).

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit**

```
feat: assign male/female gender when agents spawn
```

---

### Task 2: AgentSpriteArt — dress tier + sheet key resolver

**Files:**
- Create: `Assets/Scripts/Rendering/AgentSpriteArt.cs`
- Create: `Assets/Tests/EditMode/AgentSpriteArtTests.cs`

**Interfaces:**
```csharp
public enum AgentDressTier { Basic, Mid, Upper }

public static class AgentSpriteArt
{
    public const string ResourceRoot = "Art/Agents/";
    public const int WalkFrameCount = 4;
    public const float PixelsPerUnit = 128f;
    public const float TargetHeightCells = 0.70f;

    public static AgentDressTier DressTierFromWealth(WealthBand wealth);
    public static string ResolveSheetKey(AgentRole role, AgentGender gender, WealthBand wealth);
    public static string ResourcePath(string sheetKey); // Art/Agents/{key}
    public static void ResetForTests();
}
```

Mapping rules:
- Economy roles → `{slug}_{gender}_{tier}` where tier is `basic|mid|upper`.
- Maid/Handyman/Security → `{slug}_{gender}_uniform`.
- Criminal → `criminal_{gender}` (ignore wealth).
- Unknown role → null.

- [ ] **Step 1: Failing tests** — table-driven tests for every `AgentRole`:
  - `OfficeWorker + Male + WealthBand.Basic` → `office_worker_male_basic`
  - `HotelGuest + Female + WealthBand.Premium` → `hotel_guest_female_upper`
  - `Maid + Female + any wealth` → `maid_female_uniform`
  - `Criminal + Male` → `criminal_male`
  - Wealth mapping: Street→Basic, Mid→Mid, Upper→Upper.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement resolver + dress tier**

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit**

```
feat: add agent sprite sheet key resolver
```

---

### Task 3: AgentSpriteArt — load strips + slice walk frames

**Files:**
- Modify: `Assets/Scripts/Rendering/AgentSpriteArt.cs`
- Modify: `Assets/Tests/EditMode/AgentSpriteArtTests.cs`

**Interfaces:**
```csharp
public static Func<string, Sprite> LoadSpriteForTests;

public static Sprite GetSheet(string sheetKey);
public static Sprite GetWalkFrame(string sheetKey, int frameIndex);
public static float ScaleForTargetHeight(Sprite frameSprite); // TargetHeightCells / frameWorldHeight
```

Implementation notes:
- Load full strip sprite from Resources (bytes-first, copy `RoomDollhouseArt.LoadTexture` pattern).
- Slice frame `i` with `Rect(i * frameWidth, 0, frameWidth, tex.height)` pivot **(0.5, 0)** bottom-center.
- Cache sheets and per-frame sprites by key.
- `ResetForTests()` clears caches + test hook.

- [ ] **Step 1: Failing test** — inject 384×96 test strip (4×96 px frames) via `LoadSpriteForTests`; assert frame 0/3 rects and `ScaleForTargetHeight` ≈ 0.70 / (96/128).

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement load + slice + scale helper**

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit**

```
feat: load and slice agent walk sprite sheets
```

---

### Task 4: AgentView — sprite render, walk cycle, facing flip

**Files:**
- Modify: `Assets/Scripts/Agents/AgentView.cs`
- Create: `Assets/Tests/EditMode/AgentViewSpriteTests.cs` (optional lightweight tests for scale/flip helpers if extracted)

**Behavior:**
- Remove procedural dot as primary render; keep dot fallback only when `GetWalkFrame` returns null.
- Per pooled renderer track: last world X, walk phase timer.
- Moving when `|Δx| + |Δy| > ε`: advance frame every ~0.12s (tunable); idle → frame 0.
- `flipX = velocity.x < -ε` (hold last facing when idle).
- Position: unchanged (Riding Y bump preserved).
- `sr.color = Color.white` (drop role tint when sprite present; optional subtle tint at 0.9 if needed for role read — default white per spec).
- Scale: `AgentSpriteArt.ScaleForTargetHeight(frame)`.
- Sorting order 20.

Extract static helpers if needed:
```csharp
internal static int PickWalkFrame(float phase, bool moving);
internal static bool ShouldFlipX(float deltaX, bool previousFlip);
```

- [ ] **Step 1: Manual/play-mode checklist** documented in task report (EditMode cannot easily test MonoBehaviour without host — verify in Play Mode after Task 5 art lands).

- [ ] **Step 2: Implement AgentView rewrite**

- [ ] **Step 3: Play Mode — agents show sprites when sheets exist, flip while walking along X**

- [ ] **Step 4: Commit**

```
feat: render agents as walk-cycle sprites in AgentView
```

---

### Task 5: Agent sprite art — economy roles (30 sheets)

**Files:**
- Create: `Assets/Resources/Art/Agents/*.png` + `.bytes` + `.meta` (30 files × 2 formats + metas)
- Optional: `Assets/Editor/AgentSpriteArtImporter.cs` only if needed for meta consistency

**Art direction (match dollhouse/hotel painterly cutaway):**
- Side-view full-body figures, **~90 px tall**, **~36–48 px wide** per frame.
- Horizontal strip **4 frames** (contact, pass, contact, pass walk).
- Transparent BG; feet on bottom pixel row.
- **OfficeWorker:** business casual → suit by tier.
- **HotelGuest:** travel casual → upscale guest.
- **CondoResident:** home clothes → luxury resident.
- **StreetVisitor:** casual shopper → nicer streetwear.
- **EventVisitor:** business casual → formal event attire.
- Male/female silhouettes distinct but same scale.

- [ ] **Step 1: Generate 30 sheets** (image gen batch; consistent camera/lighting across set).

- [ ] **Step 2: Normalize** — verify each sheet width = 4 × frameWidth, height 85–96 px; duplicate to `.bytes`.

- [ ] **Step 3: Play Mode spot-check** — one agent per role at each tier visible and dressier at upper.

- [ ] **Step 4: Commit**

```
feat: add economy agent walk sprite sheets
```

---

### Task 6: Agent sprite art — staff uniforms + criminal (8 sheets)

**Files:**
- Create remaining 8 sheets under `Assets/Resources/Art/Agents/`

**Art direction:**
- **Maid:** housekeeping uniform (cart optional), M/F.
- **Handyman:** coveralls + tool belt, M/F.
- **Security:** uniform + cap/badge, M/F.
- **Criminal:** hooded/shady street outfit, M/F (single outfit, no tiers).

- [ ] **Step 1: Generate 8 sheets**

- [ ] **Step 2: Normalize + bytes**

- [ ] **Step 3: Play Mode — staff + criminal recognizable**

- [ ] **Step 4: Commit**

```
feat: add staff and criminal agent walk sprites
```

---

### Task 7: Docs closeout

**Files:**
- Modify: `docs/superpowers/specs/2026-08-27-agent-people-visuals-design.md` — Status → Implemented, note plan + art commits.

- [ ] **Step 1: Update spec status**

- [ ] **Step 2: Commit**

```
docs: mark agent people visuals implemented
```

---

## Self-review (plan vs spec)

| Spec requirement | Task |
|------------------|------|
| Gender M/F at spawn | 1 |
| Dress tier from wealth | 2 |
| Role × gender × tier matrix | 2, 5, 6 |
| Staff uniform M/F | 2, 6 |
| Criminal M/F single outfit | 2, 6 |
| 4-frame walk + flip | 3, 4 |
| ~70% cell height | 3, 4 |
| Painterly style | 5, 6 |
| No economy changes | 1 (visual only) |
| Remove dots when art present | 4 |
| EditMode resolver tests | 2, 3 |
| 38 sheets | 5, 6 |

No placeholders remain.
