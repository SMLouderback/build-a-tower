using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Soft interior multiply/shift by time of day. Basements keep weaker exterior influence.
    /// Key times match <see cref="DayNightSky"/>.
    /// </summary>
    public static class InteriorLighting
    {
        public static readonly Color DayLight = new(1f, 1f, 1f, 1f);
        public static readonly Color NightLight = new(0.52f, 0.56f, 0.72f, 1f);
        public static readonly Color SunriseLight = new(1.05f, 0.88f, 0.74f, 1f);
        public static readonly Color SunsetLight = new(1.05f, 0.80f, 0.68f, 1f);
        public static readonly Color Fluorescent = new(0.86f, 0.90f, 0.84f, 1f);

        /// <summary>Exterior sky-influence factor for subterranean / parking cells.</summary>
        public const float SubterraneanSkyBlend = 0.22f;
        /// <summary>Softens exterior sky color for occupied above-ground interiors.</summary>
        public const float MidTowerInteriorSkyBlend = 0.62f;

        public static Color Apply(Color baseColor, int minuteOfDay, bool subterranean)
        {
            var skyBlend = subterranean ? SubterraneanSkyBlend : 1f;
            return Apply(baseColor, minuteOfDay, subterranean, skyBlend);
        }

        public static Color Apply(Color baseColor, int minuteOfDay, bool subterranean, float skyBlend)
        {
            var light = LightAtMinute(minuteOfDay);
            if (subterranean)
                light = Color.Lerp(Fluorescent, light, SubterraneanSkyBlend);
            else if (skyBlend < 0.999f)
                light = Color.Lerp(Fluorescent, light, Mathf.Clamp01(skyBlend));

            return new Color(
                Mathf.Clamp01(baseColor.r * light.r),
                Mathf.Clamp01(baseColor.g * light.g),
                Mathf.Clamp01(baseColor.b * light.b),
                baseColor.a);
        }

        public static Color LightAtMinute(int minuteOfDay)
        {
            var m = ((minuteOfDay % GameClock.MinutesPerDay) + GameClock.MinutesPerDay) %
                    GameClock.MinutesPerDay;

            if (m < DayNightSky.NightEnd)
                return NightLight;
            if (m < DayNightSky.SunrisePeak)
                return Color.Lerp(NightLight, SunriseLight,
                    InverseLerp(DayNightSky.NightEnd, DayNightSky.SunrisePeak, m));
            if (m < DayNightSky.DayStart)
                return Color.Lerp(SunriseLight, DayLight,
                    InverseLerp(DayNightSky.SunrisePeak, DayNightSky.DayStart, m));
            if (m < DayNightSky.DayEnd)
                return DayLight;
            if (m < DayNightSky.SunsetPeak)
                return Color.Lerp(DayLight, SunsetLight,
                    InverseLerp(DayNightSky.DayEnd, DayNightSky.SunsetPeak, m));
            if (m < DayNightSky.NightStart)
                return Color.Lerp(SunsetLight, NightLight,
                    InverseLerp(DayNightSky.SunsetPeak, DayNightSky.NightStart, m));
            return NightLight;
        }

        static float InverseLerp(int a, int b, int value) =>
            Mathf.Clamp01((value - a) / (float)(b - a));
    }
}
