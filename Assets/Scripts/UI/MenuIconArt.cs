using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Loads square build-menu icons from Resources/Art/Menu/{id}.
    /// Keys hot magenta/pink plates, crops to content, then cover-scales into a
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

            // Flood-fill key from the border so soft pink plates and fringe vanish.
            FloodKeyMagentaPlate(px, w, h);
            KeyHotMagenta(px);

            FindOpaqueBounds(px, w, h, out var minX, out var minY, out var maxX, out var maxY);
            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);

            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            KeyHotMagenta(cropped);

            // Cover-scale into a fixed square — subject fills the button, may clip edges.
            var covered = CoverIntoSquare(cropped, cw, ch, OutputPixels);
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

        static Color[] CoverIntoSquare(Color[] src, int sw, int sh, int size)
        {
            var dest = new Color[size * size];
            var scale = Mathf.Max(size / (float)sw, size / (float)sh);
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
        /// AI menu plates are often hot pink (~R237 G10 B126), not pure #FF00FF.
        /// </summary>
        public static bool IsHotMagenta(Color c)
        {
            if (c.a < 0.08f) return true;
            if (c.g > 0.32f) return false;
            // Require red-dominant chroma toward magenta/pink.
            if (c.r < 0.55f) return false;
            if (c.b < 0.32f) return false;
            if ((c.r - c.g) < 0.28f) return false;
            if ((c.b - c.g) < 0.18f) return false;
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
