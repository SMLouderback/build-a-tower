using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BuildATower
{
    public static class OfficeCutawayArt
    {
        public const int CellPixels = 128;

        static readonly Dictionary<(string typeId, int variant), Tile[]> _tileCache = new();

        public static Func<int> RollArtVariantForTests;

        public static bool IsOffice(RoomTypeSO type) =>
            type != null && type.category == RoomCategory.Office;

        public static string ResourceLeaf(string typeId, int variant) =>
            $"{typeId}_v{ClampArtVariant(variant):D2}";

        public static string ResourcePath(string typeId, int variant) =>
            "Art/Offices/" + ResourceLeaf(typeId, variant);

        public static Vector2Int ExpectedPixelSize(Vector2Int cellSize) =>
            new Vector2Int(cellSize.x * CellPixels, CellPixels);

        public static int ClampArtVariant(int v) => v == 2 ? 2 : 1;

        public static int RollArtVariant()
        {
            if (RollArtVariantForTests != null)
                return ClampArtVariant(RollArtVariantForTests());
            return UnityEngine.Random.Range(0, 2) == 0 ? 1 : 2;
        }

        public static void AssignArtVariantIfUnset(RoomInstance room)
        {
            if (room?.Type == null || room.Type.category != RoomCategory.Office) return;
            if (room.ArtVariant != 0) return;
            room.ArtVariant = RollArtVariant();
        }

        /// <summary>
        /// Sliced cell tile for an office room column. Returns false when art is missing or
        /// panorama width does not match the room footprint (caller falls back to palette).
        /// </summary>
        public static bool TryOfficeTile(RoomInstance room, int cellX, out Tile tile)
        {
            tile = null;
            if (room?.Type == null || !IsOffice(room.Type)) return false;

            var cellIndex = cellX - room.Origin.x;
            if (cellIndex < 0 || cellIndex >= room.Size.x) return false;

            var artTypeId = ResolveArtTypeId(room.Type.id);
            var variant = ClampArtVariant(room.ArtVariant);
            var tiles = GetOrBuildTiles(artTypeId, variant, room.Size.x);
            if (tiles == null || cellIndex >= tiles.Length || tiles[cellIndex] == null)
                return false;

            tile = tiles[cellIndex];
            return true;
        }

        public static void ResetForTests()
        {
            RollArtVariantForTests = null;
            _tileCache.Clear();
        }

        static string ResolveArtTypeId(string typeId) => typeId switch
        {
            "office" => "office_base",
            "office_premium" => "office_mid_standard",
            _ => typeId
        };

        static Tile[] GetOrBuildTiles(string typeId, int variant, int widthCells)
        {
            var key = (typeId, variant);
            if (_tileCache.TryGetValue(key, out var cached))
                return cached != null && cached.Length == widthCells ? cached : null;

            var built = BuildTiles(typeId, variant, widthCells);
            _tileCache[key] = built;
            return built;
        }

        static Tile[] BuildTiles(string typeId, int variant, int widthCells)
        {
            var panPx = TryLoadPanPixels(ResourcePath(typeId, variant));
            if (panPx == null) return null;

            var panWidthPixels = widthCells * CellPixels;
            if (panPx.Length != panWidthPixels * CellPixels) return null;

            var leaf = ResourceLeaf(typeId, variant);
            var tiles = new Tile[widthCells];
            for (var i = 0; i < widthCells; i++)
            {
                var cell = ExtractCellFromPan(panPx, panWidthPixels, i);
                ForceOpaque(cell);
                tiles[i] = MakeTile($"{leaf}_c{i}", cell);
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
