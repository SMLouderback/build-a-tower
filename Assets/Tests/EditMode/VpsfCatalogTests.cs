using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class VpsfCatalogTests
    {
        [Test]
        public void TryIdentity_maps_office_ids_to_tier_and_class()
        {
            AssertIdentity(OfficeLuxury.MicroId, RoomCategory.Office, LuxuryBand.Base,
                EconomicFamily.Office, tier: 1, TenantClass.Mid);
            AssertIdentity(OfficeLuxury.StudioId, RoomCategory.Office, LuxuryBand.Base,
                EconomicFamily.Office, tier: 2, TenantClass.Mid);
            AssertIdentity(OfficeLuxury.BaseId, RoomCategory.Office, LuxuryBand.Base,
                EconomicFamily.Office, tier: 3, TenantClass.Mid);
            AssertIdentity(OfficeLuxury.MidStandardId, RoomCategory.Office, LuxuryBand.Mid,
                EconomicFamily.Office, tier: 4, TenantClass.Mid);
            AssertIdentity(OfficeLuxury.MidClinicId, RoomCategory.Office, LuxuryBand.Mid,
                EconomicFamily.Office, tier: 5, TenantClass.Mid);
            AssertIdentity(OfficeLuxury.MidTeamId, RoomCategory.Office, LuxuryBand.Mid,
                EconomicFamily.Office, tier: 6, TenantClass.Mid);
            AssertIdentity(OfficeLuxury.UpperStandardId, RoomCategory.Office, LuxuryBand.Upper,
                EconomicFamily.Office, tier: 7, TenantClass.Upper);
            AssertIdentity(OfficeLuxury.UpperCornerId, RoomCategory.Office, LuxuryBand.Upper,
                EconomicFamily.Office, tier: 8, TenantClass.Upper);
            AssertIdentity(OfficeLuxury.UpperFloorId, RoomCategory.Office, LuxuryBand.Upper,
                EconomicFamily.Office, tier: 9, TenantClass.Upper);
        }

        [Test]
        public void TryIdentity_maps_hotel_ids_to_tier_and_class()
        {
            AssertIdentity("hotel_base", RoomCategory.Hotel, LuxuryBand.Base,
                EconomicFamily.Hotel, tier: 1, TenantClass.Mid);
            AssertIdentity("hotel_accessible", RoomCategory.Hotel, LuxuryBand.Base,
                EconomicFamily.Hotel, tier: 2, TenantClass.Mid);
            AssertIdentity("hotel_mid_standard", RoomCategory.Hotel, LuxuryBand.Mid,
                EconomicFamily.Hotel, tier: 3, TenantClass.Mid);
            AssertIdentity(HotelLuxury.MidExtendedId, RoomCategory.Hotel, LuxuryBand.Mid,
                EconomicFamily.Hotel, tier: 4, TenantClass.Mid);
            AssertIdentity("hotel_studio", RoomCategory.Hotel, LuxuryBand.Mid,
                EconomicFamily.Hotel, tier: 5, TenantClass.Mid);
            AssertIdentity("hotel_junior_suite", RoomCategory.Hotel, LuxuryBand.Mid,
                EconomicFamily.Hotel, tier: 6, TenantClass.Mid);
            AssertIdentity("hotel_upper_standard", RoomCategory.Hotel, LuxuryBand.Upper,
                EconomicFamily.Hotel, tier: 7, TenantClass.Upper);
            AssertIdentity(HotelLuxury.UpperKingId, RoomCategory.Hotel, LuxuryBand.Upper,
                EconomicFamily.Hotel, tier: 8, TenantClass.Upper);
            AssertIdentity(HotelLuxury.UpperSuiteId, RoomCategory.Hotel, LuxuryBand.Upper,
                EconomicFamily.Hotel, tier: 9, TenantClass.Upper);
        }

        [Test]
        public void TryIdentity_maps_condo_ids_to_tier_and_class()
        {
            AssertIdentity(CondoLuxury.StudioId, RoomCategory.Condo, LuxuryBand.Base,
                EconomicFamily.Condo, tier: 1, TenantClass.Mid);
            AssertIdentity(CondoLuxury.AlcoveId, RoomCategory.Condo, LuxuryBand.Base,
                EconomicFamily.Condo, tier: 2, TenantClass.Mid);
            AssertIdentity(CondoLuxury.BaseId, RoomCategory.Condo, LuxuryBand.Base,
                EconomicFamily.Condo, tier: 3, TenantClass.Mid);
            AssertIdentity(CondoLuxury.MidStandardId, RoomCategory.Condo, LuxuryBand.Mid,
                EconomicFamily.Condo, tier: 4, TenantClass.Mid);
            AssertIdentity(CondoLuxury.MidLoftId, RoomCategory.Condo, LuxuryBand.Mid,
                EconomicFamily.Condo, tier: 5, TenantClass.Mid);
            AssertIdentity(CondoLuxury.MidFamilyId, RoomCategory.Condo, LuxuryBand.Mid,
                EconomicFamily.Condo, tier: 6, TenantClass.Mid);
            AssertIdentity(CondoLuxury.UpperStandardId, RoomCategory.Condo, LuxuryBand.Upper,
                EconomicFamily.Condo, tier: 7, TenantClass.Upper);
            AssertIdentity(CondoLuxury.UpperCornerId, RoomCategory.Condo, LuxuryBand.Upper,
                EconomicFamily.Condo, tier: 8, TenantClass.Upper);
            AssertIdentity(CondoLuxury.UpperPenthouseId, RoomCategory.Condo, LuxuryBand.Upper,
                EconomicFamily.Condo, tier: 9, TenantClass.Upper);
        }

        [Test]
        public void TryIdentity_unknown_id_returns_false()
        {
            var so = Room("totally_unknown_room_xyz", RoomCategory.Structure, LuxuryBand.None);
            Assert.IsFalse(VpsfCatalog.TryIdentity(so, out _, out _, out _));
        }

        [Test]
        public void TryIdentity_maps_shops_and_infrastructure()
        {
            var shop = Room("shop_food_fast", RoomCategory.Commercial, LuxuryBand.None);
            Assert.IsTrue(VpsfCatalog.TryIdentity(shop, out var shopFamily, out var shopTier, out _));
            Assert.AreEqual(EconomicFamily.Shops, shopFamily);
            Assert.AreEqual(1, shopTier);

            var lobby = Room("lobby", RoomCategory.Structure, LuxuryBand.None);
            lobby.isLobby = true;
            Assert.IsTrue(VpsfCatalog.TryIdentity(lobby, out var lobbyFamily, out _, out _));
            Assert.AreEqual(EconomicFamily.Infrastructure, lobbyFamily);
        }

        [Test]
        public void TryIdentity_null_type_returns_false()
        {
            Assert.IsFalse(VpsfCatalog.TryIdentity(null, out _, out _, out _));
        }

        [Test]
        public void TryIdentity_infers_class_from_id_prefix_when_band_none()
        {
            AssertIdentity(OfficeLuxury.UpperCornerId, RoomCategory.Office, LuxuryBand.None,
                EconomicFamily.Office, tier: 8, TenantClass.Upper);
            AssertIdentity(OfficeLuxury.MidClinicId, RoomCategory.Office, LuxuryBand.None,
                EconomicFamily.Office, tier: 5, TenantClass.Mid);
            AssertIdentity(CondoLuxury.StudioId, RoomCategory.Condo, LuxuryBand.None,
                EconomicFamily.Condo, tier: 1, TenantClass.Mid);
        }

        [Test]
        public void Cells_returns_width_times_height()
        {
            var so = Room(OfficeLuxury.BaseId, RoomCategory.Office, LuxuryBand.Base);
            so.size = new Vector2Int(5, 2);
            Assert.AreEqual(10, VpsfCatalog.Cells(so));
        }

        [Test]
        public void Cells_null_or_non_positive_returns_zero()
        {
            Assert.AreEqual(0, VpsfCatalog.Cells(null));
            var so = Room(OfficeLuxury.BaseId, RoomCategory.Office, LuxuryBand.Base);
            so.size = new Vector2Int(0, 3);
            Assert.AreEqual(0, VpsfCatalog.Cells(so));
        }

        static void AssertIdentity(
            string id,
            RoomCategory category,
            LuxuryBand band,
            EconomicFamily expectedFamily,
            int tier,
            TenantClass expectedClass)
        {
            var so = Room(id, category, band);
            Assert.IsTrue(VpsfCatalog.TryIdentity(so, out var family, out var mappedTier, out var mappedClass), id);
            Assert.AreEqual(expectedFamily, family, id);
            Assert.AreEqual(tier, mappedTier, id);
            Assert.AreEqual(expectedClass, mappedClass, id);
        }

        static RoomTypeSO Room(string id, RoomCategory category, LuxuryBand band)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = category;
            so.luxuryBand = band;
            so.size = Vector2Int.one;
            return so;
        }
    }
}
