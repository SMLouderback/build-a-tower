using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SkyCloudStyleTests
    {
        [Test]
        public void Clear_has_no_coverage()
        {
            Assert.AreEqual(0.22f, SkyCloudStyle.Coverage(WeatherKind.Clear));
            Assert.AreEqual(0f, SkyCloudStyle.Opacity(WeatherKind.Clear));
        }

        [Test]
        public void PartlyCloudy_is_bright_puff_vs_rain_storm_grey()
        {
            var noon = 12 * 60;
            var puff = SkyCloudStyle.ColorAtMinute(noon, WeatherKind.PartlyCloudy);
            var rain = SkyCloudStyle.ColorAtMinute(noon, WeatherKind.Rain);
            Assert.Greater(Luma(puff), Luma(rain));
            Assert.Greater(SkyCloudStyle.Coverage(WeatherKind.PartlyCloudy), 0f);
            Assert.Greater(SkyCloudStyle.Coverage(WeatherKind.Rain), SkyCloudStyle.Coverage(WeatherKind.PartlyCloudy));
            Assert.Greater(SkyCloudStyle.Storminess(WeatherKind.Rain), 0.5f);
            Assert.Less(SkyCloudStyle.Storminess(WeatherKind.PartlyCloudy), 0.2f);
        }

        [Test]
        public void Sunrise_and_sunset_warm_fair_weather_clouds()
        {
            var noon = SkyCloudStyle.ColorAtMinute(12 * 60, WeatherKind.PartlyCloudy);
            var sunrise = SkyCloudStyle.ColorAtMinute(DayNightSky.SunrisePeak, WeatherKind.PartlyCloudy);
            var sunset = SkyCloudStyle.ColorAtMinute(DayNightSky.SunsetPeak, WeatherKind.PartlyCloudy);

            Assert.Greater(SkyCloudStyle.SunWarmth01(DayNightSky.SunrisePeak), 0.9f);
            Assert.Greater(SkyCloudStyle.SunWarmth01(DayNightSky.SunsetPeak), 0.9f);
            Assert.AreEqual(0f, SkyCloudStyle.SunWarmth01(12 * 60), 0.001f);

            Assert.Greater(sunrise.r / Mathf.Max(0.01f, sunrise.b), noon.r / Mathf.Max(0.01f, noon.b));
            Assert.Greater(sunset.r / Mathf.Max(0.01f, sunset.b), noon.r / Mathf.Max(0.01f, noon.b));
        }

        [Test]
        public void Snow_and_blizzard_use_storm_greys()
        {
            Assert.Greater(SkyCloudStyle.Storminess(WeatherKind.Snow), 0.5f);
            Assert.Greater(SkyCloudStyle.Storminess(WeatherKind.Blizzard), SkyCloudStyle.Storminess(WeatherKind.Snow));
            var snow = SkyCloudStyle.ColorAtMinute(12 * 60, WeatherKind.Snow);
            var puff = SkyCloudStyle.ColorAtMinute(12 * 60, WeatherKind.PartlyCloudy);
            Assert.Less(Luma(snow), Luma(puff));
        }

        static float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
    }
}
