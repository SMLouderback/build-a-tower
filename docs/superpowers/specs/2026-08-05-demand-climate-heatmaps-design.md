# Build-A-Tower — Demand/Climate Graph & Tower Heatmaps

**Date:** 2026-08-05  
**Status:** Implemented  
**Depends on:** `TowerHudController` top-bar dropdowns; `TilemapTowerView` / grid cells; `MarketClimate`; `CrimeSystem` / criminal agents; `AgentSystem` movement & waits; elevator/stair capacity + research effects; `EconomySystem` room income/expense; room `noiseOutput` fields  
**Parent:** Roadmap item after numeric rebalance  
**Follow-ups:** Visual polish; future amenity noise (bars, clubs, gyms, theaters, etc.); above-ground parking (lower priority)

## 1. Goals

1. Let players open a **Maps** menu to inspect tower health spatially and over time.  
2. Ship a **climate/demand Graph** (time series).  
3. Ship **per-cell heatmaps**: Crime, Noise, Traffic, Economic (with sub-toggles).  
4. Make maps **reactive** to agents, transit capacity, security, events, research, and time of day.

## 2. Locked decisions

| Decision | Choice |
|----------|--------|
| Delivery | **Combined** — graph + all four heatmap families |
| Entry UX | Top-bar **Maps** dropdown; one mode at a time |
| Heatmap resolution | **Per cell** |
| Architecture | Sampling service + dedicated heatmap Tilemap overlay |
| Traffic window | **Today** / **30-day average** toggle |
| Economic views | **Profit** \| **Demand** \| **Blend** |
| Future amenities noise | Hooks only; not authored this slice |

## 3. Maps UX & overlay shell

### Dropdown entries

| Entry | Behavior |
|-------|----------|
| **Off** | Clear overlay; hide graph panel |
| **Graph** | No world tint; show climate/demand graph panel |
| **Crime** | Per-cell crime heatmap |
| **Noise** | Per-cell noise heatmap |
| **Traffic** | Per-cell traffic heatmap + **Today / 30-day avg** toggle |
| **Economic** | Per-cell economic heatmap + **Profit / Demand / Blend** toggle |

### Overlay

- Dedicated **Heatmap** Tilemap above rooms (sort: rooms below heatmap below ghost).  
- Active heatmap modes **grey-wash all tower room cells** (mid grey; hides room colors). Grey = zero / no data. Graph / Off: no wash.  
- **Risk scales** (Crime, Noise, Traffic, Economic Demand / Blend): **blue (good / near 0) → red (bad / near 100)**; score exactly 0 stays grey only (no blue tint).  
- **Economic Profit only:** **red (losses) → grey at zero → green (profit)**. Tower-normalized each rebuild: max observed profit room = +1, max observed |loss| = −1; only profits → no invented loss extreme; only losses → no invented profit extreme.  
- Empty sky/dirt / non-tower cells: unpainted or neutral.  
- Color-bar legend while a heatmap is active (title + meaning, swatches, 0…100 or −100 / 0 / +100 labels; Traffic window / Economic view in title).  
- Build tools remain usable; ghosts/selection draw above tint.  
- Returning to Main Menu clears Maps mode.

## 4. Per-cell scores (0–1)

Rebuild on a short interval and on day roll. Occupied / support / transit / scaffold / lobby cells participate where relevant.

### Traffic

- Inputs: agent path cells; wait/queue cells (elevator lines, stair waits).  
- **Today:** traversals + wait intensity this day, normalized.  
- **30-day avg:** rolling mean of daily cell samples.  
- **Capacity stress:** elevators/stairs near or over capacity boost cells on that shaft/flight.  
- More stairs/elevators or research capacity/efficiency **reduces** stress contribution.

### Crime

- Base from local **traffic** intensity.  
- Reduced by recent/active **security patrol** coverage.  
- Boosted near **active events** / busy event-conference activity.  
- Strong boost near **criminal agent** hangout / path cells.

### Noise

- Emit from room activity × type weights:
  - High: shops, restaurants, parking, event halls, conference when busy  
  - Medium: occupied offices; elevators / foot traffic  
  - Bursts: housekeeping carts; maintenance shop & repair rooms  
  - Security: louder when crime is active, otherwise quiet  
- Congestion (traffic score) adds ambient noise.  
- **Time-of-day sensitivity:** night hours increase “bother” on hotel/condo cells (sleep); day residential less sensitive.  
- Future bars/clubs/gyms/theaters/etc.: extension points only.

### Economic

- **Profit:** signed room net (income − expense), tower-normalized to −1..+1 (max profit / max |loss| extremes; see Overlay). Stored as signed; paint uses diverging red/grey/green.  
- **Demand:** fill/vacancy / overprice-skip stress (high = struggling or empty when it should fill), 0–1.  
- **Blend:** weighted average of **max(0, signed profit)** and demand (default **0.5 / 0.5**), clamped 0–1 risk scale.

## 5. Graph panel

- Window: **last 90 midnights** (or fewer if early game).  
- Series: **climate step** + **spend multiplier**; secondary **tower demand proxy** (rolling fill / wealth-acceptance or vacancy pressure).  
- Simple line chart + current climate name chip.  
- Readable market pulse — not a full analytics suite.

## 6. Non-goals

- Simultaneous multi-layer overlays  
- Final visual art pass  
- Changing core economy/star balance (maps consume data; don’t retune payouts)  
- Authoring new amenity room types for noise  
- Save/load of map UI preference (optional nice-to-have; not required)

## 7. Acceptance criteria

1. Maps switches Off / Graph / Crime / Noise / Traffic / Economic.  
2. Traffic supports Today vs 30-day; Economic supports Profit / Demand / Blend.  
3. Per-cell overlay paints tower cells; hotspots respond to agents, transit, crime, and night noise on residential.  
4. Adding transit or capacity research cools traffic stress on relieved corridors.  
5. Graph shows climate + spend mult history and a demand proxy.  
6. EditMode tests cover score helpers: traffic capacity stress, crime near criminal, noise night residential boost, economic blend.

## 8. Implementation sketch (non-binding)

| Piece | Role |
|-------|------|
| `TowerMapAnalytics` | Maintain daily samples + current 0–1 cell maps |
| `HeatmapOverlayView` / Tilemap | Paint scores |
| `TowerHudController` Maps UI | Dropdown, toggles, graph panel, legend |
| Agent/elevator/crime hooks | Feed traversals, waits, criminals, patrols |
| Noise weight table | Category/id-based emitters + TOD sensitivity |
| Tests | Pure score unit tests + light integration |

## 9. Roadmap reminder

After this: **visual polish**. Above-ground parking remains lower priority.
