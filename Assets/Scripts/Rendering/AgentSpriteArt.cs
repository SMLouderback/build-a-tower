using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public enum AgentDressTier
    {
        Basic,
        Mid,
        Upper
    }

    /// <summary>
    /// Loads horizontal walk strips from Resources and slices 4-frame cycles.
    /// </summary>
    public static class AgentSpriteArt
    {
        public const string ResourceRoot = "Art/Agents/";
        public const int WalkFrameCount = 4;
        /// <summary>Passing/neutral pose in the 4-frame walk strip.</summary>
        public const int IdleFrameIndex = 1;
        public const float PixelsPerUnit = 128f;
        public const float TargetHeightCells = 0.95f;
        public const float DefaultFootLift = 61f / PixelsPerUnit;
        /// <summary>Temporary tuning sink while aligning feet to dollhouse floors.</summary>
        public const float ExtraFootSinkPixels = 20f;
        public static float ExtraFootSinkWorld => ExtraFootSinkPixels / PixelsPerUnit;

        public static Func<string, Sprite> LoadSpriteForTests;

        static readonly Dictionary<string, Sprite> SheetCache = new();
        static readonly Dictionary<(string key, int frame), Sprite> FrameCache = new();
        static readonly Dictionary<int, float> FootLiftCache = new();

        public static AgentDressTier DressTierFromWealth(WealthBand wealth) => wealth switch
        {
            WealthBand.Street or WealthBand.Basic => AgentDressTier.Basic,
            WealthBand.Mid => AgentDressTier.Mid,
            WealthBand.Upper or WealthBand.Premium => AgentDressTier.Upper,
            _ => AgentDressTier.Basic
        };

        public static string ResolveSheetKey(AgentRole role, AgentGender gender, WealthBand wealth)
        {
            var genderSlug = gender == AgentGender.Female ? "female" : "male";
            if (role == AgentRole.Criminal)
                return $"criminal_{genderSlug}";

            if (!TryRoleSlug(role, out var roleSlug))
                return null;

            if (role is AgentRole.Maid or AgentRole.Handyman or AgentRole.Security)
                return $"{roleSlug}_{genderSlug}_uniform";

            var tier = DressTierSlug(DressTierFromWealth(wealth));
            return $"{roleSlug}_{genderSlug}_{tier}";
        }

        public static string ResourcePath(string sheetKey) =>
            string.IsNullOrEmpty(sheetKey) ? null : ResourceRoot + sheetKey;

        public static Sprite GetSheet(string sheetKey)
        {
            if (string.IsNullOrEmpty(sheetKey)) return null;

            var path = ResourcePath(sheetKey);
            if (string.IsNullOrEmpty(path)) return null;

            if (SheetCache.TryGetValue(path, out var cached) && cached != null)
                return cached;

            var loaded = LoadStripSprite(path);
            if (loaded != null)
                SheetCache[path] = loaded;
            return loaded;
        }

        public static Sprite GetWalkFrame(string sheetKey, int frameIndex)
        {
            if (string.IsNullOrEmpty(sheetKey)) return null;

            var frame = ((frameIndex % WalkFrameCount) + WalkFrameCount) % WalkFrameCount;
            var cacheKey = (sheetKey, frame);
            if (FrameCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var sheet = GetSheet(sheetKey);
            if (sheet == null) return null;

            var sliced = SliceWalkFrame(sheet, frame);
            if (sliced != null)
            {
                FrameCache[cacheKey] = sliced;
                FootLiftCache[sliced.GetInstanceID()] = ComputeFootLiftFromPixels(sliced);
            }
            return sliced;
        }

        public static float ScaleForTargetHeight(Sprite frameSprite)
        {
            if (frameSprite == null) return 1f;
            var bounds = frameSprite.bounds;
            var height = bounds.max.y - bounds.min.y;
            if (height <= 0.001f)
                height = bounds.size.y;
            if (height <= 0.001f) return 1f;
            return TargetHeightCells / height;
        }

        /// <summary>World-space distance from pivot (bottom-center) up to the lowest visible pixel row.</summary>
        public static float FootLiftFromPivot(Sprite frameSprite)
        {
            if (frameSprite == null) return 0f;

            var key = frameSprite.GetInstanceID();
            if (FootLiftCache.TryGetValue(key, out var cached))
                return cached;

            var lift = frameSprite.bounds.min.y;
            if (lift <= 0.001f)
                lift = ComputeFootLiftFromPixels(frameSprite);
            if (lift <= 0.001f)
                lift = DefaultFootLift;
            FootLiftCache[key] = lift;
            return lift;
        }

        public static void ResetForTests()
        {
            LoadSpriteForTests = null;
            SheetCache.Clear();
            FrameCache.Clear();
            FootLiftCache.Clear();
        }

        static bool TryRoleSlug(AgentRole role, out string slug)
        {
            slug = role switch
            {
                AgentRole.OfficeWorker => "office_worker",
                AgentRole.HotelGuest => "hotel_guest",
                AgentRole.CondoResident => "condo_resident",
                AgentRole.StreetVisitor => "street_visitor",
                AgentRole.EventVisitor => "event_visitor",
                AgentRole.Maid => "maid",
                AgentRole.Handyman => "handyman",
                AgentRole.Security => "security",
                _ => null
            };
            return slug != null;
        }

        static string DressTierSlug(AgentDressTier tier) => tier switch
        {
            AgentDressTier.Mid => "mid",
            AgentDressTier.Upper => "upper",
            _ => "basic"
        };

        static Sprite LoadStripSprite(string resourcePath)
        {
            if (LoadSpriteForTests != null)
                return LoadSpriteForTests(resourcePath);

            var tex = LoadTexture(resourcePath);
            if (tex == null) return null;

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0f, 0f),
                PixelsPerUnit);
        }

        static Sprite SliceWalkFrame(Sprite sheet, int frameIndex)
        {
            if (sheet?.texture == null) return null;

            var frameWidth = sheet.texture.width / WalkFrameCount;
            if (frameWidth <= 0) return null;

            var rect = new Rect(frameIndex * frameWidth, 0f, frameWidth, sheet.texture.height);
            return Sprite.Create(
                sheet.texture,
                rect,
                new Vector2(0.5f, 0f),
                PixelsPerUnit);
        }

        static Texture2D LoadTexture(string resourcePath)
        {
            var bytesAsset = Resources.Load<TextAsset>(resourcePath);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png == null || png.Length < 32)
            {
                var srcTex = Resources.Load<Texture2D>(resourcePath);
                if (srcTex == null) return null;
                try
                {
                    png = srcTex.EncodeToPNG();
                }
                catch (UnityException)
                {
                    return CopyTexture(srcTex);
                }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = resourcePath
            };
            if (!decoded.LoadImage(png, false))
            {
                UnityEngine.Object.Destroy(decoded);
                return null;
            }

            KeyFlatBackground(decoded);
            return decoded;
        }

        static void KeyFlatBackground(Texture2D tex, byte threshold = 240)
        {
            if (tex == null) return;

            var pixels = tex.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 0) continue;

                if (p.r >= threshold && p.g >= threshold && p.b >= threshold)
                {
                    p.a = 0;
                    pixels[i] = p;
                    continue;
                }

                var maxDiff = Mathf.Max(Mathf.Abs(p.r - p.g), Mathf.Abs(p.g - p.b));
                if (maxDiff <= 12 && p.r >= 175 && p.g >= 175 && p.b >= 175)
                {
                    p.a = 0;
                    pixels[i] = p;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }

        static float ComputeFootLiftFromPixels(Sprite frameSprite)
        {
            var tex = frameSprite.texture;
            if (tex == null || !tex.isReadable) return 0f;

            var rect = frameSprite.textureRect;
            var x0 = Mathf.FloorToInt(rect.xMin);
            var x1 = Mathf.FloorToInt(rect.xMax);
            var y0 = Mathf.FloorToInt(rect.yMin);
            var y1 = Mathf.FloorToInt(rect.yMax);

            var footRow = y0;
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    if (tex.GetPixel(x, y).a <= 32) continue;
                    footRow = y;
                    goto foundFoot;
                }
            }

            foundFoot:
            var liftPixels = footRow - y0;
            return liftPixels / frameSprite.pixelsPerUnit;
        }

        static Texture2D CopyTexture(Texture2D srcTex)
        {
            if (srcTex == null) return null;
            try
            {
                var copy = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = srcTex.name
                };
                copy.SetPixels(srcTex.GetPixels());
                copy.Apply();
                return copy;
            }
            catch (UnityException)
            {
                return srcTex;
            }
        }
    }
}
