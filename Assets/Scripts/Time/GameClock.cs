using System;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Accelerated day clock. 1 real second ≈ <see cref="minutesPerRealSecond"/> game minutes.
    /// </summary>
    public sealed class GameClock
    {
        public const int MinutesPerDay = 24 * 60;

        float _minutesPerRealSecond;
        float _minuteAccumulator;

        public float MinutesPerRealSecond
        {
            get => _minutesPerRealSecond;
            set => _minutesPerRealSecond = Mathf.Max(0.01f, value);
        }

        public GameClock(float minutesPerRealSecond = 1f, int startMinuteOfDay = 6 * 60)
        {
            MinutesPerRealSecond = minutesPerRealSecond;
            MinuteOfDay = ((startMinuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
            DayIndex = 0;
        }

        public int MinuteOfDay { get; private set; }
        public int DayIndex { get; private set; }
        public int Hour => MinuteOfDay / 60;
        public int Minute => MinuteOfDay % 60;
        public bool Paused { get; set; }
        public float LastTickGameMinutes { get; private set; }

        public event Action DayRolled;

        public void Tick(float deltaTimeSeconds)
        {
            LastTickGameMinutes = 0f;
            if (Paused || deltaTimeSeconds <= 0f) return;
            LastTickGameMinutes = deltaTimeSeconds * _minutesPerRealSecond;
            AdvanceMinutes(LastTickGameMinutes);
        }

        public void AdvanceMinutes(float deltaMinutes)
        {
            if (deltaMinutes <= 0f) return;
            _minuteAccumulator += deltaMinutes;
            var whole = Mathf.FloorToInt(_minuteAccumulator);
            if (whole <= 0) return;
            _minuteAccumulator -= whole;

            MinuteOfDay += whole;
            while (MinuteOfDay >= MinutesPerDay)
            {
                MinuteOfDay -= MinutesPerDay;
                DayIndex++;
                DayRolled?.Invoke();
            }
        }

        public string FormatHud()
        {
            var dayName = WeekdayName(DayIndex);
            return $"{dayName} {Hour:00}:{Minute:00}";
        }

        static string WeekdayName(int dayIndex)
        {
            return (dayIndex % 7) switch
            {
                0 => "Mon",
                1 => "Tue",
                2 => "Wed",
                3 => "Thu",
                4 => "Fri",
                5 => "Sat",
                _ => "Sun"
            };
        }
    }
}
