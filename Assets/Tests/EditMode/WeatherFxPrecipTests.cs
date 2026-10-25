using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class WeatherFxPrecipTests
    {
        [Test]
        public void MinViewNormY_clips_at_street_ground()
        {
            // Camera centered at y=10, half-height 10 → view [-0 .. 20]. Ground at 0 → n=0.
            Assert.AreEqual(0f, WeatherFx.MinViewNormY(cameraY: 10f, orthoHalfHeight: 10f, groundWorldY: 0f), 0.001f);

            // Camera centered at 0, half 10 → view [-10 .. 10]. Ground at 0 → n=0.5.
            Assert.AreEqual(0.5f, WeatherFx.MinViewNormY(0f, 10f, 0f), 0.001f);

            // Deep basement: camera at -20, half 10 → view [-30 .. -10]. Ground above view → 1.
            Assert.AreEqual(1f, WeatherFx.MinViewNormY(-20f, 10f, 0f), 0.001f);
        }

        [Test]
        public void WorldYFromNorm_matches_camera_space()
        {
            Assert.AreEqual(
                0f,
                WeatherFx.WorldYFromNorm(cameraY: 0f, orthoHalfHeight: 10f, normY: 0.5f),
                0.001f);
            Assert.AreEqual(
                -10f,
                WeatherFx.WorldYFromNorm(cameraY: 0f, orthoHalfHeight: 10f, normY: 0f),
                0.001f);
        }

        [Test]
        public void ShouldHidePrecip_when_ground_at_or_above_view_top()
        {
            Assert.IsTrue(WeatherFx.ShouldHidePrecip(minNormY: 1f));
            Assert.IsTrue(WeatherFx.ShouldHidePrecip(minNormY: 0.995f));
            Assert.IsFalse(WeatherFx.ShouldHidePrecip(minNormY: 0.5f));
        }
    }
}
