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
            if (room?.Type != null && room.Type.isStairs)
            {
                // Stairs draw on the rooms layer so they stay visible over condos/offices.
                var tile = GetTile(room.Type.placeholderColor);
                foreach (var cell in room.OccupiedCells())
                {
                    var tc = ToTileCell(cell);
                    roomsTilemap.SetTile(tc, tile);
                }

                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            var roomTile = GetTile(room.Type.placeholderColor);
            foreach (var cell in room.OccupiedCells())
                map.SetTile(ToTileCell(cell), roomTile);
        }

        public void PaintCell(Vector2Int cell, RoomInstance room)
        {
            if (room?.Type == null) return;
            if (room.Type.isStairs)
            {
                roomsTilemap.SetTile(ToTileCell(cell), GetTile(room.Type.placeholderColor));
                return;
            }

            var map = UsesStructureMap(room) ? structureTilemap : roomsTilemap;
            map.SetTile(ToTileCell(cell), GetTile(room.Type.placeholderColor));
        }

        public void ClearRoom(RoomInstance room)
        {
            if (room?.Type != null && room.Type.isStairs)
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
            var c = valid ? color : Color.Lerp(color, Color.red, 0.65f);
            c.a = 0.45f;
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

        /// <summary>
        /// Visual-only starter band: dirt under the lobby, yellow Floor G strip where the lobby must be dragged.
        /// G / ground / 1st floor are the same level. Does not occupy TowerGrid cells.
        /// </summary>
        public void PaintStarterGuides(int minX, int maxX)
        {
            var dirt = GetTile(new Color(0.45f, 0.32f, 0.22f, 1f));
            var lobbyGuide = GetTile(new Color(0.95f, 0.82f, 0.28f, 1f));

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

        static bool UsesStructureMap(RoomInstance room) =>
            room.Type != null && (room.Type.isLobby || room.Type.isScaffolding);
    }
}
