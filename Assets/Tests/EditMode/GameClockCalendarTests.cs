using System;
using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class GameClockCalendarTests
    {
        const int DeltaPlus2Roll = 92;

        [Test]
        public void Day0_is_saturday_1_jan_2000()
        {
            var clock = new GameClock();
            Assert.AreEqual(new DateTime(2000, 1, 1), clock.CalendarDate.Date);
            Assert.AreEqual(DayOfWeek.Saturday, clock.CalendarDate.DayOfWeek);
        }

        [Test]
        public void Crossing_jan_31_fires_MonthRolled()
        {
            var clock = new GameClock(startMinuteOfDay: 23 * 60 + 59);
            // Advance DayIndex to 30 (31 Jan), then one more day → Feb 1
            var months = 0;
            clock.MonthRolled += () => months++;
            clock.AdvanceMinutes(1 + 30 * GameClock.MinutesPerDay);
            Assert.AreEqual(1, months);
            Assert.AreEqual(2, clock.CalendarDate.Month);
            Assert.AreEqual(1, clock.CalendarDate.Day);
            Assert.AreEqual(31, clock.DayIndex);
        }

        [Test]
        public void Month_change_fires_MonthRolled_before_DayRolled()
        {
            var clock = ClockOnJan31At2359();
            var sequence = new List<string>();
            clock.DayRolled += () => sequence.Add("day");
            clock.MonthRolled += () => sequence.Add("month");
            clock.AdvanceMinutes(1);
            CollectionAssert.AreEqual(new[] { "month", "day" }, sequence);
            Assert.AreEqual(2, clock.CalendarDate.Month);
        }

        [Test]
        public void Month_change_updates_climate_before_DayRolled_snapshot()
        {
            var clock = ClockOnJan31At2359();
            var climate = new MarketClimate();
            var rng = new ScriptedRandom(DeltaPlus2Roll);
            var offsetAtDayRolled = int.MinValue;
            clock.MonthRolled += () => climate.OnMonthRolled(rng);
            clock.DayRolled += () => offsetAtDayRolled = climate.ComfortTierOffset;

            Assert.AreEqual(0, climate.ComfortTierOffset);
            clock.AdvanceMinutes(1);

            Assert.AreEqual(MarketClimate.Boom, climate.Step);
            Assert.AreEqual(2, climate.ComfortTierOffset);
            Assert.AreEqual(2, offsetAtDayRolled);
        }

        [Test]
        public void FormatHud_includes_gregorian_date_and_time()
        {
            var clock = new GameClock();
            Assert.AreEqual("Sat 01 Jan 2000  06:00", clock.FormatHud());
        }

        [Test]
        public void Same_month_day_roll_does_not_fire_MonthRolled()
        {
            var clock = new GameClock(startMinuteOfDay: 23 * 60 + 59);
            var months = 0;
            clock.MonthRolled += () => months++;
            clock.AdvanceMinutes(1);
            Assert.AreEqual(0, months);
            Assert.AreEqual(1, clock.CalendarDate.Month);
            Assert.AreEqual(2, clock.CalendarDate.Day);
        }

        static GameClock ClockOnJan31At2359()
        {
            var clock = new GameClock(startMinuteOfDay: 23 * 60 + 59);
            // DayIndex 0 Jan 1 → after 30 midnights: DayIndex 30 = 31 Jan
            clock.AdvanceMinutes(1 + 29 * GameClock.MinutesPerDay);
            Assert.AreEqual(30, clock.DayIndex);
            Assert.AreEqual(1, clock.CalendarDate.Month);
            Assert.AreEqual(31, clock.CalendarDate.Day);
            return clock;
        }

        sealed class ScriptedRandom : Random
        {
            readonly Queue<int> _rolls;

            public ScriptedRandom(params int[] rolls)
            {
                _rolls = new Queue<int>(rolls);
            }

            public override int Next(int maxValue)
            {
                if (_rolls.Count == 0)
                    throw new InvalidOperationException("ScriptedRandom exhausted.");
                var roll = _rolls.Dequeue();
                if (roll < 0 || roll >= maxValue)
                    throw new ArgumentOutOfRangeException(nameof(maxValue), roll, "Scripted roll outside range.");
                return roll;
            }
        }
    }
}
