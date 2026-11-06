# Build-A-Tower — Tower Power and Water Pressure

**Date:** 2026-10-24
**Status:** Approved (brainstorming, section by section)
**Depends on:** Utility family and basement placement (`service_loading_dock` pattern); elevator `InMaintenance`; hotel, office, and condo fill multipliers; optional room fields on `RoomSnapshotV1` (schema stays 1)
**Engine target:** Unity (2D Tilemap), desktop/Editor-first
**Parent roadmap:** Hotel health → **this spec (power, then water pressure)** → water cleanliness later
**Out of scope:** Water cleanliness, a water use/capacity chip, water towers, treatment plants, power or water heatmaps, dollhouse plates, news lines, garbage trucks, staff on the plant or the pump

## 1. Goals

Add a building-wide power pool and a tower water-pressure percent. A basement power plant feeds the pool and can be upgraded on its own clock. Rooms placed without spare power stay dark and closed. A basement pump feeds pressure. Upper hotels and upper condos fill more slowly, and their occupants take stress, while pressure is under 100%.

### Success criteria

In Play Mode a player can:

1. Reach 2 stars on the free 20 power without a plant.
2. Place a 1★ basement power plant and a 1★ basement pump from Build → Utility.
3. Start an upgrade on a selected plant or pump, pay the fee, and see that room’s output rise when the clock finishes.
4. Place another plant or pump and upgrade it on its own clock at the same time.
5. See a new room stay closed, and a new elevator stay in maintenance, when the pool cannot cover that room’s whole cost.
6. See empty hotels and leisure lose power before other rooms, and see elevators keep power until every other room is dark.
7. Read `Power 48/70` and `Water 72%` on the top bar from the first day. Water may read above 100%.

EditMode tests cover cost, both ladders, cutoff and restore order, dark rooms, the player-maintenance flag, pressure, upper-room fill and stress, a paused clock, a refused fee, and an old save.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Shape | `PowerSystem` and `WaterSystem`. Power runs first. Water ignores a broken or unpowered pump. |
| Power geography | One pool for the whole tower |
| Water geography | One pressure percent for the whole tower |
| Free power | 20, from the first day. Enough for one elevator and a normal path to 2 stars. |
| Plant | Basement only. Unlocks at 1★. Draw 0. Output depends on its level. |
| More plants | Allowed. No cap. Each adds its own output and upgrades on its own clock. |
| Pump | Basement only. Unlocks at 1★. Draws 1. Supply depends on its level. |
| More pumps | Allowed. No cap. |
| Who draws power | Every room except the lobby, the sky lobby, stairs, and the power plant |
| Elevator cost | Flat 2, at any shaft height. Normal, express, and service shafts all cost 2. |
| Other room cost | `size.y` of the room type. A room needs its whole cost or it stays off. |
| When the list runs | Place, demolish, break, repair, or an upgrade finishes. Foot traffic does not run it. |
| Who is inside | Read at that moment. A shopper stepping in later does not move power. |
| Dark room | Closed. No tenants, customers, cars, income, or service effect. People inside leave. |
| Dark elevator | Stopped. The player’s maintenance flag is a separate switch. |
| Low pressure | Upper hotels and upper condos still fill, more slowly, and occupants take daily stress. |
| Upgrades | A clock on that room. No staff. Each level takes longer and costs a fee paid up front. |
| Broken plant or pump | Adds 0. The clock pauses and resumes on repair. Finished levels stay. |
| Art | Placeholder colors and square menu icons. Dollhouse plates are a follow-up. |

## 3. Power

### 3.1 Supply

```
capacity = 20 + sum of output from plants that are not broken
```

A plant’s output by level:

| Level | Stored level | Output | Time | Fee to start |
|------:|-------------:|-------:|-----:|-------------:|
| Built | 0 | 50 | — | — |
| I | 1 | 80 | 1 day (1440 min) | 40000 |
| II | 2 | 120 | 3 days (4320) | 90000 |
| III | 3 | 170 | 7 days (10080) | 180000 |
| IV | 4 | 230 | 13 days (18720) | 310000 |
| V | 5 | 300 | 21 days (30240) | 480000 |
| VI | 6 | 380 | 31 days (44640) | 690000 |

One game day is 1440 game minutes. The fee is spent when the upgrade starts. If the wallet cannot pay, the clock does not start and the level does not change. Demolishing the room drops the progress. The fee stays spent. Two plants can run two clocks at once. Level VI has no further upgrade.

### 3.2 Cost

| Room | Cost |
|------|-----:|
| Lobby (`isLobby`), sky lobby (`isSkyLobby`), stairs (`isStairs`) | 0 |
| Power plant (`service_power`) | 0 |
| Any elevator shaft | 2 |
| Every other room | `size.y` |

A 2-floor room such as the event hall, department store, pool, theater, or cathedral costs 2. The atrium and the metro cost 3. Scaffolding, parking ramps, and pumps use this same table. Growing an elevator shaft does not change its cost.

Use, for the chip, is the sum of those costs over placed rooms. A broken room contributes 0. A dark room still contributes its cost, so use can be higher than capacity.

### 3.3 Who keeps power

Rooms with cost 0 are never on the list. Broken rooms draw 0 and are skipped. Repairing a room runs the list again.

Sort the rooms that draw power into four bands. Inside a band, the lower `instanceId` is older and gets power first.

| Band | Who | Gets power |
|-----:|-----|------------|
| 0 | Every elevator shaft | First |
| 1 | Occupied rooms that are not leisure and not elevators | Next |
| 2 | Every other drawing room that is not an empty hotel and not leisure | Next |
| 3 | Empty hotels, and every leisure room, including one with someone inside | Last |

Occupied at the moment the list runs:

- Hotel, office, or condo with a tenant. A sold condo counts as occupied.
- Shop with a shopper inside.
- Parking with a car inside.
- Service room with `StaffedWorkers > 0`, or with a person inside.

An empty hotel is a hotel with no guest. Leisure is `BuildFamily.Leisure`.

Walk that order. If the power still in the pool covers the room’s whole cost, it is powered and the cost is subtracted. If it does not, that room stays dark and the walk continues, so a later cheaper room can still receive power.

A room that changes from powered to dark vacates. A hotel guest checks out. An office lease ends. A condo becomes unsold and the resident leaves. Shoppers and cars leave. A room that was already dark does not vacate again.

A new elevator that does not receive power is stopped. When power returns, only the power cut clears. The player’s Enter / Exit Maintenance button writes a separate flag. The shaft is stopped when that flag is set or the shaft is dark. Exit Maintenance on a dark shaft leaves it stopped.

### 3.4 What dark means

A dark room is closed for as long as it stays dark.

- Hotel, office, and condo: no new tenant.
- Shop: not a visit target, and it earns nothing.
- Parking: no new car.
- Clinic aura, trash coverage, and restroom coverage: none.
- Maids and doctors do not work out of a dark housekeeping room or a dark clinic.
- Elevator: stopped, as in §3.3.

The lobby, sky lobby, stairs, and the power plant are never dark for lack of power.

## 4. Water pressure

### 4.1 Demand and supply

Height is the count of floors from the ground lobby through the highest room at or above the lobby. Basement floors do not add height. `height = max(1, highestFloorAtOrAboveLobby - lobbyFloor + 1)`. A tower whose lobby is floor 0 and whose highest room is floor 14 has height 15. A tower with only the lobby has height 1.

```
demand = height * 2 + upperHotels * 4 + upperCondos * 4
```

An upper hotel or upper condo has `LuxuryBand.Upper`. Base, mid, and offices add no demand. If `demand` would be 0, use 1.

```
supply = sum of supply from pumps that are not broken and are powered
pressurePercent = supply / demand * 100
```

There is no cap. Spare pumps read above 100%.

A 15-floor tower with 4 upper hotels and 2 upper condos has demand 54. One built pump supplies 80, so pressure is `80 / 54 * 100` (about 148%). A 40-floor tower with 10 upper rooms has demand 120. One built pump reads `80 / 120 * 100` (about 67%).

Pump supply by level:

| Level | Stored level | Supply | Time | Fee to start |
|------:|-------------:|-------:|-----:|-------------:|
| Built | 0 | 80 | — | — |
| I | 1 | 120 | 1 day (1440 min) | 30000 |
| II | 2 | 180 | 3 days (4320) | 70000 |
| III | 3 | 260 | 7 days (10080) | 140000 |

The fee, pause, demolish, and parallel-clock rules are the same as §3.1. Level III has no further upgrade. A broken or unpowered pump supplies 0.

### 4.2 Upper rooms

While `pressurePercent` is under 100, upper hotel check-in and upper condo sales use:

```
fill *= 0.25 + 0.75 * (pressurePercent / 100)
```

At 0% the multiplier is 0.25. At 50% it is 0.625. At 100% and above it is 1. This multiplies with the existing luxury, class-clash, and health fill multipliers. Base and mid rooms, and offices, do not use this multiplier.

An occupant of an upper hotel or upper condo takes stress once per day, on a day-index gate of its own:

```
gain = pressurePercent >= 100 ? 0 : round(8 * (1 - pressurePercent / 100))
```

At 0% the gain is 8. At 50% it is 4. At 100% and above it is 0. Stress still caps at 100. Round with the same half-up rule as `Mathf.RoundToInt`.

## 5. Rooms

Both are `BuildFamily.Utility`, `RoomCategory.Service`, `IncomeModel.None`, `baseIncome` 0, `maxOccupants` 0, `requiresHousekeeping` 0. Basement only (`allowAboveGround` 0, `allowBasement` 1). No staff and no auto-hire. Placeholder color only. Square menu icons in the Utility strip. English tooltip is the display name.

| | Power plant | Pump |
|--|-------------|------|
| Id | `service_power` | `service_pump` |
| Display | Power Plant | Pump |
| Stars | 1 | 1 |
| Size | 8×1 | 6×1 |
| Build cost | 140000 | 75000 |
| Noise | 0.45 | 0.35 |
| Power draw | 0 | 1 |
| Color | 0.48, 0.42, 0.32 | 0.40, 0.45, 0.48 |

## 6. HUD

The left cluster of the top bar, directly after the Save chip, shows two chips from the first day, including before income unlocks.

- `Power {use}/{capacity}` with both numbers as integers. Example: `Power 48/70`. Use may be larger than capacity: `Power 80/70`.
- `Water {percent}%` with `Mathf.RoundToInt`. Example: `Water 72%`. Above 100% stays visible: `Water 148%`.

A selected room that draws power shows `Power {cost}`, for example `Power 2`.

A selected plant shows `Power plant {level} · {output}`. A selected pump shows `Pump {level} · {supply}`. Built is the word `Built`. Levels I–VI use those Roman numerals. Examples: `Power plant II · 120`, `Pump Built · 80`.

The next upgrade is a button, `Upgrade to {level} · {days} days · ${fee}`. At the max level the button is absent. If the wallet cannot pay, the button does not start the clock.

## 7. Save and load

`SaveSchema.CurrentVersion` stays 1. Do not bump it.

On `RoomSnapshotV1`, add:

| Field | Type | Old save default | Meaning |
|-------|------|------------------|---------|
| `utilityLevel` | int | 0 | Plant or pump level. Other rooms ignore it. |
| `upgradeMinutesRemaining` | float | 0 | Minutes left on the clock. |
| `upgradeRunning` | bool | false | Clock is running. |
| `playerMaintenance` | bool | false | Elevator player flag. Other rooms ignore it. |

Who is powered is not saved. After rooms are restored, run the power list once, then read water from the result.

On that load pass, a sold condo is occupied. A service room with `StaffedWorkers > 0` is occupied. Hotel guests, office workers, shoppers, cars, and riders are not in the save, so those rooms count as empty. An old tower with no plant can load with rooms dark. A dark hotel stays empty until a later list gives it power.

## 8. Architecture

| Type | Responsibility |
|------|----------------|
| `PowerSystem` | Cost, capacity, band sort, greedy walk, upgrade fees and durations for the plant |
| `WaterSystem` | Height, demand, pump supply, pressure percent, fill multiplier, daily stress, pump fees and durations |
| `RoomInstance` | `utilityLevel`, upgrade clock, `playerMaintenance`, and whether it is powered |
| `TowerSimulation` | Tick clocks in game minutes. Run power, then water, on each trigger in §3.3. |
| `AgentSystem` | Refuse new tenants, shoppers, and cars in dark rooms. Vacate on the powered-to-dark edge. Apply the water fill multiplier and daily stress. |
| `ElevatorSystem` | Shaft is stopped when `playerMaintenance` is set or the shaft is dark. |
| `TowerHudController` | Chips and the selection lines in §6 |

`PowerSystem` does not read water. `WaterSystem` does not change who is powered.

## 9. Tests

- Stairs, lobby, and sky lobby cost 0. An elevator of any height costs 2. The atrium costs 3. A one-floor room costs 1. The plant costs 0. The pump costs 1.
- Plant outputs 50, 80, 120, 170, 230, 300, 380. Pump supplies 80, 120, 180, 260.
- A broken plant or pump adds 0. An unpowered pump adds 0.
- Free capacity is 20. One built plant makes capacity 70.
- Band 3 loses power before band 2, band 2 before band 1, and elevators last. Inside a band, the higher `instanceId` goes dark first.
- A room whose cost is larger than the power left stays dark, and a later room with a smaller cost can still be powered.
- Adding a shopper does not run the list. Placing a room does.
- A dark shop is not a visit target. A dark hotel takes no guest. A dark clinic has no aura.
- The powered-to-dark edge vacates a guest, a worker, and a sold condo. A room that stays dark does not vacate again.
- Restoring power clears the power cut and leaves `playerMaintenance` set.
- The 15-floor example is about 148%. The 40-floor example is about 67%.
- Upper fill is 0.25 at 0%, 0.625 at 50%, and 1 at 100% and at 140%. A base hotel does not use the multiplier.
- Upper stress is 8 at 0%, 4 at 50%, and 0 at 100%, once per day. An office occupant gains none.
- The clock pauses while broken, then finishes and raises the level.
- A short wallet does not start the clock.
- A snapshot without the new fields loads level 0, clock stopped, player maintenance off, then runs one power pass.

## 10. Spec self-review

- No TBDs. Costs, times, fees, bands, and formulas have one value each.
- Power runs before water, so a dark pump cannot supply pressure.
- The load pass matches the fields that actually exist on a save: `condoSold` and `StaffedWorkers`. People who are not saved count as absent.
- Schema version stays 1, matching the health snapshot rule.
- Water cleanliness, dollhouse plates, and news lines stay out of this spec.
