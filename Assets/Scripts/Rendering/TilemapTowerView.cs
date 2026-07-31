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

        readonly Dictionary<(Color color, byte edges), Tile> _tiles = new();
        readonly List<Vector3Int> _ghostCells = new();
        readonly List<Vector3Int> _selectionCells = new();
        readonly List<Vector3Int> _handleCells = new();

        public void PaintRoom(RoomInstance room)
        {
            if (room?.Type == null) return;

            var occupied = CollectOccupied(room);
            if (IsVisibleTransit(room))
            {
                // Transit draws on the rooms layer so it stays visible over rooms.
                foreach (var cell in occupied)
                {
                    var tile = GetTile(room.Type.placeholderColor, EdgeMaskFor(cell, occupied));
                    roomsTilemap.SetTile(ToTileCell(cell), tile);
                }

                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            foreach (var cell in occupied)
            {
                var tile = GetTile(room.Type.placeholderColor, EdgeMaskFor(cell, occupied));
                map.SetTile(ToTileCell(cell), tile);
            }
        }

        public void PaintCell(Vector2Int cell, RoomInstance room)
        {
            if (room?.Type == null) return;
            var occupied = CollectOccupied(room);
            var tile = GetTile(room.Type.placeholderColor, EdgeMaskFor(cell, occupied));
            if (IsVisibleTransit(room))
            {
                roomsTilemap.SetTile(ToTileCell(cell), tile);
                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            map.SetTile(ToTileCell(cell), tile);
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
        /// Visual-only starter band: dirt under the lobby, yellow Floor G strip where the lobby must be dragged.
        /// G / ground / 1st floor are the same level. Does not occupy TowerGrid cells.
        /// </summary>
        public void PaintStarterGuides(int minX, int maxX)
        {
            var dirt = GetTile(new Color(0.45f, 0.32f, 0.22f, 1f), EdgeMask.None);
            var lobbyGuide = GetTile(new Color(0.95f, 0.82f, 0.28f, 1f), EdgeMask.None);

            for (var x = minX; x <= maxX; x++)
            {
                structureTilemap.SetTile(new Vector3Int(x, TowerGrid.LobbyFloor, 0), lobbyGuide);
                for (var y = -1; y >= -5; y--)
                    structureTilemap.SetTile(new Vector3Int(x, y, 0), dirt);
            }
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

        static bool UsesStructureMap(RoomInstance room) =>
            room.Type != null && (room.Type.isLobby || room.Type.isScaffolding);

        static bool IsVisibleTransit(RoomInstance room) =>
            room?.Type != null && (room.Type.isStairs || room.Type.isElevatorShaft);

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
