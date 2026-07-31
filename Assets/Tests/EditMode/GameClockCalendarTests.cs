using System;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class GameClockCalendarTests
    {
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
    }
}
