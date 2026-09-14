# Tower Ambient Sound Design

**Status:** Approved  
**Date:** 2026-09-14

## 1. Goal

The tower is currently silent. Add a light audio layer so the building feels alive without requiring the player to keep sound loud:

1. Soft **ambient mix** from nearby / prominent rooms (camera × activity × type bias).
2. **Selection boost** so inspecting a room clarifies that room’s vibe without hard soloing.
3. Short **construction / boom** one-shot on successful room place.
4. **Options** volume controls: master mute + master volume, plus SFX / Music / Ambience sliders (Music bus wired but unused in v1).

Follow-up (out of this spec): agents getting stuck on stairs — track separately after sound ships.

## 2. Architecture

**Approach:** Profile mixer (not one unique bed per room asset).

- `TowerAudio` (MonoBehaviour or static façade + component) owns buses, PlayerPrefs volume, ambience voice pool, and one-shot playback.
- Every placeable / selectable room maps to a `SoundProfile` enum.
- ~10 Hz: score rooms, aggregate by profile, keep ≤4 looping ambience voices, fade others.
- Build / elevator arrival pings use the SFX bus.
- Clips: royalty-free / CC0 under `Assets/Resources/Audio/` with `docs/audio-attribution.md`.

**Buses (effective gain):**

`out = masterMute ? 0 : masterVolume × busVolume`

| Bus | Use |
|-----|-----|
| Master | Mute + master slider |
| SFX | Build thunk, elevator ping/doors, future UI clicks |
| Ambience | Profile beds + sparse accents |
| Music | Slider present; no tracks in v1 |

Defaults: Mute off; Master 0.70; SFX 0.70; Ambience 0.70; Music 0.50. Persist with `PlayerPrefs`.

## 3. Sound profiles & room mapping

| Profile | Bed character | Maps to |
|---------|---------------|---------|
| Office | Quiet chatter, keyboards, distant phone, soft printer | All office units |
| Restaurant | Orders, register, light kitchen | Food shops (restaurant, fast food, fine dining, taco, chicken, Mexican, etc.) |
| Retail | Soft shop murmur, bag rustle, occasional register | Non-food shops |
| Condo | Friendly talk, TV bed, light kids play | All condo units |
| Hotel | Soft TV, distant hallway, quiet room tone | All hotel rooms/suites |
| Conference | Meeting talk, occasional clap | Conference room |
| Event | Crowd murmur, sparse applause | Event hall |
| Elevator | Shaft whir while cars move; floor ping + door as SFX on arrival | Elevator shafts |
| Parking | Engines, tire roll, rare horn | Underground parking, valet, parking ramp |
| Utility | Soft HVAC / quiet tool clank | Housekeeping, maintenance, security, research lab |
| Lobby | Footsteps, soft lobby murmur | Lobby / sky lobby when selectable |
| Stairs | Soft footfalls only (very low) | Stairs |
| Build *(SFX only)* | Short construction thunk / boom | Successful place — not a looping bed |

**Mapping rules**

- Resolve via `RoomTypeSO` flags / `ResolvedBuildFamily()` / shop subgroup (food vs retail), with explicit overrides for Conference, Event, parking, elevators, stairs, utility support rooms.
- No English VO and no music stings inside ambience beds.

## 4. Mixer rules

**Per-room weight (~10 Hz):**

`weight = cameraVisibility × activity × profileBias`

- **cameraVisibility:** 0 off-screen; rises as room cells enter the view; slight bias toward view center.
- **activity:** empty / idle ≈ 0.25 whisper; agents present, open shop, or moving elevator car ≈ 0.6–1.0.
- **profileBias:** Parking / Restaurant / Elevator ≈ 1.2–1.4; Stairs / Utility ≈ 0.5–0.7; others ≈ 1.0.

**Blend**

- Sum weights by profile (many offices → one louder Office bed).
- At most **4** active ambience voices; fade others over ~0.4–0.6s.
- Cap total ambience energy so dense towers stay a soft bed.
- **Selection:** multiply the selected room’s profile contribution by ~2.5–3×. Clearing selection eases back over ~0.5s.

**SFX**

- Successful place → Build one-shot; short cooldown so drag-paint does not machine-gun.
- Elevator floor arrival → ping + short door; motion whir only while cars move (may share Elevator bed).
- Sparse accents (phone, horn, clap) only when that profile is already in the audible mix.

**Day/night (light)**

- Night: Hotel/Condo slightly more present; Office/Restaurant quieter when empty/closed. No full day/night remix beyond that in v1.

## 5. Options UI

Hook into the existing Esc pause overlay in `TowerHudController` (Paused state): add **Options** that expands or opens a sub-panel with:

- Master Mute (toggle)
- Master Volume
- SFX / Ambience / Music sliders

Sliders apply immediately. Music remains silent until tracks exist. Continue / Quit Main Menu behavior unchanged.

## 6. Assets

- Layout: `Assets/Resources/Audio/Ambience/{profile}.(ogg|wav)` and `Assets/Resources/Audio/Sfx/` for build + elevator one-shots.
- One primary loop per profile + 0–3 optional accents where useful.
- License notes in `docs/audio-attribution.md` (source, license, filenames).
- Prefer seamless loops; normalize import volumes so mixer gains stay meaningful.

## 7. Non-goals (v1)

- Music tracks / dynamic score
- Per-room unique beds
- Full 3D spatial / HRTF audio
- Voice acting / English dialogue
- Heatmap- or star-celebration-driven sound (hooks later OK)
- Fixing agents stuck on stairs (separate follow-up)

## 8. Testing

**EditMode**

- Profile mapping covers each shipped `RoomTypeSO` family / special flag.
- Bus math: mute zeros output; `master × bus` otherwise.
- Mixer selects top profiles from mocked weights and respects the 4-voice cap / selection boost.

**Play smoke**

- Place room → hear build SFX.
- Pan across occupied office / restaurant / parking → soft bed shifts.
- Select room → that profile rises in the mix.
- Esc → Options mute / sliders respond immediately.

## 9. Success criteria

1. Tower is no longer silent with ambience and SFX buses live.
2. Prominent on-camera + active rooms drive a readable low mix; selection clarifies without solo.
3. Build place plays a short construction one-shot with anti-spam cooldown.
4. Options exposes master mute/volume and SFX / Music / Ambience; prefs persist.
5. Focused EditMode tests for mapping, bus math, and mixer ranking pass.
