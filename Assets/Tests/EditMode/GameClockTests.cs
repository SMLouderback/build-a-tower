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

        [Test]
        public void Tick_records_game_minutes_advanced_by_last_tick()
        {
            var clock = new GameClock(2f);

            clock.Tick(1.5f);

            Assert.AreEqual(3f, clock.LastTickGameMinutes);
            clock.Paused = true;
            clock.Tick(1f);
            Assert.AreEqual(0f, clock.LastTickGameMinutes);
        }

        [Test]
        public void SetSpeed_changes_minutes_advanced_per_real_second()
        {
            var clock = new GameClock(1f);
            clock.MinutesPerRealSecond = 5f;
            clock.Tick(1f);
            Assert.AreEqual(5f, clock.LastTickGameMinutes);
        }
    }
}
