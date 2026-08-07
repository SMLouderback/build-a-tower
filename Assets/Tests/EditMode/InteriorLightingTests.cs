using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class InteriorLightingTests
    {
        [Test]
        public void Apply_night_is_darker_than_day_for_same_base()
        {
            var baseColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            var day = InteriorLighting.Apply(baseColor, 12 * 60, subterranean: false);
            var night = InteriorLighting.Apply(baseColor, 0, subterranean: false);

            Assert.Less(night.grayscale, day.grayscale);
        }

        [Test]
        public void Apply_subterranean_weakens_night_shift()
        {
            var baseColor = Color.white;
            var aboveNight = InteriorLighting.Apply(baseColor, 0, subterranean: false);
            var belowNight = InteriorLighting.Apply(baseColor, 0, subterranean: true);

            // Basement stays closer to fluorescent (brighter / less cool) than open night.
            Assert.Greater(belowNight.grayscale, aboveNight.grayscale);
        }
    }
}
