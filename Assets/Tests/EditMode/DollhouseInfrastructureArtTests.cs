using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class DollhouseInfrastructureArtTests
    {
        [TearDown]
        public void TearDown() => DollhouseInfrastructureArt.ResetForTests();

        [Test]
        public void GetSprite_matches_cell_canvas_and_has_floor_band()
        {
            var sprite = DollhouseInfrastructureArt.GetSprite(new Vector2Int(3, 1), true, true);
            Assert.IsNotNull(sprite);
            var tex = sprite.texture;
            Assert.AreEqual(3 * (int)RoomDollhouseArt.PixelsPerUnit, tex.width);
            Assert.AreEqual(1 * (int)RoomDollhouseArt.PixelsPerUnit, tex.height);

            var floor = tex.GetPixel(tex.width / 2, 2);
            Assert.Greater(floor.a, 0.9f);
            Assert.Less(ColorDistance(floor, DollhouseInfrastructureArt.Cement), 0.2f);

            // Center of cell (window region) stays clear so sky can show.
            var mid = tex.GetPixel(tex.width / 2, tex.height / 2);
            Assert.Less(mid.a, 0.1f);
        }

        [Test]
        public void Interior_sides_paint_party_walls()
        {
            var bothExt = DollhouseInfrastructureArt.GetSprite(new Vector2Int(4, 1), true, true);
            var neither = DollhouseInfrastructureArt.GetSprite(new Vector2Int(4, 1), false, false);
            var leftEdgeExt = bothExt.texture.GetPixel(1, bothExt.texture.height / 2);
            var leftEdgeInt = neither.texture.GetPixel(1, neither.texture.height / 2);
            Assert.Less(leftEdgeExt.a, 0.1f);
            Assert.Greater(leftEdgeInt.a, 0.9f);
        }

        [Test]
        public void SortingOrder_is_below_dollhouse_overlay()
        {
            Assert.Less(DollhouseInfrastructureArt.SortingOrder, RoomDollhouseArt.SortingOrder);
        }

        static float ColorDistance(Color a, Color b)
        {
            var dr = a.r - b.r;
            var dg = a.g - b.g;
            var db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
