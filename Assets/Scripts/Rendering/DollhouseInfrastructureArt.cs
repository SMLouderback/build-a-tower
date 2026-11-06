using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Cement floor/ceiling/party-wall underlay behind dollhouse cutaways so seams
    /// between rooms read as structure without filling exterior window panes.
    /// </summary>
    public static class DollhouseInfrastructureArt
    {
        public const int SortingOrder = RoomDollhouseArt.SortingOrder - 1;

        /// <summary>Warm concrete — close to <see cref="TowerLookPalette.BuildingShell"/>.</summary>
        public static readonly Color Cement = new(0.48f, 0.46f, 0.42f, 1f);
        public static readonly Color CementDark = new(0.38f, 0.36f, 0.33f, 1f);

        public const float FloorBandFrac = 0.16f;
        public const float CeilingBandFrac = 0.14f;
        public const float PartyWallFrac = 0.10f;

        static readonly Dictionary<CacheKey, Sprite> Cache = new();

        public static void ResetForTests() => Cache.Clear();

        public static Sprite GetSprite(Vector2Int cellSize, bool leftExterior, bool rightExterior)
        {
            if (cellSize.x < 1 || cellSize.y < 1)
                return null;

            var key = new CacheKey(cellSize.x, cellSize.y, leftExterior, rightExterior);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = Bake(cellSize, leftExterior, rightExterior);
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0f, 0f),
                RoomDollhouseArt.PixelsPerUnit);
            Cache[key] = sprite;
            return sprite;
        }

        static Texture2D Bake(Vector2Int cellSize, bool leftExterior, bool rightExterior)
        {
            var w = Mathf.Max(1, Mathf.RoundToInt(cellSize.x * RoomDollhouseArt.PixelsPerUnit));
            var h = Mathf.Max(1, Mathf.RoundToInt(cellSize.y * RoomDollhouseArt.PixelsPerUnit));
            var px = new Color[w * h];
            // Transparent by default — sky shows through cutaway / window holes.
            for (var i = 0; i < px.Length; i++)
                px[i] = Color.clear;

            var floorH = Mathf.Max(2, Mathf.RoundToInt(h * FloorBandFrac));
            var ceilH = Mathf.Max(2, Mathf.RoundToInt(h * CeilingBandFrac));
            var wallW = Mathf.Max(2, Mathf.RoundToInt(w * PartyWallFrac));

            FillRect(px, w, h, 0, 0, w, floorH, Cement);
            // Darker floor edge line.
            FillRect(px, w, h, 0, floorH - 1, w, 1, CementDark);

            FillRect(px, w, h, 0, h - ceilH, w, ceilH, Cement);
            FillRect(px, w, h, 0, h - ceilH, w, 1, CementDark);

            if (!leftExterior)
                FillRect(px, w, h, 0, 0, wallW, h, Cement);
            if (!rightExterior)
                FillRect(px, w, h, w - wallW, 0, wallW, h, Cement);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"dollhouse_infra_{cellSize.x}x{cellSize.y}"
            };
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        static void FillRect(Color[] px, int w, int h, int x0, int y0, int rw, int rh, Color c)
        {
            var x1 = Mathf.Min(w, x0 + rw);
            var y1 = Mathf.Min(h, y0 + rh);
            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
                px[y * w + x] = c;
        }

        readonly struct CacheKey
        {
            readonly int W;
            readonly int H;
            readonly bool LeftExt;
            readonly bool RightExt;

            public CacheKey(int w, int h, bool leftExt, bool rightExt)
            {
                W = w;
                H = h;
                LeftExt = leftExt;
                RightExt = rightExt;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = W;
                    hash = (hash * 397) ^ H;
                    hash = (hash * 397) ^ LeftExt.GetHashCode();
                    hash = (hash * 397) ^ RightExt.GetHashCode();
                    return hash;
                }
            }

            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

            bool Equals(CacheKey other) =>
                W == other.W && H == other.H && LeftExt == other.LeftExt && RightExt == other.RightExt;
        }
    }
}
