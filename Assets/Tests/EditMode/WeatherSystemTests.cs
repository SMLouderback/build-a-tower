using System;
using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class WeatherSystemTests
    {
        const int MinutesPerDay = GameClock.MinutesPerDay;

        [TestCase(1, Season.Winter)]
        [TestCase(2, Season.Winter)]
        [TestCase(3, Season.Spring)]
        [TestCase(4, Season.Spring)]
        [TestCase(5, Season.Spring)]
        [TestCase(6, Season.Summer)]
        [TestCase(7, Season.Summer)]
        [TestCase(8, Season.Summer)]
        [TestCase(9, Season.Fall)]
        [TestCase(10, Season.Fall)]
        [TestCase(11, Season.Fall)]
        [TestCase(12, Season.Winter)]
        public void FromMonth_maps_all_twelve_months(int month, Season expected)
        {
            Assert.AreEqual(expected, SeasonUtil.FromMonth(month));
        }

        [TestCase(0)]
        [TestCase(13)]
        public void FromMonth_rejects_out_of_range(int month)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SeasonUtil.FromMonth(month));
        }

        [Test]
        public void Bucket_weights_are_75_10_10_5_over_many_rolls()
        {
            var rng = new Random(12345);
            var counts = new Dictionary<WeatherBucket, int>
            {
                { WeatherBucket.Fair, 0 },
                { WeatherBucket.Cloudy, 0 },
                { WeatherBucket.Wet, 0 },
                { WeatherBucket.Severe, 0 }
            };

            const int rolls = 20000;
            for (var i = 0; i < rolls; i++)
                counts[WeatherSystem.RollBucket(rng)]++;

            Assert.AreEqual(0.75f, counts[WeatherBucket.Fair] / (float)rolls, 0.02f);
            Assert.AreEqual(0.10f, counts[WeatherBucket.Cloudy] / (float)rolls, 0.02f);
            Assert.AreEqual(0.10f, counts[WeatherBucket.Wet] / (float)rolls, 0.02f);
            Assert.AreEqual(0.05f, counts[WeatherBucket.Severe] / (float)rolls, 0.02f);
        }

        [Test]
        public void Rolled_kinds_over_5000_rolls_match_bucket_weights()
        {
            var rng = new Random(777);
            var fair = 0;
            var cloudy = 0;
            var wet = 0;
            var severe = 0;
            const int rolls = 5000;
            for (var i = 0; i < rolls; i++)
            {
                switch (WeatherSystem.RollKind(Season.Summer, rng))
                {
                    case WeatherKind.Clear:
                    case WeatherKind.PartlyCloudy:
                        fair++;
                        break;
                    case WeatherKind.Cloudy:
                        cloudy++;
                        break;
                    case WeatherKind.Rain:
                        wet++;
                        break;
                    case WeatherKind.Thunderstorm:
                        severe++;
                        break;
                    default:
                        Assert.Fail("Unexpected summer kind");
                        break;
                }
            }

            Assert.AreEqual(0.75f, fair / (float)rolls, 0.03f);
            Assert.AreEqual(0.10f, cloudy / (float)rolls, 0.03f);
            Assert.AreEqual(0.10f, wet / (float)rolls, 0.03f);
            Assert.AreEqual(0.05f, severe / (float)rolls, 0.03f);
        }

        [Test]
        public void Fair_bucket_splits_into_clear_and_partly_cloudy()
        {
            var rng = new Random(99);
            var clear = 0;
            var partly = 0;
            for (var i = 0; i < 4000; i++)
            {
                var kind = WeatherSystem.KindForBucket(WeatherBucket.Fair, Season.Spring, rng);
                if (kind == WeatherKind.Clear) clear++;
                else if (kind == WeatherKind.PartlyCloudy) partly++;
                else Assert.Fail("Fair must be Clear or PartlyCloudy");
            }

            Assert.Greater(clear, 0);
            Assert.Greater(partly, 0);
            Assert.AreEqual(WeatherSystem.FairClearShare, clear / 4000f, 0.04f);
        }

        [Test]
        public void Winter_wet_is_snow_and_severe_is_blizzard()
        {
            var rng = new Random(1);
            Assert.AreEqual(WeatherKind.Snow, WeatherSystem.KindForBucket(WeatherBucket.Wet, Season.Winter, rng));
            Assert.AreEqual(WeatherKind.Blizzard, WeatherSystem.KindForBucket(WeatherBucket.Severe, Season.Winter, rng));
        }

        [TestCase(Season.Spring)]
        [TestCase(Season.Summer)]
        [TestCase(Season.Fall)]
        public void Non_winter_wet_is_rain_and_severe_is_thunderstorm(Season season)
        {
            var rng = new Random(1);
            Assert.AreEqual(WeatherKind.Rain, WeatherSystem.KindForBucket(WeatherBucket.Wet, season, rng));
            Assert.AreEqual(WeatherKind.Thunderstorm, WeatherSystem.KindForBucket(WeatherBucket.Severe, season, rng));
        }

        [Test]
        public void Cloudy_bucket_is_cloudy_in_every_season()
        {
            var rng = new Random(1);
            foreach (Season season in Enum.GetValues(typeof(Season)))
                Assert.AreEqual(WeatherKind.Cloudy, WeatherSystem.KindForBucket(WeatherBucket.Cloudy, season, rng));
        }

        [TestCase(WeatherKind.Clear, 1f, 1f)]
        [TestCase(WeatherKind.PartlyCloudy, 1f, 1f)]
        [TestCase(WeatherKind.Cloudy, 0.85f, 0.95f)]
        [TestCase(WeatherKind.Rain, 0.35f, 0.80f)]
        [TestCase(WeatherKind.Snow, 0.35f, 0.80f)]
        [TestCase(WeatherKind.Thunderstorm, 0f, 0.60f)]
        [TestCase(WeatherKind.Blizzard, 0f, 0.60f)]
        public void Multiplier_table_matches_spec(WeatherKind kind, float street, float spend)
        {
            var weather = new WeatherSystem(new Random(1));
            weather.ForceSegment(kind, 100, 8 * 60, 60);
            Assert.AreEqual(kind, weather.Kind);
            Assert.AreEqual(street, weather.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(spend, weather.ShopSpendMultiplier, 0.0001f);
        }

        [Test]
        public void Clear_and_PartlyCloudy_share_full_street_traffic()
        {
            var a = new WeatherSystem(new Random(1));
            var b = new WeatherSystem(new Random(1));
            a.ForceSegment(WeatherKind.Clear, 10, 600, 120);
            b.ForceSegment(WeatherKind.PartlyCloudy, 10, 600, 120);
            Assert.AreEqual(1f, a.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(a.StreetTrafficMultiplier, b.StreetTrafficMultiplier, 0.0001f);
        }

        [Test]
        public void Thunderstorm_duration_is_60_to_120_minutes()
        {
            var seen = new HashSet<int>();
            for (var seed = 0; seed < 400; seed++)
            {
                var weather = new WeatherSystem(new Random(seed));
                weather.ForceRoll(WeatherKind.Thunderstorm, 50, 10 * 60);
                var duration = weather.SegmentEndAbsoluteMinute - weather.SegmentStartAbsoluteMinute;
                Assert.GreaterOrEqual(duration, 60);
                Assert.LessOrEqual(duration, 120);
                seen.Add((int)duration);
            }

            Assert.Greater(seen.Count, 10, "Duration should vary across seeds.");
        }

        [Test]
        public void Blizzard_ends_at_next_midnight()
        {
            var weather = new WeatherSystem(new Random(3));
            weather.ForceRoll(WeatherKind.Blizzard, 40, 9 * 60);
            Assert.AreEqual(41, weather.SegmentEndDayIndex);
            Assert.AreEqual(0, weather.SegmentEndMinuteOfDay);
        }

        [Test]
        public void Blizzard_near_midnight_lasts_at_least_one_hour()
        {
            var weather = new WeatherSystem(new Random(3));
            weather.ForceRoll(WeatherKind.Blizzard, 40, 23 * 60 + 30);
            var duration = weather.SegmentEndAbsoluteMinute - weather.SegmentStartAbsoluteMinute;
            Assert.GreaterOrEqual(duration, 60);
        }

        [Test]
        public void Blizzard_to_midnight_then_next_day_street_is_half()
        {
            var weather = new WeatherSystem(new Random(5));
            weather.ForceRoll(WeatherKind.Blizzard, 40, 9 * 60);

            weather.AdvanceTo(40, 23 * 60 + 59);
            Assert.AreEqual(WeatherKind.Blizzard, weather.Kind);
            Assert.AreEqual(0f, weather.StreetTrafficMultiplier, 0.0001f);

            // Crossing midnight ends the storm; whatever rolls next, hangover caps street at 0.5.
            weather.AdvanceTo(41, 0);
            Assert.AreEqual(WeatherHangover.BlizzardNextDayHalf, weather.Hangover);
            Assert.LessOrEqual(weather.StreetTrafficMultiplier, 0.5f + 0.0001f);

            // Force a fair segment so the hangover cap is the only limiter.
            weather.ForceSegment(WeatherKind.Clear, 41, 0, 2 * MinutesPerDay);
            Assert.AreEqual(0.5f, weather.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(0.9f, weather.ShopSpendMultiplier, 0.0001f);

            weather.AdvanceTo(41, 12 * 60);
            Assert.AreEqual(0.5f, weather.StreetTrafficMultiplier, 0.0001f);
        }

        [Test]
        public void Hangover_clears_after_the_next_day()
        {
            var weather = new WeatherSystem(new Random(5));
            weather.ForceRoll(WeatherKind.Blizzard, 40, 9 * 60);
            weather.AdvanceTo(41, 0);
            weather.ForceSegment(WeatherKind.Clear, 41, 0, 5 * MinutesPerDay);

            weather.AdvanceTo(42, 0);
            Assert.AreEqual(WeatherHangover.None, weather.Hangover);
            Assert.AreEqual(1f, weather.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(1f, weather.ShopSpendMultiplier, 0.0001f);
        }

        [Test]
        public void Blizzard_ending_mid_day_crushes_rest_of_day_then_next_day_half()
        {
            var weather = new WeatherSystem(new Random(5));
            // Rolled at 23:30 on day 40 -> minimum 60 minutes -> ends 00:30 day 41.
            weather.ForceRoll(WeatherKind.Blizzard, 40, 23 * 60 + 30);
            Assert.AreEqual(41, weather.SegmentEndDayIndex);
            Assert.Greater(weather.SegmentEndMinuteOfDay, 0);

            weather.AdvanceTo(41, weather.SegmentEndMinuteOfDay);
            Assert.AreEqual(WeatherHangover.BlizzardSameDayCleanup, weather.Hangover);

            weather.ForceSegment(WeatherKind.Clear, 41, weather.SegmentEndMinuteOfDay, 4 * MinutesPerDay);
            Assert.AreEqual(0.15f, weather.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(0.70f, weather.ShopSpendMultiplier, 0.0001f);

            weather.AdvanceTo(42, 1);
            Assert.AreEqual(WeatherHangover.BlizzardNextDayHalf, weather.Hangover);
            Assert.AreEqual(0.5f, weather.StreetTrafficMultiplier, 0.0001f);

            weather.AdvanceTo(43, 1);
            Assert.AreEqual(WeatherHangover.None, weather.Hangover);
        }

        [Test]
        public void Thunderstorm_during_hangover_keeps_street_at_zero()
        {
            var weather = new WeatherSystem(new Random(5));
            weather.ForceRoll(WeatherKind.Blizzard, 40, 9 * 60);
            weather.AdvanceTo(41, 0);
            weather.ForceSegment(WeatherKind.Thunderstorm, 41, 0, 90);
            Assert.AreEqual(0f, weather.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(0.6f, weather.ShopSpendMultiplier, 0.0001f);
        }

        [Test]
        public void AdvanceTo_keeps_segment_until_end_then_rerolls()
        {
            var weather = new WeatherSystem(new Random(21));
            weather.AdvanceTo(10, 8 * 60);
            var firstEnd = weather.SegmentEndAbsoluteMinute;
            var firstKind = weather.Kind;

            weather.AdvanceTo(10, 8 * 60 + 30);
            Assert.AreEqual(firstEnd, weather.SegmentEndAbsoluteMinute);
            Assert.AreEqual(firstKind, weather.Kind);

            var endDay = weather.SegmentEndDayIndex;
            var endMinute = weather.SegmentEndMinuteOfDay;
            weather.AdvanceTo(endDay, endMinute);
            Assert.Greater(weather.SegmentEndAbsoluteMinute, firstEnd);
            Assert.AreEqual(firstEnd, weather.SegmentStartAbsoluteMinute);
        }

        [Test]
        public void Default_segments_last_two_to_four_hours()
        {
            var weather = new WeatherSystem(new Random(2024));
            weather.AdvanceTo(0, 0);

            var checkedSegments = 0;
            var lastEnd = weather.SegmentEndAbsoluteMinute;
            for (var minute = 1; minute < 30 * MinutesPerDay; minute += 15)
            {
                weather.AdvanceTo(minute / MinutesPerDay, minute % MinutesPerDay);
                if (weather.SegmentEndAbsoluteMinute == lastEnd)
                    continue;

                lastEnd = weather.SegmentEndAbsoluteMinute;
                var duration = weather.SegmentEndAbsoluteMinute - weather.SegmentStartAbsoluteMinute;
                if (weather.Kind == WeatherKind.Thunderstorm)
                {
                    Assert.GreaterOrEqual(duration, 60);
                    Assert.LessOrEqual(duration, 120);
                }
                else if (weather.Kind != WeatherKind.Blizzard)
                {
                    Assert.GreaterOrEqual(duration, 120);
                    Assert.LessOrEqual(duration, 240);
                }

                checkedSegments++;
            }

            Assert.Greater(checkedSegments, 20);
        }

        [Test]
        public void Segment_cadence_does_not_depend_on_tick_granularity()
        {
            var coarse = new WeatherSystem(new Random(55));
            var fine = new WeatherSystem(new Random(55));
            coarse.AdvanceTo(0, 0);
            fine.AdvanceTo(0, 0);

            for (var minute = 1; minute <= 5 * MinutesPerDay; minute++)
                fine.AdvanceTo(minute / MinutesPerDay, minute % MinutesPerDay);

            // Coarse ticks stay under the catch-up limit, so segments chain identically.
            for (var minute = 6 * 60; minute <= 5 * MinutesPerDay; minute += 6 * 60)
                coarse.AdvanceTo(minute / MinutesPerDay, minute % MinutesPerDay);

            Assert.AreEqual(fine.Kind, coarse.Kind);
            Assert.AreEqual(fine.SegmentEndAbsoluteMinute, coarse.SegmentEndAbsoluteMinute);
        }

        [Test]
        public void CurrentSeason_follows_calendar_month()
        {
            var weather = new WeatherSystem(new Random(1));

            // Day 0 = 2000-01-01 -> Winter.
            weather.AdvanceTo(0, 0);
            Assert.AreEqual(Season.Winter, weather.CurrentSeason);

            // 2000-03-01 is day 60 (leap year: 31 + 29).
            weather.AdvanceTo(60, 0);
            Assert.AreEqual(Season.Spring, weather.CurrentSeason);

            var julyDay = (int)(new DateTime(2000, 7, 15) - new DateTime(2000, 1, 1)).TotalDays;
            weather.AdvanceTo(julyDay, 0);
            Assert.AreEqual(Season.Summer, weather.CurrentSeason);

            var octDay = (int)(new DateTime(2000, 10, 15) - new DateTime(2000, 1, 1)).TotalDays;
            weather.AdvanceTo(octDay, 0);
            Assert.AreEqual(Season.Fall, weather.CurrentSeason);
        }

        [Test]
        public void Default_state_is_clear_with_full_multipliers()
        {
            var weather = new WeatherSystem();
            Assert.AreEqual(WeatherKind.Clear, weather.Kind);
            Assert.AreEqual(1f, weather.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(1f, weather.ShopSpendMultiplier, 0.0001f);
            Assert.AreEqual(WeatherHangover.None, weather.Hangover);
        }

        [Test]
        public void Winter_rolls_never_produce_rain_or_thunder()
        {
            var rng = new Random(8);
            for (var i = 0; i < 3000; i++)
            {
                var kind = WeatherSystem.RollKind(Season.Winter, rng);
                Assert.AreNotEqual(WeatherKind.Rain, kind);
                Assert.AreNotEqual(WeatherKind.Thunderstorm, kind);
            }
        }

        [Test]
        public void Non_winter_rolls_never_produce_snow_or_blizzard()
        {
            var rng = new Random(9);
            for (var i = 0; i < 3000; i++)
            {
                var kind = WeatherSystem.RollKind(Season.Fall, rng);
                Assert.AreNotEqual(WeatherKind.Snow, kind);
                Assert.AreNotEqual(WeatherKind.Blizzard, kind);
            }
        }
    }
}
