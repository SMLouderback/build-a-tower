using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Whole-room dollhouse interiors. Native PNG pixels are kept as-is; callers
    /// scale a SpriteRenderer to the room's cell footprint (1 cell = 1 world unit).
    /// </summary>
    public static class RoomDollhouseArt
    {
        public const string ResourceRoot = "Art/Dollhouse/";
        public const float PixelsPerUnit = 128f;
        /// <summary>Below rooms tilemap transit (15) and stairs overlay (20).</summary>
        public const int SortingOrder = 5;

        /// <summary>Test seam: replaces Resources-backed PNG decode when set.</summary>
        public static Func<string, Sprite> LoadSpriteForTests;

        static readonly Dictionary<string, string> LeafByTypeId = new()
        {
            ["office_micro"] = "micro_office_3x1",
            ["office_studio"] = "studio_office_4x1",
            ["office_base"] = "small_office_6x1",
            ["office_mid_standard"] = "mid_office_9x1",
            ["office_mid_clinic"] = "professional_suite_10x1",
            ["office_mid_team"] = "team_bay_12x1",
            ["office_upper_standard"] = "upper_office_12x1",
            ["office_upper_corner"] = "corner_suite_14x1",
            ["office_upper_floor"] = "corporate_18x1",
            ["hotel_base"] = "base_hotel_3x1",
            ["hotel_accessible"] = "accessible_hotel_3x1",
            ["hotel_mid_standard"] = "mid_standard_hotel_4x1",
            ["hotel_mid_extended"] = "mid_extended_hotel_6x1",
            ["hotel_studio"] = "studio_hotel_5x1",
            ["hotel_junior_suite"] = "junior_suite_5x1",
            ["hotel_upper_standard"] = "upper_standard_hotel_5x1",
            ["hotel_upper_king"] = "upper_king_hotel_5x1",
            ["hotel_upper_suite"] = "upper_suite_8x1",
            ["condo_studio"] = "studio_condo_4x1",
            ["condo_alcove"] = "alcove_studio_5x1",
            ["condo_base"] = "one_bedroom_condo_8x1",
            ["condo_mid_standard"] = "mid_condo_10x1",
            ["condo_mid_loft"] = "loft_condo_12x1",
            ["condo_mid_family"] = "family_condo_14x1",
            ["condo_upper_standard"] = "upper_condo_12x1",
            ["condo_upper_corner"] = "corner_condo_14x1",
            ["condo_upper_penthouse"] = "penthouse_18x1",
            ["shop_food_fast"] = "fast_food_16x1",
            ["shop_food_restaurant"] = "restaurant_16x1",
            ["shop_food_fine"] = "fine_dining_4x1",
            ["shop_retail"] = "retail_16x1",
            ["service_housekeeping"] = "housekeeping_3x1",
            ["service_maintenance"] = "maintenance_3x1",
            ["service_security"] = "security_post_2x1",
            ["service_research"] = "research_lab_4x1",
            ["service_conference"] = "conference_8x1",
            ["service_event_hall"] = "event_hall_12x2",
            ["parking_underground"] = "underground_parking_6x1",
            ["service_valet"] = "valet_3x1",
        };

        static readonly Dictionary<string, Sprite> SpriteCache = new();

        public static string ResourceLeaf(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return null;
            return LeafByTypeId.TryGetValue(typeId, out var leaf) ? leaf : null;
        }

        public static string ResourcePath(string typeId)
        {
            var leaf = ResourceLeaf(typeId);
            return leaf == null ? null : ResourceRoot + leaf;
        }

        public static bool IsMapped(RoomTypeSO type)
        {
            if (type == null || type.isStairs || type.isElevatorShaft || type.isLobby ||
                type.isParkingRamp)
                return false;
            return ResourceLeaf(type.id) != null;
        }

        public static Vector3 OverlayScale(Vector2 spriteWorldSize, Vector2Int cellSize)
        {
            var sx = spriteWorldSize.x > 0.01f ? cellSize.x / spriteWorldSize.x : 1f;
            var sy = spriteWorldSize.y > 0.01f ? cellSize.y / spriteWorldSize.y : 1f;
            return new Vector3(sx, sy, 1f);
        }

        public static bool TrySprite(RoomInstance room, out Sprite sprite)
        {
            sprite = null;
            if (room?.Type == null || !IsMapped(room.Type)) return false;

            var path = ResourcePath(room.Type.id);
            if (string.IsNullOrEmpty(path)) return false;

            sprite = GetOrLoadSprite(path);
            return sprite != null;
        }

        public static void ResetForTests()
        {
            LoadSpriteForTests = null;
            SpriteCache.Clear();
        }

        static Sprite GetOrLoadSprite(string resourcePath)
        {
            if (LoadSpriteForTests != null)
                return LoadSpriteForTests(resourcePath);

            if (SpriteCache.TryGetValue(resourcePath, out var cached) && cached != null)
                return cached;

            var loaded = LoadSprite(resourcePath);
            if (loaded != null)
                SpriteCache[resourcePath] = loaded;
            return loaded;
        }

        static Sprite LoadSprite(string resourcePath)
        {
            var tex = LoadTexture(resourcePath);
            if (tex == null) return null;

            // Bottom-left pivot: overlay pins to the room origin and scales to the
            // cell footprint. Native pixel size is not resampled to catalog 128×N.
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0f, 0f),
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
                    // Imported texture may already be readable enough to sprite.
                    return SpriteTextureFromImported(srcTex);
                }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = resourcePath
            };
            if (!decoded.LoadImage(png, false))
            {
                UnityEngine.Object.Destroy(decoded);
                return null;
            }

            return decoded;
        }

        static Texture2D SpriteTextureFromImported(Texture2D srcTex)
        {
            if (srcTex == null) return null;
            try
            {
                var copy = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
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
