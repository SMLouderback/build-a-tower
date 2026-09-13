using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class MenuIconArtTests
    {
        [SetUp]
        public void SetUp() => MenuIconArt.ResetCache();

        [TearDown]
        public void TearDown() => MenuIconArt.ResetCache();

        [Test]
        public void TryGetTexture_missing_id_returns_false()
        {
            var ok = MenuIconArt.TryGetTexture("menu_icon_definitely_missing_abc123", out var tex);

            Assert.IsFalse(ok);
            Assert.IsNull(tex);
        }

        [Test]
        public void ResetCache_does_not_throw()
        {
            MenuIconArt.TryGetTexture("menu_icon_definitely_missing_abc123", out _);

            Assert.DoesNotThrow(() => MenuIconArt.ResetCache());
            Assert.DoesNotThrow(() => MenuIconArt.ResetCache());
        }

        [Test]
        public void TryGetTexture_known_icon_keys_magenta_and_crops()
        {
            Assert.IsTrue(MenuIconArt.TryGetTexture("tool_bulldoze", out var tex));
            Assert.IsNotNull(tex);
            Assert.AreEqual(MenuIconArt.OutputPixels, tex.width);
            Assert.AreEqual(MenuIconArt.OutputPixels, tex.height);

            var px = tex.GetPixels();
            var opaque = 0;
            var hotMagenta = 0;
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 0.08f) continue;
                opaque++;
                if (MenuIconArt.IsHotMagenta(c))
                    hotMagenta++;
            }

            Assert.Greater(opaque, px.Length / 4);
            Assert.Less(hotMagenta, opaque * 0.02f + 8);
        }

        [Test]
        public void IsHotMagenta_catches_hot_pink_plate()
        {
            // Observed menu plate ~R237 G10 B126
            var plate = new Color(237f / 255f, 10f / 255f, 126f / 255f, 1f);
            Assert.IsTrue(MenuIconArt.IsHotMagenta(plate));
            Assert.IsFalse(MenuIconArt.IsHotMagenta(new Color(0.72f, 0.55f, 0.28f, 1f))); // wood
        }
    }
}
