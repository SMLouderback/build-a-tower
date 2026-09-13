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
    }
}
