using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Loads square build-menu icons from Resources/Art/Menu/{id}.
    /// Prefers raw PNG bytes (TextAsset) decoded via LoadImage; falls back to Texture2D.
    /// Magenta plates are keyed to alpha and content is cropped tight so IMGUI can fill the button.
    /// </summary>
    public static class MenuIconArt
    {
        public const string ResourcesRoot = "Art/Menu/";

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
                    filterMode = FilterMode.Bilinear,
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

            var processed = KeyAndCrop(source, id);
            if (ownedSource)
                DestroyDecoded(source);

            if (processed == null)
                return null;

            _ownedIds.Add(id);
            return processed;
        }

        static Texture2D KeyAndCrop(Texture2D source, string id)
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
            KeyHotMagenta(px);

            FindOpaqueBounds(px, w, h, out var minX, out var minY, out var maxX, out var maxY);
            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);

            // Already nearly full-bleed — keep keyed full texture.
            if (cw >= w * 0.92f && ch >= h * 0.92f && minX <= 2 && minY <= 2)
            {
                var full = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = id
                };
                full.SetPixels(px);
                full.Apply(false, false);
                return full;
            }

            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            // Second pass: fringe after crop.
            KeyHotMagenta(cropped);

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = id
            };
            tex.SetPixels(cropped);
            tex.Apply(false, false);
            return tex;
        }

        static void KeyHotMagenta(Color[] px)
        {
            for (var i = 0; i < px.Length; i++)
            {
                if (IsHotMagenta(px[i]))
                    px[i] = Color.clear;
            }
        }

        static bool IsHotMagenta(Color c)
        {
            if (c.a < 0.08f) return true;
            return c.r > 0.70f && c.b > 0.55f && c.g < 0.28f &&
                   (c.r - c.g) > 0.35f && (c.b - c.g) > 0.28f;
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
