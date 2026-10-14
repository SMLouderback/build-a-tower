using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomDollhouseArtTests
    {
        [TearDown]
        public void TearDown() => RoomDollhouseArt.ResetForTests();

        [Test]
        public void ResourceRoot_is_art_dollhouse()
        {
            Assert.AreEqual("Art/Dollhouse/", RoomDollhouseArt.ResourceRoot);
        }

        [Test]
        public void Maps_all_approved_build_menu_filenames()
        {
            foreach (var (id, leaf) in Catalog)
            {
                Assert.AreEqual(leaf, RoomDollhouseArt.ResourceLeaf(id), id);
                Assert.AreEqual(
                    RoomDollhouseArt.ResourceRoot + leaf,
                    RoomDollhouseArt.ResourcePath(id),
                    id);
            }
        }

        [TestCase(true, true, "_ext")]
        [TestCase(true, false, "_L")]
        [TestCase(false, true, "_R")]
        [TestCase(false, false, "_int")]
        public void FaceSuffix_maps_exterior_pairs(bool leftExterior, bool rightExterior, string suffix)
        {
            Assert.AreEqual(suffix, RoomDollhouseArt.FaceSuffix(leftExterior, rightExterior));
        }

        [Test]
        public void ResourcePathForFaces_appends_suffix_to_mapped_stem()
        {
            const string stem = "micro_office_3x1";
            Assert.AreEqual(
                RoomDollhouseArt.ResourceRoot + stem + "_ext",
                RoomDollhouseArt.ResourcePathForFaces("office_micro", true, true));
            Assert.AreEqual(
                RoomDollhouseArt.ResourceRoot + stem + "_L",
                RoomDollhouseArt.ResourcePathForFaces("office_micro", true, false));
            Assert.AreEqual(
                RoomDollhouseArt.ResourceRoot + stem + "_R",
                RoomDollhouseArt.ResourcePathForFaces("office_micro", false, true));
            Assert.AreEqual(
                RoomDollhouseArt.ResourceRoot + stem + "_int",
                RoomDollhouseArt.ResourcePathForFaces("office_micro", false, false));
            Assert.IsNull(RoomDollhouseArt.ResourcePathForFaces("office_premium", true, true));
        }

        [Test]
        public void Atrium_mid_context_resolves_to_open_well_leaf_not_roof_leaf()
        {
            Assert.AreEqual("atrium_8x3", RoomDollhouseArt.ResourceLeaf("leisure_atrium"));
            Assert.AreEqual("atrium_8x3", RoomDollhouseArt.ResourceLeafForContext("leisure_atrium", AtriumStackRole.Roof));
            Assert.AreEqual("atrium_8x3", RoomDollhouseArt.ResourceLeafForContext("leisure_atrium", AtriumStackRole.Solo));

            var midLeaf = RoomDollhouseArt.ResourceLeafForContext("leisure_atrium", AtriumStackRole.Mid);
            Assert.AreEqual("atrium_8x3_mid", midLeaf);
            Assert.AreNotEqual(RoomDollhouseArt.ResourceLeaf("leisure_atrium"), midLeaf);
            Assert.AreEqual("Art/Dollhouse/atrium_8x3_mid", RoomDollhouseArt.ResourcePathForContext("leisure_atrium", AtriumStackRole.Mid));
        }

        [Test]
        public void Does_not_map_elevator_stairs_lobby_parking_ramp_or_legacy_premium()
        {
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("elevator_normal"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("stairs"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("lobby"));
            Assert.IsFalse(RoomDollhouseArt.IsMapped(Type("parking_ramp", isParkingRamp: true)));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("parking_ramp"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("office_premium"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("hotel_premium"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("condo_premium"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf(null));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf(""));
        }

        [Test]
        public void TryParseCellSizeFromLeaf_reads_stem_and_strips_face_suffix(
            [Values(
                "micro_office_3x1",
                "micro_office_3x1_ext",
                "Art/Dollhouse/studio_office_4x1_L",
                "atrium_8x3_mid",
                "atrium_8x3_mid_ext")]
            string leaf)
        {
            Assert.IsTrue(RoomDollhouseArt.TryParseCellSizeFromLeaf(leaf, out var size));
            if (leaf.Contains("atrium"))
                Assert.AreEqual(new Vector2Int(8, 3), size);
            else if (leaf.Contains("studio"))
                Assert.AreEqual(new Vector2Int(4, 1), size);
            else
                Assert.AreEqual(new Vector2Int(3, 1), size);
        }

        [Test]
        public void NormalizeToCellCanvas_matches_cells_times_ppu()
        {
            var src = new Texture2D(50, 20, TextureFormat.RGBA32, false);
            src.SetPixels(new Color[50 * 20]);
            src.Apply(false, false);
            var normalized = RoomDollhouseArt.NormalizeToCellCanvas(src, new Vector2Int(3, 1));
            Assert.AreEqual(3 * (int)RoomDollhouseArt.PixelsPerUnit, normalized.width);
            Assert.AreEqual(1 * (int)RoomDollhouseArt.PixelsPerUnit, normalized.height);
            UnityEngine.Object.DestroyImmediate(src);
            if (normalized != src)
                UnityEngine.Object.DestroyImmediate(normalized);
        }

        [Test]
        public void KeyMagentaKeepCanvas_preserves_dimensions()
        {
            const int w = 64;
            const int h = 32;
            var src = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (var i = 0; i < px.Length; i++)
                px[i] = Color.gray;
            // Magenta plate rim
            for (var x = 0; x < w; x++)
            {
                px[x] = new Color(1f, 0f, 1f, 1f);
                px[(h - 1) * w + x] = new Color(1f, 0f, 1f, 1f);
            }
            src.SetPixels(px);
            src.Apply(false, false);

            var keyed = RoomDollhouseArt.KeyMagentaKeepCanvas(src, destroySource: true);
            Assert.AreEqual(w, keyed.width);
            Assert.AreEqual(h, keyed.height);
            Assert.Less(keyed.GetPixel(0, 0).a, 0.1f);
            Assert.Greater(keyed.GetPixel(w / 2, h / 2).a, 0.9f);
            UnityEngine.Object.DestroyImmediate(keyed);
        }

        [Test]
        public void OverlayScale_fits_native_pixels_into_cell_footprint_without_assuming_128()
        {
            // 9×1 catalog cell box; native pan is ~918×176 (not 1152×128).
            const float ppu = 128f;
            var world = new Vector2(918f / ppu, 176f / ppu);
            var scale = RoomDollhouseArt.OverlayScale(world, new Vector2Int(9, 1));
            Assert.AreEqual(9f / world.x, scale.x, 0.0001f);
            Assert.AreEqual(1f / world.y, scale.y, 0.0001f);
            Assert.AreEqual(1f, scale.z);
        }

        [Test]
        public void OverlayScale_fits_two_floor_rooms()
        {
            var world = new Vector2(12f, 1.5f);
            var scale = RoomDollhouseArt.OverlayScale(world, new Vector2Int(12, 2));
            Assert.AreEqual(1f, scale.x, 0.0001f);
            Assert.AreEqual(2f / 1.5f, scale.y, 0.0001f);
        }

        [Test]
        public void TrySprite_false_for_stairs_and_elevator_even_if_loader_would_succeed()
        {
            RoomDollhouseArt.LoadSpriteForTests = _ => DummySprite();
            var stairs = Type("stairs", isStairs: true);
            var elev = Type("elevator_normal", isElevator: true);
            Assert.IsFalse(RoomDollhouseArt.TrySprite(Room(stairs), out _));
            Assert.IsFalse(RoomDollhouseArt.TrySprite(Room(elev), out _));
        }

        [Test]
        public void TrySprite_loads_mapped_leaf_and_skips_unmapped()
        {
            string loaded = null;
            RoomDollhouseArt.LoadSpriteForTests = path =>
            {
                loaded = path;
                return DummySprite();
            };

            Assert.IsTrue(RoomDollhouseArt.TrySprite(Room(Type("office_micro")), out var sprite));
            Assert.IsNotNull(sprite);
            Assert.AreEqual("Art/Dollhouse/micro_office_3x1", loaded);

            Assert.IsFalse(RoomDollhouseArt.TrySprite(Room(Type("office_premium")), out _));
        }

        [Test]
        public void SortingOrder_stays_below_transit_layers()
        {
            Assert.Less(RoomDollhouseArt.SortingOrder, 15);
        }

        [Test]
        public void Catalog_count_matches_approved_attachment_list()
        {
            Assert.AreEqual(59, Catalog.Length);
        }

        [Test]
        public void TrySprite_keys_magenta_plate_on_leisure_theater()
        {
            RoomDollhouseArt.ResetForTests();
            Assert.IsTrue(RoomDollhouseArt.TrySprite(Room(Type("leisure_theater")), out var sprite));
            Assert.IsNotNull(sprite);
            var tex = sprite.texture;
            Assert.IsNotNull(tex);
            // Corners of the keyed crop should not be hot magenta plate.
            Assert.IsFalse(MenuIconArt.IsHotMagenta(tex.GetPixel(0, 0)));
            Assert.IsFalse(MenuIconArt.IsHotMagenta(tex.GetPixel(tex.width - 1, 0)));
            Assert.IsFalse(MenuIconArt.IsHotMagenta(tex.GetPixel(0, tex.height - 1)));
            Assert.IsFalse(MenuIconArt.IsHotMagenta(tex.GetPixel(tex.width - 1, tex.height - 1)));
        }

        [Test]
        public void Committed_png_bytes_exist_for_every_mapped_leaf()
        {
            var dir = System.IO.Path.Combine(Application.dataPath, "Resources/Art/Dollhouse");
            foreach (var (_, leaf) in Catalog)
            {
                var png = System.IO.Path.Combine(dir, leaf + ".png");
                Assert.IsTrue(System.IO.File.Exists(png), "Missing committed PNG " + png);
                Assert.Greater(new System.IO.FileInfo(png).Length, 1024, png + " is too small");
            }
        }

        [Test]
        public void Committed_png_bytes_exist_for_office_micro_face_variants()
        {
            AssertFaceVariantsExist("office_micro", "micro_office_3x1");
        }

        [Test]
        public void Committed_png_bytes_exist_for_hotel_face_variants()
        {
            foreach (var (id, leaf) in Catalog)
            {
                if (!id.StartsWith("hotel_")) continue;
                AssertFaceVariantsExist(id, leaf);
            }
        }

        [Test]
        public void Committed_png_bytes_exist_for_condo_face_variants()
        {
            foreach (var (id, leaf) in Catalog)
            {
                if (!id.StartsWith("condo_")) continue;
                AssertFaceVariantsExist(id, leaf);
            }
        }

        [Test]
        public void Committed_png_bytes_exist_for_shop_face_variants()
        {
            foreach (var (id, leaf) in Catalog)
            {
                if (!id.StartsWith("shop_")) continue;
                AssertFaceVariantsExist(id, leaf);
            }
        }

        [Test]
        public void Committed_png_bytes_exist_for_service_and_parking_face_variants()
        {
            foreach (var (id, leaf) in Catalog)
            {
                if (!id.StartsWith("service_") && id != "parking_underground") continue;
                AssertFaceVariantsExist(id, leaf);
            }
        }

        [Test]
        public void Committed_png_bytes_exist_for_leisure_and_metro_face_variants()
        {
            foreach (var (id, leaf) in Catalog)
            {
                if (!id.StartsWith("leisure_") && id != "metro_station") continue;
                AssertFaceVariantsExist(id, leaf);
            }

            // Atrium mid open-well leaf also has face plates.
            var dir = System.IO.Path.Combine(Application.dataPath, "Resources/Art/Dollhouse");
            var midStem = RoomDollhouseArt.ResourceLeafForContext("leisure_atrium", AtriumStackRole.Mid);
            Assert.AreEqual("atrium_8x3_mid", midStem);
            foreach (var suffix in new[] { "_ext", "_L", "_R", "_int" })
            {
                var png = System.IO.Path.Combine(dir, midStem + suffix + ".png");
                var bytes = System.IO.Path.Combine(dir, midStem + suffix + ".bytes");
                Assert.IsTrue(System.IO.File.Exists(png), "Missing face PNG " + png);
                Assert.IsTrue(System.IO.File.Exists(bytes), "Missing face bytes " + bytes);
                Assert.Greater(new System.IO.FileInfo(png).Length, 1024, png + " is too small");
            }
        }

        static void AssertFaceVariantsExist(string typeId, string expectedStem)
        {
            var dir = System.IO.Path.Combine(Application.dataPath, "Resources/Art/Dollhouse");
            var stem = RoomDollhouseArt.ResourceLeaf(typeId);
            Assert.AreEqual(expectedStem, stem, typeId);
            foreach (var suffix in new[] { "_ext", "_L", "_R", "_int" })
            {
                var png = System.IO.Path.Combine(dir, stem + suffix + ".png");
                var bytes = System.IO.Path.Combine(dir, stem + suffix + ".bytes");
                Assert.IsTrue(System.IO.File.Exists(png), "Missing face PNG " + png);
                Assert.IsTrue(System.IO.File.Exists(bytes), "Missing face bytes " + bytes);
                Assert.Greater(new System.IO.FileInfo(png).Length, 1024, png + " is too small");
            }
        }

        [Test]
        public void Committed_png_bytes_exist_for_atrium_mid_context_leaf()
        {
            var dir = System.IO.Path.Combine(Application.dataPath, "Resources/Art/Dollhouse");
            var leaf = RoomDollhouseArt.ResourceLeafForContext("leisure_atrium", AtriumStackRole.Mid);
            var png = System.IO.Path.Combine(dir, leaf + ".png");
            var bytes = System.IO.Path.Combine(dir, leaf + ".bytes");

            Assert.IsTrue(System.IO.File.Exists(png), "Missing committed PNG " + png);
            Assert.IsTrue(System.IO.File.Exists(bytes), "Missing committed bytes " + bytes);
            Assert.Greater(new System.IO.FileInfo(png).Length, 1024, png + " is too small");
            Assert.Greater(new System.IO.FileInfo(bytes).Length, 1024, bytes + " is too small");
        }

        static readonly (string id, string leaf)[] Catalog =
        {
            ("office_micro", "micro_office_3x1"),
            ("office_studio", "studio_office_4x1"),
            ("office_base", "small_office_6x1"),
            ("office_mid_standard", "mid_office_8x1"),
            ("office_mid_clinic", "professional_suite_9x1"),
            ("office_mid_team", "team_bay_10x1"),
            ("office_upper_standard", "upper_office_10x1"),
            ("office_upper_corner", "corner_suite_12x1"),
            ("office_upper_floor", "corporate_14x1"),
            ("hotel_base", "base_hotel_3x1"),
            ("hotel_accessible", "accessible_hotel_3x1"),
            ("hotel_mid_standard", "mid_standard_hotel_4x1"),
            ("hotel_mid_extended", "mid_extended_hotel_6x1"),
            ("hotel_studio", "studio_hotel_5x1"),
            ("hotel_junior_suite", "junior_suite_5x1"),
            ("hotel_upper_standard", "upper_standard_hotel_5x1"),
            ("hotel_upper_king", "upper_king_hotel_5x1"),
            ("hotel_upper_suite", "upper_suite_8x1"),
            ("condo_studio", "studio_condo_4x1"),
            ("condo_alcove", "alcove_studio_5x1"),
            ("condo_base", "one_bedroom_condo_8x1"),
            ("condo_mid_standard", "mid_condo_9x1"),
            ("condo_mid_loft", "loft_condo_10x1"),
            ("condo_mid_family", "family_condo_12x1"),
            ("condo_upper_standard", "upper_condo_10x1"),
            ("condo_upper_corner", "corner_condo_12x1"),
            ("condo_upper_penthouse", "penthouse_14x1"),
            ("shop_food_fast", "fast_food_8x1"),
            ("shop_food_taco", "taco_counter_6x1"),
            ("shop_food_chicken", "chicken_shack_6x1"),
            ("shop_food_restaurant", "restaurant_10x1"),
            ("shop_food_mexican", "mexican_restaurant_10x1"),
            ("shop_food_fine", "fine_dining_8x1"),
            ("shop_retail", "retail_8x1"),
            ("shop_retail_gifts", "gag_gifts_6x1"),
            ("shop_retail_shoes", "shoe_store_6x1"),
            ("shop_retail_department", "department_store_12x2"),
            ("service_housekeeping", "housekeeping_4x1"),
            ("service_maintenance", "maintenance_4x1"),
            ("service_security", "security_post_2x1"),
            ("service_research", "research_lab_8x1"),
            ("service_conference", "conference_8x1"),
            ("service_event_hall", "event_hall_12x2"),
            ("service_mail", "mail_4x1"),
            ("service_recycling", "recycling_6x1"),
            ("service_loading_dock", "loading_dock_8x1"),
            ("parking_underground", "underground_parking_6x1"),
            ("service_valet", "valet_3x1"),
            ("leisure_gym", "gym_6x1"),
            ("leisure_spa", "spa_6x1"),
            ("leisure_pool", "pool_8x2"),
            ("leisure_bowling", "bowling_10x1"),
            ("leisure_theater", "theater_8x2"),
            ("leisure_casino", "casino_10x1"),
            ("leisure_nightclub", "nightclub_8x1"),
            ("leisure_chapel", "chapel_6x1"),
            ("leisure_cathedral", "cathedral_10x2"),
            ("leisure_atrium", "atrium_8x3"),
            ("metro_station", "metro_station_8x3"),
        };

        static Sprite DummySprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0f, 0f), 128f);
        }

        static RoomTypeSO Type(
            string id,
            bool isStairs = false,
            bool isElevator = false,
            bool isParkingRamp = false)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.isStairs = isStairs;
            so.isElevatorShaft = isElevator;
            so.isParkingRamp = isParkingRamp;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomInstance Room(RoomTypeSO type) =>
            new RoomInstance(1, type, Vector2Int.zero, type.size);
    }
}
