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
        public static readonly Color Day = new(0.42f, 0.70f, 0.92f, 1f);
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

        static float InverseLerp(int a, int b, int value) =>
            Mathf.Clamp01((value - a) / (float)(b - a));
    }
}
