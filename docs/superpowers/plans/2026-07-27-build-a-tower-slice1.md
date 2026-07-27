# Build-A-Tower Slice #1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship an Editor-playable cutaway sandbox where the player places a floor-1 lobby, builds placeholder rooms from ScriptableObject blueprints, demolishes non-lobby rooms, spends funds, and pans/zooms the camera.

**Architecture:** `TowerGrid` is the occupancy source of truth; `RoomTypeSO` assets define blueprints; `BuildController` validates placements and updates funds; layered Tilemaps paint colored placeholders; UIToolkit shows funds/tool/cell readout; orthographic `CutawayCamera` pans/zooms.

**Tech Stack:** Unity 6000.4.7f1 (2D + Tilemap), C#, NUnit Edit Mode tests, UIToolkit, Git.

## Global Constraints

- Project root: `c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower`
- Desktop/Editor-first; no mobile input
- Side cutaway orthographic view; placeholder colored cells + labels only
- Coordinates: `Vector2Int(x, floor)`; floor `>= 1` above, `<= -1` basement; floor `0` unused
- Starting funds: `$2,000,000`
- Lobby: floor 1 only, height 1, click-drag width; cannot demolish
- No agents, transit behavior, economy payouts, stars, or ECS
- Spec: `docs/superpowers/specs/2026-07-27-build-a-tower-slice1-design.md`
- Prefer small focused scripts; TDD for `TowerGrid`; commit after each task

## File Structure

```
Build-A-Tower/
  Assets/
    Scripts/
      BuildATower.Runtime.asmdef
      Core/
        RoomCategory.cs
        IncomeModel.cs
        BuildTool.cs
        RoomInstance.cs
        TowerGrid.cs
        FundsWallet.cs
      Data/
        RoomTypeSO.cs
      Build/
        BuildController.cs
        GhostPreview.cs
      Rendering/
        TilemapTowerView.cs
        PlaceholderTileFactory.cs   (Editor utility OR runtime color tiles)
      Camera/
        CutawayCamera.cs
      UI/
        TowerHudController.cs
        TowerHud.uxml
        TowerHud.uss
    ScriptableObjects/Rooms/
      Lobby.asset
      Office.asset
      Condo.asset
      HotelSingle.asset
      RetailFastFood.asset
    Scenes/
      TowerSandbox.unity
    Tiles/
      PlaceholderTile.asset         (optional shared tile)
    Tests/
      EditMode/
        BuildATower.Tests.asmdef
        TowerGridTests.cs
        FundsWalletTests.cs
  ProjectSettings/ ...              (Unity-generated)
  Packages/ ...                     (Unity-generated)
```

---

### Task 1: Bootstrap Unity 2D project in repo root

**Files:**
- Create: Unity project files under `Build-A-Tower/` (`Assets/`, `Packages/`, `ProjectSettings/`)
- Create: `Build-A-Tower/.gitignore` (Unity template)
- Keep: existing `docs/`, `README.md`, `.git/`

**Interfaces:**
- Consumes: none
- Produces: openable Unity project at repo root with 2D Tilemap package

- [ ] **Step 1: Add Unity `.gitignore`**

Create `Build-A-Tower/.gitignore` with standard Unity ignores (`[Ll]ibrary/`, `[Tt]emp/`, `[Oo]bj/`, `[Bb]uild/`, `[Ll]ogs/`, `.vs/`, `*.csproj`, `*.sln`, etc.). Keep `Assets/`, `Packages/`, `ProjectSettings/` tracked.

- [ ] **Step 2: Create the Unity project into the repo folder**

Preferred: Unity Hub → New Project → **2D (Built-in Render Pipeline)** or **2D URP**, version **6000.4.7f1**, location parent `Escape`, name `Build-A-Tower` — if Hub refuses non-empty folder, create in a temp folder and copy `Assets`, `Packages`, `ProjectSettings` into the existing repo (do not overwrite `docs/` or `.git/`).

CLI alternative (empty Assets only):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -batchmode -quit -createProject "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower-UnityTmp"
```

Then merge generated folders into `Build-A-Tower` and delete the temp project.

- [ ] **Step 3: Ensure 2D Tilemap packages**

In `Packages/manifest.json`, ensure at least:

```json
"com.unity.feature.2d": "2.0.1",
"com.unity.ugui": "2.0.0",
"com.unity.modules.uielements": "1.0.0"
```

(Exact versions may differ by Unity 6 template — use Hub template defaults if present. Must include Tilemap support.)

- [ ] **Step 4: Open project once and save empty scene path**

Open the project in Unity Editor, create folder `Assets/Scenes`, save scene as `Assets/Scenes/TowerSandbox.unity`, set it as the first enabled scene in Build Settings.

- [ ] **Step 5: Commit**

```powershell
cd "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower"
git add .gitignore Packages ProjectSettings Assets
git commit -m "chore: bootstrap Unity 2D project for Build-A-Tower"
```

---

### Task 2: Runtime asmdef + enums + `RoomTypeSO`

**Files:**
- Create: `Assets/Scripts/BuildATower.Runtime.asmdef`
- Create: `Assets/Scripts/Core/RoomCategory.cs`
- Create: `Assets/Scripts/Core/IncomeModel.cs`
- Create: `Assets/Scripts/Core/BuildTool.cs`
- Create: `Assets/Scripts/Data/RoomTypeSO.cs`

**Interfaces:**
- Consumes: none
- Produces:
  - `enum RoomCategory { Structure, Office, Condo, Hotel, Commercial, Transit, Parking, Service }`
  - `enum IncomeModel { None, QuarterlyRent, NightlyRate, UpfrontSale, TrafficVariable }`
  - `enum BuildTool { Select, Bulldoze, PlaceRoom }`
  - `class RoomTypeSO : ScriptableObject` with public fields listed below

- [ ] **Step 1: Create runtime asmdef**

`Assets/Scripts/BuildATower.Runtime.asmdef`:

```json
{
  "name": "BuildATower.Runtime",
  "rootNamespace": "BuildATower",
  "references": [
    "Unity.TextMeshPro",
    "UnityEngine.UIElementsModule"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

If TextMeshPro reference fails to resolve in this Unity version, remove that reference entry.

- [ ] **Step 2: Add enums**

`Assets/Scripts/Core/RoomCategory.cs`:

```csharp
namespace BuildATower
{
    public enum RoomCategory
    {
        Structure,
        Office,
        Condo,
        Hotel,
        Commercial,
        Transit,
        Parking,
        Service
    }
}
```

`Assets/Scripts/Core/IncomeModel.cs`:

```csharp
namespace BuildATower
{
    public enum IncomeModel
    {
        None,
        QuarterlyRent,
        NightlyRate,
        UpfrontSale,
        TrafficVariable
    }
}
```

`Assets/Scripts/Core/BuildTool.cs`:

```csharp
namespace BuildATower
{
    public enum BuildTool
    {
        Select,
        Bulldoze,
        PlaceRoom
    }
}
```

- [ ] **Step 3: Add `RoomTypeSO`**

`Assets/Scripts/Data/RoomTypeSO.cs`:

```csharp
using UnityEngine;

namespace BuildATower
{
    [CreateAssetMenu(menuName = "Build-A-Tower/Room Type", fileName = "RoomType")]
    public class RoomTypeSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public RoomCategory category;
        public Vector2Int size = Vector2Int.one;
        public int buildCost = 1000;
        public Color placeholderColor = Color.gray;
        public IncomeModel incomeModel = IncomeModel.None;
        public int baseIncome;
        [Range(0f, 1f)] public float noiseOutput;
        [Range(0f, 1f)] public float noiseSensitivity;
        public bool requiresHousekeeping;
        public bool hasActiveHours;
        public int activeHoursStart;
        public int activeHoursEnd;
        public bool allowAboveGround = true;
        public bool allowBasement;
        public bool isLobby;
    }
}
```

Note: Spec’s `int?` active hours map to `hasActiveHours` + start/end for Unity serialization.

- [ ] **Step 4: Verify compile in Editor**

Open Unity Console — zero errors for new scripts.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts
git commit -m "feat: add RoomTypeSO and core enums"
```

---

### Task 3: `RoomInstance`, `TowerGrid`, and Edit Mode tests (TDD)

**Files:**
- Create: `Assets/Scripts/Core/RoomInstance.cs`
- Create: `Assets/Scripts/Core/TowerGrid.cs`
- Create: `Assets/Tests/EditMode/BuildATower.Tests.asmdef`
- Create: `Assets/Tests/EditMode/TowerGridTests.cs`

**Interfaces:**
- Consumes: `RoomTypeSO`, `RoomCategory`
- Produces:
  - `class RoomInstance` — `int InstanceId`, `RoomTypeSO Type`, `Vector2Int Origin`, `int Evaluation`, `IEnumerable<Vector2Int> OccupiedCells()`
  - `class TowerGrid`:
    - `bool HasLobby { get; }`
    - `int MinX { get; }` / `int MaxX { get; }` (lobby bounds; undefined until lobby)
    - `bool TryGetRoomAt(Vector2Int cell, out RoomInstance room)`
    - `bool CanPlaceLobby(int minX, int maxX, int floor)`
    - `bool TryPlaceLobby(RoomTypeSO lobbyType, int minX, int maxX, int floor, out RoomInstance room)`
    - `bool CanPlace(RoomTypeSO type, Vector2Int origin)`
    - `bool TryPlace(RoomTypeSO type, Vector2Int origin, out RoomInstance room)`
    - `bool TryDemolishAt(Vector2Int cell, out RoomInstance removed)` — fails for lobby
    - `IReadOnlyList<RoomInstance> Rooms { get; }`

- [ ] **Step 1: Create test asmdef**

`Assets/Tests/EditMode/BuildATower.Tests.asmdef`:

```json
{
  "name": "BuildATower.Tests",
  "rootNamespace": "BuildATower.Tests",
  "references": [
    "BuildATower.Runtime",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: Write failing tests**

`Assets/Tests/EditMode/TowerGridTests.cs`:

```csharp
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TowerGridTests
    {
        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.displayName = "Lobby";
            so.category = RoomCategory.Structure;
            so.size = new Vector2Int(1, 1);
            so.buildCost = 1000;
            so.isLobby = true;
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.displayName = "Office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.buildCost = 40000;
            so.allowAboveGround = true;
            so.allowBasement = false;
            return so;
        }

        RoomTypeSO Retail()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "retail";
            so.displayName = "Retail";
            so.category = RoomCategory.Commercial;
            so.size = new Vector2Int(16, 1);
            so.buildCost = 100000;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        [Test]
        public void Cannot_place_office_before_lobby()
        {
            var grid = new TowerGrid();
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, 1)));
        }

        [Test]
        public void Place_lobby_sets_bounds_and_occupancy()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 29, 1, out var lobby));
            Assert.IsTrue(grid.HasLobby);
            Assert.AreEqual(0, grid.MinX);
            Assert.AreEqual(29, grid.MaxX);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var at));
            Assert.AreSame(lobby, at);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(29, 1), out _));
        }

        [Test]
        public void Cannot_place_outside_lobby_bounds()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 1, out _);
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(15, 2))); // 15..23 exceeds max 20
        }

        [Test]
        public void Cannot_overlap_rooms()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 1, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 2), out _));
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(5, 2)));
        }

        [Test]
        public void Basement_rules_respected()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 1, out _);
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(0, -1)));
            Assert.IsTrue(grid.CanPlace(Retail(), new Vector2Int(0, -1)));
        }

        [Test]
        public void Demolish_frees_cells_but_blocks_lobby()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 1, out _);
            grid.TryPlace(Office(), new Vector2Int(0, 2), out var office);
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(3, 2), out var removed));
            Assert.AreEqual(office.InstanceId, removed.InstanceId);
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(0, 2)));
            Assert.IsFalse(grid.TryDemolishAt(new Vector2Int(0, 1), out _));
        }

        [Test]
        public void Lobby_rejects_non_floor_1_and_invalid_span()
        {
            var grid = new TowerGrid();
            Assert.IsFalse(grid.CanPlaceLobby(0, 10, 2));
            Assert.IsFalse(grid.CanPlaceLobby(10, 5, 1));
        }
    }
}
```

- [ ] **Step 3: Run tests — expect FAIL**

Unity → Window → General → Test Runner → EditMode → Run All.

Expected: failures because `TowerGrid` / `RoomInstance` missing.

CLI (optional):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -batchmode -projectPath "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower" `
  -runTests -testPlatform EditMode -logFile "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower\Logs\editmode.log" -quit
```

- [ ] **Step 4: Implement `RoomInstance` and `TowerGrid`**

`Assets/Scripts/Core/RoomInstance.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class RoomInstance
    {
        public int InstanceId { get; }
        public RoomTypeSO Type { get; }
        public Vector2Int Origin { get; }
        public Vector2Int Size { get; }
        public int Evaluation { get; set; } = 100;

        public RoomInstance(int instanceId, RoomTypeSO type, Vector2Int origin, Vector2Int size)
        {
            InstanceId = instanceId;
            Type = type;
            Origin = origin;
            Size = size;
        }

        public IEnumerable<Vector2Int> OccupiedCells()
        {
            for (var dy = 0; dy < Size.y; dy++)
            for (var dx = 0; dx < Size.x; dx++)
                yield return new Vector2Int(Origin.x + dx, Origin.y + dy);
        }
    }
}
```

`Assets/Scripts/Core/TowerGrid.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class TowerGrid
    {
        readonly Dictionary<Vector2Int, RoomInstance> _cells = new();
        readonly List<RoomInstance> _rooms = new();
        int _nextId = 1;

        public bool HasLobby { get; private set; }
        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public IReadOnlyList<RoomInstance> Rooms => _rooms;

        public bool TryGetRoomAt(Vector2Int cell, out RoomInstance room) =>
            _cells.TryGetValue(cell, out room);

        public bool CanPlaceLobby(int minX, int maxX, int floor)
        {
            if (HasLobby) return false;
            if (floor != 1) return false;
            if (maxX < minX) return false;
            for (var x = minX; x <= maxX; x++)
            {
                var cell = new Vector2Int(x, floor);
                if (_cells.ContainsKey(cell)) return false;
            }
            return true;
        }

        public bool TryPlaceLobby(RoomTypeSO lobbyType, int minX, int maxX, int floor, out RoomInstance room)
        {
            room = null;
            if (lobbyType == null || !lobbyType.isLobby) return false;
            if (!CanPlaceLobby(minX, maxX, floor)) return false;

            var width = maxX - minX + 1;
            room = new RoomInstance(_nextId++, lobbyType, new Vector2Int(minX, floor), new Vector2Int(width, 1));
            Register(room);
            HasLobby = true;
            MinX = minX;
            MaxX = maxX;
            return true;
        }

        public bool CanPlace(RoomTypeSO type, Vector2Int origin)
        {
            if (type == null || type.isLobby) return false;
            if (!HasLobby) return false;
            if (type.size.x <= 0 || type.size.y <= 0) return false;

            for (var dy = 0; dy < type.size.y; dy++)
            for (var dx = 0; dx < type.size.x; dx++)
            {
                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (cell.y == 0) return false;
                if (!IsFloorAllowed(type, cell.y)) return false;
                if (cell.x < MinX || cell.x > MaxX) return false;
                if (_cells.ContainsKey(cell)) return false;
            }
            return true;
        }

        public bool TryPlace(RoomTypeSO type, Vector2Int origin, out RoomInstance room)
        {
            room = null;
            if (!CanPlace(type, origin)) return false;
            room = new RoomInstance(_nextId++, type, origin, type.size);
            Register(room);
            return true;
        }

        public bool TryDemolishAt(Vector2Int cell, out RoomInstance removed)
        {
            removed = null;
            if (!_cells.TryGetValue(cell, out var room)) return false;
            if (room.Type != null && room.Type.isLobby) return false;

            foreach (var c in room.OccupiedCells())
                _cells.Remove(c);
            _rooms.Remove(room);
            removed = room;
            return true;
        }

        static bool IsFloorAllowed(RoomTypeSO type, int floor)
        {
            if (floor > 0) return type.allowAboveGround;
            if (floor < 0) return type.allowBasement;
            return false;
        }

        void Register(RoomInstance room)
        {
            foreach (var c in room.OccupiedCells())
                _cells[c] = room;
            _rooms.Add(room);
        }
    }
}
```

- [ ] **Step 5: Run tests — expect PASS**

Re-run EditMode tests. All `TowerGridTests` green.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core Assets/Tests
git commit -m "feat: add TowerGrid with Edit Mode placement tests"
```

---

### Task 4: `FundsWallet` + tests

**Files:**
- Create: `Assets/Scripts/Core/FundsWallet.cs`
- Create: `Assets/Tests/EditMode/FundsWalletTests.cs`

**Interfaces:**
- Consumes: none
- Produces: `FundsWallet` with `int Balance`, `bool CanAfford(int)`, `bool TrySpend(int)`, `void Add(int)`

- [ ] **Step 1: Write failing tests**

```csharp
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class FundsWalletTests
    {
        [Test]
        public void TrySpend_fails_when_insufficient()
        {
            var wallet = new FundsWallet(1000);
            Assert.IsFalse(wallet.TrySpend(1001));
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void TrySpend_succeeds_when_affordable()
        {
            var wallet = new FundsWallet(2_000_000);
            Assert.IsTrue(wallet.TrySpend(40_000));
            Assert.AreEqual(1_960_000, wallet.Balance);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement**

```csharp
namespace BuildATower
{
    public sealed class FundsWallet
    {
        public int Balance { get; private set; }

        public FundsWallet(int startingBalance) => Balance = startingBalance;

        public bool CanAfford(int amount) => amount >= 0 && Balance >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount)) return false;
            Balance -= amount;
            return true;
        }

        public void Add(int amount)
        {
            if (amount < 0) return;
            Balance += amount;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core/FundsWallet.cs Assets/Tests/EditMode/FundsWalletTests.cs
git commit -m "feat: add FundsWallet with spend validation"
```

---

### Task 5: Tilemap view + placeholder painting

**Files:**
- Create: `Assets/Scripts/Rendering/TilemapTowerView.cs`
- Modify: `Assets/Scenes/TowerSandbox.unity` (hierarchy setup in Task 10; this task is script-only)

**Interfaces:**
- Consumes: `RoomInstance`, `RoomTypeSO`
- Produces: `TilemapTowerView` MonoBehaviour methods:
  - `void PaintRoom(RoomInstance room)`
  - `void ClearRoom(RoomInstance room)`
  - `void SetGhost(Vector2Int origin, Vector2Int size, Color color, bool valid)`
  - `void ClearGhost()`
  - Serialized refs: `Tilemap structureTilemap`, `Tilemap roomsTilemap`, `Tilemap ghostTilemap`

- [ ] **Step 1: Implement `TilemapTowerView`**

Use runtime-created colored `Tile` instances (no art pipeline):

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BuildATower
{
    public sealed class TilemapTowerView : MonoBehaviour
    {
        [SerializeField] Tilemap structureTilemap;
        [SerializeField] Tilemap roomsTilemap;
        [SerializeField] Tilemap ghostTilemap;

        readonly Dictionary<Color, Tile> _tiles = new();
        readonly List<Vector3Int> _ghostCells = new();

        public void PaintRoom(RoomInstance room)
        {
            var map = room.Type.isLobby ? structureTilemap : roomsTilemap;
            var tile = GetTile(room.Type.placeholderColor);
            foreach (var cell in room.OccupiedCells())
                map.SetTile(ToTileCell(cell), tile);
        }

        public void ClearRoom(RoomInstance room)
        {
            var map = room.Type.isLobby ? structureTilemap : roomsTilemap;
            foreach (var cell in room.OccupiedCells())
                map.SetTile(ToTileCell(cell), null);
        }

        public void SetGhost(Vector2Int origin, Vector2Int size, Color color, bool valid)
        {
            ClearGhost();
            var c = color;
            c.a = valid ? 0.45f : 0.45f;
            if (!valid) c = Color.Lerp(color, Color.red, 0.65f);
            var tile = GetTile(c);
            for (var dy = 0; dy < size.y; dy++)
            for (var dx = 0; dx < size.x; dx++)
            {
                var cell = ToTileCell(new Vector2Int(origin.x + dx, origin.y + dy));
                ghostTilemap.SetTile(cell, tile);
                _ghostCells.Add(cell);
            }
        }

        public void ClearGhost()
        {
            foreach (var cell in _ghostCells)
                ghostTilemap.SetTile(cell, null);
            _ghostCells.Clear();
        }

        Tile GetTile(Color color)
        {
            if (_tiles.TryGetValue(color, out var existing)) return existing;
            var tile = ScriptableObject.CreateInstance<Tile>();
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            tile.color = Color.white;
            _tiles[color] = tile;
            return tile;
        }

        static Vector3Int ToTileCell(Vector2Int logic) =>
            new(logic.x, logic.y, 0);
    }
}
```

Logical floor `y` maps 1:1 to Tilemap y so floor 1 sits just above y=0 ground line.

- [ ] **Step 2: Compile check**

Unity Console clean.

- [ ] **Step 3: Commit**

```powershell
git add Assets/Scripts/Rendering/TilemapTowerView.cs
git commit -m "feat: add TilemapTowerView placeholder painter"
```

---

### Task 6: `GhostPreview` helper (optional thin wrapper) + `BuildController`

**Files:**
- Create: `Assets/Scripts/Build/BuildController.cs`

**Interfaces:**
- Consumes: `TowerGrid`, `FundsWallet`, `TilemapTowerView`, `RoomTypeSO`, `BuildTool`
- Produces: `BuildController` MonoBehaviour:
  - `TowerGrid Grid { get; }`
  - `FundsWallet Wallet { get; }`
  - `BuildTool CurrentTool { get; }`
  - `RoomTypeSO SelectedRoomType { get; }`
  - `Vector2Int? HoverCell { get; }`
  - `event System.Action StateChanged`
  - `void SetTool(BuildTool tool)`
  - `void SetRoomType(RoomTypeSO type)`
  - Lobby drag: mouse down/up on empty tower before lobby exists
  - Place: click when tool is PlaceRoom
  - Demolish: click when tool is Bulldoze

- [ ] **Step 1: Implement `BuildController`**

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BuildATower
{
    public sealed class BuildController : MonoBehaviour
    {
        [SerializeField] TilemapTowerView view;
        [SerializeField] Camera worldCamera;
        [SerializeField] RoomTypeSO lobbyType;
        [SerializeField] int startingFunds = 2_000_000;

        public TowerGrid Grid { get; private set; }
        public FundsWallet Wallet { get; private set; }
        public BuildTool CurrentTool { get; private set; } = BuildTool.PlaceRoom;
        public RoomTypeSO SelectedRoomType { get; private set; }
        public Vector2Int? HoverCell { get; private set; }
        public event Action StateChanged;

        bool _draggingLobby;
        int _dragStartX;

        void Awake()
        {
            Grid = new TowerGrid();
            Wallet = new FundsWallet(startingFunds);
            SelectedRoomType = lobbyType;
        }

        void Update()
        {
            if (worldCamera == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                view.ClearGhost();
                return;
            }

            var cell = ScreenToCell(Input.mousePosition);
            HoverCell = cell;
            HandleLobbyDrag(cell);
            HandleHoverGhost(cell);
            HandleClicks(cell);
        }

        public void SetTool(BuildTool tool)
        {
            CurrentTool = tool;
            view.ClearGhost();
            StateChanged?.Invoke();
        }

        public void SetRoomType(RoomTypeSO type)
        {
            SelectedRoomType = type;
            CurrentTool = BuildTool.PlaceRoom;
            StateChanged?.Invoke();
        }

        void HandleLobbyDrag(Vector2Int cell)
        {
            if (Grid.HasLobby || lobbyType == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                _draggingLobby = true;
                _dragStartX = cell.x;
            }

            if (_draggingLobby)
            {
                var minX = Mathf.Min(_dragStartX, cell.x);
                var maxX = Mathf.Max(_dragStartX, cell.x);
                var width = maxX - minX + 1;
                var cost = width * lobbyType.buildCost;
                var valid = Grid.CanPlaceLobby(minX, maxX, 1) && cell.y == 1 && Wallet.CanAfford(cost);
                view.SetGhost(new Vector2Int(minX, 1), new Vector2Int(width, 1), lobbyType.placeholderColor, valid);

                if (Input.GetMouseButtonUp(0))
                {
                    _draggingLobby = false;
                    if (valid && Wallet.TrySpend(cost) &&
                        Grid.TryPlaceLobby(lobbyType, minX, maxX, 1, out var room))
                    {
                        view.PaintRoom(room);
                        StateChanged?.Invoke();
                    }
                    view.ClearGhost();
                }
            }
        }

        void HandleHoverGhost(Vector2Int cell)
        {
            if (_draggingLobby || !Grid.HasLobby) return;
            if (CurrentTool != BuildTool.PlaceRoom || SelectedRoomType == null || SelectedRoomType.isLobby)
            {
                view.ClearGhost();
                return;
            }

            var cost = SelectedRoomType.buildCost;
            var valid = Grid.CanPlace(SelectedRoomType, cell) && Wallet.CanAfford(cost);
            view.SetGhost(cell, SelectedRoomType.size, SelectedRoomType.placeholderColor, valid);
        }

        void HandleClicks(Vector2Int cell)
        {
            if (!Input.GetMouseButtonDown(0) || _draggingLobby) return;

            if (CurrentTool == BuildTool.Bulldoze)
            {
                if (Grid.TryDemolishAt(cell, out var removed))
                {
                    view.ClearRoom(removed);
                    StateChanged?.Invoke();
                }
                return;
            }

            if (!Grid.HasLobby) return;
            if (CurrentTool != BuildTool.PlaceRoom || SelectedRoomType == null || SelectedRoomType.isLobby) return;

            var cost = SelectedRoomType.buildCost;
            if (!Wallet.CanAfford(cost)) return;
            if (!Grid.TryPlace(SelectedRoomType, cell, out var room)) return;
            if (!Wallet.TrySpend(cost))
            {
                // Should not happen; grid already placed — keep simple: spend first in final code order
            }
            view.PaintRoom(room);
            StateChanged?.Invoke();
        }

        Vector2Int ScreenToCell(Vector3 screen)
        {
            var world = worldCamera.ScreenToWorldPoint(screen);
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }
    }
}
```

**Fix spend order before commit:** change place path to spend first then place, rolling back is unnecessary if `TrySpend` precedes `TryPlace` and place failure refunds:

Replace the place-click body with:

```csharp
var cost = SelectedRoomType.buildCost;
if (!Grid.CanPlace(SelectedRoomType, cell) || !Wallet.CanAfford(cost)) return;
if (!Wallet.TrySpend(cost)) return;
if (!Grid.TryPlace(SelectedRoomType, cell, out var room))
{
    Wallet.Add(cost);
    return;
}
view.PaintRoom(room);
StateChanged?.Invoke();
```

- [ ] **Step 2: Compile check**

- [ ] **Step 3: Commit**

```powershell
git add Assets/Scripts/Build/BuildController.cs
git commit -m "feat: add BuildController for lobby drag and room place/demolish"
```

---

### Task 7: `CutawayCamera`

**Files:**
- Create: `Assets/Scripts/Camera/CutawayCamera.cs`

**Interfaces:**
- Consumes: main orthographic Camera
- Produces: pan with right or middle mouse; zoom with scroll wheel; clamps optional soft bounds

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;

namespace BuildATower
{
    public sealed class CutawayCamera : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] float panSpeed = 1f;
        [SerializeField] float zoomSpeed = 2f;
        [SerializeField] float minOrtho = 5f;
        [SerializeField] float maxOrtho = 40f;

        Vector3 _lastMouse;

        void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            targetCamera.orthographic = true;
        }

        void Update()
        {
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                targetCamera.orthographicSize = Mathf.Clamp(
                    targetCamera.orthographicSize - scroll * zoomSpeed, minOrtho, maxOrtho);

            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                _lastMouse = Input.mousePosition;

            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                var delta = Input.mousePosition - _lastMouse;
                _lastMouse = Input.mousePosition;
                var worldDelta = targetCamera.ScreenToWorldPoint(Vector3.zero) -
                                 targetCamera.ScreenToWorldPoint(delta);
                transform.position += new Vector3(worldDelta.x * panSpeed, worldDelta.y * panSpeed, 0f);
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```powershell
git add Assets/Scripts/Camera/CutawayCamera.cs
git commit -m "feat: add orthographic cutaway pan/zoom camera"
```

---

### Task 8: UIToolkit HUD

**Files:**
- Create: `Assets/Scripts/UI/TowerHud.uxml`
- Create: `Assets/Scripts/UI/TowerHud.uss`
- Create: `Assets/Scripts/UI/TowerHudController.cs`

**Interfaces:**
- Consumes: `BuildController`, list of placeable `RoomTypeSO` (non-lobby)
- Produces: HUD showing funds, selected tool/room, hover cell/floor; buttons for Bulldoze + each room type

- [ ] **Step 1: Create UXML**

`Assets/Scripts/UI/TowerHud.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <Style src="TowerHud.uss" />
  <ui:VisualElement name="root" class="root">
    <ui:Label name="funds-label" text="Funds: $0" class="funds" />
    <ui:Label name="tool-label" text="Tool: —" class="tool" />
    <ui:Label name="cell-label" text="Cell: —" class="cell" />
    <ui:VisualElement name="toolbar" class="toolbar">
      <ui:Button name="btn-bulldoze" text="Bulldoze" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create USS**

`Assets/Scripts/UI/TowerHud.uss`:

```css
.root {
  position: absolute;
  left: 12px;
  top: 12px;
  padding: 10px;
  background-color: rgba(0, 0, 0, 0.55);
  border-radius: 6px;
  min-width: 220px;
}

.funds, .tool, .cell {
  color: rgb(240, 240, 240);
  margin-bottom: 4px;
  font-size: 14px;
}

.toolbar {
  flex-direction: row;
  flex-wrap: wrap;
  margin-top: 8px;
}

.toolbar Button {
  margin: 2px;
}
```

- [ ] **Step 3: Create controller**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BuildATower
{
    public sealed class TowerHudController : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] BuildController build;
        [SerializeField] List<RoomTypeSO> placeableRooms = new();

        Label _funds;
        Label _tool;
        Label _cell;
        VisualElement _toolbar;

        void OnEnable()
        {
            var root = document.rootVisualElement;
            _funds = root.Q<Label>("funds-label");
            _tool = root.Q<Label>("tool-label");
            _cell = root.Q<Label>("cell-label");
            _toolbar = root.Q<VisualElement>("toolbar");

            root.Q<Button>("btn-bulldoze").clicked += () => build.SetTool(BuildTool.Bulldoze);

            foreach (var room in placeableRooms)
            {
                if (room == null || room.isLobby) continue;
                var captured = room;
                var btn = new Button(() => build.SetRoomType(captured)) { text = room.displayName };
                _toolbar.Add(btn);
            }

            build.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (build != null) build.StateChanged -= Refresh;
        }

        void Update() => RefreshHoverOnly();

        void Refresh()
        {
            if (_funds == null || build == null) return;
            _funds.text = $"Funds: ${build.Wallet.Balance:N0}";
            var roomName = build.SelectedRoomType != null ? build.SelectedRoomType.displayName : "—";
            _tool.text = $"Tool: {build.CurrentTool} / {roomName}";
            RefreshHoverOnly();
        }

        void RefreshHoverOnly()
        {
            if (_cell == null || build == null) return;
            if (build.HoverCell.HasValue)
            {
                var c = build.HoverCell.Value;
                var floorLabel = c.y > 0 ? c.y.ToString() : $"B{-c.y}";
                _cell.text = $"Cell: ({c.x}, floor {floorLabel})";
            }
            else _cell.text = "Cell: —";
        }
    }
}
```

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/UI
git commit -m "feat: add UIToolkit build HUD"
```

---

### Task 9: Create `RoomTypeSO` assets

**Files:**
- Create: `Assets/ScriptableObjects/Rooms/Lobby.asset`
- Create: `Assets/ScriptableObjects/Rooms/Office.asset`
- Create: `Assets/ScriptableObjects/Rooms/Condo.asset`
- Create: `Assets/ScriptableObjects/Rooms/HotelSingle.asset`
- Create: `Assets/ScriptableObjects/Rooms/RetailFastFood.asset`

**Interfaces:**
- Consumes: `RoomTypeSO` CreateAssetMenu
- Produces: authored assets with values below

- [ ] **Step 1: Create assets in Editor**

Use Assets → Create → Build-A-Tower → Room Type. Set fields:

| Asset | id | size | buildCost | color | flags |
|-------|-----|------|-----------|-------|-------|
| Lobby | `lobby` | 1×1 (per cell) | 5000 / cell | warm gray | `isLobby=true`, above only |
| Office | `office` | 9×1 | 40000 | blue | above only, QuarterlyRent, baseIncome 5000, active hours 9–17 |
| Condo | `condo` | 16×1 | 80000 | green | above only, UpfrontSale, high noiseSensitivity |
| HotelSingle | `hotel_single` | 4×1 | 20000 | pink/red | above only, NightlyRate, requiresHousekeeping |
| RetailFastFood | `retail` | 16×1 | 100000 | orange | above+basement, TrafficVariable, higher noiseOutput |

- [ ] **Step 2: Commit**

```powershell
git add Assets/ScriptableObjects
git commit -m "feat: add starter RoomTypeSO catalog"
```

---

### Task 10: Wire `TowerSandbox` scene

**Files:**
- Modify: `Assets/Scenes/TowerSandbox.unity`

**Interfaces:**
- Consumes: all gameplay components
- Produces: playable scene

- [ ] **Step 1: Hierarchy**

```
TowerSandbox
├── Grid (Grid component, cell size 1,1,1)
│   ├── Structure (Tilemap + TilemapRenderer) sorting 0
│   ├── Rooms (Tilemap + TilemapRenderer) sorting 1
│   └── Ghost (Tilemap + TilemapRenderer) sorting 2
├── Main Camera (Orthographic, CutawayCamera)
├── BuildSystems
│   ├── TilemapTowerView
│   └── BuildController
└── HUD
    ├── UIDocument (source: TowerHud.uxml)
    ├── EventSystem
    └── TowerHudController
```

- [ ] **Step 2: Hook references**

- `TilemapTowerView` → three tilemaps
- `BuildController` → view, camera, Lobby asset, starting funds 2000000
- `TowerHudController` → document, build, placeable rooms list (Office, Condo, HotelSingle, RetailFastFood)
- Camera position near `(15, 5, -10)`, ortho size ~12
- Optional: simple Sprite background for sky (top) / dirt (y≤0) — solid color quads OK

- [ ] **Step 3: Enter Play Mode smoke test**

Drag lobby on floor 1; place an office; bulldoze office; confirm funds change.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scenes/TowerSandbox.unity
git commit -m "feat: wire TowerSandbox playable build scene"
```

---

### Task 11: Spec verification checklist

**Files:**
- Modify: none required (manual QA)

- [ ] **Step 1: Run EditMode tests** — all green

- [ ] **Step 2: Manual Play Mode checklist (from spec)**

- [ ] Cannot place Office before Lobby
- [ ] Cannot place outside lobby X bounds
- [ ] Cannot overlap rooms
- [ ] Insufficient funds blocks placement
- [ ] Demolish frees cells and allows re-place
- [ ] Lobby demolish is blocked
- [ ] Camera pan/zoom usable with a tall stack (place many floors of offices)

- [ ] **Step 3: Update README with how to play**

Append to `README.md`:

```markdown
## Play Slice #1

1. Open this folder in Unity 6000.4.7f1.
2. Open `Assets/Scenes/TowerSandbox.unity`.
3. Press Play.
4. Drag on floor 1 to place the Lobby, then use the HUD to place rooms.
5. Right/middle-drag to pan; scroll to zoom; Bulldoze to remove non-lobby rooms.
```

- [ ] **Step 4: Final commit**

```powershell
git add README.md
git commit -m "docs: add Slice #1 play instructions"
```

---

## Spec coverage self-check

| Spec item | Task |
|-----------|------|
| RoomTypeSO fields + catalog | 2, 9 |
| TowerGrid lobby/bounds/overlap/basement/demolish | 3 |
| Funds $2,000,000 + spend on build | 4, 6 |
| Tilemap layers / placeholders | 5, 10 |
| Lobby click-drag width | 6 |
| Place / bulldoze | 6 |
| Orthographic pan/zoom | 7 |
| UIToolkit HUD | 8 |
| Scene wiring | 10 |
| Verification checklist | 11 |
| Out-of-scope agents/transit/economy | intentionally omitted |

## Type consistency notes

- Logical cell `Vector2Int(x, floor)` ↔ Tilemap `Vector3Int(x, floor, 0)`
- Lobby cost = `width * lobbyType.buildCost`
- `RoomTypeSO.isLobby` gates lobby APIs and demolish protection
- `BuildController.StateChanged` refreshes HUD after place/demolish/tool change
