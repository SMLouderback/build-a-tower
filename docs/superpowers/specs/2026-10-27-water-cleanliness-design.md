# Build-A-Tower — Water Cleanliness

**Date:** 2026-10-27
**Status:** Approved (brainstorming, section by section)
**Depends on:** `WaterSystem` pressure; `HealthSystem` per-floor illness; hotel, office, and condo fill; `BuildSubgroup.Food` street-visit weight; utility level and upgrade clock on `RoomSnapshotV1` (schema stays 1)
**Engine target:** Unity (2D Tilemap), desktop/Editor-first
**Parent roadmap:** Hotel health → power and water pressure → **this spec (water cleanliness)**
**Out of scope:** Dollhouse plates, news lines, retail shops that are not restaurants, staff or wages on the treatment room, a water use/capacity chip, water heatmaps, garbage trucks

## 1. Goals

Add a tower-wide water-cleanliness percent that drifts toward treatment supply versus sewage. Low cleanliness slows move-ins, with higher bands hit harder, cuts restaurant walk-ins, and adds illness on floors where someone lives or works. A basement treatment room, upgradeable on its own clock, is what cleans the supply.

### Success criteria

In Play Mode a player can:

1. Read `Clean 100%` on a new tower, and watch it fall over the next several game hours as occupied hotels, offices, and condos outrun treatment.
2. Place a 1★ basement Water Treatment room from Build → Utility and see the percent climb toward the new target over the following hours.
3. Upgrade that room on its own clock, and run a second treatment room’s clock at the same time.
4. See a Base room keep most of its fill, a Mid room lose more, and an Upper room lose the most, while the water is dirty. Offices use the same slopes.
5. See a restaurant draw fewer walk-ins while each visit still pays the same.
6. See illness rise on a floor that has an occupied hotel, office, or condo, and stay flat from the water on an empty or shop-only floor.

EditMode tests cover the target, hourly drift, fill slopes, illness, restaurant weight, a dark or broken treatment room, the upgrade table, and an old save opening at 100.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Shape | `CleanlinessSystem` formulas. `TowerSimulation` stores the percent and drifts it. |
| Geography | One percent for the whole tower |
| Motion | Every game hour, move 10% of the gap from the stored percent to that hour’s target |
| Target | `supply / sewage × 100` when sewage is above 0. Sewage 0 targets 100. The target and the stored percent may sit above 100. |
| Sewage | 1 for each hotel with a guest, office with a worker, or sold condo. Shops, staffed services, and empty rooms add 0. |
| Supply | Sum of treatment output for rooms that are powered and not broken. A dark or broken treatment room adds 0. |
| Effects | Read the stored percent. At or above 100, dirt is 0. |
| Move-ins | Hotels, offices, and condos. Base cut 0.15, Mid cut 0.35, Upper cut 0.65. A missing luxury band uses Base. |
| Illness | `0.08 × dirt` per game minute on a floor with an occupied hotel, office, or condo. Added to the existing floor meter. |
| Restaurants | `BuildSubgroup.Food` street-visit weight times `1 - 0.40 × dirt`. Pay per visit is unchanged. Retail is unchanged. |
| Treatment | Basement only. Unlocks at 1★. Draws 1 power. Output depends on its level. |
| More treatment rooms | Allowed. No cap. Each adds its own output and upgrades on its own clock. |
| Upgrades | Same clock rules as the pump. Fee up front. Short wallet does not start. Broken pauses. Demolish drops progress. Finished levels stay. Max level III. |
| Art | Placeholder color and the Utility square icon. No dollhouse plate. |
| Save | Schema stays 1. Absent cleanliness snapshot means 100. |

## 3. The meter

Dirt, for every effect in §4, is `0` when the stored percent is at or above 100. Otherwise it is `1 - stored / 100`.

Sewage is counted at the moment of each drift step. A hotel counts when it has a guest. An office counts when it has a worker. A condo counts when it is sold. Each such room adds 1, whatever its height.

Supply is the sum of `TreatmentSupply(level)` for treatment rooms that are powered and not broken.

```
target = sewage == 0 ? 100 : supply / sewage * 100
stored = stored + (target - stored) * 0.10
```

One step runs for each whole 60 game minutes accumulated since the previous step. A 150-minute tick runs two steps and keeps a 30-minute remainder. Each step recounts sewage and supply, then applies the formula once. A partial hour does not step.

A new tower starts at 100. Placing a treatment room does not snap the percent. The next hour steps toward the new target.

Examples, stored starting at 100 and target held at 0: after 1 hour, 90. After 3 hours, 72.9. After 8 hours, about 43. After 12 hours, about 28.

## 4. Effects

### 4.1 Move-ins

When a hotel, office, or condo fill multiplier is chosen, multiply by `1 - cut × dirt`.

| Band | Cut | At 0% | At 50% | At 100% and above |
|------|-----|-------|--------|-------------------|
| Base, or no band | 0.15 | 0.85 | 0.925 | 1 |
| Mid | 0.35 | 0.65 | 0.825 | 1 |
| Upper | 0.65 | 0.35 | 0.675 | 1 |

This multiplies with the fill factors already on that check, including the pressure multiplier on upper hotels and condos. Pressure still ignores offices and Base/Mid living.

### 4.2 Illness

Once a game minute, each floor that has at least one sewage room (§3) gains `0.08 × dirt` on the existing illness meter. An empty floor and a shop-only floor gain 0 from the water. Existing decay, sick-room, guest, clinic, and quarantine rules are unchanged.

At 0% cleanliness the add is 0.08 per minute, which climbs past the 0.05 decay. At 50% the add is 0.04, so decay still wins unless guests or a sick room are also pushing.

### 4.3 Restaurants

When a `BuildSubgroup.Food` room’s street-visit weight is used, multiply that weight by `1 - 0.40 × dirt`. At 0% the weight is 60% of the catalog weight. At 50% it is 80%. At 100% and above it is unchanged. The public-restroom `×1.05` still stacks. Income per visit is unchanged. Retail subgroups are unchanged.

## 5. Treatment room

| Field | Value |
|-------|--------|
| id | `service_treatment` |
| Display name | Water Treatment |
| Stars | 1 |
| Size | 6×1 |
| Cost | 90000 |
| Noise | 0.40 |
| Color | (0.32, 0.46, 0.40) |
| Placement | Basement only |
| Family / category | Utility / Service |
| Income | None |
| Housekeeping | No |
| Staff | None |
| Power draw | 1 (`size.y`) |

Output by stored level 0..3: 12, 20, 32, 48. That is the number of sewage rooms the level holds at a 100 target.

Upgrade fees for destination levels 1..3: 30000, 70000, 140000. Durations in game minutes: 1440, 4320, 10080. Level 3 has no further upgrade.

The clock reuses `utilityLevel`, `upgradeMinutesRemaining`, and `upgradeRunning`. `TryBeginUpgrade` spends the fee when the clock starts. A broken room pauses the clock. Repair resumes it. Demolish drops the progress and keeps the fee spent.

## 6. HUD

The left cluster, directly after the water chip, shows `Clean {RoundToInt(stored)}%` from the first day, in both the economy-unlocked and the pre-economy bar. Examples: `Clean 72%`, `Clean 140%`.

A selected treatment room shows `Treatment {level} · {output}`. Level 0 reads `Built`. Levels 1..3 read `I`, `II`, `III`. Examples: `Treatment Built · 12`, `Treatment II · 32`.

The button reads `Upgrade to {level} · {days} days · ${fee}`. Example: `Upgrade to II · 3 days · $70,000`. It hides at level III. A short wallet does not start the clock.

## 7. Save and load

Schema stays 1. Do not add a bare float on `TowerSnapshotV1`. JsonUtility would read a missing float as 0 and open every old tower filthy.

`CleanlinessSnapshotV1` is optional on `TowerSnapshotV1`. It holds `float percent`. Absent or null means 100. Present uses the saved value, including 0.

Treatment level and its clock use the utility fields already on `RoomSnapshotV1`. They default to level 0, clock stopped. `Powered` is still not saved. After load, the existing power pass runs before the next cleanliness step, so a treatment room that loads dark adds 0 supply.

## 8. Architecture

| Type | Responsibility |
|------|----------------|
| `CleanlinessSystem` | Target, one drift step, dirt, fill multiplier, illness per minute, restaurant weight, treatment output, fees, and durations |
| `TowerSimulation` | Store the percent and the minute remainder. Each whole hour, recount supply and sewage, then step. |
| `HealthSystem` | Add the water illness onto the existing per-floor meter |
| `AgentSystem` | Multiply hotel, office, and condo fill by the band slope |
| `ShopVisitRules` | Multiply Food street-visit weight by the restaurant factor |
| `TowerHudController` | The chip, the selection line, and the upgrade button |

`CleanlinessSystem` does not change who is powered and does not change pressure. Pressure does not read cleanliness.

## 9. Tests

- Sewage 0 targets 100, including when supply is 0.
- Supply 12 and sewage 12 target 100. Supply 12 and sewage 24 target 50. Supply 0 and sewage 8 target 0. Supply 48 and sewage 12 target 400.
- One step from 100 toward 0 is 90. Three steps are 90, then 81, then 72.9.
- A 150-minute advance from a fresh remainder takes two steps and leaves 30 minutes.
- Dirt is 1 at 0%, 0.5 at 50%, and 0 at 100% and at 140%.
- Fill is 0.85 / 0.65 / 0.35 at 0% for Base / Mid / Upper, 0.925 / 0.825 / 0.675 at 50%, and 1 at 100% and at 140%.
- Illness per minute is 0.08 at 0%, 0.04 at 50%, and 0 at 100%. A floor with no sewage room gains 0.
- Restaurant weight factor is 0.60 at 0%, 0.80 at 50%, and 1 at 100%.
- Treatment outputs are 12, 20, 32, 48. Fees are 30000, 70000, 140000. Minutes are 1440, 4320, 10080.
- A dark or broken treatment room adds 0. A powered built room adds 12.
- An absent cleanliness snapshot restores 100. A present 0 stays 0.
- The catalog row matches §5. The chip string is `Clean 72%` and `Clean 140%`. The button string is `Upgrade to II · 3 days · $70,000`.

## 10. Spec self-review

- No TBDs. Cuts, rates, outputs, fees, and strings have one value each.
- The stored percent is what effects read. The target is only the drift destination, so placing treatment does not clean the tower on that tick.
- A missing save field is an absent snapshot, not a 0 float, so old towers open clean.
- Power still runs before a cleanliness step, so an unpowered treatment room cannot supply.
- Retail, dollhouse plates, news, and treatment staff stay out of this spec.
