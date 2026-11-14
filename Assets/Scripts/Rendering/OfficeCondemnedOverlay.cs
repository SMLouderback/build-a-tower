using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Procedural caution-tape overlay for broken office rooms.
    /// </summary>
    public static class OfficeCondemnedOverlay
    {
        public const int SortingOrder = 22;
        const int PixelsPerCell = 32;
        const float WashLerp = 0.65f;
        static readonly Color GreyWash = new(0.38f, 0.38f, 0.4f, 1f);

        static readonly Dictionary<(int w, int h), Sprite> SpriteCache = new();

        public static Color BrokenTileTint => Color.Lerp(Color.white, GreyWash, WashLerp);

        public static (int width, int height) PixelSize(int widthCells, int heightCells) =>
            (Mathf.Max(32, widthCells * PixelsPerCell), Mathf.Max(24, heightCells * PixelsPerCell));

        public static Sprite GetSprite(int widthCells, int heightCells)
        {
            var key = (Mathf.Max(1, widthCells), Mathf.Max(1, heightCells));
            if (SpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var sprite = BuildSprite(key.Item1, key.Item2);
            SpriteCache[key] = sprite;
            return sprite;
        }

        static Sprite BuildSprite(int widthCells, int heightCells)
        {
            var (w, h) = PixelSize(widthCells, heightCells);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Condemned_{widthCells}x{heightCells}"
            };

            var yellow = new Color(0.98f, 0.82f, 0.08f, 0.82f);
            var black = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            const int stripe = 14;

            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var diag = (x + y) / stripe;
                tex.SetPixel(x, y, (diag & 1) == 0 ? yellow : black);
            }

            DrawLabel(tex, w, h);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                new Vector2(0.5f, 0.5f),
                PixelsPerCell);
        }

        static void DrawLabel(Texture2D tex, int w, int h)
        {
            const string label = "CONDEMNED";
            const int charW = 5;
            const int charH = 7;
            const int gap = 1;
            var textW = label.Length * charW + (label.Length - 1) * gap;
            var scale = Mathf.Clamp(Mathf.Min(w / (float)(textW + 8), h / (float)(charH + 10)), 1f, 3f);
            var drawW = Mathf.RoundToInt(textW * scale);
            var drawH = Mathf.RoundToInt(charH * scale);
            var startX = (w - drawW) / 2;
            var startY = (h - drawH) / 2;

            var plate = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            FillRect(tex, startX - 4, startY - 3, drawW + 8, drawH + 6, plate);

            var ink = new Color(0.98f, 0.2f, 0.15f, 1f);
            var cursor = startX;
            foreach (var ch in label)
            {
                DrawGlyph(tex, ch, cursor, startY, scale, ink);
                cursor += Mathf.RoundToInt((charW + gap) * scale);
            }
        }

        static void DrawGlyph(Texture2D tex, char ch, int x, int y, float scale, Color color)
        {
            if (!TryGlyph(ch, out var rows)) return;
            var px = Mathf.Max(1, Mathf.RoundToInt(scale));
            for (var row = 0; row < rows.Length; row++)
            {
                var bits = rows[row];
                for (var col = 0; col < 5; col++)
                {
                    if ((bits & (1 << (4 - col))) == 0) continue;
                    FillRect(
                        tex,
                        x + Mathf.RoundToInt(col * scale),
                        y + Mathf.RoundToInt((6 - row) * scale),
                        px,
                        px,
                        color);
                }
            }
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            var maxX = tex.width;
            var maxY = tex.height;
            for (var py = y; py < y + h; py++)
            {
                if (py < 0 || py >= maxY) continue;
                for (var px = x; px < x + w; px++)
                {
                    if (px < 0 || px >= maxX) continue;
                    tex.SetPixel(px, py, color);
                }
            }
        }

        static bool TryGlyph(char ch, out byte[] rows)
        {
            rows = null;
            switch (char.ToUpperInvariant(ch))
            {
                case 'C':
                    rows = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
                    return true;
                case 'O':
                    rows = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
                    return true;
                case 'N':
                    rows = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 };
                    return true;
                case 'D':
                    rows = new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E };
                    return true;
                case 'E':
                    rows = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
                    return true;
                case 'M':
                    rows = new byte[] { 0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11 };
                    return true;
                default:
                    return false;
            }
        }
    }
}
