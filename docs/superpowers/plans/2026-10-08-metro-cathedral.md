# Metro Station & Cathedral Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship basement Metro stations with a world-wide subway tunnel and stacking traffic/travel benefits, plus a Leisure Cathedral wedding venue with full art.

**Architecture:** `MetroSystem` owned by `TowerSimulation` validates multi-station caps/spacing, exposes street/travel multipliers, and drives tunnel presence. Cathedral is a standard Leisure `RoomTypeSO` with spacing checks. Presenters handle tunnel strip + train motion and dollhouse plates.

**Tech Stack:** Unity 6 C#, EditMode NUnit, Resources room assets + dollhouse/menu art (`.bytes` / PNG), existing atrium multi-floor placement patterns.

**Spec:** `docs/superpowers/specs/2026-10-08-metro-cathedral-design.md`

## Global Constraints

- Metro: 8×3 basement; $520k; unlock 3★; max stations 1/2/3 at 3★/4★/5★; ≥24 tile left-edge spacing.
- Tunnel: one shared world-width strip at bottom metro level when StationCount ≥ 1; train motion.
- Street +8% per station (soft-cap 0.20); travel relief ~5% each (soft-cap 0.12).
- Cathedral: Leisure 10×2; $420k; 4★; ≥16 tile spacing; wedding/prestige leisure draw; no VIP agents.
- Full dollhouse + menu art; magenta key plate only; multi-faith inclusive copy.
- Out of scope: VIP leaders, medical/cleanliness, above-ground parking, leisure four backlog.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Simulation/MetroSystem.cs` | Caps, spacing, multipliers, station registry |
| `Assets/Scripts/Simulation/TowerSimulation.cs` | Own/tick Metro; expose for agents/HUD |
| `Assets/Scripts/Agents/AgentSystem.cs` | Apply street metro multiplier |
| `Assets/Scripts/Persistence/SaveContracts.cs` + mapper | Metro snapshot |
| `Assets/Resources/Rooms/MetroStation.asset` | Room SO |
| `Assets/Resources/Rooms/LeisureCathedral.asset` | Room SO |
| `Assets/Scripts/UI/TowerHudController.cs` | Catalog load + placement hooks |
| `Assets/Scripts/Rendering/MetroTunnelView.cs` | World tunnel + train |
| Dollhouse/Menu art paths per spec §5 |
| `Assets/Tests/EditMode/MetroSystemTests.cs` | Core rules |
| `Assets/Tests/EditMode/CathedralCatalogTests.cs` | Catalog + spacing |

Unity EditMode (editor on this machine often `6000.4.7f1`):

```text
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" -runTests -testPlatform EditMode -testFilter "MetroSystemTests|CathedralCatalogTests" -testResults "TestResults\metro-cathedral.xml" -logFile "unity-metro-cathedral.log" -nographics
```

---

### Task 1: MetroSystem core (TDD)

**Files:**
- Create: `Assets/Scripts/Simulation/MetroSystem.cs`
- Create: `Assets/Tests/EditMode/MetroSystemTests.cs` (+ `.meta` for both)

**Interfaces:**
```csharp
public sealed class MetroSystem
{
    public const int MinLeftEdgeSpacingTiles = 24;
    public const float StreetBonusPerStation = 0.08f;
    public const float StreetBonusSoftCap = 0.20f;
    public const float TravelReliefPerStation = 0.05f;
    public const float TravelReliefSoftCap = 0.12f;

    public int StationCount { get; }
    public bool HasTunnel => StationCount > 0;
    public static int MaxStationsFor(int stars);
    public float StreetTrafficMultiplier { get; } // 1 + min(cap, count * bonus)
    public float TravelReliefMultiplier { get; }  // 1 - min(cap, count * relief) OR 1+ relief as agreed in tests — use 1 + capped relief for “improvement”
    public bool CanPlace(IReadOnlyList<RectInt> existingLeftAnchors, RectInt candidate, int stars, out string reason);
    public void RegisterStation(RectInt footprint);
    public void UnregisterStation(RectInt footprint);
    public void Clear();
}
```

Use **left-edge X** of each station footprint for spacing. `MaxStationsFor`: `<3 → 0`, `3 → 1`, `4 → 2`, `≥5 → 3`.

- [ ] **Step 1: Write failing tests** for MaxStationsFor ladder; spacing reject/accept; street/travel soft caps; HasTunnel; register/unregister.
- [ ] **Step 2: Run EditMode `MetroSystemTests` — expect fail.**
- [ ] **Step 3: Implement `MetroSystem`.**
- [ ] **Step 4: Tests pass.**
- [ ] **Step 5: Commit** `feat: add MetroSystem caps spacing and multipliers`

---

### Task 2: MetroStation RoomTypeSO + catalog + basement multi-floor place

**Files:**
- Create: `Assets/Resources/Rooms/MetroStation.asset` (+ meta) — id `metro_station`, displayName Metro Station, size 8×3, buildCost 520000, requiredStars 3, allowBasement true, Transit family (match how other transit rooms set family flags / ResolvedBuildFamily).
- Modify: `TowerHudController.CollectMenuRoomButtons` — `TryAddRoomButton(..., Resources.Load<RoomTypeSO>("Rooms/MetroStation"))`.
- Modify: build/placement path used for atrium multi-floor so metro 8×3 places as one stack in basement (reuse atrium multi-floor if `size.y > 1`; add metro-specific validation calling `MetroSystem.CanPlace`).
- Wire `TowerSimulation` to construct `MetroSystem`, register/unregister on place/demolish.

- [ ] **Step 1: Failing catalog/placement test** if pure helper extractable; else EditMode test that Loads SO fields + CanPlace integration with fake anchors.
- [ ] **Step 2: Implement asset + catalog + register hooks.**
- [ ] **Step 3: Commit** `feat: add MetroStation room and placement wiring`

---

### Task 3: Street/travel multipliers + snapshot

**Files:**
- Modify: `AgentSystem` street spawn to multiply by `simulation.Metro.StreetTrafficMultiplier` (compose with weather/atrium).
- Apply `TravelReliefMultiplier` in the smallest existing elevator-wait / path-stress hook (document exact call site in commit).
- Modify: `SaveContracts` / `TowerSnapshotMapper` — `MetroSnapshotV1` with station footprints or instance ids + restore into `MetroSystem`.
- Create: `Assets/Tests/EditMode/MetroSnapshotTests.cs`

- [ ] **Step 1: Failing tests** for multiplier composition helper + snapshot round-trip.
- [ ] **Step 2: Implement.**
- [ ] **Step 3: Commit** `feat: apply metro multipliers and persist stations`

---

### Task 4: Metro tunnel view + art

**Files:**
- Create: `Assets/Scripts/Rendering/MetroTunnelView.cs` — if `Metro.HasTunnel`, draw/ensure world-width tunnel at bottom metro Y; animate train along X.
- Art: dollhouse `metro_station_8x3`, world `metro_tunnel`, menu `transit_metro` (`.bytes` + magenta key per project convention).
- Ensure view created with scene / TowerSimulation bootstrap (same pattern as WeatherFx).

- [ ] **Step 1: Implement view + art assets.**
- [ ] **Step 2: Manual smoke or art path tests if project has RoomDollhouseArtTests pattern — add Metro paths.**
- [ ] **Step 3: Commit** `art: metro station tunnel plates and MetroTunnelView`

---

### Task 5: Cathedral room + spacing + leisure wiring + art

**Files:**
- Create: `Assets/Resources/Rooms/LeisureCathedral.asset` — id `leisure_cathedral`, 10×2, $420k, 4★, Leisure.
- Create: `Assets/Scripts/Simulation/CathedralPlacement.cs` — `MinLeftEdgeSpacingTiles = 16`; `CanPlace(existing, candidate, out reason)`.
- Modify: `TowerHudController` load Cathedral; placement validates spacing.
- Hook leisure visit prestige/draw higher than Chapel (extend existing leisure tables / amenity weights — match patterns in `LeisureVisitRules` or equivalent).
- Art: `cathedral_10x2` dollhouse + `leisure_cathedral` menu icon.
- Create: `Assets/Tests/EditMode/CathedralCatalogTests.cs`

- [ ] **Step 1: Failing tests** for SO fields, spacing, catalog contains cathedral.
- [ ] **Step 2: Implement room, spacing, leisure weight, art.**
- [ ] **Step 3: Commit** `feat: add Leisure Cathedral wedding venue`

---

### Task 6: Play Mode verification checklist + docs touch

- [ ] Manual checklist from spec §7.
- [ ] Optional: one-line README deferred note that Metro/Cathedral shipped; medical appendix stays future.
- [ ] Commit if README touched: `docs: note Metro and Cathedral shipped`

## Spec coverage

| Spec item | Task |
|-----------|------|
| MetroSystem caps/spacing/multipliers | 1 |
| Station room + place | 2 |
| Street/travel + save | 3 |
| Tunnel + art | 4 |
| Cathedral + art | 5 |
| VIP / medical OOS | appendix only |

## Self-review notes

- No VIP implementation tasks.
- Soft-cap constants match spec §2–3.
- Tunnel is shared world strip, not per-station geometry.
