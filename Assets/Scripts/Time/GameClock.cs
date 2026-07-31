using System;
using System.Globalization;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Accelerated day clock. 1 real second ≈ <see cref="minutesPerRealSecond"/> game minutes.
    /// Calendar epoch is Saturday 1 January 2000 at <see cref="DayIndex"/> 0.
    /// </summary>
    public sealed class GameClock
    {
        public const int MinutesPerDay = 24 * 60;
        static readonly DateTime Epoch = new DateTime(2000, 1, 1);

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

        /// <summary>Gregorian date for the current <see cref="DayIndex"/> (time-of-day is midnight on that date).</summary>
        public DateTime CalendarDate => Epoch.AddDays(DayIndex);

        public event Action DayRolled;
        public event Action MonthRolled;

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
                var previousMonth = CalendarDate.Month;
                var previousYear = CalendarDate.Year;
                DayIndex++;
                // Month first so climate (and similar) updates before midnight DayRolled consumers.
                if (CalendarDate.Month != previousMonth || CalendarDate.Year != previousYear)
                    MonthRolled?.Invoke();
                DayRolled?.Invoke();
            }
        }

        public string FormatHud()
        {
            var date = CalendarDate.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture);
            return $"{date}  {Hour:00}:{Minute:00}";
        }
    }
}
