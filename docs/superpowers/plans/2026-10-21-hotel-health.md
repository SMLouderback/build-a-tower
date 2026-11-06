# Hotel Health Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-floor illness meter, four Utility rooms, doctor visits, maid clean modes, fill slowdown, and a city quarantine.

**Architecture:** `HealthSystem` owns floor illness, sick share, and the quarantine latch. `HealthCoverage` answers trash and restroom range questions. `HealthNews` emits ticker lines. `AgentSystem` applies guest stress, runs doctors, and blocks new entry during quarantine. Crime stays untouched.

**Tech Stack:** Unity, C#, EditMode NUnit tests under `Assets/Tests/EditMode`.

## Global Constraints

- `HealthSystem` does not reference `CrimeSystem`.
- Illness is 0–100 per floor. Tower average counts built floors with no entry as 0.
- Guest raise is `+0.04` per hotel guest per minute only when trash does not cover the floor. Each sick hotel, office, or condo on the floor adds `+0.15` per minute.
- Natural decay is `0.05` per minute. Each working clinic adds `0.04` on every floor plus `0.20 * (1 - distance/8)` for distance 0–8.
- A floor at illness `>= 40` for `360` consecutive minutes marks one room sick (hotel preferred, else lowest instance id), then the timer resets.
- Doctor completion subtracts `18` illness (floor at 0) and clears the sick flag. Treatment is `24*60` to `48*60` game minutes after arrival.
- Sick share counts only hotels, offices, and condos. Fill is `1` below the category threshold. At the threshold it is `0.85`, then linear to `0` at `0.25`. Thresholds: hotel `0.10`, office `0.15`, condo `0.20`.
- Quarantine turns on at sick share `>= 0.25` and off only at `<= 0.22`. No new street entry, street shoppers, hotel check-ins, office leases, or condo sales. Nobody is evicted.
- Clean mode multipliers: Quick `0.6` / `0.25`, Normal `1.0` / `0.08`, Deep `1.6` / `0.02`. Tenant rooms get the sick flag. Restrooms and venues get re-dirtied instead.
- Rooms: `service_public_restroom` 0★ 3×1 $18000, `service_office_restroom` 0★ 3×1 $22000, `service_trash` 1★ 6×1 $48000, `service_clinic` 2★ 6×1 $85000. Clinic wage `$400`, auto-hire 1, staff cap 4.
- Trash range 20 cells and 3 floors. Public restroom 12 cells, 2 floors, capacity 3, street weight `×1.05` once. Office restroom 16 cells, 1 floor, capacity 4, `-2` stress once per day, Base and Mid offices only.
- Optional `HealthSnapshotV1`. Missing block means healthy, Normal, quarantine off. `RoomSnapshotV1.sick` defaults false. Do not bump `SaveSchema.CurrentVersion`.
- No dollhouse plates, no health map, no power or water, no garbage trucks, no wage change for maids.
- Magenta `#FF00FF` is a key plate only if art is touched. Do not recolor existing rooms.

---

### Task 1: Floor illness tick

**Files:**
- Create: `Assets/Scripts/Economy/HealthSystem.cs`
- Test: `Assets/Tests/EditMode/HealthSystemTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `HealthSystem` with `const float MaxIllness = 100f`, `const float GuestRaisePerMinute = 0.04f`, `const float SickRoomRaisePerMinute = 0.15f`, `const float NaturalDecayPerMinute = 0.05f`, `const float ClinicBaselinePerMinute = 0.04f`, `const float ClinicProximityPerMinute = 0.20f`, `const int ClinicProximityFloors = 8`, `GetIllness(int floor)`, `SetIllness(int floor, float value)`, `AverageIllness`, `Tick(float deltaGameMinutes, IReadOnlyDictionary<int, int> hotelGuestsByFloor, IReadOnlyDictionary<int, bool> trashCoveredByFloor, IReadOnlyDictionary<int, int> sickRoomsByFloor, IReadOnlyList<int> workingClinicFloors, int towerFloorCount)`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using System.Collections.Generic;

namespace BuildATower.Tests
{
    public class HealthSystemTests
    {
        [Test]
        public void Guest_floor_without_trash_rises()
        {
            var health = new HealthSystem();
            health.Tick(1f,
                new Dictionary<int, int> { [3] = 2 },
                new Dictionary<int, bool> { [3] = false },
                new Dictionary<int, int>(),
                new List<int>(),
                towerFloorCount: 1);
            Assert.AreEqual(0.08f, health.GetIllness(3), 0.0001f);
        }

        [Test]
        public void Guest_floor_with_trash_does_not_rise()
        {
            var health = new HealthSystem();
            health.Tick(1f,
                new Dictionary<int, int> { [3] = 4 },
                new Dictionary<int, bool> { [3] = true },
                new Dictionary<int, int>(),
                new List<int>(),
                towerFloorCount: 1);
            Assert.AreEqual(0f, health.GetIllness(3), 0.0001f);
        }

        [Test]
        public void Clinic_on_its_floor_beats_eight_floors_away_and_stacks()
        {
            var near = new HealthSystem();
            near.SetIllness(0, 50f);
            near.SetIllness(8, 50f);
            near.Tick(1f, new Dictionary<int, int>(), new Dictionary<int, bool>(),
                new Dictionary<int, int>(), new List<int> { 0 }, towerFloorCount: 9);
            var farDrop = 50f - near.GetIllness(8);
            var nearDrop = 50f - near.GetIllness(0);
            Assert.Greater(nearDrop, farDrop);

            var two = new HealthSystem();
            two.SetIllness(0, 50f);
            two.Tick(1f, new Dictionary<int, int>(), new Dictionary<int, bool>(),
                new Dictionary<int, int>(), new List<int> { 0, 1 }, towerFloorCount: 2);
            Assert.Less(two.GetIllness(0), near.GetIllness(0));
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails to compile**

- [ ] **Step 3: Implement `HealthSystem.Tick`** with the constants above. Decay applies only to floors that currently have illness or that received a raise this tick. Clamp 0–100. `AverageIllness` divides the sum by `towerFloorCount` when that count is positive.

- [ ] **Step 4: Run `HealthSystemTests` and confirm they pass**

- [ ] **Step 5: Commit** `feat: tick per-floor illness from guests, sick rooms, and clinics`

### Task 2: Sick share, fill, quarantine

**Files:**
- Modify: `Assets/Scripts/Economy/HealthSystem.cs`
- Test: `Assets/Tests/EditMode/HealthSystemTests.cs`

**Interfaces:**
- Consumes: `HealthSystem` from Task 1
- Produces: `HealthSystem.FillMultiplier(float sickShare, float threshold)`, `HealthSystem.EvaluateQuarantine(bool currentlyOn, float sickShare)`, `const float QuarantineOn = 0.25f`, `const float QuarantineOff = 0.22f`, `const float HotelFillThreshold = 0.10f`, `const float OfficeFillThreshold = 0.15f`, `const float CondoFillThreshold = 0.20f`

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Fill_is_full_below_threshold_and_085_at_threshold()
{
    Assert.AreEqual(1f, HealthSystem.FillMultiplier(0.09f, HealthSystem.HotelFillThreshold), 0.0001f);
    Assert.AreEqual(0.85f, HealthSystem.FillMultiplier(0.10f, HealthSystem.HotelFillThreshold), 0.0001f);
    Assert.AreEqual(0f, HealthSystem.FillMultiplier(0.25f, HealthSystem.HotelFillThreshold), 0.0001f);
    Assert.AreEqual(0.85f, HealthSystem.FillMultiplier(0.15f, HealthSystem.OfficeFillThreshold), 0.0001f);
    Assert.AreEqual(0.85f, HealthSystem.FillMultiplier(0.20f, HealthSystem.CondoFillThreshold), 0.0001f);
}

[Test]
public void Quarantine_latches_until_22_percent()
{
    Assert.IsFalse(HealthSystem.EvaluateQuarantine(false, 0.24f));
    Assert.IsTrue(HealthSystem.EvaluateQuarantine(false, 0.25f));
    Assert.IsTrue(HealthSystem.EvaluateQuarantine(true, 0.23f));
    Assert.IsFalse(HealthSystem.EvaluateQuarantine(true, 0.22f));
}
```

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Implement the two pure functions exactly as the spec formula in §4**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: add health fill multipliers and quarantine latch`

### Task 3: Coverage

**Files:**
- Create: `Assets/Scripts/Economy/HealthCoverage.cs`
- Test: `Assets/Tests/EditMode/HealthCoverageTests.cs`

**Interfaces:**
- Consumes: room origin, size, type id, broken, dirty
- Produces: `HealthCoverage.TrashCovers(int floor, int originX, IReadOnlyList<RoomInstance> rooms)`, `HealthCoverage.AssignPublicRestrooms(...)`, `HealthCoverage.AssignOfficeRestrooms(...)`. A public assignment yields at most 3 need rooms per restroom and a street bonus flag. An office assignment yields at most 4 Base/Mid offices and ignores Upper.

- [ ] **Step 1: Tests for trash range (20 cells, 3 floors), broken trash covering nothing, public capacity 3 with `×1.05` once, office restroom ignoring Upper and capping at 4**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Implement nearest-with-capacity assignment. Tie-break lower instance id. Dirty or broken restrooms cover nothing.**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: add trash and restroom coverage queries`

### Task 4: News lines

**Files:**
- Create: `Assets/Scripts/Economy/HealthNews.cs`
- Test: `Assets/Tests/EditMode/HealthNewsTests.cs`

**Interfaces:**
- Consumes: previous sick share, new sick share, previous quarantine, new quarantine
- Produces: `HealthNews.Collect(float previousShare, float newShare, bool wasQuarantined, bool quarantined)` returning the spec §8 strings in order, each at most once per call

- [ ] **Step 1: Test a jump from 0 to 0.25 emits the 0.08, 0.10, 0.13, 0.15, 0.18, 0.20, 0.23 warnings and the quarantine major line. A later call that stays at 0.25 emits nothing. Dropping quarantine emits the lifted line.**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Implement the crossings. Ops lines use `TowerNewsCategory.OpsSerious`. Quarantine on and off use `TowerNewsCategory.MajorEvent`.**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: add illness threshold news lines`

### Task 5: Clean mode

**Files:**
- Create: `Assets/Scripts/Economy/CleanMode.cs`
- Test: `Assets/Tests/EditMode/CleanModeTests.cs`

**Interfaces:**
- Produces: `enum CleanMode { Quick, Normal, Deep }`, `CleanModeRules.TimeMultiplier(CleanMode)`, `CleanModeRules.IllnessChance(CleanMode)`, `CleanModeRules.MarksTenantSick(RoomCategory)`, `CleanModeRules.RedirtiesInstead(string roomTypeId)`

- [ ] **Step 1: Test Quick/Normal/Deep multipliers `0.6/1/1.6` and chances `0.25/0.08/0.02`. Hotels, offices, and condos mark sick. `service_public_restroom`, `service_office_restroom`, conference, and event hall redirty.**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Implement the table from spec §7**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: add maid clean mode speed and illness risk`

### Task 6: Sick flag and save

**Files:**
- Modify: `Assets/Scripts/Core/RoomInstance.cs`
- Modify: `Assets/Scripts/Persistence/SaveContracts.cs`
- Modify: `Assets/Scripts/Persistence/TowerSnapshotMapper.cs`
- Test: `Assets/Tests/EditMode/HealthSnapshotTests.cs`

**Interfaces:**
- Produces: `RoomInstance.Sick`, `RoomInstance.SetSick(bool)`, `HealthSnapshotV1` with `float[] floorIndices`, `float[] illness`, `string cleanMode`, `bool quarantined`. Null health block restores Normal, no illness, quarantine off. Absent `sick` on a room restores false. Do not change `SaveSchema.CurrentVersion`.

- [ ] **Step 1: Test a snapshot with `health == null` and rooms without `sick` restores a healthy tower. A round-trip keeps illness on floor 4, Deep clean, quarantine on, and one sick room.**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Add the fields and mapper branches beside the existing optional `weather` block**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: persist illness, clean mode, quarantine, and sick rooms`

### Task 7: Catalog rooms

**Files:**
- Create: `Assets/Resources/Rooms/ServicePublicRestroom.asset`
- Create: `Assets/Resources/Rooms/ServiceOfficeRestroom.asset`
- Create: `Assets/Resources/Rooms/ServiceTrash.asset`
- Create: `Assets/Resources/Rooms/ServiceClinic.asset`
- Modify: `Assets/Scripts/UI/TowerHudController.cs` menu load list
- Modify: `Assets/Scripts/Economy/EconomySystem.cs` clinic wage `$400`
- Test: `Assets/Tests/EditMode/HealthRoomCatalogTests.cs`

**Interfaces:**
- Produces: the four ids, sizes, stars, costs, noise, zones, and housekeeping flags from spec §5. Clinic `requires` staff cap behavior matches Housekeeping auto-hire 1.

- [ ] **Step 1: Test each asset loads with the spec id, size, stars, cost, and housekeeping flag**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Add the assets by copying Housekeeping or Mail and overwriting the serialized fields. Register the four buttons next to the other Utility rooms. Add `DoctorWagePerDay = 400` and bill `service_clinic` staff the same way maids are billed.**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: add clinic, trash, and restroom rooms`

### Task 8: Wire the meter, doctors, stress, and quarantine

**Files:**
- Modify: `Assets/Scripts/Agents/Agent.cs` (`AgentRole.Doctor`, treatment minutes remaining)
- Modify: `Assets/Scripts/Agents/AgentSystem.cs`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs`
- Test: `Assets/Tests/EditMode/HealthTickTests.cs`

**Interfaces:**
- Consumes: Tasks 1–7
- Produces: simulation tick builds the guest, trash, sick-room, and clinic-floor inputs and calls `HealthSystem.Tick`. Illness `>= 40` for `360` minutes calls `SetSick(true)` on one room. Hotel guests take `12 * illness/100` stress once per day. Doctors spawn from clinic staff. On arrival they roll `24*60..48*60` and on completion call the illness drop of `18` and `SetSick(false)`. Quarantine rejects new street spawns and new hotel, office, and condo tenancy. Maid finish uses `CleanModeRules`. Public and office restrooms are maid targets after hotels. Street weight `×1.05` when a shop or restaurant has a public-restroom assignment. Covered office workers lose `2` stress once per day. `HealthNews.Collect` results are pushed to `TowerNews`.

- [ ] **Step 1: Tests for the 360-minute mark preferring a hotel, doctor completion dropping illness by 18, quarantine blocking a new check-in, and a Quick-clean finish setting `Sick` on a hotel while redirtying a public restroom**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Implement the wiring. Keep the logic that decides numbers inside `HealthSystem`, `HealthCoverage`, `CleanModeRules`, and `HealthNews`. `AgentSystem` only applies those results.**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: run illness, doctors, clean risk, and quarantine`

### Task 9: HUD

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Test: `Assets/Tests/EditMode/HealthHudTests.cs`

**Interfaces:**
- Consumes: `HealthSystem.AverageIllness`, sick share, quarantine, `CleanMode`, `RoomInstance.Sick`, clinic `StaffedWorkers`
- Produces: top bar text with average illness and sick-share percent, plus a quarantine label when latched. Clinic selection shows `Doctors (n/4)`. Sick room selection says it is waiting on a doctor. Housekeeping selection sets the tower clean mode.

- [ ] **Step 1: Test a formatter that renders `Illness 12 · Sick 10%` and `Illness 40 · Sick 25% · Quarantine`**

- [ ] **Step 2: Confirm fail**

- [ ] **Step 3: Add the formatter and the three selection controls. Do not add a map mode.**

- [ ] **Step 4: Confirm pass**

- [ ] **Step 5: Commit** `feat: show illness, quarantine, and clean mode in the HUD`

---

## Spec coverage

Meter tick, clinic aura, trash gate: Task 1 and Task 8. Fill and quarantine: Task 2 and Task 8. Coverage and restroom bonuses: Task 3 and Task 8. News: Task 4 and Task 8. Clean mode: Task 5 and Task 8. Save: Task 6. Catalog and wages: Task 7. HUD: Task 9. Dollhouse plates, power, and water are out of scope on purpose.
