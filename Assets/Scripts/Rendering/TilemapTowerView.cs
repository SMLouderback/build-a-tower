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
