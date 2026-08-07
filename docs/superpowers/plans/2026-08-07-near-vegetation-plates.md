# Near Vegetation Plates + Far Skyline Peek — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Checkbox steps for tracking. User said “go for it” — implement inline after saving this plan.

**Goal:** Hand-painted `near_grass` + transparent `near_trees` parallax strips; raise far city so it peeks above mid roofs.

**Architecture:** Split `ParallaxBackdrop` veg into two `SpawnStrip` layers with separate load modes (opaque grass / alpha trees). Raise `farMaxHeight` and bias far scale toward height. Generate AI plates into `Resources/Art/Parallax/`.

**Tech Stack:** Unity Built-in RP, Resources `.bytes` + `LoadImage`, SpriteRenderer strips.

**Spec:** `docs/superpowers/specs/2026-08-07-near-vegetation-plates-design.md`

## Global Constraints

- Grass: opaque, tileable; trees: transparent sky/gaps.
- Resources: `Art/Parallax/near_grass`, `Art/Parallax/near_trees`.
- Lags: grass ~0.97, trees ~0.985; sorting −145 / −140.
- Far city must read above mid roofs; keep existing `far_city` art.
- Procedural fallback if plates missing.

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Rendering/ParallaxBackdrop.cs` | Split veg; load modes; far height |
| `Assets/Resources/Art/Parallax/near_grass.*` | Painted grass |
| `Assets/Resources/Art/Parallax/near_trees.*` | Painted trees (alpha) |
| Spec status → Implemented | Docs |

---

### Task 1: Far city peek + split veg wiring in `ParallaxBackdrop`

**Files:** Modify `Assets/Scripts/Rendering/ParallaxBackdrop.cs`

- [ ] Raise `farMaxHeight` to `8.0f`; far Y to `groundY + 0.55f`.
- [ ] In `SpawnStrip`, add `bool preferHeight = false`; when true, `scale = maxH / bh` (ignore width crush) or `scale = Mathf.Max(maxH/bh * 0.92f, Mathf.Min(maxH/bh, targetW/bw))` — use **preferHeight for far only**: `scale = maxH / bh` capped so tile isn’t absurd: `scale = Mathf.Min(maxH / bh, (targetW * 1.35f) / bw)`.
- [ ] Replace `_veg*` with `_grass*` / `_trees*`: lags, heights (`grassMaxHeight=1.2`, `treeMaxHeight=2.8`), offsets, tint both.
- [ ] Spawn grass (−145, `near_grass`, grass fallback) then trees (−140, `near_trees`, tree fallback).

### Task 2: Load modes — city / grass / trees

- [ ] Enum or flags on load: `City`, `GrassOpaque`, `TreesAlpha`.
- [ ] City: keep flood sky/ground, blobs, fade, force opaque.
- [ ] Grass: skip sky/ground flood; edge fade; force opaque.
- [ ] Trees: key white/black/light-sky from borders; preserve alpha; edge fade; **no** force-opaque on remaining pixels.
- [ ] Split `BuildVegetationStrip` into `BuildGrassFallback` + `BuildTreesFallback` (reuse disk/tree helpers).

### Task 3: Generate and deploy plates

- [ ] Generate hand-painted `near_grass` (~1024×128 look) and `near_trees` (~1024×256, transparent bg).
- [ ] Normalize, key plates, save `.png` + `.bytes` + metas under `Assets/Resources/Art/Parallax/`.
- [ ] Play Mode: grass under trees; city through canopy; far tops above mid.

### Task 4: Mark spec Implemented

- [ ] Update spec status; brief README note if parallax section exists.
