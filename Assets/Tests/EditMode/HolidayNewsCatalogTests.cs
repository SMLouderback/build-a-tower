using System;
using System.Linq;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class HolidayNewsCatalogTests
    {
        [Test]
        public void Fixed_holidays_match_month_and_day()
        {
            AssertHoliday(new DateTime(2000, 1, 1), "new_year");
            AssertHoliday(new DateTime(2001, 2, 14), "valentine");
            AssertHoliday(new DateTime(2002, 7, 4), "july4");
            AssertHoliday(new DateTime(2003, 10, 31), "halloween");
            AssertHoliday(new DateTime(2004, 12, 25), "christmas");
            Assert.IsFalse(HolidayNewsCatalog.IsHoliday(new DateTime(2000, 1, 2)));
        }

        [Test]
        public void Floating_holidays_match_known_2000_dates()
        {
            // Verified Gregorian calendar for year 2000.
            AssertHoliday(new DateTime(2000, 1, 17), "mlk");
            AssertHoliday(new DateTime(2000, 2, 21), "presidents");
            AssertHoliday(new DateTime(2000, 4, 23), "easter");
            AssertHoliday(new DateTime(2000, 5, 14), "mothers");
            AssertHoliday(new DateTime(2000, 5, 29), "memorial");
            AssertHoliday(new DateTime(2000, 6, 18), "fathers");
            AssertHoliday(new DateTime(2000, 9, 4), "labor");
            AssertHoliday(new DateTime(2000, 10, 9), "columbus");
            AssertHoliday(new DateTime(2000, 11, 23), "thanksgiving");
        }

        [Test]
        public void EasterSunday_matches_known_years()
        {
            Assert.AreEqual(new DateTime(2000, 4, 23), HolidayNewsCatalog.EasterSunday(2000));
            Assert.AreEqual(new DateTime(2001, 4, 15), HolidayNewsCatalog.EasterSunday(2001));
            Assert.AreEqual(new DateTime(2024, 3, 31), HolidayNewsCatalog.EasterSunday(2024));
            Assert.AreEqual(new DateTime(2026, 4, 5), HolidayNewsCatalog.EasterSunday(2026));
        }

        [Test]
        public void Holiday_lines_are_non_empty()
        {
            var date = new DateTime(2000, 10, 31);
            var match = HolidayNewsCatalog.MatchesFor(date).Single();
            Assert.AreEqual("halloween", match.Id);
            Assert.GreaterOrEqual(match.Lines.Length, 2);
            Assert.IsTrue(match.Lines.All(l => !string.IsNullOrWhiteSpace(l)));
        }

        [Test]
        public void GameClock_DateForDayIndex_matches_epoch()
        {
            Assert.AreEqual(new DateTime(2000, 1, 1), GameClock.DateForDayIndex(0));
            Assert.AreEqual(new DateTime(2000, 7, 4), GameClock.DateForDayIndex(185)); // 2000 leap year
        }

        static void AssertHoliday(DateTime date, string expectedId)
        {
            var matches = HolidayNewsCatalog.MatchesFor(date);
            Assert.IsTrue(matches.Any(m => m.Id == expectedId), $"Expected {expectedId} on {date:yyyy-MM-dd}");
            Assert.IsTrue(HolidayNewsCatalog.IsHoliday(date));
        }
    }
}
