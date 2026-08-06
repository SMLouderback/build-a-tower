using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BuildATower
{
    public sealed class TilemapTowerView : MonoBehaviour
    {
        const int TilePixels = 16;
        const int BorderThickness = 2;

        [SerializeField] Tilemap structureTilemap;
        [SerializeField] Tilemap roomsTilemap;
        [SerializeField] Tilemap ghostTilemap;
        [SerializeField] Tilemap heatmapTilemap;

        readonly Dictionary<(Color color, byte edges), Tile> _tiles = new();
        readonly List<Vector3Int> _ghostCells = new();
        readonly List<Vector3Int> _selectionCells = new();
        readonly List<Vector3Int> _handleCells = new();
        readonly List<Vector3Int> _heatmapCells = new();

        /// <param name="skipCell">
        /// When painting a non-transit room, skip these logic cells (e.g. stairs/elevator
        /// still own the cell in the grid). Prevents underlay rooms from erasing transit tiles.
        /// </param>
        public void PaintRoom(RoomInstance room, System.Func<Vector2Int, bool> skipCell = null)
        {
            if (room?.Type == null) return;

            var color = RoomPaintColor(room);
            var occupied = CollectOccupied(room);
            if (IsVisibleTransit(room))
            {
                // Transit draws on the rooms layer so it stays visible over rooms.
                foreach (var cell in occupied)
                {
                    var tile = GetTile(color, EdgeMaskFor(cell, occupied));
                    roomsTilemap.SetTile(ToTileCell(cell), tile);
                }

                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            foreach (var cell in occupied)
            {
                if (skipCell != null && skipCell(cell)) continue;
                var tile = GetTile(color, EdgeMaskFor(cell, occupied));
                map.SetTile(ToTileCell(cell), tile);
            }
        }

        public void PaintCell(Vector2Int cell, RoomInstance room)
        {
            if (room?.Type == null) return;
            var occupied = CollectOccupied(room);
            var tile = GetTile(RoomPaintColor(room), EdgeMaskFor(cell, occupied));
            if (IsVisibleTransit(room))
            {
                roomsTilemap.SetTile(ToTileCell(cell), tile);
                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            map.SetTile(ToTileCell(cell), tile);
        }

        /// <summary>Repaint every stairs/elevator room (call after underlay paints).</summary>
        public void PaintTransitRooms(IEnumerable<RoomInstance> rooms)
        {
            if (rooms == null) return;
            foreach (var room in rooms)
            {
                if (IsVisibleTransit(room))
                    PaintRoom(room);
            }
        }

        public void ClearRoom(RoomInstance room)
        {
            if (IsVisibleTransit(room))
            {
                foreach (var cell in room.OccupiedCells())
                    roomsTilemap.SetTile(ToTileCell(cell), null);
                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            foreach (var cell in room.OccupiedCells())
                map.SetTile(ToTileCell(cell), null);
        }

        public void ClearCell(Vector2Int cell, bool structureMap)
        {
            var map = structureMap ? structureTilemap : roomsTilemap;
            map.SetTile(ToTileCell(cell), null);
        }

        public void SetGhost(Vector2Int origin, Vector2Int size, Color color, bool valid)
        {
            ClearGhost();
            // Keep selection/handles from fighting the ghost preview occupancy list.
            var c = valid ? color : Color.Lerp(color, Color.red, 0.65f);
            c.a = 0.45f;
            var occupied = new HashSet<Vector2Int>();
            for (var dy = 0; dy < size.y; dy++)
            for (var dx = 0; dx < size.x; dx++)
                occupied.Add(new Vector2Int(origin.x + dx, origin.y + dy));

            foreach (var logic in occupied)
            {
                var cell = ToTileCell(logic);
                ghostTilemap.SetTile(cell, GetTile(c, EdgeMaskFor(logic, occupied)));
                _ghostCells.Add(cell);
            }
        }

        public void ClearGhost()
        {
            foreach (var cell in _ghostCells)
                ghostTilemap.SetTile(cell, null);
            _ghostCells.Clear();
        }

        public void SetSelection(RoomInstance room)
        {
            ClearSelection();
            if (room == null || ghostTilemap == null) return;
            var color = new Color(1f, 1f, 1f, 0.28f);
            var occupied = CollectOccupied(room);
            foreach (var logic in occupied)
            {
                var tc = ToTileCell(logic);
                ghostTilemap.SetTile(tc, GetTile(color, EdgeMaskFor(logic, occupied)));
                _selectionCells.Add(tc);
            }
        }

        public void ClearSelection()
        {
            if (ghostTilemap == null) return;
            foreach (var cell in _selectionCells)
                ghostTilemap.SetTile(cell, null);
            _selectionCells.Clear();
        }

        public void EnsureHeatmapTilemap()
        {
            if (heatmapTilemap == null)
            {
                if (roomsTilemap == null) return;
                var parent = roomsTilemap.transform.parent;
                if (parent == null) return;

                var go = new GameObject("Heatmap");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                heatmapTilemap = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>();
            }

            ApplyHeatmapSorting();
        }

        void ApplyHeatmapSorting()
        {
            if (heatmapTilemap == null ||
                !heatmapTilemap.TryGetComponent<TilemapRenderer>(out var heatmapRenderer))
                return;

            var roomsOrder = 0;
            if (roomsTilemap != null &&
                roomsTilemap.TryGetComponent<TilemapRenderer>(out var roomsRenderer))
                roomsOrder = roomsRenderer.sortingOrder;

            var order = roomsOrder + 1;

            if (ghostTilemap != null &&
                ghostTilemap.TryGetComponent<TilemapRenderer>(out var ghostRenderer))
            {
                // Keep ghosts/selection above heatmap (rooms < heatmap < ghost).
                if (ghostRenderer.sortingOrder <= order)
                    ghostRenderer.sortingOrder = order + 1;
                order = Mathf.Min(order, ghostRenderer.sortingOrder - 1);
            }

            heatmapRenderer.sortingOrder = Mathf.Max(order, roomsOrder + 1);
        }

        public void PaintHeatmap(
            IEnumerable<Vector2Int> roomCells,
            IEnumerable<KeyValuePair<Vector2Int, float>> scores,
            HeatmapColorScale scale)
        {
            EnsureHeatmapTilemap();
            ClearHeatmap();
            if (heatmapTilemap == null) return;

            // Grey-wash all tower room cells so room colors do not show through.
            if (roomCells != null)
            {
                foreach (var cell in roomCells)
                {
                    var tc = ToTileCell(cell);
                    heatmapTilemap.SetTile(tc, GetTile(HeatmapColors.Grey, EdgeMask.All));
                    _heatmapCells.Add(tc);
                }
            }

            if (scores == null) return;

            foreach (var kv in scores)
            {
                Color c;
                if (scale == HeatmapColorScale.Profit)
                {
                    if (!HeatmapColors.TryProfitColor(kv.Value, out c)) continue;
                }
                else
                {
                    // Score exactly 0 = grey only (no blue tint).
                    if (kv.Value <= 0.02f) continue;
                    c = HeatmapColors.RiskColor(kv.Value);
                }

                var tc = ToTileCell(kv.Key);
                heatmapTilemap.SetTile(tc, GetTile(c, EdgeMask.All));
                _heatmapCells.Add(tc);
            }
        }

        public void ClearHeatmap()
        {
            if (heatmapTilemap == null) return;
            foreach (var cell in _heatmapCells)
                heatmapTilemap.SetTile(cell, null);
            _heatmapCells.Clear();
        }

        public void SetElevatorEdgeHandles(RoomInstance shaft)
        {
            ClearEdgeHandles();
            if (shaft?.Type == null || !shaft.Type.isElevatorShaft || ghostTilemap == null)
                return;

            var color = new Color(1f, 0.85f, 0.2f, 0.75f);
            var tile = GetTile(color, EdgeMask.All);
            var minY = shaft.Origin.y;
            var maxY = minY + shaft.Size.y - 1;
            var top = ToTileCell(new Vector2Int(shaft.Origin.x, maxY));
            var bottom = ToTileCell(new Vector2Int(shaft.Origin.x, minY));
            ghostTilemap.SetTile(top, tile);
            ghostTilemap.SetTile(bottom, tile);
            _handleCells.Add(top);
            if (top != bottom)
                _handleCells.Add(bottom);
        }

        public void ClearEdgeHandles()
        {
            if (ghostTilemap == null) return;
            foreach (var cell in _handleCells)
                ghostTilemap.SetTile(cell, null);
            _handleCells.Clear();
        }

        /// <summary>
        /// Visual-only starter band: yellow Floor G lobby drag hint, and a wide dirt fill
        /// under ground for basement context. Does not occupy TowerGrid cells.
        /// </summary>
        /// <param name="lobbyMinX">Inclusive left of lobby guide strip.</param>
        /// <param name="lobbyMaxX">Inclusive right of lobby guide strip.</param>
        /// <param name="dirtMinX">Inclusive left of underground dirt.</param>
        /// <param name="dirtMaxX">Inclusive right of underground dirt.</param>
        /// <param name="dirtDepth">How many floors below G to paint dirt (default 10).</param>
        public void PaintStarterGuides(
            int lobbyMinX,
            int lobbyMaxX,
            int dirtMinX,
            int dirtMaxX,
            int dirtDepth = 10)
        {
            var dirt = GetTile(DirtBand.Color, EdgeMask.None);
            var lobbyGuide = GetTile(new Color(0.95f, 0.82f, 0.28f, 1f), EdgeMask.None);

            for (var x = lobbyMinX; x <= lobbyMaxX; x++)
                structureTilemap.SetTile(new Vector3Int(x, TowerGrid.LobbyFloor, 0), lobbyGuide);

            var depth = Mathf.Max(1, dirtDepth);
            for (var x = dirtMinX; x <= dirtMaxX; x++)
            {
                for (var y = -1; y >= -depth; y--)
                    structureTilemap.SetTile(new Vector3Int(x, y, 0), dirt);
            }
        }

        /// <summary>Repaints brown dirt on the structure layer (basement empty cells).</summary>
        public void PaintDirtCell(Vector2Int cell)
        {
            if (structureTilemap == null) return;
            var dirt = GetTile(DirtBand.Color, EdgeMask.None);
            structureTilemap.SetTile(ToTileCell(cell), dirt);
        }

        public void ClearStructureRow(int floor, int minX, int maxX)
        {
            for (var x = minX; x <= maxX; x++)
                structureTilemap.SetTile(new Vector3Int(x, floor, 0), null);
        }

        Tile GetTile(Color color, byte edges)
        {
            var key = (color, edges);
            if (_tiles.TryGetValue(key, out var existing)) return existing;

            var tile = ScriptableObject.CreateInstance<Tile>();
            var tex = new Texture2D(TilePixels, TilePixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var border = Color.Lerp(color, Color.black, 0.55f);
            border.a = color.a;

            for (var y = 0; y < TilePixels; y++)
            for (var x = 0; x < TilePixels; x++)
            {
                var onBorder =
                    ((edges & EdgeMask.Left) != 0 && x < BorderThickness) ||
                    ((edges & EdgeMask.Right) != 0 && x >= TilePixels - BorderThickness) ||
                    ((edges & EdgeMask.Bottom) != 0 && y < BorderThickness) ||
                    ((edges & EdgeMask.Top) != 0 && y >= TilePixels - BorderThickness);
                tex.SetPixel(x, y, onBorder ? border : color);
            }

            tex.Apply();
            tile.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, TilePixels, TilePixels),
                new Vector2(0.5f, 0.5f),
                TilePixels);
            tile.color = Color.white;
            _tiles[key] = tile;
            return tile;
        }

        static HashSet<Vector2Int> CollectOccupied(RoomInstance room)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var cell in room.OccupiedCells())
                occupied.Add(cell);
            return occupied;
        }

        static byte EdgeMaskFor(Vector2Int cell, HashSet<Vector2Int> occupied)
        {
            byte edges = EdgeMask.None;
            if (!occupied.Contains(new Vector2Int(cell.x - 1, cell.y))) edges |= EdgeMask.Left;
            if (!occupied.Contains(new Vector2Int(cell.x + 1, cell.y))) edges |= EdgeMask.Right;
            if (!occupied.Contains(new Vector2Int(cell.x, cell.y - 1))) edges |= EdgeMask.Bottom;
            if (!occupied.Contains(new Vector2Int(cell.x, cell.y + 1))) edges |= EdgeMask.Top;
            return edges;
        }

        static Vector3Int ToTileCell(Vector2Int logic) =>
            new(logic.x, logic.y, 0);

        /// <summary>
        /// Broken: dark desaturated. Dirty (else): brownish wash. Otherwise placeholder.
        /// </summary>
        public static Color RoomPaintColor(RoomInstance room)
        {
            var baseColor = room?.Type != null ? room.Type.placeholderColor : Color.magenta;
            if (room == null) return baseColor;
            if (room.IsBroken)
            {
                var gray = baseColor.grayscale;
                var c = Color.Lerp(baseColor, new Color(gray, gray, gray, baseColor.a), 0.75f);
                return new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, baseColor.a);
            }

            if (room.Dirty)
                return Color.Lerp(baseColor, new Color(0.45f, 0.28f, 0.12f, baseColor.a), 0.55f);

            return baseColor;
        }

        static bool UsesStructureMap(RoomInstance room) =>
            room.Type != null && (room.Type.isLobby || room.Type.isScaffolding);

        static bool IsVisibleTransit(RoomInstance room) =>
            room?.Type != null &&
            (room.Type.isStairs || room.Type.isElevatorShaft || room.Type.isParkingRamp);

        static class EdgeMask
        {
            public const byte None = 0;
            public const byte Left = 1;
            public const byte Right = 2;
            public const byte Bottom = 4;
            public const byte Top = 8;
            public const byte All = Left | Right | Bottom | Top;
        }
    }
}
