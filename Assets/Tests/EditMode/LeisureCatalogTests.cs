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
        public void BuildFamily_Leisure_is_enum_value_7()
        {
            Assert.AreEqual(7, (int)BuildFamily.Leisure);
        }
    }
}
