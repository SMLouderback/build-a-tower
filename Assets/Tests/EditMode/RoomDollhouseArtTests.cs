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

        [Test]
        public void Does_not_map_elevator_stairs_lobby_or_legacy_premium()
        {
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("elevator_normal"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("stairs"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("lobby"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("office_premium"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("hotel_premium"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf("condo_premium"));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf(null));
            Assert.IsNull(RoomDollhouseArt.ResourceLeaf(""));
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
        public void Catalog_count_matches_approved_attachment_list()
        {
            Assert.AreEqual(40, Catalog.Length);
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

        static readonly (string id, string leaf)[] Catalog =
        {
            ("office_micro", "micro_office_3x1"),
            ("office_studio", "studio_office_4x1"),
            ("office_base", "small_office_6x1"),
            ("office_mid_standard", "mid_office_9x1"),
            ("office_mid_clinic", "professional_suite_10x1"),
            ("office_mid_team", "team_bay_12x1"),
            ("office_upper_standard", "upper_office_12x1"),
            ("office_upper_corner", "corner_suite_14x1"),
            ("office_upper_floor", "corporate_18x1"),
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
            ("condo_mid_standard", "mid_condo_10x1"),
            ("condo_mid_loft", "loft_condo_12x1"),
            ("condo_mid_family", "family_condo_14x1"),
            ("condo_upper_standard", "upper_condo_12x1"),
            ("condo_upper_corner", "corner_condo_14x1"),
            ("condo_upper_penthouse", "penthouse_18x1"),
            ("shop_food_fast", "fast_food_16x1"),
            ("shop_food_restaurant", "restaurant_16x1"),
            ("shop_food_fine", "fine_dining_4x1"),
            ("shop_retail", "retail_16x1"),
            ("service_housekeeping", "housekeeping_3x1"),
            ("service_maintenance", "maintenance_3x1"),
            ("service_security", "security_post_2x1"),
            ("service_research", "research_lab_4x1"),
            ("service_conference", "conference_8x1"),
            ("service_event_hall", "event_hall_12x2"),
            ("parking_underground", "underground_parking_6x1"),
            ("service_valet", "valet_3x1"),
            ("parking_ramp", "parking_ramp_3x2"),
        };

        static Sprite DummySprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0f, 0f), 128f);
        }

        static RoomTypeSO Type(string id, bool isStairs = false, bool isElevator = false)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.isStairs = isStairs;
            so.isElevatorShaft = isElevator;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomInstance Room(RoomTypeSO type) =>
            new RoomInstance(1, type, Vector2Int.zero, type.size);
    }
}
