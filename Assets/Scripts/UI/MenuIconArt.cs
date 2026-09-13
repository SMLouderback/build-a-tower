using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Loads square build-menu icons from Resources/Art/Menu/{id}.
    /// Prefers raw PNG bytes (TextAsset) decoded via LoadImage; falls back to Texture2D.
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

            var bytesAsset = Resources.Load<TextAsset>(path);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png == null || png.Length < 32)
            {
                var resourceTex = Resources.Load<Texture2D>(path);
                return resourceTex;
            }

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = id
            };

            if (!decoded.LoadImage(png, false))
            {
                DestroyDecoded(decoded);
                return null;
            }

            _ownedIds.Add(id);
            return decoded;
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
