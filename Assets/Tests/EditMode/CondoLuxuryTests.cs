using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CondoLuxuryTests
    {
        [Test]
        public void Basic_accepts_base_only()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Base, WealthBand.Basic, CondoLuxury.StudioId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Basic, CondoLuxury.MidStandardId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Basic, CondoLuxury.UpperPenthouseId));
        }

        [Test]
        public void Mid_accepts_mid_only()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Mid, CondoLuxury.MidLoftId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Base, WealthBand.Mid, CondoLuxury.BaseId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Mid, CondoLuxury.UpperStandardId));
        }

        [Test]
        public void Upper_accepts_family_and_all_upper()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Upper, CondoLuxury.MidFamilyId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Mid, WealthBand.Upper, CondoLuxury.MidStandardId));
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Upper, CondoLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_accepts_corner_and_penthouse_only()
        {
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Premium, CondoLuxury.UpperCornerId));
            Assert.IsTrue(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Premium, CondoLuxury.UpperPenthouseId));
            Assert.IsFalse(CondoLuxury.AcceptsBuyer(LuxuryBand.Upper, WealthBand.Premium, CondoLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_prefers_penthouse_over_corner()
        {
            Assert.Less(
                CondoLuxury.PremiumUnitPreferenceRank(WealthBand.Premium, CondoLuxury.UpperPenthouseId),
                CondoLuxury.PremiumUnitPreferenceRank(WealthBand.Premium, CondoLuxury.UpperCornerId));
        }

        [Test]
        public void Condo_catalog_assets_match_spec()
        {
            AssertCondo("Rooms/CondoStudio", "condo_studio", LuxuryBand.Base, 0, 4, 1, 35000, 65000);
            AssertCondo("Rooms/CondoAlcove", "condo_alcove", LuxuryBand.Base, 0, 5, 2, 45000, 85000);
            AssertCondo("Rooms/CondoBase", "condo_base", LuxuryBand.Base, 0, 8, 2, 80000, 150000);
            AssertCondo("Rooms/CondoMidStandard", "condo_mid_standard", LuxuryBand.Mid, 2, 10, 3, 120000, 200000);
            AssertCondo("Rooms/CondoMidLoft", "condo_mid_loft", LuxuryBand.Mid, 2, 12, 2, 140000, 230000);
            AssertCondo("Rooms/CondoMidFamily", "condo_mid_family", LuxuryBand.Mid, 2, 14, 4, 160000, 270000);
            AssertCondo("Rooms/CondoUpperStandard", "condo_upper_standard", LuxuryBand.Upper, 3, 12, 3, 180000, 300000);
            AssertCondo("Rooms/CondoUpperCorner", "condo_upper_corner", LuxuryBand.Upper, 3, 14, 4, 220000, 360000);
            AssertCondo("Rooms/CondoUpperPenthouse", "condo_upper_penthouse", LuxuryBand.Upper, 3, 18, 4, 280000, 450000);
        }

        [Test]
        public void Office_placeholder_colors_are_not_identical()
        {
            var colors = new[]
            {
                Resources.Load<RoomTypeSO>("Rooms/OfficeMicro").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeStudio").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeBase").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeMidStandard").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeMidClinic").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeMidTeam").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeUpperStandard").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeUpperCorner").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/OfficeUpperFloor").placeholderColor,
            };
            var distinct = new System.Collections.Generic.HashSet<Color>();
            foreach (var c in colors)
                distinct.Add(c);
            Assert.GreaterOrEqual(distinct.Count, 7, "Offices should vary placeholderColor within the blue family");
        }

        [Test]
        public void Condo_placeholder_colors_are_not_identical()
        {
            var colors = new[]
            {
                Resources.Load<RoomTypeSO>("Rooms/CondoStudio").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoAlcove").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoBase").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoMidStandard").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoMidLoft").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoMidFamily").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoUpperStandard").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoUpperCorner").placeholderColor,
                Resources.Load<RoomTypeSO>("Rooms/CondoUpperPenthouse").placeholderColor,
            };
            var distinct = new System.Collections.Generic.HashSet<Color>();
            foreach (var c in colors)
                distinct.Add(c);
            Assert.GreaterOrEqual(distinct.Count, 7, "Condos should vary placeholderColor within the green family");
        }

        static void AssertCondo(string path, string id, LuxuryBand band, int stars, int width, int occ, int build, int sale)
        {
            var so = Resources.Load<RoomTypeSO>(path);
            Assert.IsNotNull(so, path);
            Assert.AreEqual(id, so.id);
            Assert.AreEqual(RoomCategory.Condo, so.category);
            Assert.AreEqual(band, so.luxuryBand);
            Assert.AreEqual(stars, so.requiredStars);
            Assert.AreEqual(width, so.size.x);
            Assert.AreEqual(1, so.size.y);
            Assert.AreEqual(occ, so.maxOccupants);
            Assert.AreEqual(build, so.buildCost);
            Assert.AreEqual(sale, so.baseIncome);
            Assert.AreEqual(IncomeModel.UpfrontSale, so.incomeModel);
        }
    }
}
