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
            Assert.IsTrue(MenuIconArt.TryGetTexture("tool_select", out var tex));
            Assert.IsNotNull(tex);

            var px = tex.GetPixels();
            var opaque = 0;
            var hotMagenta = 0;
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 0.08f) continue;
                opaque++;
                if (c.r > 0.70f && c.b > 0.55f && c.g < 0.28f)
                    hotMagenta++;
            }

            Assert.Greater(opaque, 0);
            // After keying, remaining hot-magenta fringe should be tiny.
            Assert.Less(hotMagenta, opaque * 0.02f + 8);
        }
    }
}
