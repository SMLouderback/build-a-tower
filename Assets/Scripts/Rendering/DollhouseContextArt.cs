using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Caches per-room dollhouse context sprites (atrium leaf + exterior face plate selection).
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
            var typeId = type == null ? null : type.id;
            var key = new CacheKey(typeId, leftExterior, rightExterior, atriumRole);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var resolved = ResolveSourceSprite(type, leftExterior, rightExterior, atriumRole, baseSprite);
            if (resolved.Sprite == null)
                return null;

            var baked = Bake(resolved.Sprite, leftExterior, rightExterior, resolved.SealMissingFace);
            Cache[key] = baked;
            return baked;
        }

        public static void ResetForTests() => Cache.Clear();

        /// <summary>
        /// Side band fraction sealed when that face is interior (shared with a neighbor).
        /// </summary>
        public const float InteriorSealFrac = 0.30f;

        readonly struct ResolvedSource
        {
            public readonly Sprite Sprite;
            public readonly bool SealMissingFace;

            public ResolvedSource(Sprite sprite, bool sealMissingFace)
            {
                Sprite = sprite;
                SealMissingFace = sealMissingFace;
            }
        }

        static ResolvedSource ResolveSourceSprite(
            RoomTypeSO type,
            bool leftExterior,
            bool rightExterior,
            AtriumStackRole atriumRole,
            Sprite baseSprite)
        {
            if (type == null)
                return new ResolvedSource(baseSprite, sealMissingFace: true);

            var cells = type.size.x > 0 && type.size.y > 0 ? type.size : new Vector2Int(1, 1);

            if (IsAtrium(type) && atriumRole == AtriumStackRole.Mid)
            {
                var faceMidPath = RoomDollhouseArt.ResourcePathForFaces(
                    type.id, leftExterior, rightExterior, atriumRole);
                var faceMid = RoomDollhouseArt.LoadSpriteAtResourcePath(faceMidPath, cells);
                if (faceMid != null)
                    return new ResolvedSource(faceMid, sealMissingFace: false);

                var midPath = RoomDollhouseArt.ResourcePathForContext(type.id, atriumRole);
                var mid = RoomDollhouseArt.LoadSpriteAtResourcePath(midPath, cells);
                if (mid != null)
                    return new ResolvedSource(mid, sealMissingFace: true);

                return new ResolvedSource(baseSprite, sealMissingFace: true);
            }

            var facePath = RoomDollhouseArt.ResourcePathForFaces(
                type.id, leftExterior, rightExterior, atriumRole);
            var faceSprite = RoomDollhouseArt.LoadSpriteAtResourcePath(facePath, cells);
            if (faceSprite != null)
                return new ResolvedSource(faceSprite, sealMissingFace: false);

            if (baseSprite != null)
                return new ResolvedSource(baseSprite, sealMissingFace: true);

            var legacyPath = RoomDollhouseArt.ResourcePathForContext(type.id, atriumRole);
            return new ResolvedSource(
                RoomDollhouseArt.LoadSpriteAtResourcePath(legacyPath, cells),
                sealMissingFace: true);
        }

        static Sprite Bake(Sprite source, bool leftExterior, bool rightExterior, bool sealMissingFace)
        {
            var sourceTex = source.texture;
            var pixels = ReadPixels(sourceTex);
            if (pixels == null || pixels.Length == 0)
                return source;

            var w = sourceTex.width;
            var h = sourceTex.height;
            if (sealMissingFace && (!leftExterior || !rightExterior))
                SealInteriorSides(pixels, w, h, leftExterior, rightExterior);

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
        /// Replace side-window glass / clear / chroma on interior faces with sampled wall paint
        /// so adjacent rooms do not keep an exterior window into the neighbor.
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
            // Sky / glass cyan already filled opaque blue.
            if (c.b > 0.55f && c.b > c.r + 0.08f && c.b >= c.g - 0.02f && c.g > 0.30f)
                return true;
            // Dark mirror panes.
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (v <= 0.36f && s <= 0.28f && c.b >= c.r - 0.06f)
                return true;
            return false;
        }

        static bool IsFurnitureLike(Color c)
        {
            if (c.a < 0.15f)
                return false;
            // Plants.
            if (c.g > c.r + 0.08f && c.g > c.b + 0.08f && c.g > 0.22f)
                return true;
            // Wood / warm furniture.
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
            readonly bool LeftExterior;
            readonly bool RightExterior;
            readonly AtriumStackRole AtriumRole;

            public CacheKey(string typeId, bool leftExterior, bool rightExterior, AtriumStackRole atriumRole)
            {
                TypeId = typeId ?? "";
                LeftExterior = leftExterior;
                RightExterior = rightExterior;
                AtriumRole = atriumRole;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TypeId.GetHashCode();
                    hash = (hash * 397) ^ LeftExterior.GetHashCode();
                    hash = (hash * 397) ^ RightExterior.GetHashCode();
                    hash = (hash * 397) ^ (int)AtriumRole;
                    return hash;
                }
            }

            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

            bool Equals(CacheKey other) =>
                TypeId == other.TypeId &&
                LeftExterior == other.LeftExterior &&
                RightExterior == other.RightExterior &&
                AtriumRole == other.AtriumRole;
        }
    }
}
