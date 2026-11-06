using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Pure cloud look: coverage by weather, albedo (puffy white vs storm grey), and
    /// sunrise/sunset warmth matching real sunlit cloud color shifts.
    /// </summary>
    public static class SkyCloudStyle
    {
        public static readonly Color DayPuff = new(0.97f, 0.98f, 1f, 1f);
        public static readonly Color DayOvercast = new(0.78f, 0.80f, 0.84f, 1f);
        public static readonly Color StormGrey = new(0.42f, 0.45f, 0.50f, 1f);
        public static readonly Color StormDark = new(0.28f, 0.30f, 0.34f, 1f);
        public static readonly Color SunriseLit = new(1f, 0.62f, 0.42f, 1f);
        public static readonly Color SunsetLit = new(1f, 0.48f, 0.32f, 1f);
        public static readonly Color SunsetAfterglow = new(0.72f, 0.38f, 0.55f, 1f);
        public static readonly Color NightCloud = new(0.12f, 0.14f, 0.22f, 1f);

        /// <summary>0 = none, 1 = full bank. Clear keeps a light fair-weather bank.</summary>
        public static float Coverage(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Clear: return 0.22f;
                case WeatherKind.PartlyCloudy: return 0.48f;
                case WeatherKind.Cloudy: return 0.82f;
                case WeatherKind.Rain:
                case WeatherKind.Snow: return 0.92f;
                case WeatherKind.Thunderstorm:
                case WeatherKind.Blizzard: return 1f;
                default: return 0.22f;
            }
        }

        /// <summary>0 = bright puff, 1 = darkest storm body (before sun warmth).</summary>
        public static float Storminess(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.PartlyCloudy: return 0.05f;
                case WeatherKind.Cloudy: return 0.35f;
                case WeatherKind.Rain:
                case WeatherKind.Snow: return 0.72f;
                case WeatherKind.Thunderstorm:
                case WeatherKind.Blizzard: return 0.95f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 0 at night/day, peaks near 1 at sunrise and sunset peaks (real-world sunlit clouds).
        /// </summary>
        public static float SunWarmth01(int minuteOfDay)
        {
            var m = ((minuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) %
                    GameClock.MinutesPerDay;

            if (m < DayNightSky.NightEnd || m >= DayNightSky.NightStart)
                return 0f;

            // Dawn ramp → peak → fall into day.
            if (m < DayNightSky.SunrisePeak)
                return Smooth01(InverseLerp(DayNightSky.NightEnd, DayNightSky.SunrisePeak, m));
            if (m < DayNightSky.DayStart)
                return Smooth01(1f - InverseLerp(DayNightSky.SunrisePeak, DayNightSky.DayStart, m));

            // Day: no warm wash on white puffs.
            if (m < DayNightSky.DayEnd)
                return 0f;

            if (m < DayNightSky.SunsetPeak)
                return Smooth01(InverseLerp(DayNightSky.DayEnd, DayNightSky.SunsetPeak, m));
            return Smooth01(1f - InverseLerp(DayNightSky.SunsetPeak, DayNightSky.NightStart, m));
        }

        public static Color WarmTintAtMinute(int minuteOfDay)
        {
            var m = ((minuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) %
                    GameClock.MinutesPerDay;

            if (m < DayNightSky.DayStart)
                return SunriseLit;
            if (m < DayNightSky.SunsetPeak)
                return SunsetLit;
            // Late dusk afterglow pulls slightly magenta/purple like real high clouds.
            return Color.Lerp(
                SunsetLit,
                SunsetAfterglow,
                InverseLerp(DayNightSky.SunsetPeak, DayNightSky.NightStart, m));
        }

        public static Color Albedo(WeatherKind kind)
        {
            var s = Storminess(kind);
            if (s <= 0.2f)
                return Color.Lerp(DayPuff, DayOvercast, s / 0.2f);
            if (s <= 0.75f)
                return Color.Lerp(DayOvercast, StormGrey, (s - 0.2f) / 0.55f);
            return Color.Lerp(StormGrey, StormDark, (s - 0.75f) / 0.25f);
        }

        /// <summary>Final tinted cloud RGB (alpha left at 1; layer applies coverage alpha).</summary>
        public static Color ColorAtMinute(int minuteOfDay, WeatherKind kind)
        {
            var albedo = Albedo(kind);
            var warmth = SunWarmth01(minuteOfDay);
            var storm = Storminess(kind);

            // Storms still catch sunset fire on edges, but muted vs fair-weather puffs.
            var warmthAmt = warmth * Mathf.Lerp(1f, 0.45f, storm);
            var lit = Color.Lerp(albedo, MultiplyToward(albedo, WarmTintAtMinute(minuteOfDay)), warmthAmt);

            // Night: sink toward deep blue-grey when warmth is gone and sky is night.
            if (IsNightMinute(minuteOfDay) && warmthAmt <= 0f && Coverage(kind) > 0f)
                lit = Color.Lerp(lit, NightCloud, 0.85f);

            lit.a = 1f;
            return lit;
        }

        public static Color ColorAt(GameClock clock, WeatherKind kind) =>
            ColorAtMinute(clock != null ? clock.MinuteOfDay : DayNightSky.DayStart + 60, kind);

        /// <summary>Per-sprite opacity scale from coverage (layer may multiply further).</summary>
        public static float Opacity(WeatherKind kind)
        {
            var c = Coverage(kind);
            if (c <= 0f) return 0f;
            return Mathf.Lerp(0.55f, 0.92f, c);
        }

        static bool IsNightMinute(int minuteOfDay)
        {
            var m = ((minuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) %
                    GameClock.MinutesPerDay;
            return m < DayNightSky.NightEnd || m >= DayNightSky.NightStart;
        }

        static Color MultiplyToward(Color baseCol, Color tint)
        {
            return new Color(
                Mathf.Clamp01(baseCol.r * Mathf.Lerp(1f, tint.r * 1.15f, 0.85f)),
                Mathf.Clamp01(baseCol.g * Mathf.Lerp(1f, tint.g * 1.05f, 0.85f)),
                Mathf.Clamp01(baseCol.b * Mathf.Lerp(1f, tint.b * 0.95f, 0.85f)),
                1f);
        }

        static float InverseLerp(int a, int b, int value) =>
            Mathf.Clamp01((value - a) / (float)(b - a));

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
