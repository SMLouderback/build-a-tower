# Visual verify

Fast Unity Game-view loop for Build-A-Tower art/gameplay checks.

## Quick start

1. Open the `Build-A-Tower` project in Unity (Pipeline server must be reachable).
2. From the project root:

```powershell
.\Tools\visual-verify\run.ps1 -Scenario micro-office-adjacency
```

3. Open `Temp/visual-verify/<label>-<stamp>/game.png` (or ask the agent to `Read` it).
4. Check `game.analysis.json` for the magenta gate.

## Layout

| Path | Role |
|------|------|
| `scenarios/*.json` | Stars, funds, lobby span, room placements, expect notes |
| `run.ps1` | Orchestrates open → play → place → capture → analyze |
| `analyze_screenshot.py` | Magenta / edge-sky metrics |
| `AgentScripts/VisualVerify.cs` | In-memory Play Mode placement + camera frame |
| `.cursor/skills/bat-visual-verify/` | Agent skill for the iteration loop |

## Adding a scenario

Copy `scenarios/micro-office-adjacency.json`, set `stars` high enough for the room unlock, list `placements` with `RoomTypeSO.id` + grid `x,y`, and write human `expect.notes` for visual QA.
