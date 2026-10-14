using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Caches per-room dollhouse context sprites (atrium leaf selection; face plates in later phases).
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

            var source = ResolveSourceSprite(type, atriumRole, baseSprite);
            if (source == null)
                return null;

            var baked = Bake(source);
            Cache[key] = baked;
            return baked;
        }

        public static void ResetForTests() => Cache.Clear();

        static Sprite ResolveSourceSprite(RoomTypeSO type, AtriumStackRole atriumRole, Sprite baseSprite)
        {
            if (IsAtrium(type) && atriumRole == AtriumStackRole.Mid)
            {
                var mid = LoadSprite(RoomDollhouseArt.ResourcePathForContext(type.id, atriumRole));
                if (mid != null)
                    return mid;
            }

            if (baseSprite != null)
                return baseSprite;

            if (type == null)
                return null;

            var room = new RoomInstance(0, type, Vector2Int.zero, type.size);
            return RoomDollhouseArt.TrySprite(room, out var sprite) ? sprite : null;
        }

        static Sprite LoadSprite(string resourcePath)
        {
            if (RoomDollhouseArt.LoadSpriteForTests != null)
                return RoomDollhouseArt.LoadSpriteForTests(resourcePath);

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
                return null;

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                Vector2.zero,
                RoomDollhouseArt.PixelsPerUnit);
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
