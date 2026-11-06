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

        /// <summary>Gregorian date for an arbitrary day index (same epoch as <see cref="CalendarDate"/>).</summary>
        public static DateTime DateForDayIndex(int dayIndex) => Epoch.AddDays(dayIndex);

        public event Action DayRolled;
        public event Action MonthRolled;

        public ClockSnapshotV1 CaptureSnapshot()
        {
            return new ClockSnapshotV1
            {
                dayIndex = DayIndex,
                minuteOfDay = MinuteOfDay,
                minuteAccumulator = _minuteAccumulator,
                minutesPerRealSecond = _minutesPerRealSecond,
                paused = Paused
            };
        }

        public void RestoreSnapshot(ClockSnapshotV1 snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.dayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(snapshot), "The restored day index cannot be negative.");
            if (snapshot.minuteOfDay < 0 || snapshot.minuteOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(snapshot), "The restored minute must be within the day.");
            if (!IsFinite(snapshot.minuteAccumulator) ||
                snapshot.minuteAccumulator < 0f ||
                snapshot.minuteAccumulator >= 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(snapshot),
                    "The restored minute accumulator must be finite and in [0, 1).");
            if (!IsFinite(snapshot.minutesPerRealSecond) || snapshot.minutesPerRealSecond <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(snapshot),
                    "The restored clock speed must be finite and positive.");

            DayIndex = snapshot.dayIndex;
            MinuteOfDay = snapshot.minuteOfDay;
            _minuteAccumulator = snapshot.minuteAccumulator;
            _minutesPerRealSecond = snapshot.minutesPerRealSecond;
            Paused = snapshot.paused;
            LastTickGameMinutes = 0f;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
