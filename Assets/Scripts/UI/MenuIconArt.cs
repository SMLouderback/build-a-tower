using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Loads square build-menu icons from Resources/Art/Menu/{id}.
    /// Keys hot magenta/pink plates, trims sparse margins, then cover-scales into a
    /// fixed square so IMGUI StretchToFill shows a large subject with no letterbox.
    /// </summary>
    public static class MenuIconArt
    {
        public const string ResourcesRoot = "Art/Menu/";
        public const int OutputPixels = 128;

        static readonly Dictionary<string, Texture2D> _cache = new();
        static readonly HashSet<string> _ownedIds = new();

        public static bool TryGetTexture(string id, out Texture2D tex)
        {
            tex = null;
            if (string.IsNullOrEmpty(id))
                return false;

            if (_cache.TryGetValue(id, out tex))
                return tex != null;

            tex = LoadTexture(id);
            _cache[id] = tex;
            return tex != null;
        }

        public static void ResetCache()
        {
            foreach (var id in _ownedIds)
            {
                if (_cache.TryGetValue(id, out var owned) && owned != null)
                    DestroyDecoded(owned);
            }

            _cache.Clear();
            _ownedIds.Clear();
        }

        static Texture2D LoadTexture(string id)
        {
            var path = ResourcesRoot + id;
            Texture2D source = null;
            var ownedSource = false;

            var bytesAsset = Resources.Load<TextAsset>(path);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png != null && png.Length >= 32)
            {
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = id + "_src"
                };
                if (!source.LoadImage(png, false))
                {
                    DestroyDecoded(source);
                    return null;
                }

                ownedSource = true;
            }
            else
            {
                source = Resources.Load<Texture2D>(path);
                if (source == null)
                    return null;
            }

            var processed = KeyCropAndCover(source, id);
            if (ownedSource)
                DestroyDecoded(source);

            if (processed == null)
                return null;

            _ownedIds.Add(id);
            return processed;
        }

        static Texture2D KeyCropAndCover(Texture2D source, string id)
        {
            Color[] px;
            try
            {
                px = source.GetPixels();
            }
            catch (UnityException)
            {
                return source;
            }

            var w = source.width;
            var h = source.height;

            FloodKeyMagentaPlate(px, w, h);
            KeyHotMagenta(px);
            ScrubNearMagentaFringe(px, w, h);

            FindOpaqueBounds(px, w, h, out var minX, out var minY, out var maxX, out var maxY);
            TrimSparseMargins(px, w, h, ref minX, ref minY, ref maxX, ref maxY);

            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);

            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            KeyHotMagenta(cropped);
            ScrubNearMagentaFringe(cropped, cw, ch);

            // Slight overscan so transparent AABB padding never letterboxes the button.
            var covered = CoverIntoSquare(cropped, cw, ch, OutputPixels, overscan: 1.12f);
            KeyHotMagenta(covered);

            var tex = new Texture2D(OutputPixels, OutputPixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = id
            };
            tex.SetPixels(covered);
            tex.Apply(false, false);
            return tex;
        }

        static Color[] CoverIntoSquare(Color[] src, int sw, int sh, int size, float overscan)
        {
            var dest = new Color[size * size];
            var scale = Mathf.Max(size / (float)sw, size / (float)sh) * Mathf.Max(1f, overscan);
            var dw = Mathf.Max(1, Mathf.RoundToInt(sw * scale));
            var dh = Mathf.Max(1, Mathf.RoundToInt(sh * scale));
            var ox = (dw - size) / 2;
            var oy = (dh - size) / 2;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var sx = Mathf.Clamp((int)((x + ox + 0.5f) / dw * sw), 0, sw - 1);
                var sy = Mathf.Clamp((int)((y + oy + 0.5f) / dh * sh), 0, sh - 1);
                dest[y * size + x] = src[sy * sw + sx];
            }

            return dest;
        }

        /// <summary>
        /// Shrink AABB while edge rows/cols are mostly empty so floating subjects zoom up.
        /// </summary>
        static void TrimSparseMargins(
            Color[] px, int w, int h,
            ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            const float minFill = 0.04f;
            var guard = 0;
            while (guard++ < 512 && minY < maxY)
            {
                if (RowOpaqueFraction(px, w, minX, maxX, minY) >= minFill) break;
                minY++;
            }

            guard = 0;
            while (guard++ < 512 && minY < maxY)
            {
                if (RowOpaqueFraction(px, w, minX, maxX, maxY) >= minFill) break;
                maxY--;
            }

            guard = 0;
            while (guard++ < 512 && minX < maxX)
            {
                if (ColOpaqueFraction(px, w, minY, maxY, minX) >= minFill) break;
                minX++;
            }

            guard = 0;
            while (guard++ < 512 && minX < maxX)
            {
                if (ColOpaqueFraction(px, w, minY, maxY, maxX) >= minFill) break;
                maxX--;
            }
        }

        static float RowOpaqueFraction(Color[] px, int w, int minX, int maxX, int y)
        {
            var span = Mathf.Max(1, maxX - minX + 1);
            var count = 0;
            for (var x = minX; x <= maxX; x++)
            {
                if (px[y * w + x].a >= 0.08f) count++;
            }

            return count / (float)span;
        }

        static float ColOpaqueFraction(Color[] px, int w, int minY, int maxY, int x)
        {
            var span = Mathf.Max(1, maxY - minY + 1);
            var count = 0;
            for (var y = minY; y <= maxY; y++)
            {
                if (px[y * w + x].a >= 0.08f) count++;
            }

            return count / (float)span;
        }

        static void FloodKeyMagentaPlate(Color[] px, int w, int h)
        {
            var visit = new bool[w * h];
            var q = new Queue<int>();

            void TryEnq(int x, int y)
            {
                if ((uint)x >= w || (uint)y >= h) return;
                var i = y * w + x;
                if (visit[i] || !IsHotMagenta(px[i])) return;
                visit[i] = true;
                q.Enqueue(i);
            }

            for (var x = 0; x < w; x++)
            {
                TryEnq(x, 0);
                TryEnq(x, h - 1);
            }
            for (var y = 0; y < h; y++)
            {
                TryEnq(0, y);
                TryEnq(w - 1, y);
            }

            while (q.Count > 0)
            {
                var i = q.Dequeue();
                px[i] = Color.clear;
                var x = i % w;
                var y = i / w;
                TryEnq(x + 1, y);
                TryEnq(x - 1, y);
                TryEnq(x, y + 1);
                TryEnq(x, y - 1);
            }
        }

        static void KeyHotMagenta(Color[] px)
        {
            for (var i = 0; i < px.Length; i++)
            {
                if (IsHotMagenta(px[i]))
                    px[i] = Color.clear;
            }
        }

        /// <summary>
        /// Clear soft pink fringe pixels that sit next to already-cleared plate.
        /// </summary>
        static void ScrubNearMagentaFringe(Color[] px, int w, int h)
        {
            var copy = (Color[])px.Clone();
            for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                var i = y * w + x;
                if (copy[i].a < 0.08f) continue;
                if (!IsNearMagentaFringe(copy[i])) continue;

                var clearN =
                    copy[i - 1].a < 0.08f ||
                    copy[i + 1].a < 0.08f ||
                    copy[i - w].a < 0.08f ||
                    copy[i + w].a < 0.08f;
                if (clearN)
                    px[i] = Color.clear;
            }
        }

        static bool IsNearMagentaFringe(Color c)
        {
            if (c.a < 0.08f) return true;
            if (c.g > 0.40f) return false;
            if (c.r < 0.45f) return false;
            // Soft pink / magenta bleed (looser than plate key).
            return (c.r - c.g) > 0.18f && c.b > 0.22f && (c.b - c.g) > 0.08f;
        }

        /// <summary>
        /// AI menu plates are often hot pink (~R237 G10 B126), not pure #FF00FF.
        /// </summary>
        public static bool IsHotMagenta(Color c)
        {
            if (c.a < 0.08f) return true;
            if (c.g > 0.34f) return false;
            if (c.r < 0.50f) return false;
            if (c.b < 0.28f) return false;
            if ((c.r - c.g) < 0.24f) return false;
            if ((c.b - c.g) < 0.14f) return false;
            return true;
        }

        static void FindOpaqueBounds(
            Color[] px, int w, int h,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = w;
            minY = h;
            maxX = -1;
            maxY = -1;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (px[y * w + x].a < 0.08f) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX)
            {
                minX = 0;
                minY = 0;
                maxX = w - 1;
                maxY = h - 1;
            }
        }

        static void DestroyDecoded(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
