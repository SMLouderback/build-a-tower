using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BuildATower
{
    public static class HotelCutawayArt
    {
        public const int CellPixels = 128;

        static readonly Dictionary<string, Vector2Int> ExpectedSizesByKey = new()
        {
            ["hotel_3_base"] = new Vector2Int(384, 128),
            ["hotel_4_mid"] = new Vector2Int(512, 128),
            ["hotel_5_mid"] = new Vector2Int(640, 128),
            ["hotel_6_mid"] = new Vector2Int(768, 128),
            ["hotel_5_upper"] = new Vector2Int(640, 128),
            ["hotel_8_upper"] = new Vector2Int(1024, 128),
        };

        static readonly Dictionary<string, Tile[]> _tileCache = new();

        /// <summary>Test seam: replaces Resources-backed BuildTiles when set.</summary>
        public static Func<string, int, Tile[]> BuildTilesForTests;

        public static bool IsHotel(RoomTypeSO type) =>
            type != null && type.category == RoomCategory.Hotel;

        public static string ResolveArtKey(RoomTypeSO type)
        {
            if (!IsHotel(type)) return string.Empty;

            if (type.id == "hotel_premium" &&
                (type.luxuryBand == LuxuryBand.None || type.luxuryBand == LuxuryBand.Mid))
                return "hotel_4_mid";

            var bandSuffix = BandSuffix(type.luxuryBand);
            if (bandSuffix == null) return string.Empty;

            var key = $"hotel_{type.size.x}_{bandSuffix}";
            return ExpectedSizesByKey.ContainsKey(key) ? key : string.Empty;
        }

        public static string ResourcePath(string artKey) => "Art/Hotels/" + artKey;

        public static Vector2Int ExpectedPixelSize(string artKey)
        {
            if (string.IsNullOrEmpty(artKey)) return Vector2Int.zero;
            return ExpectedSizesByKey.TryGetValue(artKey, out var size) ? size : Vector2Int.zero;
        }

        /// <summary>
        /// Sliced cell tile for a hotel room column. Returns false when art is missing or
        /// panorama width does not match the room footprint (caller falls back to palette).
        /// </summary>
        public static bool TryHotelTile(RoomInstance room, int cellX, out Tile tile)
        {
            tile = null;
            if (room?.Type == null || !IsHotel(room.Type)) return false;

            var artKey = ResolveArtKey(room.Type);
            if (string.IsNullOrEmpty(artKey)) return false;

            var cellIndex = cellX - room.Origin.x;
            if (cellIndex < 0 || cellIndex >= room.Size.x) return false;

            var tiles = GetOrBuildTiles(artKey, room.Size.x);
            if (tiles == null || cellIndex >= tiles.Length || tiles[cellIndex] == null)
                return false;

            tile = tiles[cellIndex];
            return true;
        }

        public static void ResetForTests()
        {
            BuildTilesForTests = null;
            _tileCache.Clear();
        }

        /// <summary>Test helper: seed a cache entry (e.g. wrong-width poison).</summary>
        public static void SeedCacheForTests(string artKey, Tile[] tiles)
        {
            if (string.IsNullOrEmpty(artKey)) return;
            if (tiles == null)
                _tileCache.Remove(artKey);
            else
                _tileCache[artKey] = tiles;
        }

        /// <summary>Test helper: cached slice count for an art key (0 if absent).</summary>
        public static int CachedTileCountForTests(string artKey)
        {
            if (string.IsNullOrEmpty(artKey)) return 0;
            return _tileCache.TryGetValue(artKey, out var tiles) && tiles != null ? tiles.Length : 0;
        }

        static string BandSuffix(LuxuryBand band) => band switch
        {
            LuxuryBand.Base => "base",
            LuxuryBand.Mid => "mid",
            LuxuryBand.Upper => "upper",
            _ => null
        };

        static Tile[] GetOrBuildTiles(string artKey, int widthCells)
        {
            if (_tileCache.TryGetValue(artKey, out var cached))
            {
                if (cached != null && cached.Length == widthCells)
                    return cached;
                // Width mismatch or prior failed build — do not keep a poisoned entry.
                _tileCache.Remove(artKey);
            }

            var built = BuildTiles(artKey, widthCells);
            // Only cache successful builds so a bad width never blocks later correct loads.
            if (built != null)
                _tileCache[artKey] = built;
            return built;
        }

        static Tile[] BuildTiles(string artKey, int widthCells)
        {
            if (BuildTilesForTests != null)
                return BuildTilesForTests(artKey, widthCells);

            var expected = ExpectedPixelSize(artKey);
            if (expected.x != widthCells * CellPixels || expected.y != CellPixels)
                return null;

            var panPx = TryLoadPanPixels(ResourcePath(artKey));
            if (panPx == null) return null;

            var panWidthPixels = widthCells * CellPixels;
            if (panPx.Length != panWidthPixels * CellPixels) return null;

            var tiles = new Tile[widthCells];
            for (var i = 0; i < widthCells; i++)
            {
                var cell = ExtractCellFromPan(panPx, panWidthPixels, i);
                ForceOpaque(cell);
                tiles[i] = MakeTile($"{artKey}_c{i}", cell);
            }

            return tiles;
        }

        static Color[] TryLoadPanPixels(string resourcePath)
        {
            var bytesAsset = Resources.Load<TextAsset>(resourcePath);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png == null || png.Length < 32)
            {
                var tex = Resources.Load<Texture2D>(resourcePath);
                if (tex == null) return null;
                try
                {
                    png = tex.EncodeToPNG();
                }
                catch (UnityException)
                {
                    return null;
                }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(png, false))
            {
                UnityEngine.Object.Destroy(decoded);
                return null;
            }

            if (decoded.height != CellPixels || decoded.width % CellPixels != 0)
            {
                UnityEngine.Object.Destroy(decoded);
                return null;
            }

            var px = decoded.GetPixels();
            UnityEngine.Object.Destroy(decoded);
            return px;
        }

        static Color[] ExtractCellFromPan(Color[] panPx, int panWidthPixels, int slice)
        {
            var cell = new Color[CellPixels * CellPixels];
            var x0 = slice * CellPixels;
            for (var y = 0; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
                cell[y * CellPixels + x] = panPx[y * panWidthPixels + x0 + x];
            return cell;
        }

        static void ForceOpaque(Color[] px)
        {
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                c.a = 1f;
                px[i] = c;
            }
        }

        static Tile MakeTile(string name, Color[] pixels)
        {
            var tex = new Texture2D(CellPixels, CellPixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            tex.SetPixels(pixels);
            tex.Apply();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, CellPixels, CellPixels),
                new Vector2(0.5f, 0.5f),
                CellPixels);
            tile.color = Color.white;
            tile.name = name;
            return tile;
        }
    }
}
