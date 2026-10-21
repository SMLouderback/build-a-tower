# Build-A-Tower — Hotel Health

**Date:** 2026-10-21
**Status:** Approved (brainstorming, section by section)
**Depends on:** `CrimeSystem` as a shape reference only; `AgentSystem` maid/staff pattern; `TowerNews`; tower snapshot optional blocks (`WeatherSnapshotV1` style)
**Engine target:** Unity (2D Tilemap), desktop/Editor-first
**Parent roadmap:** Health this spec → tower services (power, water pressure, water cleanliness) next
**Out of scope:** Power plants, pumps, water towers, treatment, garbage trucks, a health map mode, maid wage changes, upper-office restroom need

## 1. Goals

Add a per-floor illness meter and four Utility rooms that move it: Trash Collection, Clinic, Public Restroom, and Office Restroom. Hotel guests on a sick floor take stress. Sick hotel, office, and condo rooms slow new tenancy and, at 25%, quarantine the tower. Maids have one tower-wide clean mode that trades speed for illness risk.

### Success criteria

In Play Mode a player can:

1. Place all four rooms from Build → Utility at their star gates.
2. Watch a hotel floor with guests and no trash coverage gain illness, and hotel guests there gain daily stress.
3. Hire clinic doctors who clear a sick room in 24–48 game hours and drop that floor’s illness.
4. Switch Housekeeping between Quick, Normal, and Deep and see clean time and illness risk change.
5. See new hotel, office, and condo fill slow at 10%, 15%, and 20% sick share, and see a quarantine at 25% stop new street traffic and new tenancy.
6. Read ticker warnings before each threshold and a major notice when quarantine starts or ends.

EditMode tests cover the meter, coverage, gates, clean mode, restroom effects, and old saves.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Shape | New `HealthSystem`. Crime is a reference only. Do not add fields to `CrimeSystem`. |
| Geography | Per floor, illness 0–100 |
| Who feels floor stress | Hotel guests on that floor only. Once per day. |
| Fill this slice | Yes. New tenancy only. Nobody is evicted. |
| Hotel illness source | Guests on a floor with no trash coverage, plus sick rooms on that floor |
| Public restroom | Does not raise hotel illness. Street-traffic bonus for nearby shops and restaurants. |
| Office restroom | Lower and mid offices only. Daily stress cut. Does not lower floor illness. |
| In-unit baths | Hotels and condos do not create restroom need |
| Clinic aura | Works with zero staff if the clinic is not broken. More clinics stack. |
| Doctors | 0–4 per clinic, auto-hire 1, $400/day. One sick room at a time. 24–48 game hours after arrival. |
| Clean mode | One tower-wide setting. Default Normal. Changed from any Housekeeping panel. |
| Quarantine | On at 25% sick share. Off at or below 22%. |
| Art | Utility placeholder colors and menu entries. Dollhouse plates are a follow-up. |

## 3. Floor illness

`HealthSystem` stores `float` illness per floor, clamped 0–100. Missing floors read as 0. Tower average divides the sum by the number of built floors, so empty floors count as zero. A tower with no built floors has average 0.

Tick once per simulation step with `deltaGameMinutes`, same cadence as `CrimeSystem.Tick`.

### 3.1 Raise

Per game minute, for each floor:

- If at least one hotel guest is on the floor and no working Trash Collection covers it: `+0.04` per guest.
- For each sick hotel, office, or condo on the floor: `+0.15`.

A floor with no guests and no sick rooms does not rise from these rules.

### 3.2 Decay

Per game minute, for each floor that has illness:

- Natural decay `0.05`. This alone must not hold a guest floor that lacks trash.
- Each working clinic: `0.04` on every floor.
- Each working clinic also adds `0.20 * (1 - distanceFloors / 8)` when `distanceFloors` is 0 through 8. Distance is the absolute difference in floor index. Past 8 floors the proximity term is 0. The tower-wide `0.04` still applies.

A broken clinic contributes nothing. Staff count does not change the aura.

### 3.3 Becoming sick

A floor that stays at illness `>= 40` for `360` consecutive game minutes marks one room sick, then the timer resets. The timer also resets if illness falls below 40.

The marked room is a hotel, office, or condo on that floor that is not sick and not broken. Prefer a hotel if any qualify. Otherwise the lowest `instanceId`. One room per trigger.

### 3.4 Doctor completion

Clearing a sick room subtracts `18` illness from that room’s floor, then clears the sick flag.

### 3.5 Guest stress

Once per day, each hotel guest whose current floor illness is `> 0` gains `12 * (illness / 100)` stress, capped at 100. Use a day-index gate so the same guest is not charged twice in one day. Office workers, condo residents, and shoppers do not take this stress.

## 4. Sick share, fill, quarantine

Sick share is `sickTenantRooms / tenantRooms`, where tenant rooms are placed hotels, offices, and condos. Other categories are not in the numerator or the denominator. Zero tenant rooms means sick share `0`.

New-tenancy multiplier for a category is `1` below its threshold. At the threshold it is `0.85`, then it falls linearly to `0` at `0.25`:

```
span = 0.25 - threshold
t = clamp((sickShare - threshold) / span, 0, 1)
fill = 0.85 * (1 - t)    when sickShare >= threshold
fill = 1                 when sickShare < threshold
```

At exactly `0.25`, quarantine is also on, so new tenancy is blocked even if a multiplier would still be computed.

| Sick share | Effect |
|------------|--------|
| `>= 0.10` | New hotel stays use the hotel fill multiplier |
| `>= 0.15` | New office leases use the office fill multiplier |
| `>= 0.20` | New condo sales and move-ins use the condo fill multiplier |
| `>= 0.25` | Quarantine |

Quarantine turns on when sick share crosses `0.25` upward. It turns off only when sick share is `<= 0.22`. While on:

- No new street-origin agents enter.
- Shops and restaurants receive no street-origin customers. `WeatherSystem.StreetTrafficMultiplier` is treated as 0 for spawning.
- No new hotel check-ins, office leases, or condo sales.
- People already inside stay. Fill multipliers are not evictions.

## 5. Rooms

All four are `BuildFamily.Utility`, `RoomCategory.Service`, `IncomeModel.None`, `baseIncome` 0. Broken rooms do not provide coverage, aura, doctors, or bonuses.

| Id | Display | Stars | Size | Build $ | Noise | Zone | Housekeeping |
|----|---------|------:|------|--------:|------:|------|----------------|
| `service_public_restroom` | Public Restroom | 0 | 3×1 | 18000 | 0.10 | Above ground | Yes |
| `service_office_restroom` | Office Restroom | 0 | 3×1 | 22000 | 0.10 | Above ground | Yes |
| `service_trash` | Trash Collection | 1 | 6×1 | 48000 | 0.40 | Above or basement | No |
| `service_clinic` | Clinic | 2 | 6×1 | 85000 | 0.15 | Above or basement | Yes |

Clinic staff: 0–4, auto-hire 1 on place, wage `$400` per doctor per day. Same wage path as maids (`EconomySystem`).

### 5.1 Trash Collection

Covers a floor when a working trash room is within `20` cells horizontally (origin to origin) and `3` floors vertically. No vehicle-access rule. No staff. No garbage-truck agents.

### 5.2 Public Restroom

Need rooms: lobby, shops, restaurants, and leisure amenities. Hotels and condos are not need.

Range: `12` cells, `2` floors. Each clean, working restroom covers up to `3` need rooms. Assignment is nearest restroom with remaining capacity. Ties go to the lower `instanceId`. A need room with no assignment gets nothing.

A shop or restaurant with an assignment gets `×1.05` street-visit weight. The bonus does not stack. A dirty or broken restroom covers nothing.

### 5.3 Office Restroom

Need rooms: offices whose `luxuryBand` is `Base` or `Mid`. `Upper` offices are neither need nor beneficiaries.

Range: `16` cells, `1` floor. Each clean, working restroom covers up to `4` need offices, nearest first, then lower `instanceId`.

A covered office worker gets `-2` stress once per day, floored at 0. This does not change floor illness.

### 5.4 Clinic

Aura rules are in §3.2. Doctors are in §6. A clinic with zero staff still has an aura if it is not broken. A broken clinic has neither aura nor doctors.

## 6. Doctors

`AgentRole.Doctor`. Spawn and despawn from clinic `StaffedWorkers` the way maids spawn from Housekeeping.

A free doctor claims the sick hotel, office, or condo with the lowest `instanceId` that has no doctor already on it. They walk there. On arrival, roll a treatment length uniformly from `24 * 60` to `48 * 60` game minutes. The timer runs only while they are on that job. Demolishing the room cancels the job. When the timer hits zero, apply §3.4.

Agents are not in the tower snapshot. On load, sick flags and floor illness remain. Doctors are hired again from clinic staffing and start new visits. In-progress treatment does not resume.

## 7. Clean mode

`CleanMode`: `Quick`, `Normal`, `Deep`. Default `Normal`. One value for the tower, edited from any Housekeeping selection panel.

| Mode | Time multiplier | Illness chance |
|------|----------------:|---------------:|
| Quick | 0.6 | 0.25 |
| Normal | 1.0 | 0.08 |
| Deep | 1.6 | 0.02 |

Maid work minutes are `cleanProgress * ResearchEffects.CleanMinutesMultiplier * modeMultiplier`.

Roll when the maid finishes:

- Hotel, office, or condo: on failure set the sick flag. The clean still completes. The room stays sick until a doctor finishes.
- Public restroom, office restroom, conference room, event hall: on failure the room is dirty again and needs another clean. Do not set the sick flag.

Public and office restrooms are maid targets. Priority after hotels and before the conference room and event hall. Broken rooms are still skipped.

## 8. News

Push through `TowerNews`. Fire each line once per upward crossing. Do not repeat while the tower stays past that line.

| When | Category | Text |
|------|----------|------|
| Sick share crosses `0.08` | OpsSerious | "Illness is nearing 10%. New hotel stays will slow." |
| Crosses `0.10` | OpsSerious | "10% of tenant rooms are sick. New hotel stays are slowing." |
| Crosses `0.13` | OpsSerious | "Illness is nearing 15%. Office leasing will slow." |
| Crosses `0.15` | OpsSerious | "15% of tenant rooms are sick. Office leasing is slowing." |
| Crosses `0.18` | OpsSerious | "Illness is nearing 20%. Condo sales will slow." |
| Crosses `0.20` | OpsSerious | "20% of tenant rooms are sick. Condo sales are slowing." |
| Crosses `0.23` | OpsSerious | "Illness is nearing 25%. The city can quarantine the tower." |
| Quarantine turns on | MajorEvent | "City quarantine. No one enters or leaves. New stays, leases, and sales are stopped." |
| Quarantine turns off | MajorEvent | "Quarantine lifted. The tower is open to new tenants and street traffic." |

Recovery under `0.10`, `0.15`, or `0.20` does not need its own line. Quarantine off is the only downward major notice.

## 9. Save and HUD

Optional `HealthSnapshotV1` on `TowerSnapshotV1`. Null or absent means illness all zero, `CleanMode.Normal`, quarantine off, and the “already warned” crossing flags clear. `RoomSnapshotV1` gains `bool sick`, default false. Do not bump `SaveSchema.CurrentVersion`.

Top bar shows average illness (0–100) and sick share as a percent. While quarantined, the bar says so. Clinic selection shows `Doctors (n/4)`. A sick room’s selection says it is waiting on a doctor. Housekeeping selection shows the Quick / Normal / Deep control.

No new map mode.

## 10. Architecture

| Unit | Responsibility |
|------|----------------|
| `HealthSystem` | Floor illness, sick-share math, quarantine latch, clean-mode enum, tick |
| `HealthCoverage` | Trash, public-restroom, and office-restroom range tests and assignment |
| `HealthNews` | Threshold crossing lines |
| `RoomInstance.Sick` | Sick flag |
| `AgentSystem` | Guest stress, doctor spawn and treatment, block new street entry and new tenancy while quarantined |
| `TowerSimulation` | Tick health, push news |
| `EconomySystem` | Clinic doctor wages |
| `TowerHudController` | Bar, selection, clean-mode control |
| Snapshot types | Optional health block and `sick` |

`HealthSystem` does not reference `CrimeSystem`. Fill hooks read sick share and the quarantine latch. They do not read crime.

## 11. Tests

EditMode:

- Guest floor with no trash rises; the same floor with trash does not.
- Clinic aura is stronger on the clinic floor than 8 floors away, and a second clinic adds more.
- A broken clinic adds nothing.
- Illness held at `>= 40` for `360` minutes marks one hotel before an office.
- Doctor completion clears sick and subtracts `18`, floored at 0.
- Sick share ignores shops and restrooms.
- Fill multipliers at `0.09`, `0.10`, `0.15`, `0.20`, and `0.25`.
- Quarantine turns on at `0.25` and stays on until `<= 0.22`.
- Clean-mode multipliers and illness chances.
- Public restroom assigns at most 3 need rooms and applies `×1.05` once.
- Office restroom ignores `Upper` offices and applies `-2` stress once per day.
- A snapshot with no health block restores Normal, healthy, not quarantined.

## 12. Spec self-review

- Meter, rooms, clean mode, quarantine, and save each have numbers. No TBDs.
- Public restrooms do not raise hotel illness. Trash and sick rooms do.
- Office restrooms cut office stress only.
- Sick share counts hotels, offices, and condos only.
- Quarantine uses a 22% release so it does not flicker.
- Power and water are explicitly the next spec.
