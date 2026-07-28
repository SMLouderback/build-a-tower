using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class GameClockTests
    {
        [Test]
        public void Advance_wraps_day_and_increments_index()
        {
            var clock = new GameClock(1f, 23 * 60 + 50);
            Assert.AreEqual(0, clock.DayIndex);
            clock.AdvanceMinutes(15);
            Assert.AreEqual(1, clock.DayIndex);
            Assert.AreEqual(5, clock.MinuteOfDay);
        }

        [Test]
        public void FormatHud_includes_weekday_and_time()
        {
            var clock = new GameClock(1f, 9 * 60 + 5);
            StringAssert.Contains("09:05", clock.FormatHud());
        }
    }
}
