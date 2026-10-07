using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Maps game clock minutes to a sky <see cref="Camera.backgroundColor"/> with
    /// night, sunrise, day, and sunset transitions.
    /// </summary>
    public static class DayNightSky
    {
        // Approximate key times (minutes from midnight).
        public const int NightEnd = 5 * 60;          // 05:00
        public const int SunrisePeak = 6 * 60 + 15;  // 06:15
        public const int DayStart = 7 * 60 + 30;     // 07:30
        public const int DayEnd = 18 * 60;           // 18:00
        public const int SunsetPeak = 19 * 60;       // 19:00
        public const int NightStart = 20 * 60 + 30;  // 20:30

        public static readonly Color Night = new(0.05f, 0.07f, 0.18f, 1f);
        public static readonly Color Sunrise = new(0.95f, 0.45f, 0.28f, 1f);
        // Brighter cutaway day blue (locked band ~0.42–0.55 / 0.70–0.78 / 0.92–0.95).
        public static readonly Color Day = new(0.48f, 0.74f, 0.94f, 1f);
        public static readonly Color Sunset = new(0.92f, 0.38f, 0.22f, 1f);

        public static Color ColorAtMinute(int minuteOfDay)
        {
            var m = ((minuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) %
                    GameClock.MinutesPerDay;

            if (m < NightEnd)
                return Night;
            if (m < SunrisePeak)
                return Color.Lerp(Night, Sunrise, InverseLerp(NightEnd, SunrisePeak, m));
            if (m < DayStart)
                return Color.Lerp(Sunrise, Day, InverseLerp(SunrisePeak, DayStart, m));
            if (m < DayEnd)
                return Day;
            if (m < SunsetPeak)
                return Color.Lerp(Day, Sunset, InverseLerp(DayEnd, SunsetPeak, m));
            if (m < NightStart)
                return Color.Lerp(Sunset, Night, InverseLerp(SunsetPeak, NightStart, m));
            return Night;
        }

        public static Color ColorAt(GameClock clock) =>
            clock == null ? Day : ColorAtMinute(clock.MinuteOfDay);

        /// <summary>Peak white blend applied at lightning pulse = 1.</summary>
        public const float LightningMaxBlend = 0.85f;

        /// <summary>Per-kind tint knobs. <see cref="WeatherKind.Clear"/> is (0, 1, 0) = identity.</summary>
        public static void WeatherTintParams(
            WeatherKind kind, out float desaturate, out float brightness, out float lift)
        {
            // lift: blend toward a pale grey-white (snow overcast) after desaturation/brightness.
            switch (kind)
            {
                case WeatherKind.PartlyCloudy:
                    desaturate = 0.12f; brightness = 0.96f; lift = 0f; break;
                case WeatherKind.Cloudy:
                    desaturate = 0.40f; brightness = 0.82f; lift = 0f; break;
                case WeatherKind.Rain:
                    desaturate = 0.55f; brightness = 0.62f; lift = 0f; break;
                case WeatherKind.Snow:
                    desaturate = 0.55f; brightness = 0.85f; lift = 0.18f; break;
                case WeatherKind.Thunderstorm:
                    desaturate = 0.65f; brightness = 0.40f; lift = 0f; break;
                case WeatherKind.Blizzard:
                    desaturate = 0.75f; brightness = 0.78f; lift = 0.30f; break;
                default:
                    desaturate = 0f; brightness = 1f; lift = 0f; break;
            }
        }

        /// <summary>
        /// Weather tint for the sky: desaturate toward luma grey, scale brightness, optionally
        /// lift toward pale overcast, then blend toward white for a lightning pulse (0..1).
        /// <see cref="WeatherKind.Clear"/> with no pulse returns the input unchanged. Alpha is preserved.
        /// </summary>
        public static Color ApplyWeather(
            Color baseColor, WeatherKind kind, float lightningPulse01 = 0f)
        {
            WeatherTintParams(kind, out var desat, out var bright, out var lift);

            var result = baseColor;
            if (desat > 0f || bright < 1f || lift > 0f)
            {
                var luma = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
                var r = Mathf.Lerp(baseColor.r, luma, desat) * bright;
                var g = Mathf.Lerp(baseColor.g, luma, desat) * bright;
                var b = Mathf.Lerp(baseColor.b, luma, desat) * bright;
                if (lift > 0f)
                {
                    const float pale = 0.82f;
                    r = Mathf.Lerp(r, pale, lift);
                    g = Mathf.Lerp(g, pale, lift);
                    b = Mathf.Lerp(b, pale, lift);
                }

                result = new Color(r, g, b, baseColor.a);
            }

            var pulse = Mathf.Clamp01(lightningPulse01);
            if (pulse > 0f)
            {
                var k = pulse * LightningMaxBlend;
                result = new Color(
                    Mathf.Lerp(result.r, 1f, k),
                    Mathf.Lerp(result.g, 1f, k),
                    Mathf.Lerp(result.b, 1f, k),
                    result.a);
            }

            return result;
        }

        static float InverseLerp(int a, int b, int value) =>
            Mathf.Clamp01((value - a) / (float)(b - a));
    }
}
