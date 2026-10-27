---
name: bat-visual-verify
description: >-
  Fast in-editor visual iteration for Build-A-Tower dollhouse/art/gameplay:
  place adjusted rooms (forcing stars/funds), capture a Game view screenshot,
  analyze magenta/gaps, then visually confirm the PNG. Use when verifying room
  art, adjacency faces, chroma keying, or any “does it look right in game?” loop.
---

# Build-A-Tower visual verify loop

Use this instead of asking the user to manually place rooms and paste screenshots.

## Prerequisites

- Unity Editor open on `Build-A-Tower` with Pipeline server reachable (`unity pipeline list`).
- `unity` CLI on PATH (already used by `com.unity.pipeline`).

## One-shot orchestrator

From the Build-A-Tower project root:

```powershell
.\Tools\visual-verify\run.ps1 -Scenario micro-office-adjacency
```

Outputs under `Temp/visual-verify/<label>-<stamp>/` (copied from the pipeline capture):

| File | Purpose |
|------|---------|
| `game.png` | Game-view capture for `Read` tool |
| `game.analysis.json` | Magenta % + edge sky bleed metrics |
| `game.manifest.json` | Stars/funds/placed rooms |

Raw capture also lands at `Assets/Temp/visual-verify/latest/game.png` (authoring sandbox). Both `Temp/` and `Assets/Temp/` are gitignored.

Pass `-KeepPlaying` to leave Play Mode running for another iteration.

## Agent loop (do this every art/visual fix)

1. **Edit** code or regenerate art.
2. If C# under `Assets/` changed: `unity command recompile` then poll `recompile_status` until completed.
3. **Run** `.\Tools\visual-verify\run.ps1 -Scenario <name>` (or create a new scenario JSON).
4. **Read** `Temp/visual-verify/.../game.png` with the Read tool (vision).
5. **Check** `game.analysis.json` — `passed:false` on magenta is an automatic fail.
6. Compare against scenario `expect.notes`. If wrong → fix → repeat from step 1. Do not ask the user for another screenshot unless Unity Pipeline is down.

## Scenario JSON

Put files in `Tools/visual-verify/scenarios/<name>.json`:

```json
{
  "label": "my-case",
  "stars": 2,
  "funds": 5000000,
  "lobbyMin": 0,
  "lobbyMax": 20,
  "placements": [
    { "type": "office_micro", "x": 0, "y": 1 },
    { "type": "office_micro", "x": 3, "y": 1 }
  ],
  "expect": {
    "maxMagentaPct": 0.15,
    "notes": [
      "Shared wall between micros has no window.",
      "Walls are cool grey, not hollow sky."
    ]
  }
}
```

- `stars` → `StarSystem.ForceStars` (unlock rooms without grinding).
- `funds` → wallet restore (afford placement).
- `type` must match `RoomTypeSO.id` (e.g. `office_micro`, `hotel_base`).

## Manual Unity commands (when debugging the harness)

```bash
unity command set_autotick --enable true
unity command open_scene --path Assets/Scenes/TowerSandbox.unity
unity command editor_play
# wait ~3s
# Copy scenario to the drop file (avoids CLI quoting issues with spaces in paths):
#   copy Tools/visual-verify/scenarios/<name>.json Temp/visual-verify/current-scenario.json
unity command run_script --file AgentScripts/VisualVerify.cs --entry VisualVerify.PlaceScenario
unity command capture_game_view --width 1600 --height 900 --source camera --save_path Assets/Temp/visual-verify/latest/game.png
unity command editor_stop
```

Drop-file is preferred. Pipe encoding (advanced): `stars|funds|lobbyMin,lobbyMax|type:x,y;type:x,y|label`

## Analysis rules of thumb

| Symptom in PNG | Likely cause |
|----------------|--------------|
| Hollow room / sky through walls | Over-keyed chroma on face plate (`_ext`) |
| Hot pink/magenta patches | Leftover key fringe or wrong face leaf |
| Window on shared party wall | Face suffix / `SealInteriorSides` not applied |
| Tan vs cool-grey swap when neighbors appear | Different face variants (`_int` vs `_ext`) |

## Do not

- Do not invent Playwright/browser stacks for this Unity Game view.
- Do not grind star goals manually — always `ForceStars` via the scenario.
- Do not treat analysis JSON alone as visual sign-off — always `Read` the PNG.
