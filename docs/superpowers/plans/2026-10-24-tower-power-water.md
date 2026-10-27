# Tower Power and Water Pressure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a building-wide power pool with upgradeable basement plants, and a tower water-pressure percent fed by upgradeable basement pumps.

**Architecture:** `PowerSystem` owns cost, capacity, the four-band greedy assignment, and the plant upgrade table. `WaterSystem` owns height, demand, pressure, upper-room fill, daily stress, and the pump table. `TowerSimulation` ticks clocks and runs power before water. Dark rooms close in `AgentSystem`. A shaft is stopped when the player flag is set or the shaft is dark.

**Tech Stack:** Unity 6000.4.7f1, C#, EditMode NUnit tests under `Assets/Tests/EditMode`.

**Spec:** `docs/superpowers/specs/2026-10-24-tower-power-water-design.md`

## Global Constraints

- Free capacity is 20. Plant outputs by stored level 0..6 are 50, 80, 120, 170, 230, 300, 380. Fees are 40000, 90000, 180000, 310000, 480000, 690000. Durations in game minutes are 1440, 4320, 10080, 18720, 30240, 44640.
- Pump supplies by stored level 0..3 are 80, 120, 180, 260. Fees are 30000, 70000, 140000. Durations are 1440, 4320, 10080.
- Lobby, sky lobby, stairs, and `service_power` cost 0. Every elevator shaft costs 2 at any height. Every other room costs `size.y`. A room needs its whole cost or it stays dark. Broken rooms contribute 0 use and 0 supply.
- Bands, lowest `instanceId` first inside a band: 0 elevators, 1 occupied non-leisure, 2 other drawing rooms, 3 empty hotels and all leisure. If the leftover pool cannot cover a room, skip it and keep walking.
- The list runs on place, demolish, break, repair, and upgrade completion. Foot traffic does not run it.
- A powered-to-dark edge checks a guest out, ends an office lease, unsells a condo, and sends shoppers and cars out. A room that stays dark does not vacate again.
- Shaft stops when `playerMaintenance` is true or the shaft is dark. Restoring power does not clear `playerMaintenance`.
- Height = `max(1, highestFloorAtOrAboveLobby - lobbyFloor + 1)`. Demand = `height * 2 + upperHotels * 4 + upperCondos * 4`, minimum 1. `pressurePercent = supply / demand * 100`, uncapped.
- Upper hotel and upper condo fill multiplies by `0.25 + 0.75 * (pressurePercent / 100)` when pressure is under 100, else 1. Daily stress gain is `RoundToInt(8 * (1 - pressurePercent / 100))` when under 100, else 0. Own day-index gate. Stress caps at 100.
- `service_power`: 1★, 8×1, $140000, noise 0.45, color (0.48, 0.42, 0.32), basement only, draw 0. `service_pump`: 1★, 6×1, $75000, noise 0.35, color (0.40, 0.45, 0.48), basement only, draw 1. Both Utility, Service, no income, no staff, no housekeeping.
- Chips after the Save chip, from day one: `Power {use}/{capacity}` and `Water {RoundToInt(percent)}%`. Selection: `Power {cost}`, `Power plant {level} · {output}`, `Pump {level} · {supply}`, button `Upgrade to {level} · {days} days · ${fee}`.
- Save schema stays 1. Room fields `utilityLevel`, `upgradeMinutesRemaining`, `upgradeRunning`, `playerMaintenance` default to 0 / 0 / false / false. Powered set is not saved. Load runs one power pass. Sold condos and `StaffedWorkers > 0` count as occupied. Unsaved people count as absent.
- No dollhouse plates, no news lines, no water cleanliness, no staff wages on these rooms. Magenta `#FF00FF` is a key plate only.
- Commit with `git commit --only` on the task files. Do not `git add -A`. The working tree has unrelated art, tutorial, and fireworks edits.
- Unity EditMode: run without `-quit`. Example filter command is in each test step.

---

### Task 1: Power cost and plant output

**Files:**
- Create: `Assets/Scripts/Economy/PowerSystem.cs`
- Test: `Assets/Tests/EditMode/PowerSystemTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `PowerSystem.FreeCapacity = 20`, `Cost(RoomTypeSO type)`, `PlantOutput(int level)`, `PlantUpgradeFee(int nextLevel)`, `PlantUpgradeMinutes(int nextLevel)`, `Capacity(int[] plantLevels, bool[] plantBroken)`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void StairsLobbyAndPlantCostNothing()
{
    Assert.AreEqual(0, PowerSystem.Cost(StairsType()));
    Assert.AreEqual(0, PowerSystem.Cost(LobbyType()));
    Assert.AreEqual(0, PowerSystem.Cost(SkyLobbyType()));
    Assert.AreEqual(0, PowerSystem.Cost(PlantType()));
}

[Test]
public void ElevatorCostsTwoAtAnyHeight()
{
    Assert.AreEqual(2, PowerSystem.Cost(ElevatorType(height: 2)));
    Assert.AreEqual(2, PowerSystem.Cost(ElevatorType(height: 10)));
}

[Test]
public void OtherRoomsCostTheirFloorCount()
{
    Assert.AreEqual(1, PowerSystem.Cost(RoomType(sizeY: 1)));
    Assert.AreEqual(3, PowerSystem.Cost(AtriumType()));
}

[Test]
public void PlantLadderAndCapacity()
{
    CollectionAssert.AreEqual(new[] { 50, 80, 120, 170, 230, 300, 380 },
        Enumerable.Range(0, 7).Select(PowerSystem.PlantOutput).ToArray());
    Assert.AreEqual(70, PowerSystem.Capacity(new[] { 0 }, new[] { false }));
    Assert.AreEqual(20, PowerSystem.Capacity(new[] { 0 }, new[] { true }));
}
```

- [ ] **Step 2: Run the test and confirm it fails to compile**

Run Unity EditMode `-testFilter PowerSystemTests` without `-quit`.
Expected: compile failure because `PowerSystem` does not exist.

- [ ] **Step 3: Implement the table**

`Cost` returns 0 for `isLobby`, `isSkyLobby`, `isStairs`, or id `service_power`. It returns 2 for `isElevatorShaft`. Otherwise it returns `size.y`. `Capacity` returns `20 + sum of PlantOutput(level)` for entries whose broken flag is false. Levels outside 0..6 return 0 output. `PlantUpgradeFee` and `PlantUpgradeMinutes` take the destination level 1..6.

- [ ] **Step 4: Re-run `PowerSystemTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

`git commit --only` those two files. Message: `feat: price power by room height and plant level`

### Task 2: Power assignment

**Files:**
- Modify: `Assets/Scripts/Economy/PowerSystem.cs`
- Test: `Assets/Tests/EditMode/PowerSystemTests.cs`

**Interfaces:**
- Consumes: `Cost` from Task 1
- Produces: `PowerSystem.Assign(IReadOnlyList<PowerNeed> needs, int capacity)` returning the powered instance ids. `PowerNeed` is `(int instanceId, int cost, PowerBand band)`. `PowerBand` is `Elevator = 0, Occupied = 1, Other = 2, ShedFirst = 3`.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void ShedFirstGoesDarkBeforeOtherEmpty()
{
    var powered = PowerSystem.Assign(new[]
    {
        new PowerNeed(1, 1, PowerBand.ShedFirst),
        new PowerNeed(2, 1, PowerBand.Other)
    }, 1);
    CollectionAssert.AreEqual(new[] { 2 }, powered);
}

[Test]
public void ElevatorOutranksOccupiedAndEmpty()
{
    var powered = PowerSystem.Assign(new[]
    {
        new PowerNeed(3, 1, PowerBand.Elevator),
        new PowerNeed(2, 1, PowerBand.Occupied),
        new PowerNeed(1, 1, PowerBand.Other)
    }, 2);
    CollectionAssert.AreEquivalent(new[] { 3, 2 }, powered);
}

[Test]
public void OlderRoomInABandKeepsPower()
{
    var powered = PowerSystem.Assign(new[]
    {
        new PowerNeed(9, 1, PowerBand.Other),
        new PowerNeed(4, 1, PowerBand.Other)
    }, 1);
    CollectionAssert.AreEqual(new[] { 4 }, powered);
}

[Test]
public void SkipsARoomThatCostsMoreThanWhatIsLeft()
{
    var powered = PowerSystem.Assign(new[]
    {
        new PowerNeed(1, 3, PowerBand.Other),
        new PowerNeed(2, 2, PowerBand.Other)
    }, 2);
    CollectionAssert.AreEqual(new[] { 2 }, powered);
}
```

- [ ] **Step 2: Run `PowerSystemTests` and confirm the new tests fail**

- [ ] **Step 3: Implement `Assign`**

Order by band, then `instanceId`. Walk once. Subtract cost only when `remaining >= cost`.

- [ ] **Step 4: Re-run `PowerSystemTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: assign power by cutoff band`

### Task 3: Upgrade clock

**Files:**
- Modify: `Assets/Scripts/Economy/PowerSystem.cs`
- Test: `Assets/Tests/EditMode/PowerSystemTests.cs`

**Interfaces:**
- Consumes: plant fee and minute tables
- Produces: `bool TryBeginUpgrade(ref int level, ref float minutesRemaining, ref bool running, int maxLevel, int fee, int wallet, out int walletAfter)`. `TickUpgrade(ref int level, ref float minutesRemaining, ref bool running, float deltaGameMinutes, bool broken)` returns true when the level increases.

- [ ] **Step 1: Write the failing test**

A short wallet leaves level 0 and `running` false. A paid start sets minutes to 1440 and subtracts 40000. Ticking while broken does not reduce minutes. Ticking 1440 minutes while healthy sets level 1, `running` false, and reports completion. Level 6 cannot start.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Implement the two methods**

`TryBeginUpgrade` refuses when `running`, when `level >= maxLevel`, or when `wallet < fee`. `TickUpgrade` returns false immediately when `broken` or not `running`. On reaching 0, increment level, clear `running`, return true.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: time power upgrades and pause them while broken`

### Task 4: Save utility fields

**Files:**
- Modify: `Assets/Scripts/Persistence/SaveContracts.cs` (`RoomSnapshotV1`)
- Modify: `Assets/Scripts/Core/RoomInstance.cs` (capture and restore)
- Test: `Assets/Tests/EditMode/PowerSnapshotTests.cs`

**Interfaces:**
- Consumes: nothing from `PowerSystem`
- Produces: `RoomInstance.UtilityLevel`, `UpgradeMinutesRemaining`, `UpgradeRunning`, `PlayerMaintenance`, `Powered`. Capture writes them. Restore reads them. `Powered` is not a snapshot field. A missing field stays at the CLR default because these are value fields on the existing snapshot class.

- [ ] **Step 1: Write the failing test**

Round-trip level 2, 100 minutes, running true, player maintenance true. A snapshot built without setting the new fields restores level 0, clock stopped, player maintenance false.

- [ ] **Step 2: Run `PowerSnapshotTests` and confirm failure**

- [ ] **Step 3: Add the fields next to `sick` and copy them in `CaptureSnapshot` and `RestoreSnapshot`**

Do not change `SaveSchema.CurrentVersion`.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: save utility level and elevator maintenance`

### Task 5: Power plant room

**Files:**
- Create: `Assets/Resources/Rooms/ServicePower.asset` and its `.meta` by duplicating `ServiceLoadingDock.asset`
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (one `TryAddRoomButton` for `Rooms/ServicePower` in the Utility list)
- Test: `Assets/Tests/EditMode/PowerRoomCatalogTests.cs`

**Interfaces:**
- Consumes: room id `service_power`
- Produces: a loadable `RoomTypeSO` with the catalog row in the Global Constraints

- [ ] **Step 1: Write the failing test**

Load `Resources/Rooms/ServicePower`. Assert id, display name `Power Plant`, stars 1, size (8, 1), cost 140000, noise 0.45, colors, `allowAboveGround` false, `allowBasement` true, housekeeping false, income none, family Utility, category Service.

- [ ] **Step 2: Run `PowerRoomCatalogTests` and confirm the load fails**

- [ ] **Step 3: Add the asset and the menu button**

Copy the loading-dock YAML. Set `allowAboveGround: 0`, `allowBasement: 1`, `requiredStars: 1`, `buildFamily: 5`, `category: 7`. Insert the button next to the other Utility loads. Commit only the new asset, its meta, and the Utility-button hunk of `TowerHudController.cs`. That file has unrelated unstaged edits. Stage the button hunk only, the same way the health HUD commits did.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: add the basement power plant`

### Task 6: Run the power list in the simulation

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Scripts/Core/RoomInstance.cs` (`Powered` set by the list)
- Test: `Assets/Tests/EditMode/PowerTickTests.cs`

**Interfaces:**
- Consumes: `PowerSystem.Assign`, `TickUpgrade`, `Capacity`, `Cost`
- Produces: `TowerSimulation.Power`. After place, demolish, break, repair, upgrade completion, and the post-load restore, every drawing room's `Powered` matches `Assign`. Clock tick uses game minutes and calls `TickUpgrade`.

- [ ] **Step 1: Write the failing test**

Build a grid with one elevator (cost 2), one empty hotel (cost 1), and capacity 20. Both are powered. Add rooms until the empty hotel is the one that goes dark and the elevator stays powered. Completing a plant upgrade from level 0 to 1 raises capacity by 30 (50 to 80) and can turn that hotel back on. A broken plant drops its output to 0.

- [ ] **Step 2: Run `PowerTickTests` and confirm failure**

- [ ] **Step 3: Wire the triggers**

Keep the health, tutorial, and cloud edits that are already in `TowerSimulation.cs` unstaged. Commit only the power hunks.

Build `PowerNeed` from the grid at each trigger. Band 3: empty hotel or leisure. Band 0: elevator. Band 1: occupied non-leisure. Band 2: the rest. Occupied means a tenant, a sold condo, a shopper inside, a car inside, or `StaffedWorkers > 0`.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: power rooms from the plant pool`

### Task 7: Close dark rooms

**Files:**
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Modify: `Assets/Scripts/Transit/ElevatorShaftRuntime.cs` and the maintenance button path in `TowerHudController.cs` / `BuildController`
- Test: `Assets/Tests/EditMode/PowerTickTests.cs`

**Interfaces:**
- Consumes: `RoomInstance.Powered`, `PlayerMaintenance`
- Produces: dark hotels, offices, and condos refuse new tenants. Dark shops are not visit targets. Dark parking refuses a car. Dark clinic, trash, and restroom coverage treats the room as absent. Dark housekeeping sends no maids. Dark clinic sends no doctors. The powered-to-dark edge vacates once. `InMaintenance` is true when `PlayerMaintenance || !Powered` for a shaft.

- [ ] **Step 1: Write the failing test**

A dark hotel returns false from `TryFindHotelRoomForGuest`. A room that flips from powered to dark checks the guest out once. Flipping it again while it stays dark does not check anyone out a second time. `PlayerMaintenance` true and `Powered` true stays in maintenance. `PlayerMaintenance` true and a later `Powered` false, then `Powered` true, stays in maintenance.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Add the gates on the existing early-return paths**

Follow the quarantine early-returns in `TryFindHotelRoomForGuest`, `TryFindOfficeDeskForWorker`, and `TryFindCondoForBuyer`. Shop picking returns null for a dark shop the same way a broken shop is skipped. Coverage helpers skip unpowered rooms the same way they skip broken rooms.

- [ ] **Step 4: Re-run `PowerTickTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: close rooms and shafts that lose power`

Commit only the power hunks in `TowerHudController.cs`.

### Task 8: Power HUD

**Files:**
- Create: `Assets/Scripts/UI/PowerHudFormat.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Test: `Assets/Tests/EditMode/PowerHudTests.cs`

**Interfaces:**
- Consumes: use and capacity integers, pressure percent, plant or pump level and output
- Produces: `PowerHudFormat.FormatPower(int use, int capacity)`, `FormatWater(float pressurePercent)`, `FormatRoomCost(int cost)`, `FormatPlant(int level, int output)`, `FormatPump(int level, int supply)`, `FormatUpgrade(int nextLevel, int days, int fee)`

- [ ] **Step 1: Write the failing test**

`FormatPower(48, 70)` is `Power 48/70`. `FormatPower(80, 70)` is `Power 80/70`. `FormatWater(72.4f)` is `Water 72%`. `FormatWater(148f)` is `Water 148%`. `FormatPlant(0, 50)` is `Power plant Built · 50`. `FormatPlant(2, 120)` is `Power plant II · 120`. `FormatUpgrade(2, 3, 90000)` is `Upgrade to II · 3 days · $90,000`.

- [ ] **Step 2: Run `PowerHudTests` and confirm failure**

- [ ] **Step 3: Draw the chips after the Save chip in both the economy-unlocked and the pre-economy branches of `DrawTopInfoBar`**

Selection uses the formatters. The upgrade button calls `TryBeginUpgrade` and spends the fee. Hide it at max level.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: show power use and plant upgrades in the HUD`

Path-limit the HUD file to the power hunks.

### Task 9: Water pressure math

**Files:**
- Create: `Assets/Scripts/Economy/WaterSystem.cs`
- Test: `Assets/Tests/EditMode/WaterSystemTests.cs`

**Interfaces:**
- Consumes: nothing from the scene
- Produces: `Height(int lobbyFloor, int highestFloorAtOrAboveLobby)`, `Demand(int height, int upperHotels, int upperCondos)`, `PressurePercent(int supply, int demand)`, `PumpSupply(int level)`, `UpperFillMultiplier(float pressurePercent)`, `UpperStressGain(float pressurePercent)`, pump fee and minute methods for destination levels 1..3

- [ ] **Step 1: Write the failing test**

Height(0, 14) is 15. Height(0, 0) is 1. Demand(15, 4, 2) is 54. PressurePercent(80, 54) is `80f / 54f * 100f`. Demand(40, 10, 0) is 80, plus 10 upper rooms is 120, and PressurePercent(80, 120) is `80f / 120f * 100f`. Fill is 0.25 at 0, 0.625 at 50, 1 at 100 and at 140. Stress is 8, 4, and 0 for those three pressures. Pump supplies are 80, 120, 180, 260.

- [ ] **Step 2: Run `WaterSystemTests` and confirm failure**

- [ ] **Step 3: Implement the formulas from the spec**

Demand uses `Math.Max(1, height * 2 + upperHotels * 4 + upperCondos * 4)`.

- [ ] **Step 4: Re-run**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: measure tower water pressure`

### Task 10: Pump room

**Files:**
- Create: `Assets/Resources/Rooms/ServicePump.asset` and `.meta` from the loading-dock YAML
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (one Utility button)
- Test: `Assets/Tests/EditMode/PowerRoomCatalogTests.cs` (add the pump asserts in this file)

**Interfaces:**
- Consumes: id `service_pump`
- Produces: the pump catalog row in the Global Constraints

- [ ] **Step 1: Extend the catalog test for the pump row**

- [ ] **Step 2: Run and confirm the new asserts fail**

- [ ] **Step 3: Add the asset and the menu button**

Size (6, 1), cost 75000, noise 0.35, color (0.40, 0.45, 0.48), basement only, stars 1.

- [ ] **Step 4: Re-run `PowerRoomCatalogTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: add the basement water pump`

### Task 11: Apply pressure to upper rooms and the water chip

**Files:**
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Test: `Assets/Tests/EditMode/WaterSystemTests.cs` and `Assets/Tests/EditMode/PowerTickTests.cs`

**Interfaces:**
- Consumes: `WaterSystem` formulas, `RoomInstance.Powered`, pump `UtilityLevel`
- Produces: `TowerSimulation.Water`. Supply sums powered, unbroken pumps. Upper hotel and upper condo fill multiply by `UpperFillMultiplier`. Upper occupants gain `UpperStressGain` once per day on their own day index. The water chip uses `FormatWater`. An unpowered pump adds 0. Pump upgrades use `TickUpgrade` with max level 3 and the pump fee table.

- [ ] **Step 1: Write the failing test**

One unpowered built pump supplies 0, so a 15-floor tower with 4 upper hotels and 2 upper condos is at 0% and upper fill is 0.25. Powering that pump moves pressure to about 148% and upper fill to 1. A base hotel's fill path does not call the water multiplier. An upper guest at 0% gains 8 stress on the first day and 0 on the same day index again.

- [ ] **Step 2: Run and confirm failure**

- [ ] **Step 3: Multiply fill only when `LuxuryBand.Upper` and the category is hotel or condo**

Call it beside the existing health fill multiply in `TryFindHotelRoomForGuest` and `TryFindCondoForBuyer`. Do not multiply office fill. Run water after the power list.

- [ ] **Step 4: Re-run `WaterSystemTests` and `PowerTickTests`**

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: slow upper rooms when water pressure is low`

Path-limit `TowerSimulation.cs` and `TowerHudController.cs` to these hunks.

---

## Self-review

- Spec §3.1–3.2 → Tasks 1 and 3. §3.3–3.4 → Tasks 2, 6, and 7. §4 → Tasks 9 and 11. §5 → Tasks 5 and 10. §6 → Tasks 8 and 11. §7 → Task 4 and the load pass in Task 6. §9 tests are named in the task steps.
- No task tells the implementer to bump `SaveSchema.CurrentVersion` or to add dollhouse plates.
- `PowerNeed`, `Assign`, `TickUpgrade`, and the water formula names are the same in later tasks as in the task that produces them.
