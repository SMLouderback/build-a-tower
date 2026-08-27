# Build-A-Tower — Agent People Visuals

**Date:** 2026-08-27  
**Status:** Approved — ready for implementation plan  
**Depends on:** `AgentView`, `Agent` / `AgentSystem`, `AgentWealth` (`WealthBand`), painterly room cutaway art (128 px/cell)  
**Parent:** Visual polish → make tower occupants read as people, not map dots

## 1. Goals

1. Replace role-colored circular dots with **painterly human figures** that match hotel/office cutaway art language.  
2. Scale agents to **~⅔–¾ cell height** (~85–96 px of 128 px/cell) so they read as in-room people, not UI pins.  
3. **Role-readable** silhouettes/outfits: office workers, hotel guests, condo residents, shoppers, event visitors, maid, handyman, security, criminal.  
4. **Gender** (male/female) and **dress tier** (Basic / Mid / Upper+) from wealth for economy roles; **uniform + gender** for staff.  
5. **Simple walk cycles** (2–4 frames); horizontal flip for facing — no separate left/right art.  
6. Keep sim/view separation: new render data only; no economy rule changes.

## 2. Locked decisions

| Topic | Choice |
|-------|--------|
| Approach | **Full painted sprite sheets** per variant (Approach 1) — not layered paper dolls, not zoom LOD dots |
| Style | Flat painterly cutaway language (match room interiors) |
| Height | ~⅔–¾ cell (~85–96 px) |
| Animation | 4 walk frames per variant; flip X for direction |
| Gender | New `AgentGender` (`Male` / `Female`); ~50/50 at spawn; stable for lifetime |
| Dress tiers | **3 render tiers** mapped from `WealthBand` (see §3) |
| Staff | Maid, Handyman, Security: uniform only, **M/F variants** |
| Criminal | M/F, **single outfit** (no wealth dress-up) |
| Dots | Removed when sprites ship (no dual-mode LOD in v1) |

## 3. Variant matrix

### Dress tier (render-only)

| WealthBand | DressTier |
|------------|-----------|
| Street, Basic | Basic |
| Mid | Mid |
| Upper, Premium | Upper |

Staff roles ignore dress tier. Criminal ignores dress tier.

### Economy roles (dress tier × gender)

Each uses **M/F × Basic/Mid/Upper** walk sheets:

| AgentRole | Notes |
|-----------|-------|
| OfficeWorker | Business casual → suit by tier |
| HotelGuest | Travel/casual → upscale guest by tier |
| CondoResident | Home casual → luxury resident by tier |
| StreetVisitor | Street/shopping clothes by tier |
| EventVisitor | Event-appropriate dress by tier |

### Staff (uniform × gender)

| AgentRole | Notes |
|-----------|-------|
| Maid | Housekeeping uniform, M/F |
| Handyman | Work coveralls/tool belt, M/F |
| Security | Security uniform, M/F |

### Criminal

| AgentRole | Notes |
|-----------|-------|
| Criminal | Single shady outfit, M/F |

**Sheet count (v1):** 5 economy roles × 2 × 3 = 30; 3 staff × 2 = 6; criminal × 2 = 2 → **~38 sheets** (4 frames each).

## 4. Art format

- Path: `Assets/Resources/Art/Agents/`  
- Naming: `{role}_{gender}_{tier}` — e.g. `office_worker_male_mid`, `maid_female_uniform`, `criminal_male`  
  - Economy: `{roleSlug}_{male|female}_{basic|mid|upper}`  
  - Staff: `{roleSlug}_{male|female}_uniform`  
  - Criminal: `criminal_{male|female}`  
- Sheet layout: **horizontal strip**, 4 frames × frame width; frame height = character height (~85–96 px)  
- PPU: **128** (1 world unit = 1 cell width; scale sprite height to target cell fraction)  
- Ship `.png` + identical `.bytes` (bytes-first load like hotels/offices)  
- Transparent background; feet on bottom row of frame (anchor bottom-center in view)

## 5. Code architecture

### Sim (`Agent`, `AgentSystem`)

- Add `AgentGender Gender { get; set; }` on `Agent`.  
- Assign gender in spawn paths (`rng.Next(2)`).  
- Existing `Wealth` + `Role` unchanged.

### Resolver (`AgentSpriteArt` — new static helper)

- `ResolveSheetKey(AgentRole role, AgentGender gender, WealthBand wealth) → string`  
- Maps wealth → dress tier; staff/criminal override per §3.  
- Loads/caches sprite strips from `Resources/Art/Agents/`.  
- `SliceWalkFrame(Sprite sheet, int frameIndex, int frameCount = 4)`.

### View (`AgentView`)

- Replace procedural dot with pooled `SpriteRenderer` using resolved sheet.  
- Walk frame from movement speed + time (same pool pattern as today).  
- `flipX` when velocity.x < 0.  
- Scale so rendered height ≈ **0.70 × cell** (tunable constant).  
- `sortingOrder` stays above room overlays (~20); below elevator cars (30).  
- Fallback: flat role-colored capsule/dot if sheet missing (dev only; tests assert mapped roles load).

### Tests (EditMode)

- Dress tier mapping table tests.  
- Sheet key resolver for every `AgentRole`.  
- Frame slice dimensions.  
- Optional: one loaded sheet per family smoke test.

## 6. Out of scope (v1)

- Zoom-level LOD (dots far / people near).  
- Layered outfit compositing.  
- Idle/wait/custom animations beyond walk strip.  
- Per-agent hair/skin randomization beyond M/F sheets.  
- Wealth affecting staff uniforms.

## 7. Success criteria

1. At default tower zoom, agents read as **people**, not circles.  
2. Role identifiable at a glance (outfit silhouette + optional legacy role color tint off or very subtle).  
3. Richer guests/workers look dressier than Basic tier in the same role.  
4. Walk animation plays while moving; sprite flips with direction.  
5. No regression to agent sim, pathing, or elevator/stairs visibility.
