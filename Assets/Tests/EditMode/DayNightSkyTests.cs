using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class DayNightSkyTests
    {
        [Test]
        public void ColorAtMinute_is_day_at_noon()
        {
            var noon = DayNightSky.ColorAtMinute(12 * 60);
            Assert.AreEqual(DayNightSky.Day.r, noon.r, 0.001f);
            Assert.AreEqual(DayNightSky.Day.g, noon.g, 0.001f);
            Assert.AreEqual(DayNightSky.Day.b, noon.b, 0.001f);
        }

        [Test]
        public void ColorAtMinute_is_night_at_midnight()
        {
            var midnight = DayNightSky.ColorAtMinute(0);
            Assert.AreEqual(DayNightSky.Night.r, midnight.r, 0.001f);
            Assert.AreEqual(DayNightSky.Night.g, midnight.g, 0.001f);
            Assert.AreEqual(DayNightSky.Night.b, midnight.b, 0.001f);
        }

        [Test]
        public void ColorAtMinute_sunrise_is_between_night_and_day()
        {
            var sunrise = DayNightSky.ColorAtMinute(DayNightSky.SunrisePeak);
            Assert.AreEqual(DayNightSky.Sunrise.r, sunrise.r, 0.001f);
            Assert.Greater(sunrise.r, DayNightSky.Night.r);
            Assert.Greater(sunrise.r, DayNightSky.Day.r * 0.5f);
        }

        [Test]
        public void ColorAtMinute_sunset_transitions_toward_night()
        {
            var midSunset = DayNightSky.ColorAtMinute((DayNightSky.SunsetPeak + DayNightSky.NightStart) / 2);
            Assert.Less(midSunset.b, DayNightSky.Day.b);
            Assert.Greater(midSunset.r, DayNightSky.Night.r);
        }

        [Test]
        public void ColorAt_uses_clock_minute()
        {
            var clock = new GameClock(1f, 12 * 60);
            Assert.AreEqual(DayNightSky.Day.r, DayNightSky.ColorAt(clock).r, 0.001f);

            clock = new GameClock(1f, 0);
            Assert.AreEqual(DayNightSky.Night.r, DayNightSky.ColorAt(clock).r, 0.001f);
        }
    }
}
