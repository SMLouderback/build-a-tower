# Tower Ambient Sound Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a profile-based tower ambient mixer, build/elevator SFX, and Esc Options volume controls so the game is no longer silent.

**Architecture:** Map rooms to `SoundProfile`, score on-camera activity into ≤4 looping ambience voices with selection boost, play build/elevator one-shots on an SFX bus, and persist master/SFX/Music/Ambience volumes from the Esc pause Options panel via `TowerAudio`.

**Tech Stack:** Unity 6 IMGUI HUD, `AudioSource`/`AudioClip`, `PlayerPrefs`, Resources audio under `Assets/Resources/Audio/`, EditMode NUnit tests.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-14-tower-ambient-sound-design.md`
- Royalty-free / CC0 (or clearly temporary placeholders listed in `docs/audio-attribution.md`)
- No English VO; Music bus wired but silent in v1
- Do not fix stairs agent-stuck bug in this plan (tracked follow-up)
- Leave unrelated dirty WIP unstaged when committing

---

### Task 1: SoundProfile mapping

**Files:**
- Create: `Assets/Scripts/Audio/SoundProfile.cs`
- Create: `Assets/Scripts/Audio/SoundProfileMap.cs`
- Test: `Assets/Tests/EditMode/SoundProfileMapTests.cs`

**Interfaces:**
- Produces: `enum SoundProfile { None, Office, Restaurant, Retail, Condo, Hotel, Conference, Event, Elevator, Parking, Utility, Lobby, Stairs, Build }`
- Produces: `static SoundProfile SoundProfileMap.ForRoom(RoomTypeSO type)`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SoundProfileMapTests
    {
        [Test]
        public void ForRoom_maps_families_and_specials()
        {
            Assert.AreEqual(SoundProfile.Office, SoundProfileMap.ForRoom(Room("office_base", RoomCategory.Office)));
            Assert.AreEqual(SoundProfile.Hotel, SoundProfileMap.ForRoom(Room("hotel_base", RoomCategory.Hotel)));
            Assert.AreEqual(SoundProfile.Condo, SoundProfileMap.ForRoom(Room("condo_base", RoomCategory.Condo)));
            Assert.AreEqual(SoundProfile.Restaurant, SoundProfileMap.ForRoom(Shop("shop_restaurant", BuildSubgroup.Food)));
            Assert.AreEqual(SoundProfile.Retail, SoundProfileMap.ForRoom(Shop("shop_retail", BuildSubgroup.Retail)));
            Assert.AreEqual(SoundProfile.Conference, SoundProfileMap.ForRoom(Id("conference")));
            Assert.AreEqual(SoundProfile.Event, SoundProfileMap.ForRoom(Id("event_hall")));
            Assert.AreEqual(SoundProfile.Parking, SoundProfileMap.ForRoom(Parking()));
            Assert.AreEqual(SoundProfile.Elevator, SoundProfileMap.ForRoom(Elevator()));
            Assert.AreEqual(SoundProfile.Stairs, SoundProfileMap.ForRoom(Stairs()));
            Assert.AreEqual(SoundProfile.Utility, SoundProfileMap.ForRoom(Room("security_post", RoomCategory.Service)));
            Assert.AreEqual(SoundProfile.Lobby, SoundProfileMap.ForRoom(Lobby()));
        }

        static RoomTypeSO Room(string id, RoomCategory cat)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = cat;
            return so;
        }

        static RoomTypeSO Shop(string id, BuildSubgroup sub)
        {
            var so = Room(id, RoomCategory.Commercial);
            so.buildFamily = BuildFamily.Shops;
            so.buildSubgroup = sub;
            return so;
        }

        static RoomTypeSO Id(string id)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            return so;
        }

        static RoomTypeSO Parking()
        {
            var so = Id(ParkingStalls.ParkingId);
            so.category = RoomCategory.Parking;
            return so;
        }

        static RoomTypeSO Elevator()
        {
            var so = Id("elevator_normal");
            so.isElevatorShaft = true;
            return so;
        }

        static RoomTypeSO Stairs()
        {
            var so = Id("stairs");
            so.isStairs = true;
            return so;
        }

        static RoomTypeSO Lobby()
        {
            var so = Id("lobby");
            so.isLobby = true;
            return so;
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (Unity EditMode; close Editor if batchmode blocked):
`Unity.exe -batchmode -nographics -projectPath <proj> -runTests -testPlatform EditMode -testFilter SoundProfileMapTests -testResults <xml>`

Expected: FAIL (type missing)

- [ ] **Step 3: Implement mapping**

```csharp
public enum SoundProfile
{
    None,
    Office,
    Restaurant,
    Retail,
    Condo,
    Hotel,
    Conference,
    Event,
    Elevator,
    Parking,
    Utility,
    Lobby,
    Stairs,
    Build
}

public static class SoundProfileMap
{
    public static SoundProfile ForRoom(RoomTypeSO type)
    {
        if (type == null) return SoundProfile.None;
        if (type.isLobby || type.isSkyLobby) return SoundProfile.Lobby;
        if (type.isStairs) return SoundProfile.Stairs;
        if (type.isElevatorShaft) return SoundProfile.Elevator;
        if (type.isParkingRamp ||
            type.id == ParkingStalls.ParkingId ||
            type.id == ParkingStalls.ValetId ||
            type.id == ParkingStalls.RampId ||
            type.category == RoomCategory.Parking)
            return SoundProfile.Parking;
        if (type.id == "conference") return SoundProfile.Conference;
        if (type.id == "event_hall") return SoundProfile.Event;
        return type.ResolvedBuildFamily() switch
        {
            BuildFamily.Office => SoundProfile.Office,
            BuildFamily.Hotel => SoundProfile.Hotel,
            BuildFamily.Condo => SoundProfile.Condo,
            BuildFamily.Shops => type.ResolvedBuildSubgroup() == BuildSubgroup.Food
                ? SoundProfile.Restaurant
                : SoundProfile.Retail,
            BuildFamily.Utility => SoundProfile.Utility,
            _ => SoundProfile.None
        };
    }
}
```

Adjust `conference` / `event_hall` ids to match shipped assets if different (`Resources.Load` check).

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Audio/SoundProfile.cs Assets/Scripts/Audio/SoundProfileMap.cs Assets/Tests/EditMode/SoundProfileMapTests.cs
git commit -m "feat: map room types to sound profiles"
```

---

### Task 2: Volume buses + PlayerPrefs

**Files:**
- Create: `Assets/Scripts/Audio/AudioBuses.cs`
- Test: `Assets/Tests/EditMode/AudioBusesTests.cs`

**Interfaces:**
- Produces: `AudioBuses` with `MasterMute`, `Master`, `Sfx`, `Music`, `Ambience` (0–1), `float Effective(AudioBus bus)`, `Load()` / `Save()`
- Produces: `enum AudioBus { Sfx, Music, Ambience }`

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Effective_mute_zeros_all_buses()
{
    var b = new AudioBuses { MasterMute = true, Master = 1f, Sfx = 1f, Ambience = 1f, Music = 1f };
    Assert.AreEqual(0f, b.Effective(AudioBus.Sfx));
    Assert.AreEqual(0f, b.Effective(AudioBus.Ambience));
}

[Test]
public void Effective_is_master_times_bus()
{
    var b = new AudioBuses { MasterMute = false, Master = 0.5f, Sfx = 0.4f };
    Assert.AreEqual(0.2f, b.Effective(AudioBus.Sfx), 0.0001f);
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement**

Defaults: mute false; master/sfx/ambience 0.70; music 0.50. Pref keys: `bat_audio_mute`, `bat_audio_master`, `bat_audio_sfx`, `bat_audio_music`, `bat_audio_ambience`. Clamp 0–1 on set. `Load`/`Save` via `PlayerPrefs`.

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: audio bus levels with mute and prefs"
```

---

### Task 3: Ambient weight mixer (pure logic)

**Files:**
- Create: `Assets/Scripts/Audio/AmbientMixer.cs`
- Test: `Assets/Tests/EditMode/AmbientMixerTests.cs`

**Interfaces:**
- Produces: `AmbientMixer.ProfileBias(SoundProfile)` 
- Produces: `AmbientMixer.ScoreRoom(visibility, activity, profile)`
- Produces: `List<(SoundProfile profile, float weight)> AmbientMixer.RankProfiles(IEnumerable<(SoundProfile, float)> roomWeights, SoundProfile? selectedBoost, int maxVoices = 4)`
- Selection boost multiplier **2.75f**; empty activity floor handled by caller (0.25)

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void RankProfiles_keeps_top_four_and_boosts_selection()
{
    var rooms = new[]
    {
        (SoundProfile.Office, 1f),
        (SoundProfile.Office, 1f),
        (SoundProfile.Restaurant, 0.8f),
        (SoundProfile.Parking, 0.7f),
        (SoundProfile.Condo, 0.6f),
        (SoundProfile.Retail, 0.5f),
    };
    var ranked = AmbientMixer.RankProfiles(rooms, SoundProfile.Condo, maxVoices: 4);
    Assert.AreEqual(4, ranked.Count);
    Assert.IsTrue(ranked.Exists(e => e.profile == SoundProfile.Condo));
    var condo = ranked.Find(e => e.profile == SoundProfile.Condo).weight;
    Assert.Greater(condo, 0.6f * 2f); // boosted
}

[Test]
public void ProfileBias_marks_loud_and_quiet_types()
{
    Assert.Greater(AmbientMixer.ProfileBias(SoundProfile.Parking), 1.1f);
    Assert.Less(AmbientMixer.ProfileBias(SoundProfile.Stairs), 0.8f);
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement** per spec §4 (aggregate by profile, boost selected profile sum, take top 4, optional normalize/cap)

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: ambient profile ranking mixer"
```

---

### Task 4: TowerAudio runtime + clip loading

**Files:**
- Create: `Assets/Scripts/Audio/TowerAudio.cs`
- Create: `Assets/Resources/Audio/Ambience/` + `Sfx/` with placeholder loops/one-shots (generate short WAV if CC0 not yet curated)
- Create: `docs/audio-attribution.md`
- Modify: scene bootstrap / `TowerSimulation` or HUD host to ensure `TowerAudio` exists
- Test: `Assets/Tests/EditMode/TowerAudioTests.cs` (load mapping path names; PlayBuildSfx cooldown logic if extracted static)

**Interfaces:**
- Produces: `TowerAudio.Instance` (or Find) with `AudioBuses Buses`, `void TickAmbient(...)`, `void PlayBuildSfx()`, `void PlayElevatorPing()`, `void SetSelectedRoom(RoomInstance room)`
- Resources paths: `Audio/Ambience/{profile}` lowercase enum name; `Audio/Sfx/build_place`, `Audio/Sfx/elevator_ping`, `Audio/Sfx/elevator_door`
- Missing clip → skip that voice (no throw); log once

- [ ] **Step 1: Add attribution stub + generate/import placeholder clips** (document as temporary if generated)

- [ ] **Step 2: Implement `TowerAudio`**
  - Pool of 4 looping `AudioSource`s (ambience) + 1–2 one-shot sources (SFX)
  - Apply `Buses.Effective` to volumes each tick / on prefs change
  - `PlayBuildSfx`: ignore if last play &lt; 0.12s ago
  - `TickAmbient`: accept pre-ranked profile weights OR compute from injected room samples

- [ ] **Step 3: EditMode test** for build SFX cooldown helper and/or Resources path constants

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: TowerAudio runtime with ambience pool and SFX"
```

---

### Task 5: Wire scoring + build place + selection

**Files:**
- Modify: `Assets/Scripts/Build/BuildController.cs` — after successful `TryPlaceSelected` (and lobby/sky/scaffold place if desired), call `TowerAudio.PlayBuildSfx()`
- Modify: `Assets/Scripts/Simulation/TowerSimulation.cs` or new `TowerAudioDriver.cs` — each Update/Fixed (~10 Hz): gather rooms from grid, camera visibility via `Camera.main.WorldToViewportPoint` on room bounds, activity via agents whose `Cell` is inside room footprint (or `HomeRoom` match), elevator moving cars boost Elevator activity, call mixer + `TowerAudio.TickAmbient`
- Modify: selection path in `BuildController` when `SelectedRoom` changes → `TowerAudio.SetSelectedRoom`

**Interfaces:**
- Consumes: `SoundProfileMap`, `AmbientMixer`, `TowerAudio`
- Visibility: clamp viewport 0–1, presence if any cell center in slightly expanded viewport
- Activity: 0.25 if no agents; else lerp toward 1.0 by count (cap ~1.0 at 3+ agents); Elevator: if any car `State == Moving` treat activity ≥ 0.8

- [ ] **Step 1: Hook build SFX on successful place**

- [ ] **Step 2: Hook ambient tick + selection boost**

- [ ] **Step 3: Play-mode smoke checklist (manual): place, pan, select

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: wire build SFX and ambient scoring to tower"
```

---

### Task 6: Elevator ping / door SFX

**Files:**
- Modify: `Assets/Scripts/Transit/ElevatorSystem.cs` and/or car transition where state enters `DoorsOpen` from `Moving`
- Modify: `TowerAudio.PlayElevatorPing()` / `PlayElevatorDoor()`

- [ ] **Step 1: Detect edge** Moving → DoorsOpen (or Idle arrival) once per stop

- [ ] **Step 2: Play ping (+ door) on SFX bus with light rate limit per shaft

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: elevator arrival ping and door SFX"
```

---

### Task 7: Esc Options volume UI

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs` — extend `PauseUiState` with `Options` (or nested panel); taller pause panel; Master Mute toggle + 4 sliders; Resume/Main Menu unchanged
- Apply bus changes immediately via `TowerAudio.Buses` + `Save()`

- [ ] **Step 1: Add Options button on Paused panel**

- [ ] **Step 2: Draw mute + sliders; write prefs on change**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: Esc Options mute and volume mixers"
```

---

### Task 8: Verify + mark spec Implemented

**Files:**
- Modify: `docs/superpowers/specs/2026-09-14-tower-ambient-sound-design.md` status → Implemented
- Ensure `docs/audio-attribution.md` lists every shipped clip

- [ ] **Step 1: Run EditMode filters** `SoundProfileMapTests|AudioBusesTests|AmbientMixerTests|TowerAudioTests`

- [ ] **Step 2: Play smoke** per spec §8

- [ ] **Step 3: Mark spec Implemented; commit**

```bash
git commit -m "docs: mark tower ambient sound spec implemented"
```

---

## Spec coverage check

| Spec section | Task |
|--------------|------|
| Profile mapping | 1 |
| Buses / prefs | 2, 7 |
| Mixer / selection boost | 3, 5 |
| Build SFX | 4, 5 |
| Elevator SFX | 6 |
| Options UI | 7 |
| Assets / attribution | 4, 8 |
| Tests / success criteria | 1–3, 8 |

## Follow-up (not in this plan)

Agents stuck on stairs (landing / floor transition) — debug after sound ships.
