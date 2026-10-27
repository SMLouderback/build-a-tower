using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class OfficeCondemnedOverlayTests
    {
        [Test]
        public void BrokenTileTint_lerps_white_toward_grey()
        {
            var tint = OfficeCondemnedOverlay.BrokenTileTint;
            Assert.Less(tint.r, 1f);
            Assert.Less(tint.g, 1f);
            Assert.Less(tint.b, 1f);
            Assert.AreEqual(1f, tint.a);
        }

        [Test]
        public void PixelSize_scales_with_room_footprint()
        {
            var (w, h) = OfficeCondemnedOverlay.PixelSize(9, 1);
            Assert.AreEqual(288, w);
            Assert.AreEqual(32, h);
        }

        [Test]
        public void GetSprite_returns_non_null_for_valid_size()
        {
            var sprite = OfficeCondemnedOverlay.GetSprite(6, 1);
            Assert.NotNull(sprite);
            Assert.Greater(sprite.texture.width, 0);
            Assert.Greater(sprite.texture.height, 0);
        }
    }
}
