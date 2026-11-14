using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SkyCloudLayerTests
    {
        [Test]
        public void FeatherCloudAlpha_softens_hard_silhouette_edges()
        {
            const int w = 32;
            const int h = 32;
            var src = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                // Hard square block in the middle — pre-feather cloud mask shape.
                var inside = x >= 8 && x < 24 && y >= 8 && y < 24;
                px[y * w + x] = inside ? Color.white : Color.clear;
            }

            src.SetPixels(px);
            src.Apply(false, false);

            var soft = SkyCloudLayer.FeatherCloudAlpha(src, radius: 4);
            Assert.IsNotNull(soft);

            // Corner of the hard square should no longer be fully opaque.
            var edge = soft.GetPixel(8, 8);
            Assert.Less(edge.a, 0.95f);
            Assert.Greater(edge.a, 0.02f);

            // Core stays solid.
            var core = soft.GetPixel(16, 16);
            Assert.Greater(core.a, 0.85f);

            Object.DestroyImmediate(soft);
        }

        [Test]
        public void KeyCloudMagentaPlate_clears_hot_magenta_and_keeps_white()
        {
            const int w = 16;
            const int h = 16;
            var src = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (var i = 0; i < px.Length; i++)
                px[i] = new Color(1f, 0f, 1f, 1f);
            px[5 * w + 5] = Color.white;
            src.SetPixels(px);
            src.Apply(false, false);

            var keyed = SkyCloudLayer.KeyCloudMagentaPlate(src, destroySource: true);
            Assert.IsNotNull(keyed);
            Assert.Greater(keyed.width, 1);
            Assert.Greater(keyed.height, 1);

            var foundWhite = false;
            var keyedPx = keyed.GetPixels();
            for (var i = 0; i < keyedPx.Length; i++)
            {
                Assert.IsFalse(MenuIconArt.IsHotMagenta(keyedPx[i]));
                if (keyedPx[i].a > 0.5f && keyedPx[i].r > 0.9f)
                    foundWhite = true;
            }

            Assert.IsTrue(foundWhite);
            Object.DestroyImmediate(keyed);
        }
    }
}
