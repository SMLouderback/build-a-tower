# Water Cleanliness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tower water-cleanliness percent that drifts hourly toward treatment supply versus sewage, and a basement treatment room that cleans it.

**Architecture:** `CleanlinessSystem` owns the target, one drift step, dirt, fill, illness, restaurant weight, and the treatment table. `TowerSimulation` stores the percent and steps it once per game hour. Illness, move-ins, and restaurant traffic read the stored percent. Treatment upgrades reuse the pump clock.

**Tech Stack:** Unity 6000.4.7f1, C#, EditMode NUnit tests under `Assets/Tests/EditMode`.

**Spec:** `docs/superpowers/specs/2026-10-27-water-cleanliness-design.md`

## Global Constraints

- Target is 100 when sewage is 0, else `supply / sewage * 100`. One step is `stored + (target - stored) * 0.10`. A step runs for each whole 60 game minutes. A 150-minute tick takes two steps and keeps 30 minutes.
- Sewage is 1 per hotel with a guest, office with a worker, or sold condo. Shops, staff, and empty rooms add 0.
- Supply sums `TreatmentSupply` for treatment rooms that are powered and not broken. A dark or broken room adds 0.
- Outputs by stored level 0..3 are 12, 20, 32, 48. Fees for destination levels 1..3 are 30000, 70000, 140000. Durations are 1440, 4320, 10080. Level 3 is the max.
- Dirt is 0 at or above 100, else `1 - stored / 100`. Fill cuts are Base 0.15, Mid 0.35, Upper 0.65, and a missing band uses Base. Illness per minute is `0.08 * dirt` on a floor with a sewage room, else 0. Restaurant factor is `1 - 0.40 * dirt` for `BuildSubgroup.Food` only.
- `service_treatment`: 1★, 6×1, $90000, noise 0.40, color (0.32, 0.46, 0.40), basement only, draw 1. Utility, Service, no income, no staff, no housekeeping. Display name `Water Treatment`.
- Chip after the water chip, from day one, in both bars: `Clean {RoundToInt(stored)}%`. Selection: `Treatment {level} · {output}` with `Built` / `I` / `II` / `III`. Button: `Upgrade to {level} · {days} days · ${fee}`. Hidden at level III.
- Save schema stays 1. `CleanlinessSnapshotV1` is optional. Absent means 100. A saved 0 stays 0. Utility clock fields already exist. `Powered` is not saved.
- No dollhouse plates, no news lines, no retail weight change, no treatment staff. Magenta `#FF00FF` is a key plate only.
- Commit with `git commit --only` on the task files. Do not `git add -A`. `TowerSimulation.cs` and `TowerHudController.cs` may have unrelated unstaged edits. `git commit --only` on a path commits the whole file. Copy the dirty file aside, check out HEAD, apply only this task, commit, then restore the dirty copy and re-apply the task edit.
- Unity EditMode runs without `-quit`. If the editor already has this project open, do not close it. Report BLOCKED.

---

### Task 1: Cleanliness formulas

**Files:**
- Create: `Assets/Scripts/Economy/CleanlinessSystem.cs`
- Test: `Assets/Tests/EditMode/CleanlinessSystemTests.cs`

**Interfaces:**
- Consumes: `LuxuryBand`
- Produces: `CleanlinessSystem.Target(int supply, int sewage)`, `Step(float stored, float target)`, `Dirt(float stored)`, `FillMultiplier(LuxuryBand band, float stored)`, `IllnessPerMinute(float stored)`, `RestaurantWeightFactor(float stored)`, `TreatmentSupply(int level)`, `TreatmentUpgradeFee(int nextLevel)`, `TreatmentUpgradeMinutes(int nextLevel)`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void Target_is_100_when_nobody_is_home()
{
    Assert.AreEqual(100f, CleanlinessSystem.Target(0, 0));
    Assert.AreEqual(100f, CleanlinessSystem.Target(12, 12));
    Assert.AreEqual(50f, CleanlinessSystem.Target(12, 24));
    Assert.AreEqual(0f, CleanlinessSystem.Target(0, 8));
    Assert.AreEqual(400f, CleanlinessSystem.Target(48, 12));
}

[Test]
public void Step_closes_a_tenth_of_the_gap()
{
    Assert.AreEqual(90f, CleanlinessSystem.Step(100f, 0f));
    Assert.AreEqual(81f, CleanlinessSystem.Step(90f, 0f));
    Assert.AreEqual(72.9f, CleanlinessSystem.Step(81f, 0f), 0.001f);
}

[Test]
public void Effects_use_the_locked_slopes()
{
    Assert.AreEqual(1f, CleanlinessSystem.Dirt(0f));
    Assert.AreEqual(0.5f, CleanlinessSystem.Dirt(50f));
    Assert.AreEqual(0f, CleanlinessSystem.Dirt(100f));
    Assert.AreEqual(0f, CleanlinessSystem.Dirt(140f));
    Assert.AreEqual(0.85f, CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 0f));
    Assert.AreEqual(0.65f, CleanlinessSystem.FillMultiplier(LuxuryBand.Mid, 0f));
    Assert.AreEqual(0.35f, CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 0f));
    Assert.AreEqual(0.85f, CleanlinessSystem.FillMultiplier(LuxuryBand.None, 0f));
    Assert.AreEqual(0.925f, CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 50f), 0.001f);
    Assert.AreEqual(1f, CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 140f));
    Assert.AreEqual(0.08f, CleanlinessSystem.IllnessPerMinute(0f));
    Assert.AreEqual(0.04f, CleanlinessSystem.IllnessPerMinute(50f));
    Assert.AreEqual(0f, CleanlinessSystem.IllnessPerMinute(100f));
    Assert.AreEqual(0.60f, CleanlinessSystem.RestaurantWeightFactor(0f));
    Assert.AreEqual(0.80f, CleanlinessSystem.RestaurantWeightFactor(50f));
    Assert.AreEqual(1f, CleanlinessSystem.RestaurantWeightFactor(140f));
}

[Test]
public void Treatment_table_matches_the_pump_clock_shape()
{
    CollectionAssert.AreEqual(new[] { 12, 20, 32, 48 },
        new[] { 0, 1, 2, 3 }.Select(CleanlinessSystem.TreatmentSupply).ToArray());
    Assert.AreEqual(30000, CleanlinessSystem.TreatmentUpgradeFee(1));
    Assert.AreEqual(70000, CleanlinessSystem.TreatmentUpgradeFee(2));
    Assert.AreEqual(140000, CleanlinessSystem.TreatmentUpgradeFee(3));
    Assert.AreEqual(0, CleanlinessSystem.TreatmentUpgradeFee(4));
    Assert.AreEqual(1440, CleanlinessSystem.TreatmentUpgradeMinutes(1));
    Assert.AreEqual(4320, CleanlinessSystem.TreatmentUpgradeMinutes(2));
    Assert.AreEqual(10080, CleanlinessSystem.TreatmentUpgradeMinutes(3));
}
```

- [ ] **Step 2: Run `CleanlinessSystemTests` and confirm the type is missing**

```
"C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" -runTests -testPlatform EditMode -testFilter CleanlinessSystemTests -testResults "C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\TestResults\cleanliness-task1.xml" -logFile "C:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\TestResults\cleanliness-task1.log"
```

Expected: FAIL. Do not pass `-quit`.

- [ ] **Step 3: Implement the formulas**

```csharp
public static class CleanlinessSystem
{
    static readonly int[] Outputs = { 12, 20, 32, 48 };
    static readonly int[] Fees = { 30000, 70000, 140000 };
    static readonly int[] Minutes = { 1440, 4320, 10080 };

    public static float Target(int supply, int sewage) =>
        sewage <= 0 ? 100f : supply / (float)sewage * 100f;

    public static float Step(float stored, float target) =>
        stored + (target - stored) * 0.10f;

    public static float Dirt(float stored) =>
        stored >= 100f ? 0f : 1f - stored / 100f;

    public static float FillMultiplier(LuxuryBand band, float stored)
    {
        var cut = band == LuxuryBand.Mid ? 0.35f : band == LuxuryBand.Upper ? 0.65f : 0.15f;
        return 1f - cut * Dirt(stored);
    }

    public static float IllnessPerMinute(float stored) => 0.08f * Dirt(stored);
    public static float RestaurantWeightFactor(float stored) => 1f - 0.40f * Dirt(stored);

    public static int TreatmentSupply(int level) =>
        level < 0 || level > 3 ? 0 : Outputs[level];

    public static int TreatmentUpgradeFee(int nextLevel) =>
        nextLevel < 1 || nextLevel > 3 ? 0 : Fees[nextLevel - 1];

    public static int TreatmentUpgradeMinutes(int nextLevel) =>
        nextLevel < 1 || nextLevel > 3 ? 0 : Minutes[nextLevel - 1];
}
```

Write a full MonoImporter `.meta` for the new script and the test. If Unity writes a two-line guid stub, expand it and keep that guid.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: measure water cleanliness`

---

### Task 2: Treatment room

**Files:**
- Create: `Assets/Resources/Rooms/ServiceTreatment.asset` and `.meta` by copying `ServicePump.asset`
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (one Utility `TryAddRoomButton` for `Rooms/ServiceTreatment`)
- Test: `Assets/Tests/EditMode/CleanlinessRoomTests.cs`

**Interfaces:**
- Consumes: room id `service_treatment`
- Produces: a loadable `RoomTypeSO` with the catalog row in the Global Constraints

- [ ] **Step 1: Write the failing test**

Load `Resources/Rooms/ServiceTreatment`. Assert id `service_treatment`, display name `Water Treatment`, stars 1, size (6, 1), cost 90000, noise 0.40, color about (0.32, 0.46, 0.40), `allowAboveGround` false, `allowBasement` true, housekeeping false, income none, family Utility, category Service.

- [ ] **Step 2: Run `CleanlinessRoomTests` and confirm the load fails**

- [ ] **Step 3: Add the asset and the menu button**

Copy the pump YAML. Set the catalog fields. Give the asset its own `.meta` guid. Insert one button next to the pump button. Path-limit `TowerHudController.cs` as described in Global Constraints.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: add the basement water treatment room`

---

### Task 3: Save the percent

**Files:**
- Modify: `Assets/Scripts/Persistence/SaveContracts.cs`
- Modify: `Assets/Scripts/Persistence/TowerSnapshotMapper.cs`
- Modify: `Assets/Scripts/Economy/CleanlinessSystem.cs` (`IsAbsent`)
- Test: `Assets/Tests/EditMode/CleanlinessSystemTests.cs`

**Interfaces:**
- Consumes: `TowerSnapshotV1`
- Produces: `CleanlinessSnapshotV1` with `float percent`, and `CleanlinessSystem.IsAbsent(CleanlinessSnapshotV1 snapshot)`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void Absent_snapshot_means_clean_and_a_saved_zero_stays_zero()
{
    Assert.IsTrue(CleanlinessSystem.IsAbsent(null));
    Assert.IsFalse(CleanlinessSystem.IsAbsent(new CleanlinessSnapshotV1 { percent = 0f }));
    Assert.AreEqual(0f, new CleanlinessSnapshotV1 { percent = 0f }.percent);
}
```

Also round-trip a tower snapshot through `TowerSnapshotMapper`: a null `cleanliness` field restores 100, and a snapshot whose `cleanliness.percent` is 0 restores 0. Do not bump `SaveSchema.CurrentVersion`.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Add the optional snapshot**

```csharp
public sealed class CleanlinessSnapshotV1
{
    public float percent;
}
```

Add `public CleanlinessSnapshotV1 cleanliness;` on `TowerSnapshotV1`. `IsAbsent` returns true only for null. In the mapper, write the field only when capturing, and when restoring use 100 if `IsAbsent`, otherwise the saved percent.

- [ ] **Step 4: Re-run `CleanlinessSystemTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: save the cleanliness percent`

---

### Task 4: Drift once an hour

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Test: `Assets/Tests/EditMode/CleanlinessTickTests.cs`

**Interfaces:**
- Consumes: `CleanlinessSystem.Target`, `Step`, `TreatmentSupply`, `PowerSystem.TickUpgrade`
- Produces: `TowerSimulation.Cleanliness` (the stored percent) and `TickCleanliness(float deltaGameMinutes)`. A powered unbroken treatment room adds its supply. A hotel guest, office worker, or sold condo adds 1 sewage. Each whole hour recounts, then steps. Remainder carries.

- [ ] **Step 1: Write the failing test**

Build a tower with stored cleanliness 100 and no treatment. Two sold condos make sewage 2 and supply 0, so the target is 0. `TickCleanliness(150f)` runs two steps: 90, then 81. A further `TickCleanliness(30f)` does not step. A powered, unbroken level-0 treatment room with sewage 12 targets 100. A broken or unpowered treatment room adds 0. Tick the treatment clock with `PowerSystem.TickUpgrade` for `TreatmentUpgradeMinutes(1)` while healthy and assert the level becomes 1 and supply becomes 20.

- [ ] **Step 2: Run `CleanlinessTickTests` and confirm failure**

- [ ] **Step 3: Wire the hour loop and the treatment clock**

Keep the power list and the water-pressure pass as they are. On load, the existing power pass finishes before the first cleanliness step, so a treatment room that loads dark adds 0. Add the cleanliness remainder next to the power clocks. `TickCleanliness` adds the delta, and while the remainder is at least 60, subtracts 60, recounts supply and sewage, and replaces the stored percent with `Step`. Treatment rooms use id `service_treatment`. Their clock uses `PowerSystem.TickUpgrade` and pauses while `IsBroken`. Do not snap the percent when a room is placed.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: drift cleanliness every game hour`

Path-limit `TowerSimulation.cs`.

---

### Task 5: Illness from dirty water

**Files:**
- Modify: `Assets/Scripts/Economy/HealthSystem.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Test: `Assets/Tests/EditMode/CleanlinessTickTests.cs`

**Interfaces:**
- Consumes: `CleanlinessSystem.IllnessPerMinute`, the stored percent, sewage rooms
- Produces: once a game minute, a floor with a sewage room gains that illness on the existing meter. A floor without one gains 0 from the water.

- [ ] **Step 1: Write the failing test**

At stored 0, one minute on a floor with a sold condo raises illness by 0.08. At stored 50 the same floor rises by 0.04. A floor with only a shop rises by 0. Existing decay is unchanged.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Add the water term inside the existing per-minute health pass**

Call `IllnessPerMinute(stored)` once, then add it to each floor that currently has a sewage room. Do not add a second illness meter. Do not change decay, guest illness, sick rooms, or quarantine.

- [ ] **Step 4: Re-run `CleanlinessTickTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: add illness from dirty water`

Path-limit `TowerSimulation.cs`.

---

### Task 6: Slow move-ins

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Test: `Assets/Tests/EditMode/CleanlinessTickTests.cs`

**Interfaces:**
- Consumes: `CleanlinessSystem.FillMultiplier`, `TowerSimulation.Cleanliness`
- Produces: hotel, office, and condo fill each multiply by the band slope. Upper hotel and condo pressure multipliers still apply on top.

- [ ] **Step 1: Write the failing test**

With stored cleanliness 0, a Base hotel fill is 0.85 times its pre-cleanliness fill, a Mid office is 0.65 times, and an Upper condo is 0.35 times. At stored 140 all three multipliers are 1.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Multiply at the three existing fill sites**

Use the same effective band those sites already use, so a missing band is Base. Put the cleanliness multiply next to the existing health and pressure multiplies.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: slow move-ins when the water is dirty`

---

### Task 7: Fewer restaurant walk-ins

**Files:**
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs`
- Test: `Assets/Tests/EditMode/CleanlinessTickTests.cs`

**Interfaces:**
- Consumes: `CleanlinessSystem.RestaurantWeightFactor`, `BuildSubgroup.Food`
- Produces: Food street-visit weight times the factor. Retail weight is unchanged. Pay per visit is unchanged. `PublicRestroomStreetMultiplier` still stacks.

- [ ] **Step 1: Write the failing test**

A Food room with catalog street weight 2 at stored cleanliness 0 contributes weight 1.2. At stored 100 it contributes 2. A retail room at stored 0 keeps its catalog weight.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Multiply only Food weights**

In the street-weight read used for visit choice, if `buildSubgroup` is `Food`, multiply by `RestaurantWeightFactor`. Leave the restroom bonus in place.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: send fewer diners when the water is dirty`

---

### Task 8: Cleanliness HUD

**Files:**
- Modify: `Assets/Scripts/UI/PowerHudFormat.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Test: `Assets/Tests/EditMode/CleanlinessHudTests.cs`

**Interfaces:**
- Consumes: stored percent, treatment level and output, `TreatmentUpgradeFee`, `TreatmentUpgradeMinutes`, `PowerSystem.TryBeginUpgrade`
- Produces: `PowerHudFormat.FormatClean(float stored)`, selection `Treatment {level} · {output}`, upgrade button for `service_treatment` with max level 3

- [ ] **Step 1: Write the failing test**

```csharp
Assert.AreEqual("Clean 72%", PowerHudFormat.FormatClean(72.4f));
Assert.AreEqual("Clean 140%", PowerHudFormat.FormatClean(140f));
```

Assert the selection helper returns `Treatment Built · 12` for level 0 and `Treatment II · 32` for level 2. Assert the button text for destination 2 is `Upgrade to II · 3 days · $70,000`.

- [ ] **Step 2: Run `CleanlinessHudTests` and confirm failure**

- [ ] **Step 3: Draw the chip and the treatment controls**

Draw `FormatClean` immediately after the water chip in both branches of `DrawTopInfoBar`. On a selected `service_treatment` room, show the level line and the upgrade button. The button calls `TryBeginUpgrade` with max level 3, the treatment fee, and the treatment minutes. Hide it at level 3. A short wallet does not start the clock. Path-limit `TowerHudController.cs`.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: show cleanliness and treatment upgrades in the HUD`

---

## Self-review

- Spec §3 → Tasks 1 and 4. §4.1 → Task 6. §4.2 → Task 5. §4.3 → Task 7. §5 → Task 2. §6 → Task 8. §7 → Task 3. §9 tests are named in the task steps.
- No task bumps `SaveSchema.CurrentVersion` or adds a dollhouse plate.
- `Target`, `Step`, `Dirt`, `FillMultiplier`, `IllnessPerMinute`, `RestaurantWeightFactor`, and the treatment table names are the same in later tasks as in Task 1.
- The treatment clock calls `PowerSystem.TickUpgrade` and `TryBeginUpgrade`. It does not invent a second clock.
