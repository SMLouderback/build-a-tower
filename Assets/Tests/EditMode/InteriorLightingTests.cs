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

        [Test]
        public void Apply_mid_tower_blend_softens_sky_without_replacing_basement_fluorescent()
        {
            var baseColor = Color.white;
            var openNight = InteriorLighting.Apply(baseColor, 0, subterranean: false);
            var midInterior = InteriorLighting.Apply(
                baseColor,
                0,
                subterranean: false,
                skyBlend: InteriorLighting.MidTowerInteriorSkyBlend);
            var basementDefault = InteriorLighting.Apply(baseColor, 0, subterranean: true);
            var basementWithParam = InteriorLighting.Apply(
                baseColor,
                0,
                subterranean: true,
                skyBlend: InteriorLighting.MidTowerInteriorSkyBlend);

            Assert.Greater(midInterior.grayscale, openNight.grayscale);
            Assert.Less(midInterior.grayscale, basementDefault.grayscale);
            AssertColorClose(basementDefault, basementWithParam);
        }

        static void AssertColorClose(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f);
            Assert.AreEqual(expected.g, actual.g, 0.001f);
            Assert.AreEqual(expected.b, actual.b, 0.001f);
            Assert.AreEqual(expected.a, actual.a, 0.001f);
        }
    }
}
