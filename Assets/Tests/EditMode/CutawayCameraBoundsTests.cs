using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CutawayCameraBoundsTests
    {
        [Test]
        public void Horizontal_padding_grows_with_viewport_to_clear_hud()
        {
            CutawayCamera.ComputeScrollableBounds(
                gridMinX: 0f,
                gridMaxX: 20f,
                towerMinY: 0f,
                towerMaxY: 10f,
                viewportWidth: 40f,
                viewportHeight: 20f,
                out var minX,
                out var maxX,
                out _,
                out _);

            var expectedPad = Mathf.Max(
                CutawayCamera.MinHorizontalPadding,
                40f * CutawayCamera.HorizontalViewportPadFraction);
            // Fallback playfield may extend further; pad is a minimum clearance past the tower.
            Assert.LessOrEqual(minX, 0f - expectedPad);
            Assert.GreaterOrEqual(maxX, 20f + expectedPad);
            Assert.Greater(expectedPad, 5f);
        }

        [Test]
        public void Vertical_max_lets_top_floors_sit_at_bottom_of_view()
        {
            const float towerTop = 30f;
            const float viewportHeight = 16f;

            CutawayCamera.ComputeScrollableBounds(
                0f, 10f, 0f, towerTop, 20f, viewportHeight,
                out _, out _, out var minY, out var maxY);

            // Fully scrolled up: bottom of viewport = maxY - viewportHeight
            // should land at towerTop - MinVisibleTopFloors.
            Assert.AreEqual(
                towerTop - CutawayCamera.MinVisibleTopFloors,
                maxY - viewportHeight,
                0.001f);
            Assert.Less(minY, 0f);
        }

        [Test]
        public void Narrow_tower_still_keeps_wide_fallback_playfield()
        {
            CutawayCamera.ComputeScrollableBounds(
                0f, 4f, 0f, 2f, 10f, 10f,
                out var minX, out var maxX, out _, out _);

            Assert.LessOrEqual(minX, -80f);
            Assert.GreaterOrEqual(maxX, 100f);
        }
    }
}
