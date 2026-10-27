# Structure Cutaway Art — Implementation Plan

> **For agentic workers:** Implement task-by-task. Do not commit unless asked.

**Goal:** Lobby / elevator / stairs cutaway sprites + painter wiring.  
**Spec:** `docs/superpowers/specs/2026-08-07-structure-cutaway-art-design.md`

## Tasks

### Task 1: Generate art PNGs into `Assets/Resources/Art/Structure/` ✅
- Lobby mids (6); caps deferred
- Elevator top / mid / bottom
- Stairs 64×64

### Task 2: Runtime sprite tiles + StructureCutawayArt helper ✅
- `StructureCutawayArt` loads via `Resources.Load` and caches Tiles / stairs Sprite

### Task 3: Wire TilemapTowerView paint paths ✅
- Lobby cells → hashed mid variants
- Elevator cells → top/mid/bottom
- Stairs → 2×2 SpriteRenderer overlay

### Task 4: Metas, README note, mark spec Implemented ✅
