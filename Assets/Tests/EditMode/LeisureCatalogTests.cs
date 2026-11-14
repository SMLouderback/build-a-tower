using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class LeisureCatalogTests
    {
        static RoomTypeSO Load(string resourceName) =>
            Resources.Load<RoomTypeSO>("Rooms/" + resourceName);

        [Test]
        public void Leisure_assets_match_spec_footprints_and_stars()
        {
            AssertRoom(Load("LeisureGym"), "leisure_gym", 6, 1, 2, true);
            AssertRoom(Load("LeisureSpa"), "leisure_spa", 6, 1, 5, false);
            AssertRoom(Load("LeisurePool"), "leisure_pool", 8, 2, 3, true);
            AssertRoom(Load("LeisureBowling"), "leisure_bowling", 10, 1, 3, true);
            AssertRoom(Load("LeisureTheater"), "leisure_theater", 8, 2, 4, true);
        }

        static void AssertRoom(RoomTypeSO so, string id, int w, int h, int stars, bool basement)
        {
            Assert.IsNotNull(so, id);
            Assert.AreEqual(id, so.id);
            Assert.AreEqual(new Vector2Int(w, h), so.size);
            Assert.AreEqual(stars, so.requiredStars);
            Assert.AreEqual(BuildFamily.Leisure, so.ResolvedBuildFamily());
            Assert.AreEqual(IncomeModel.TrafficVariable, so.incomeModel);
            Assert.AreEqual(basement, so.allowBasement);
            Assert.IsTrue(so.allowAboveGround);
        }

        [Test]
        public void New_leisure_attractions_match_spec()
        {
            AssertRoom(Load("LeisureCasino"), "leisure_casino", 10, 1, 4, true);
            AssertRoom(Load("LeisureNightclub"), "leisure_nightclub", 8, 1, 3, true);
            AssertRoom(Load("LeisureChapel"), "leisure_chapel", 6, 1, 2, true);

            var atrium = Load("LeisureAtrium");
            Assert.IsNotNull(atrium);
            Assert.AreEqual("leisure_atrium", atrium.id);
            Assert.AreEqual(new Vector2Int(8, 3), atrium.size);
            Assert.AreEqual(3, atrium.requiredStars);
            Assert.AreEqual(IncomeModel.None, atrium.incomeModel);
            Assert.IsTrue(atrium.isAtrium);
            Assert.AreEqual(0, atrium.maxOccupants);
            Assert.IsFalse(ShopVisitRules.IsTrafficVenue(atrium));
        }

        [Test]
        public void Casino_nightclub_chapel_noise_and_street_weights()
        {
            var casino = Load("LeisureCasino");
            Assert.AreEqual(0.85f, casino.noiseOutput, 0.001f);
            Assert.AreEqual(1.5f, casino.streetVisitWeight, 0.001f);
            var club = Load("LeisureNightclub");
            Assert.AreEqual(0.90f, club.noiseOutput, 0.001f);
            Assert.AreEqual(1.75f, club.streetVisitWeight, 0.001f);
            var chapel = Load("LeisureChapel");
            Assert.AreEqual(0.15f, chapel.noiseOutput, 0.001f);
        }

        [Test]
        public void New_leisure_attractions_dwell_ranges()
        {
            Assert.AreEqual((40, 70), ShopVisitRules.DwellRangeForId("leisure_casino"));
            Assert.AreEqual((50, 90), ShopVisitRules.DwellRangeForId("leisure_nightclub"));
            Assert.AreEqual((20, 40), ShopVisitRules.DwellRangeForId("leisure_chapel"));
        }

        [Test]
        public void BuildFamily_Leisure_is_enum_value_7()
        {
            Assert.AreEqual(7, (int)BuildFamily.Leisure);
        }
    }
}
