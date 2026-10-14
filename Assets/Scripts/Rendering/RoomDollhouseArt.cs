using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Whole-room dollhouse interiors. Magenta/pink chroma plates are keyed at load;
    /// callers scale a SpriteRenderer to the room's cell footprint (1 cell = 1 world unit).
    /// </summary>
    public static class RoomDollhouseArt
    {
        public const string ResourceRoot = "Art/Dollhouse/";
        public const float PixelsPerUnit = 128f;
        const string AtriumMidLeaf = "atrium_8x3_mid";
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
            ["shop_food_taco"] = "taco_counter_12x1",
            ["shop_food_chicken"] = "chicken_shack_12x1",
            ["shop_food_restaurant"] = "restaurant_16x1",
            ["shop_food_mexican"] = "mexican_restaurant_16x1",
            ["shop_food_fine"] = "fine_dining_4x1",
            ["shop_retail"] = "retail_16x1",
            ["shop_retail_gifts"] = "gag_gifts_10x1",
            ["shop_retail_shoes"] = "shoe_store_12x1",
            ["shop_retail_department"] = "department_store_16x2",
            ["service_housekeeping"] = "housekeeping_3x1",
            ["service_maintenance"] = "maintenance_3x1",
            ["service_security"] = "security_post_2x1",
            ["service_research"] = "research_lab_4x1",
            ["service_conference"] = "conference_8x1",
            ["service_event_hall"] = "event_hall_12x2",
            ["service_mail"] = "mail_4x1",
            ["service_recycling"] = "recycling_6x1",
            ["service_loading_dock"] = "loading_dock_8x1",
            ["parking_underground"] = "underground_parking_6x1",
            ["service_valet"] = "valet_3x1",
            ["leisure_gym"] = "gym_6x1",
            ["leisure_spa"] = "spa_6x1",
            ["leisure_pool"] = "pool_8x2",
            ["leisure_bowling"] = "bowling_10x1",
            ["leisure_theater"] = "theater_8x2",
            ["leisure_casino"] = "casino_10x1",
            ["leisure_nightclub"] = "nightclub_8x1",
            ["leisure_chapel"] = "chapel_6x1",
            ["leisure_cathedral"] = "cathedral_10x2",
            ["leisure_atrium"] = "atrium_8x3",
            ["metro_station"] = "metro_station_8x3",
        };

        static readonly Dictionary<string, Sprite> SpriteCache = new();

        public static string ResourceLeaf(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return null;
            return LeafByTypeId.TryGetValue(typeId, out var leaf) ? leaf : null;
        }

        public static string ResourceLeafForContext(string typeId, AtriumStackRole atriumRole)
        {
            if (typeId == "leisure_atrium" && atriumRole == AtriumStackRole.Mid)
                return AtriumMidLeaf;
            return ResourceLeaf(typeId);
        }

        public static string ResourcePath(string typeId)
        {
            var leaf = ResourceLeaf(typeId);
            return leaf == null ? null : ResourceRoot + leaf;
        }

        public static string ResourcePathForContext(string typeId, AtriumStackRole atriumRole)
        {
            var leaf = ResourceLeafForContext(typeId, atriumRole);
            return leaf == null ? null : ResourceRoot + leaf;
        }

        public static string FaceSuffix(bool leftExterior, bool rightExterior)
        {
            if (leftExterior && rightExterior) return "_ext";
            if (leftExterior) return "_L";
            if (rightExterior) return "_R";
            return "_int";
        }

        public static string ResourcePathForFaces(string typeId, bool leftExterior, bool rightExterior)
        {
            var stem = ResourceLeaf(typeId);
            return stem == null ? null : ResourceRoot + stem + FaceSuffix(leftExterior, rightExterior);
        }

        public static string ResourcePathForFaces(
            string typeId,
            bool leftExterior,
            bool rightExterior,
            AtriumStackRole atriumRole)
        {
            var stem = ResourceLeafForContext(typeId, atriumRole);
            return stem == null ? null : ResourceRoot + stem + FaceSuffix(leftExterior, rightExterior);
        }

        public static Sprite LoadSpriteAtResourcePath(string resourcePath) =>
            LoadSpriteAtResourcePath(resourcePath, default);

        public static Sprite LoadSpriteAtResourcePath(string resourcePath, Vector2Int cellSize)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            return GetOrLoadSprite(resourcePath, cellSize);
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

        /// <summary>
        /// Parses <c>3x1</c> from leaves like <c>micro_office_3x1_ext</c> / <c>atrium_8x3_mid</c>.
        /// </summary>
        public static bool TryParseCellSizeFromLeaf(string leafOrPath, out Vector2Int cellSize)
        {
            cellSize = default;
            if (string.IsNullOrEmpty(leafOrPath)) return false;
            var leaf = leafOrPath;
            var slash = leaf.LastIndexOf('/');
            if (slash >= 0) leaf = leaf.Substring(slash + 1);
            if (leaf.EndsWith("_ext", StringComparison.Ordinal) ||
                leaf.EndsWith("_int", StringComparison.Ordinal))
                leaf = leaf.Substring(0, leaf.Length - 4);
            else if (leaf.EndsWith("_L", StringComparison.Ordinal) ||
                     leaf.EndsWith("_R", StringComparison.Ordinal))
                leaf = leaf.Substring(0, leaf.Length - 2);
            else if (leaf.EndsWith("_mid", StringComparison.Ordinal))
                leaf = leaf.Substring(0, leaf.Length - 4);

            var m = Regex.Match(leaf, @"(\d+)x(\d+)$", RegexOptions.CultureInvariant);
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, out var w) ||
                !int.TryParse(m.Groups[2].Value, out var h))
                return false;
            if (w < 1 || h < 1) return false;
            cellSize = new Vector2Int(w, h);
            return true;
        }

        /// <summary>
        /// Stretch-resamples art onto a fixed cell canvas so every plate for a footprint
        /// shares floor/ceiling alignment (avoids neighbor morph when face leaves swap).
        /// </summary>
        public static Texture2D NormalizeToCellCanvas(Texture2D source, Vector2Int cellSize)
        {
            if (source == null || cellSize.x < 1 || cellSize.y < 1)
                return source;

            var tw = Mathf.Max(1, Mathf.RoundToInt(cellSize.x * PixelsPerUnit));
            var th = Mathf.Max(1, Mathf.RoundToInt(cellSize.y * PixelsPerUnit));
            if (source.width == tw && source.height == th)
                return source;

            Color[] srcPx;
            try
            {
                srcPx = source.GetPixels();
            }
            catch (UnityException)
            {
                return source;
            }

            var sw = source.width;
            var sh = source.height;
            var dstPx = new Color[tw * th];
            for (var y = 0; y < th; y++)
            for (var x = 0; x < tw; x++)
            {
                var u = (x + 0.5f) / tw;
                var v = (y + 0.5f) / th;
                var sx = Mathf.Clamp(Mathf.FloorToInt(u * sw), 0, sw - 1);
                var sy = Mathf.Clamp(Mathf.FloorToInt(v * sh), 0, sh - 1);
                dstPx[y * tw + x] = srcPx[sy * sw + sx];
            }

            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = source.name + "_cell"
            };
            tex.SetPixels(dstPx);
            tex.Apply(false, false);
            return tex;
        }

        public static bool TrySprite(RoomInstance room, out Sprite sprite)
        {
            sprite = null;
            if (room?.Type == null || !IsMapped(room.Type)) return false;

            var path = ResourcePath(room.Type.id);
            if (string.IsNullOrEmpty(path)) return false;

            var cells = room.Size.x > 0 && room.Size.y > 0 ? room.Size : room.Type.size;
            sprite = GetOrLoadSprite(path, cells);
            return sprite != null;
        }

        public static void ResetForTests()
        {
            LoadSpriteForTests = null;
            SpriteCache.Clear();
        }

        static Sprite GetOrLoadSprite(string resourcePath, Vector2Int cellSize)
        {
            if (LoadSpriteForTests != null)
                return LoadSpriteForTests(resourcePath);

            if (cellSize.x < 1 || cellSize.y < 1)
                TryParseCellSizeFromLeaf(resourcePath, out cellSize);

            var cacheKey = cellSize.x > 0
                ? resourcePath + "@" + cellSize.x + "x" + cellSize.y
                : resourcePath;

            if (SpriteCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var loaded = LoadSprite(resourcePath, cellSize);
            if (loaded != null)
                SpriteCache[cacheKey] = loaded;
            return loaded;
        }

        static Sprite LoadSprite(string resourcePath, Vector2Int cellSize)
        {
            var tex = DecodeTexture(resourcePath);
            if (tex == null) return null;

            // Keep canvas size — opaque-bounds crop was re-stretching face variants
            // to different aspects and misaligning neighbor floors/ceilings.
            tex = KeyMagentaKeepCanvas(tex, destroySource: true);
            if (tex == null) return null;

            if (cellSize.x > 0 && cellSize.y > 0)
            {
                var normalized = NormalizeToCellCanvas(tex, cellSize);
                if (normalized != null && normalized != tex)
                {
                    UnityEngine.Object.Destroy(tex);
                    tex = normalized;
                }
            }

            // Bottom-left pivot: overlay pins to the room origin. Canvas matches
            // catalog cells×PPU so OverlayScale is ~identity across face variants.
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0f, 0f),
                PixelsPerUnit);
        }

        static Texture2D DecodeTexture(string resourcePath)
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

        static Texture2D LoadTexture(string resourcePath)
        {
            var decoded = DecodeTexture(resourcePath);
            if (decoded == null) return null;
            return KeyCropMagentaPlate(decoded, destroySource: true);
        }

        /// <summary>
        /// Clear magenta plate/fringe in place — preserves width/height so cell canvases stay aligned.
        /// </summary>
        public static Texture2D KeyMagentaKeepCanvas(Texture2D source, bool destroySource)
        {
            if (source == null) return null;

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

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = source.name
            };
            tex.SetPixels(px);
            tex.Apply(false, false);

            if (destroySource)
                UnityEngine.Object.Destroy(source);
            return tex;
        }

        /// <summary>
        /// Flood-key hot magenta/pink plates (AI chroma), scrub fringe, crop to opaque bounds.
        /// Reuses <see cref="MenuIconArt.IsHotMagenta"/> so dollhouse and menu share one detector.
        /// Prefer <see cref="KeyMagentaKeepCanvas"/> for cell-aligned dollhouse overlays.
        /// </summary>
        public static Texture2D KeyCropMagentaPlate(Texture2D source, bool destroySource)
        {
            if (source == null) return null;

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
            if (maxX < minX || maxY < minY)
            {
                if (destroySource)
                    UnityEngine.Object.Destroy(source);
                return null;
            }

            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);
            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            KeyHotMagenta(cropped);
            ScrubNearMagentaFringe(cropped, cw, ch);

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = source.name
            };
            tex.SetPixels(cropped);
            tex.Apply(false, false);

            if (destroySource)
                UnityEngine.Object.Destroy(source);
            return tex;
        }

        static void FloodKeyMagentaPlate(Color[] px, int w, int h)
        {
            var visit = new bool[w * h];
            var q = new Queue<int>();

            void TryEnq(int x, int y)
            {
                if ((uint)x >= w || (uint)y >= h) return;
                var i = y * w + x;
                if (visit[i] || !MenuIconArt.IsHotMagenta(px[i])) return;
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
                if (MenuIconArt.IsHotMagenta(px[i]))
                    px[i] = Color.clear;
            }
        }

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
            return (c.r - c.g) > 0.18f && c.b > 0.22f && (c.b - c.g) > 0.08f;
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
