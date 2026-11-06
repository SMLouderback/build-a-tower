using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildMenuPopoutLayoutTests
    {
        [Test]
        public void ChooseSide_prefers_right_when_space()
        {
            var dock = new Rect(500f, 100f, 100f, 300f);
            var side = BuildMenuPopoutLayout.ChooseSide(dock, new Vector2(160f, 280f), screenWidth: 800f);
            Assert.AreEqual(BuildMenuPopoutSide.Right, side);
        }

        [Test]
        public void ChooseSide_flips_left_when_right_clips()
        {
            var dock = new Rect(700f, 100f, 100f, 300f);
            var side = BuildMenuPopoutLayout.ChooseSide(dock, new Vector2(160f, 280f), screenWidth: 800f);
            Assert.AreEqual(BuildMenuPopoutSide.Left, side);
        }

        [Test]
        public void Place_right_and_left_rects()
        {
            var dock = new Rect(600f, 80f, 100f, 200f);
            var size = new Vector2(150f, 180f);
            var right = BuildMenuPopoutLayout.Place(dock, size, BuildMenuPopoutSide.Right, gap: 4f);
            Assert.AreEqual(704f, right.x, 0.01f);
            Assert.AreEqual(80f, right.y, 0.01f);
            Assert.AreEqual(150f, right.width, 0.01f);

            var left = BuildMenuPopoutLayout.Place(dock, size, BuildMenuPopoutSide.Left, gap: 4f);
            Assert.AreEqual(600f - 4f - 150f, left.x, 0.01f);
        }
    }
}
