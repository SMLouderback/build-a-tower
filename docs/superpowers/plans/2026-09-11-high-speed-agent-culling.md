# High-Speed Agent Culling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** At 10x/60x, stagger-fade outdoor walking agent sprites while keeping idle room pops, elevator cars, full simulation, and map overlays.

**Architecture:** Pure `AgentSpeedLod` helper owns threshold and classification. `AgentView` owns per-sprite alpha/stagger state and a per-frame fade-start budget. `TowerSimulation` passes `GameClock.MinutesPerRealSecond` into `Sync`. Elevator cars and overlays are untouched.

**Tech Stack:** Unity 6000.4.7f1, C#, NUnit EditMode

## Global Constraints

- High-speed when `MinutesPerRealSecond >= 10` (covers `10x` and `60x`).
- Idle-in-room phases always full opacity when otherwise renderable: `AtHome`, `Working`, `Staying`, `VisitingShop`.
- LOD-hide targets: agents that pass `AgentView.ShouldRenderSprite` and are not idle-in-room (includes `WaitingAtElevator`, walking, stairs, lobby).
- `Riding` stays hidden via existing `ShouldRenderSprite`.
- Both raise-to-high-speed and drop-below-10x use staggered fades (not instant).
- Per-frame budget on **new fade starts**; randomize stagger delays.
- Pause freezes fade progress (`dt == 0` when paused).
- Simulation, elevator cars, traffic/economic overlays unchanged.
- Spec: `docs/superpowers/specs/2026-09-11-high-speed-agent-culling-design.md`

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Agents/AgentSpeedLod.cs` | Threshold, classification, fade constants, budget helper |
| `Assets/Scripts/Agents/AgentView.cs` | Per-sprite fade state, Sync overload with clock rate |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Pass minutes-per-real-second into AgentView.Sync |
| `Assets/Tests/EditMode/AgentSpeedLodTests.cs` | Pure helper + budget tests |
| `Assets/Tests/EditMode/AgentViewSpriteTests.cs` | Idle-forces-visible / classification regressions if needed |

---

### Task 1: Pure `AgentSpeedLod` helper

**Files:**
- Create: `Assets/Scripts/Agents/AgentSpeedLod.cs`
- Create: `Assets/Tests/EditMode/AgentSpeedLodTests.cs`

**Interfaces:**
- Produces `AgentSpeedLod.HighSpeedThreshold`, `IsHighSpeed`, `IsIdleInRoom`, `NeedsLodHide`, fade/budget constants, `TryBeginFade`

- [ ] **Step 1: Write failing tests**

```csharp
[TestCase(9.99f, false)]
[TestCase(10f, true)]
[TestCase(60f, true)]
public void IsHighSpeed_uses_threshold(float rate, bool expected) =>
    Assert.AreEqual(expected, AgentSpeedLod.IsHighSpeed(rate));

[TestCase(AgentPhase.AtHome, true)]
[TestCase(AgentPhase.Working, true)]
[TestCase(AgentPhase.Staying, true)]
[TestCase(AgentPhase.VisitingShop, true)]
[TestCase(AgentPhase.Moving, false)]
[TestCase(AgentPhase.WaitingAtElevator, false)]
[TestCase(AgentPhase.Riding, false)]
public void Idle_in_room_phases(AgentPhase phase, bool expected) =>
    Assert.AreEqual(expected, AgentSpeedLod.IsIdleInRoom(phase));

[Test]
public void NeedsLodHide_false_for_idle_room_and_riders()
{
    var idle = new Agent { Visible = true, Phase = AgentPhase.AtHome };
    var rider = new Agent { Visible = true, Phase = AgentPhase.Riding };
    var walker = new Agent { Visible = true, Phase = AgentPhase.Moving };
    Assert.IsFalse(AgentSpeedLod.NeedsLodHide(idle));
    Assert.IsFalse(AgentSpeedLod.NeedsLodHide(rider));
    Assert.IsTrue(AgentSpeedLod.NeedsLodHide(walker));
}

[Test]
public void TryBeginFade_respects_per_frame_budget()
{
    var started = 0;
    for (var i = 0; i < AgentSpeedLod.MaxNewFadesPerFrame + 5; i++)
    {
        if (AgentSpeedLod.TryBeginFade(ref started))
            continue;
        Assert.AreEqual(AgentSpeedLod.MaxNewFadesPerFrame, started);
        return;
    }
    Assert.Fail("budget never blocked");
}

[Test]
public void StepFade_reaches_zero_and_one()
{
    var a = 1f;
    a = AgentSpeedLod.StepAlpha(a, target: 0f, dt: 10f);
    Assert.AreEqual(0f, a, 0.001f);
    a = AgentSpeedLod.StepAlpha(a, target: 1f, dt: 10f);
    Assert.AreEqual(1f, a, 0.001f);
}

[Test]
public void TargetAlpha_idle_room_wins_over_high_speed()
{
    Assert.AreEqual(
        1f,
        AgentSpeedLod.TargetAlpha(
            highSpeed: true,
            needsLodHide: false,
            isIdleInRoom: true));
    Assert.AreEqual(
        0f,
        AgentSpeedLod.TargetAlpha(
            highSpeed: true,
            needsLodHide: true,
            isIdleInRoom: false));
    Assert.AreEqual(
        1f,
        AgentSpeedLod.TargetAlpha(
            highSpeed: false,
            needsLodHide: true,
            isIdleInRoom: false));
}
```

- [ ] **Step 2: Run red**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter AgentSpeedLodTests -testResults .superpowers/sdd/agent-lod-task1.xml -logFile .superpowers/sdd/agent-lod-task1.log
```

Expected: compile failure / missing type.

- [ ] **Step 3: Implement helper**

```csharp
namespace BuildATower
{
    public static class AgentSpeedLod
    {
        public const float HighSpeedThreshold = 10f;
        public const float FadeDurationSeconds = 0.6f;
        public const float MaxStaggerDelaySeconds = 1.4f;
        public const int MaxNewFadesPerFrame = 12;

        public static bool IsHighSpeed(float minutesPerRealSecond) =>
            minutesPerRealSecond >= HighSpeedThreshold;

        public static bool IsIdleInRoom(AgentPhase phase) =>
            phase is AgentPhase.AtHome
                or AgentPhase.Working
                or AgentPhase.Staying
                or AgentPhase.VisitingShop;

        public static bool NeedsLodHide(Agent agent) =>
            AgentView.ShouldRenderSprite(agent) &&
            !IsIdleInRoom(agent.Phase);

        public static bool TryBeginFade(ref int startedThisFrame)
        {
            if (startedThisFrame >= MaxNewFadesPerFrame) return false;
            startedThisFrame++;
            return true;
        }

        public static float RandomStaggerDelay(System.Random rng) =>
            (float)(rng.NextDouble() * MaxStaggerDelaySeconds);

        public static float TargetAlpha(
            bool highSpeed,
            bool needsLodHide,
            bool isIdleInRoom)
        {
            if (isIdleInRoom) return 1f;
            if (!needsLodHide) return 1f;
            return highSpeed ? 0f : 1f;
        }

        public static float StepAlpha(float current, float target, float dt)
        {
            if (FadeDurationSeconds <= 0f) return target;
            var step = dt / FadeDurationSeconds;
            if (current < target) return Mathf.Min(target, current + step);
            if (current > target) return Mathf.Max(target, current - step);
            return current;
        }
    }
}
```

- [ ] **Step 4: Run green and commit**

Expected: all `AgentSpeedLodTests` pass.

```bash
git add Assets/Scripts/Agents/AgentSpeedLod.cs Assets/Scripts/Agents/AgentSpeedLod.cs.meta Assets/Tests/EditMode/AgentSpeedLodTests.cs Assets/Tests/EditMode/AgentSpeedLodTests.cs.meta
git commit -m "feat: add high-speed agent LOD helper"
```

---

### Task 2: AgentView fade state and Sync wiring

**Files:**
- Modify: `Assets/Scripts/Agents/AgentView.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Tests/EditMode/AgentViewSpriteTests.cs` if Sync regressions are needed

**Interfaces:**
- Consumes Task 1 helpers including `TargetAlpha` and `StepAlpha`
- Produces `AgentView.Sync(agents, grid, minutesPerRealSecond, paused)`
- Existing `Sync(agents)` / `Sync(agents, grid)` remain and pass `rate: 1`, `paused: false` for tests

- [ ] **Step 1: Extend `AgentVisualState` and Sync**

```csharp
struct AgentVisualState
{
    public float LastX;
    public float LastY;
    public float WalkTimer;
    public int FrameIndex;
    public bool FlipX;
    public string SheetKey;
    public float Alpha;
    public float FadeDelay;
    public float FadeTarget;
    public bool FadeActive;
}

public void Sync(IReadOnlyList<Agent> agents) => Sync(agents, null, 1f, false);
public void Sync(IReadOnlyList<Agent> agents, TowerGrid grid) =>
    Sync(agents, grid, 1f, false);

public void Sync(
    IReadOnlyList<Agent> agents,
    TowerGrid grid,
    float minutesPerRealSecond,
    bool paused)
```

Inside the per-agent loop, after existing `ShouldRenderSprite` / shaft checks:

1. Maintain `startedFades` counter reset each Sync.
2. Compute `highSpeed = AgentSpeedLod.IsHighSpeed(minutesPerRealSecond)`.
3. If idle-in-room: set `Alpha = 1`, `FadeActive = false`, `FadeTarget = 1`, enable sprite.
4. Else if `NeedsLodHide`: desired target = `AgentSpeedLod.TargetAlpha(...)`. If target differs from current `FadeTarget`/`Alpha` intent and not already fading toward it, call `TryBeginFade`; on success set `FadeActive`, `FadeTarget`, `FadeDelay = RandomStaggerDelay(_rng)`.
5. If `paused`, skip delay/alpha stepping (`dt = 0` for fades).
6. Else count down `FadeDelay`, then `Alpha = StepAlpha(Alpha, FadeTarget, dt)`.
7. Apply `sr.color` with alpha multiplied; if `Alpha <= 0.001` set `sr.enabled = false` and skip walk-frame updates.
8. Keep a single `System.Random` field on `AgentView` for stagger.

When allocating new visual states, initialize `Alpha = 1`, `FadeTarget = 1`.

Preserve existing position/sprite selection for enabled agents with `Alpha > 0`.

- [ ] **Step 2: Wire TowerSimulation**

```csharp
agentView.Sync(
    _agents.Agents,
    build?.Grid,
    _clock.MinutesPerRealSecond,
    _clock.Paused);
```

- [ ] **Step 3: Run tests and commit**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "AgentSpeedLodTests|AgentViewSpriteTests" -testResults .superpowers/sdd/agent-lod-task2.xml -logFile .superpowers/sdd/agent-lod-task2.log
```

Expected: pass.

```bash
git add Assets/Scripts/Agents/AgentView.cs Assets/Scripts/Simulation/TowerSimulation.cs Assets/Tests/EditMode/AgentViewSpriteTests.cs
git commit -m "feat: fade outdoor agents at high simulation speed"
```

---

### Task 3: Regression verification and spec completion

**Files:**
- Modify: `docs/superpowers/specs/2026-09-11-high-speed-agent-culling-design.md`

- [ ] **Step 1: Focused EditMode**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "AgentSpeedLodTests|AgentViewSpriteTests" -testResults .superpowers/sdd/agent-lod-final.xml -logFile .superpowers/sdd/agent-lod-final.log
```

Expected: zero failed tests in these fixtures.

- [ ] **Step 2: Play Mode smoke**

1. At `5x`, walkers visible in halls.
2. Switch to `10x`: outdoor walkers stagger-fade out; idle room pops remain; elevator cars move.
3. Traffic overlay still shows busy corridors.
4. Switch to `60x`: same LOD behavior, less hitch.
5. Drop to `2x`: walkers stagger-fade back in, not all at once.
6. Pause mid-fade: fade freezes; resume continues.

- [ ] **Step 3: Mark spec Implemented and commit**

```markdown
**Status:** Implemented
```

```bash
git add docs/superpowers/specs/2026-09-11-high-speed-agent-culling-design.md
git commit -m "docs: mark high-speed agent culling implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Threshold ≥ 10 | 1 |
| Idle room always visible | 1, 2 |
| Outdoor/wait LOD hide | 1, 2 |
| Staggered fade both directions | 2 |
| Per-frame start budget | 1, 2 |
| Pause freezes fades | 2 |
| Pass clock rate from simulation | 2 |
| Overlays/sim/cars unchanged | 2 (no edits), 3 smoke |
| Tests + Implemented status | 1–3 |

No TBD placeholders. `StepAlpha` / `TargetAlpha` ship in Task 1 with the helper; Task 2 only wires view state and simulation.
