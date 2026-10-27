using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class WeatherSkyTintTests
    {
        static float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        static float Chroma(Color c) =>
            Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));

        [Test]
        public void Clear_is_identity()
        {
            var src = DayNightSky.Day;
            var tinted = DayNightSky.ApplyWeather(src, WeatherKind.Clear);
            Assert.AreEqual(src.r, tinted.r, 1e-5f);
            Assert.AreEqual(src.g, tinted.g, 1e-5f);
            Assert.AreEqual(src.b, tinted.b, 1e-5f);
            Assert.AreEqual(src.a, tinted.a, 1e-5f);
        }

        [Test]
        public void Rain_is_darker_and_less_saturated_than_clear()
        {
            var src = DayNightSky.Day;
            var rain = DayNightSky.ApplyWeather(src, WeatherKind.Rain);
            Assert.Less(Luma(rain), Luma(src));
            Assert.Less(Chroma(rain), Chroma(src));
            Assert.AreEqual(1f, rain.a, 1e-5f);
        }

        [Test]
        public void Thunderstorm_is_darker_than_rain()
        {
            var src = DayNightSky.Day;
            Assert.Less(
                Luma(DayNightSky.ApplyWeather(src, WeatherKind.Thunderstorm)),
                Luma(DayNightSky.ApplyWeather(src, WeatherKind.Rain)));
        }

        [Test]
        public void Cloudy_sits_between_clear_and_rain()
        {
            var src = DayNightSky.Day;
            var cloudy = Luma(DayNightSky.ApplyWeather(src, WeatherKind.Cloudy));
            Assert.Less(cloudy, Luma(src));
            Assert.Greater(cloudy, Luma(DayNightSky.ApplyWeather(src, WeatherKind.Rain)));
        }

        [Test]
        public void Snow_is_less_saturated_than_clear()
        {
            var src = DayNightSky.Day;
            Assert.Less(Chroma(DayNightSky.ApplyWeather(src, WeatherKind.Snow)), Chroma(src));
            Assert.Less(Chroma(DayNightSky.ApplyWeather(src, WeatherKind.Blizzard)), Chroma(src));
        }

        [Test]
        public void Lightning_pulse_brightens_toward_white()
        {
            var src = DayNightSky.Day;
            var storm = DayNightSky.ApplyWeather(src, WeatherKind.Thunderstorm);
            var flash = DayNightSky.ApplyWeather(src, WeatherKind.Thunderstorm, 1f);
            Assert.Greater(Luma(flash), Luma(storm));
            Assert.Greater(flash.r, 0.8f);
        }

        [Test]
        public void Zero_pulse_matches_no_pulse()
        {
            var a = DayNightSky.ApplyWeather(DayNightSky.Sunset, WeatherKind.Rain);
            var b = DayNightSky.ApplyWeather(DayNightSky.Sunset, WeatherKind.Rain, 0f);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Every_kind_keeps_channels_in_range_for_all_sky_colors()
        {
            foreach (WeatherKind kind in System.Enum.GetValues(typeof(WeatherKind)))
            foreach (var m in new[] { 0, 6 * 60, 12 * 60, 19 * 60 })
            foreach (var pulse in new[] { 0f, 1f })
            {
                var c = DayNightSky.ApplyWeather(DayNightSky.ColorAtMinute(m), kind, pulse);
                Assert.That(c.r, Is.InRange(0f, 1f));
                Assert.That(c.g, Is.InRange(0f, 1f));
                Assert.That(c.b, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void WeatherFx_profiles_match_kinds()
        {
            Assert.AreEqual(PrecipStyle.None, WeatherFx.ProfileFor(WeatherKind.Clear).Style);
            Assert.AreEqual(PrecipStyle.None, WeatherFx.ProfileFor(WeatherKind.Cloudy).Style);
            Assert.AreEqual(PrecipStyle.Rain, WeatherFx.ProfileFor(WeatherKind.Rain).Style);
            Assert.AreEqual(PrecipStyle.Snow, WeatherFx.ProfileFor(WeatherKind.Snow).Style);
            Assert.AreEqual(PrecipStyle.Rain, WeatherFx.ProfileFor(WeatherKind.Thunderstorm).Style);
            Assert.AreEqual(PrecipStyle.Snow, WeatherFx.ProfileFor(WeatherKind.Blizzard).Style);

            Assert.IsTrue(WeatherFx.ProfileFor(WeatherKind.Thunderstorm).Lightning);
            Assert.IsFalse(WeatherFx.ProfileFor(WeatherKind.Rain).Lightning);
            Assert.Greater(
                WeatherFx.ProfileFor(WeatherKind.Blizzard).Density,
                WeatherFx.ProfileFor(WeatherKind.Snow).Density);
        }

        [Test]
        public void WeatherFx_lightning_envelope_is_bounded_and_peaks()
        {
            Assert.AreEqual(0f, WeatherFx.LightningEnvelope(-1f));
            Assert.AreEqual(0f, WeatherFx.LightningEnvelope(10f));
            var max = 0f;
            for (var t = 0f; t < 0.6f; t += 0.01f)
            {
                var v = WeatherFx.LightningEnvelope(t);
                Assert.That(v, Is.InRange(0f, 1f));
                max = Mathf.Max(max, v);
            }

            Assert.Greater(max, 0.9f);
        }
    }
}
