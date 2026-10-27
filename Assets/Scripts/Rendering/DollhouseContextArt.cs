using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Caches per-room dollhouse sprites. Uses a single plate per room type
    /// (no exterior-face switching / side sealing).
    /// </summary>
    public static class DollhouseContextArt
    {
        static readonly Dictionary<CacheKey, Sprite> Cache = new();

        public static Sprite GetSprite(
            RoomTypeSO type,
            bool leftExterior,
            bool rightExterior,
            AtriumStackRole atriumRole,
            BuildFamily family,
            Sprite baseSprite = null)
        {
            _ = family;
            _ = leftExterior;
            _ = rightExterior;
            var typeId = type == null ? null : type.id;
            var key = new CacheKey(typeId, atriumRole);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var source = ResolveSourceSprite(type, atriumRole, baseSprite);
            if (source == null)
                return null;

            var baked = Bake(source);
            Cache[key] = baked;
            return baked;
        }

        public static void ResetForTests() => Cache.Clear();

        /// <summary>Kept for tests / tools; runtime bake no longer seals sides.</summary>
        public const float InteriorSealFrac = 0.30f;

        static Sprite ResolveSourceSprite(
            RoomTypeSO type,
            AtriumStackRole atriumRole,
            Sprite baseSprite)
        {
            if (type == null)
                return baseSprite;

            var cells = type.size.x > 0 && type.size.y > 0 ? type.size : new Vector2Int(1, 1);

            if (IsAtrium(type) && atriumRole == AtriumStackRole.Mid)
            {
                var midPath = RoomDollhouseArt.ResourcePathForContext(type.id, atriumRole);
                var mid = RoomDollhouseArt.LoadSpriteAtResourcePath(midPath, cells);
                if (mid != null)
                    return mid;
                return baseSprite;
            }

            if (baseSprite != null)
                return baseSprite;

            var path = RoomDollhouseArt.ResourcePathForContext(type.id, atriumRole);
            return RoomDollhouseArt.LoadSpriteAtResourcePath(path, cells);
        }

        static Sprite Bake(Sprite source)
        {
            var sourceTex = source.texture;
            var pixels = ReadPixels(sourceTex);
            if (pixels == null || pixels.Length == 0)
                return source;

            var w = sourceTex.width;
            var h = sourceTex.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = sourceTex.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                name = sourceTex.name + "_context"
            };
            tex.SetPixels(pixels);
            tex.Apply(false, false);

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, w, h),
                source.pivot / source.rect.size,
                RoomDollhouseArt.PixelsPerUnit);
        }

        /// <summary>
        /// Legacy helper retained for EditMode coverage of plaster sampling.
        /// Not used by <see cref="Bake"/>.
        /// </summary>
        public static void SealInteriorSides(
            Color[] px,
            int w,
            int h,
            bool leftExterior,
            bool rightExterior)
        {
            if (px == null || w < 4 || h < 4)
                return;

            var wall = SampleWallColor(px, w, h);
            var y0 = Mathf.Max(0, Mathf.RoundToInt(h * 0.10f));
            var y1 = Mathf.Min(h, Mathf.RoundToInt(h * 0.85f));
            var band = Mathf.Max(2, Mathf.RoundToInt(w * InteriorSealFrac));

            if (!leftExterior)
                SealBand(px, w, h, 0, band, y0, y1, wall);
            if (!rightExterior)
                SealBand(px, w, h, w - band, w, y0, y1, wall);
        }

        static void SealBand(
            Color[] px,
            int w,
            int h,
            int x0,
            int x1,
            int y0,
            int y1,
            Color wall)
        {
            x0 = Mathf.Clamp(x0, 0, w);
            x1 = Mathf.Clamp(x1, 0, w);
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var i = y * w + x;
                var c = px[i];
                if (IsFurnitureLike(c))
                    continue;
                if (IsSealableGlass(c))
                    px[i] = wall;
            }
        }

        static Color SampleWallColor(Color[] px, int w, int h)
        {
            var x0 = w / 3;
            var x1 = (2 * w) / 3;
            var y0 = h / 4;
            var y1 = (2 * h) / 3;
            var r = 0f;
            var g = 0f;
            var b = 0f;
            var n = 0;
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var c = px[y * w + x];
                if (c.a < 0.85f)
                    continue;
                if (RoomDollhouseArt.IsDollhouseChromaKey(c))
                    continue;
                if (IsFurnitureLike(c))
                    continue;
                Color.RGBToHSV(c, out _, out var s, out var v);
                if (s > 0.45f || v < 0.25f || v > 0.92f)
                    continue;
                r += c.r;
                g += c.g;
                b += c.b;
                n++;
            }

            if (n == 0)
                return new Color(0.49f, 0.54f, 0.60f, 1f);
            return new Color(r / n, g / n, b / n, 1f);
        }

        static bool IsSealableGlass(Color c)
        {
            if (c.a < 0.15f)
                return true;
            if (RoomDollhouseArt.IsDollhouseChromaKey(c))
                return true;
            if (c.b > 0.55f && c.b > c.r + 0.08f && c.b >= c.g - 0.02f && c.g > 0.30f)
                return true;
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (v <= 0.36f && s <= 0.28f && c.b >= c.r - 0.06f)
                return true;
            return false;
        }

        static bool IsFurnitureLike(Color c)
        {
            if (c.a < 0.15f)
                return false;
            if (c.g > c.r + 0.08f && c.g > c.b + 0.08f && c.g > 0.22f)
                return true;
            if (c.r > c.g + 0.08f && c.r > c.b + 0.08f && c.r > 0.22f && c.g > 0.10f)
                return true;
            return false;
        }

        static Color[] ReadPixels(Texture2D tex)
        {
            try
            {
                return tex.GetPixels();
            }
            catch (UnityException)
            {
                return ReadPixelsViaRenderTexture(tex);
            }
        }

        static Color[] ReadPixelsViaRenderTexture(Texture2D tex)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
                copy.Apply(false, false);
                var pixels = copy.GetPixels();
                DestroyObject(copy);
                return pixels;
            }
            catch (UnityException)
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static bool IsAtrium(RoomTypeSO type)
        {
            if (type == null)
                return false;
            return type.isAtrium || type.id == "leisure_atrium";
        }

        static void DestroyObject(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        readonly struct CacheKey
        {
            readonly string TypeId;
            readonly AtriumStackRole AtriumRole;

            public CacheKey(string typeId, AtriumStackRole atriumRole)
            {
                TypeId = typeId ?? "";
                AtriumRole = atriumRole;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TypeId.GetHashCode() * 397) ^ (int)AtriumRole;
                }
            }

            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

            bool Equals(CacheKey other) =>
                TypeId == other.TypeId && AtriumRole == other.AtriumRole;
        }
    }
}
