using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HotelCutawayArtTests
    {
        [TearDown]
        public void TearDown() => HotelCutawayArt.ResetForTests();

        [Test]
        public void CellPixels_is_128() => Assert.AreEqual(128, HotelCutawayArt.CellPixels);

        [Test]
        public void IsHotel_true_for_hotel_category()
        {
            Assert.IsTrue(HotelCutawayArt.IsHotel(Hotel("hotel_base", 3, LuxuryBand.Base)));
            Assert.IsFalse(HotelCutawayArt.IsHotel(null));
            Assert.IsFalse(HotelCutawayArt.IsHotel(Office()));
            Assert.IsFalse(HotelCutawayArt.IsHotel(Lobby()));
        }

        [Test]
        public void ResolveArtKey_maps_all_nine_menu_hotels()
        {
            Assert.AreEqual("hotel_3_base", HotelCutawayArt.ResolveArtKey(Hotel("hotel_base", 3, LuxuryBand.Base)));
            Assert.AreEqual("hotel_3_base", HotelCutawayArt.ResolveArtKey(Hotel("hotel_accessible", 3, LuxuryBand.Base)));
            Assert.AreEqual("hotel_4_mid", HotelCutawayArt.ResolveArtKey(Hotel("hotel_mid_standard", 4, LuxuryBand.Mid)));
            Assert.AreEqual("hotel_5_mid", HotelCutawayArt.ResolveArtKey(Hotel("hotel_studio", 5, LuxuryBand.Mid)));
            Assert.AreEqual("hotel_5_mid", HotelCutawayArt.ResolveArtKey(Hotel("hotel_junior_suite", 5, LuxuryBand.Mid)));
            Assert.AreEqual("hotel_6_mid", HotelCutawayArt.ResolveArtKey(Hotel("hotel_mid_extended", 6, LuxuryBand.Mid)));
            Assert.AreEqual("hotel_5_upper", HotelCutawayArt.ResolveArtKey(Hotel("hotel_upper_standard", 5, LuxuryBand.Upper)));
            Assert.AreEqual("hotel_5_upper", HotelCutawayArt.ResolveArtKey(Hotel("hotel_upper_king", 5, LuxuryBand.Upper)));
            Assert.AreEqual("hotel_8_upper", HotelCutawayArt.ResolveArtKey(Hotel("hotel_upper_suite", 8, LuxuryBand.Upper)));
        }

        [Test]
        public void ResolveArtKey_legacy_premium_maps_to_hotel_4_mid()
        {
            var unsetBand = Hotel("hotel_premium", 4, LuxuryBand.None);
            var midBand = Hotel("hotel_premium", 4, LuxuryBand.Mid);
            Assert.AreEqual("hotel_4_mid", HotelCutawayArt.ResolveArtKey(unsetBand));
            Assert.AreEqual("hotel_4_mid", HotelCutawayArt.ResolveArtKey(midBand));
        }

        [Test]
        public void ResolveArtKey_non_hotel_or_unknown_combo_returns_empty()
        {
            Assert.AreEqual(string.Empty, HotelCutawayArt.ResolveArtKey(null));
            Assert.AreEqual(string.Empty, HotelCutawayArt.ResolveArtKey(Office()));
            Assert.AreEqual(string.Empty, HotelCutawayArt.ResolveArtKey(Hotel("hotel_base", 3, LuxuryBand.Mid)));
            Assert.AreEqual(string.Empty, HotelCutawayArt.ResolveArtKey(Hotel("hotel_unknown", 7, LuxuryBand.Mid)));
        }

        [Test]
        public void ResourcePath_prefixes_art_hotels()
        {
            Assert.AreEqual("Art/Hotels/hotel_3_base", HotelCutawayArt.ResourcePath("hotel_3_base"));
            Assert.AreEqual("Art/Hotels/hotel_8_upper", HotelCutawayArt.ResourcePath("hotel_8_upper"));
        }

        [Test]
        public void ExpectedPixelSize_matches_catalog_table()
        {
            Assert.AreEqual(new Vector2Int(384, 128), HotelCutawayArt.ExpectedPixelSize("hotel_3_base"));
            Assert.AreEqual(new Vector2Int(512, 128), HotelCutawayArt.ExpectedPixelSize("hotel_4_mid"));
            Assert.AreEqual(new Vector2Int(640, 128), HotelCutawayArt.ExpectedPixelSize("hotel_5_mid"));
            Assert.AreEqual(new Vector2Int(768, 128), HotelCutawayArt.ExpectedPixelSize("hotel_6_mid"));
            Assert.AreEqual(new Vector2Int(640, 128), HotelCutawayArt.ExpectedPixelSize("hotel_5_upper"));
            Assert.AreEqual(new Vector2Int(1024, 128), HotelCutawayArt.ExpectedPixelSize("hotel_8_upper"));
            Assert.AreEqual(Vector2Int.zero, HotelCutawayArt.ExpectedPixelSize(string.Empty));
            Assert.AreEqual(Vector2Int.zero, HotelCutawayArt.ExpectedPixelSize("hotel_unknown"));
        }

        static RoomTypeSO Hotel(string id, int width, LuxuryBand band)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Hotel;
            so.allowAboveGround = true;
            so.size = new Vector2Int(width, 1);
            so.luxuryBand = band;
            return so;
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.allowAboveGround = true;
            so.size = new Vector2Int(9, 1);
            return so;
        }
    }
}
