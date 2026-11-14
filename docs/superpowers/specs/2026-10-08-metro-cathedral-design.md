# Build-A-Tower — Metro Station & Cathedral

**Date:** 2026-10-08  
**Status:** Approved (brainstorming)  
**Depends on:** Transit/Leisure catalogs; atrium-style multi-floor placement; street traffic multipliers; leisure visit rules; basement placement; land/width expansion  
**Engine target:** Unity (2D), desktop/Editor-first  

**Backlog order (product):** Metro+Cathedral (this) → above-ground parking → Leisure four (Arcade / Rec Room / Observation Deck / Sky Lounge) → medical / cleanliness (appendix).

## 1. Goals

Ship two new placeables:

1. **Metro Station** — basement 3-floor transit hub with a world-wide subway tunnel and stacking street/travel benefits when multiple stations fit.
2. **Cathedral** — Leisure prestige wedding/ceremony venue (Chapel remains the small amenity).

### Success criteria

1. Player can place a basement Metro **8×3** stack; first metro creates a **full-world-width** tunnel at the bottom metro level with train motion.
2. Additional metros allowed only with **star cap** + **≥24 tile** horizontal spacing; each stacks street + mild travel relief (soft-capped).
3. Player can place **Cathedral 10×2** in Leisure with spacing; stronger leisure/wedding draw than Chapel; multi-faith inclusive art/copy.
4. Full dollhouse + menu art for station, tunnel, Cathedral.
5. EditMode tests cover metro caps/spacing/multipliers and cathedral catalog wiring.
6. Religious-leader VIP agents are **not** required this pass.

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Architecture | Approach 1 — `MetroSystem` + Cathedral as Leisure room |
| Metro footprint | 8×3 basement station |
| Metro tunnel | World-wide strip on bottom station level; one shared tunnel |
| Multi-metro | Stars gate max count + ≥24 tile left-edge spacing |
| Metro cost / stars | $520,000; unlock 3★; max 1/2/3 at 3★/4★/5★ |
| Metro effects | +8% street spawn per station (soft cap); ~5% travel/wait relief each (soft cap) |
| Cathedral family | Leisure |
| Cathedral footprint | 10×2; multiple with ≥16 tile spacing |
| Cathedral cost / stars | $420,000; 4★ |
| Cathedral role | Prestige wedding/ceremony venue; stronger leisure draw |
| VIP leaders | Deferred |
| Art | Full dollhouse + menu icons (station, tunnel, Cathedral) |
| Tone | Inclusive multi-faith; not Christianity-only |

## 3. Metro

### 3.1 Station

- Transit-family `RoomTypeSO` (e.g. id `metro_station`), `size (8,3)`, `allowBasement`, placed as one multi-floor build (atrium-like).
- Anchor floor chosen so all three floors stay in basement band; bottom floor is the **platform / tunnel interface** level.
- Menu icon under Transit family.

### 3.2 World tunnel

- Owned/visualized by `MetroSystem` + a thin presenter (parallax/world layer or dedicated renderer).
- Appears when `MetroCount >= 1`; spans **full world/grid width** at the bottom metro Y.
- Train animation along the tunnel (looping motion). Additional stations share the same tunnel (no parallel tunnels).

### 3.3 Multi-station rules

```text
MaxStations(stars) = stars >= 5 ? 3 : stars >= 4 ? 2 : stars >= 3 ? 1 : 0
MinLeftEdgeSpacingTiles = 24
StreetBonusPerStation = 0.08  (soft-cap total street metro bonus, e.g. at 0.20)
TravelReliefPerStation = 0.05 (soft-cap e.g. at 0.12)
```

- Reject placement if over cap or spacing violated; clear help text.
- Removing last metro hides tunnel and clears bonuses.

### 3.4 `MetroSystem` API (conceptual)

- `int StationCount`
- `bool HasTunnel => StationCount > 0`
- `int MaxStationsFor(int stars)`
- `bool CanPlace(TowerGrid grid, RectInt footprint, int stars, out string reason)`
- `float StreetTrafficMultiplier`
- `float TravelReliefMultiplier`
- Snapshot fields for save/load (station instance ids or footprints + tunnel flag)

Wire street spawn through `AgentSystem` (same pattern as weather/atrium multipliers). Travel relief: mild elevator wait / path comfort adjustment — keep formula localized and tested.

## 4. Cathedral

- Leisure `RoomTypeSO` (e.g. id `leisure_cathedral`), `size (10,2)`, cost $420k, 4★.
- Placement: standard leisure rules + **≥16 tiles** between cathedral left-edges (helper on leisure placement or small `CathedralPlacement` util).
- Gameplay: higher prestige/draw than Chapel; wedding/ceremony income via existing leisure visit / amenity patterns (no new VIP agent type).
- Art/copy: multi-faith landmark (avoid single-religion-only framing).
- Spec note for later: religious-leader VIP visitors (global faiths) when VIP framework exists.

## 5. Art

| Asset | Path pattern |
|-------|----------------|
| Metro dollhouse | `Assets/Resources/Art/Dollhouse/metro_station_8x3.*` |
| Tunnel strip | `Assets/Resources/Art/World/metro_tunnel.*` (or Parallax/World) |
| Metro menu | `Assets/Resources/Art/Menu/transit_metro.*` (bytes preferred) |
| Cathedral dollhouse | `Assets/Resources/Art/Dollhouse/cathedral_10x2.*` |
| Cathedral menu | `Assets/Resources/Art/Menu/leisure_cathedral.*` |

Warm industrial dollhouse aesthetic; magenta = key plate only.

## 6. Out of scope

- VIP religious-leader agents
- Medical center, cleanliness/disease metrics, pest control (see appendix)
- Above-ground parking (next backlog slice)
- Arcade / Rec Room / Observation Deck / Sky Lounge (after parking)
- Numeric rebalance of the whole economy

## 7. Verification

### Manual

1. Place first metro in basement → tunnel spans world; train moves.
2. Fail second metro until width/stars allow; succeed with spacing.
3. Street traffic / travel relief change with 1–3 stations; demolish last → tunnel gone.
4. Place Cathedral; spacing reject; leisure visits/income behave; Chapel still available.

### EditMode

- `MaxStationsFor` ladder; spacing checks; street/travel soft-cap math.
- Cathedral present in Leisure catalog; size/cost/stars match table.
- Snapshot round-trip for metro stations + tunnel flag.

## Appendix A — Parked: medical / cleanliness / disease (later)

Not this pass. Capture only:

- **Medical center** room: tower-wide health boost + stronger local bonus near the center.
- **Cleanliness / disease** metric: hotels high (transient guests), offices mid, condos lower; gathering / food-service areas high.
- **Housekeeping / maintenance** help cleanliness and disease control.
- **Pest control** especially near restaurants / concessions.
- Feeds future tower-health gameplay after Metro → parking → leisure backlog items.
