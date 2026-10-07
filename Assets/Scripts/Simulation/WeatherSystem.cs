using System;

namespace BuildATower
{
    /// <summary>
    /// Sim-owned weather: rolls a bucket (Fair 75 / Cloudy 10 / Wet 10 / Severe 5) every
    /// 2–4 game hours and maps it to a <see cref="WeatherKind"/> by season. Presenters and
    /// agents read <see cref="StreetTrafficMultiplier"/> / <see cref="ShopSpendMultiplier"/>;
    /// they never roll. Economic <see cref="MarketClimate"/> stays separate.
    /// </summary>
    public sealed class WeatherSystem
    {
        // Bucket weights (percent, sum 100).
        public const int FairWeightPercent = 75;
        public const int CloudyWeightPercent = 10;
        public const int WetWeightPercent = 10;
        public const int SevereWeightPercent = 5;

        /// <summary>When the fair bucket is rolled, share that becomes Clear (rest is PartlyCloudy).</summary>
        public const int FairClearPercent = 60;
        public const float FairClearShare = FairClearPercent / 100f;

        // Segment cadence (game minutes).
        public const int MinSegmentMinutes = 2 * 60;
        public const int MaxSegmentMinutes = 4 * 60;
        public const int MinThunderstormMinutes = 60;
        public const int MaxThunderstormMinutes = 2 * 60;
        /// <summary>Shortest blizzard when rolled right before midnight, so the event is visible.</summary>
        public const int MinBlizzardMinutes = 60;

        // Multiplier table (spec §3.6).
        public const float FairStreet = 1f;
        public const float FairSpend = 1f;
        public const float CloudyStreet = 0.85f;
        public const float CloudySpend = 0.95f;
        public const float WetStreet = 0.35f;
        public const float WetSpend = 0.80f;
        public const float SevereStreet = 0f;
        public const float SevereSpend = 0.60f;
        public const float HangoverNextDayStreet = 0.50f;
        public const float HangoverNextDaySpend = 0.90f;
        public const float HangoverSameDayStreet = 0.15f;
        public const float HangoverSameDaySpend = 0.70f;

        const int MinutesPerDay = GameClock.MinutesPerDay;

        /// <summary>If the sim jumps further than this past a segment end, restart from "now" instead of replaying.</summary>
        const long MaxCatchUpMinutes = 2L * MinutesPerDay;

        readonly Random _rng;
        bool _started;
        long _segmentStart;
        long _segmentEnd;

        public WeatherSystem(Random rng = null)
        {
            _rng = rng ?? new Random();
            Kind = WeatherKind.Clear;
            CurrentSeason = Season.Winter; // day 0 = 1 Jan 2000
        }

        public Season CurrentSeason { get; private set; }
        public WeatherKind Kind { get; private set; }
        public WeatherHangover Hangover { get; private set; }

        /// <summary>Last calendar day (inclusive) the current hangover state applies to.</summary>
        public int HangoverEndDayIndex { get; private set; }

        /// <summary>Absolute game minute (<c>dayIndex * 1440 + minuteOfDay</c>) the current segment began.</summary>
        public long SegmentStartAbsoluteMinute => _segmentStart;

        /// <summary>Absolute game minute the current segment ends (re-roll happens at/after this).</summary>
        public long SegmentEndAbsoluteMinute => _segmentEnd;

        public int SegmentEndDayIndex => (int)(_segmentEnd / MinutesPerDay);
        public int SegmentEndMinuteOfDay => (int)(_segmentEnd % MinutesPerDay);

        public float StreetTrafficMultiplier
        {
            get
            {
                var street = BaseStreet(Kind);
                switch (Hangover)
                {
                    case WeatherHangover.BlizzardSameDayCleanup:
                        return Math.Min(street, HangoverSameDayStreet);
                    case WeatherHangover.BlizzardNextDayHalf:
                        return Math.Min(street, HangoverNextDayStreet);
                    default:
                        return street;
                }
            }
        }

        public float ShopSpendMultiplier
        {
            get
            {
                var spend = BaseSpend(Kind);
                switch (Hangover)
                {
                    case WeatherHangover.BlizzardSameDayCleanup:
                        return Math.Min(spend, HangoverSameDaySpend);
                    case WeatherHangover.BlizzardNextDayHalf:
                        return Math.Min(spend, HangoverNextDaySpend);
                    default:
                        return spend;
                }
            }
        }

        /// <summary>
        /// Advance to the given game time. Rolls a new segment each time the current one expires,
        /// chaining from the previous segment end so cadence is independent of tick size.
        /// </summary>
        public void AdvanceTo(int dayIndex, int minuteOfDay)
        {
            var now = Absolute(dayIndex, minuteOfDay);
            CurrentSeason = SeasonForDay(dayIndex);

            if (!_started)
            {
                _started = true;
                BeginRolledSegment(RollKind(CurrentSeason, _rng), now);
            }

            while (now >= _segmentEnd)
            {
                var endedAt = _segmentEnd;
                if (Kind == WeatherKind.Blizzard)
                    BeginBlizzardHangover(endedAt);

                var start = now - endedAt > MaxCatchUpMinutes ? now : endedAt;
                var season = SeasonForDay((int)(start / MinutesPerDay));
                BeginRolledSegment(RollKind(season, _rng), start);
            }

            UpdateHangover(dayIndex);
        }

        /// <summary>Test/debug seam: start a segment of <paramref name="kind"/> using normal duration rules.</summary>
        public void ForceRoll(WeatherKind kind, int dayIndex, int minuteOfDay)
        {
            _started = true;
            CurrentSeason = SeasonForDay(dayIndex);
            BeginRolledSegment(kind, Absolute(dayIndex, minuteOfDay));
        }

        /// <summary>Test/debug seam: start a segment with an explicit length (minutes). Hangover is untouched.</summary>
        public void ForceSegment(WeatherKind kind, int dayIndex, int minuteOfDay, int durationMinutes)
        {
            if (durationMinutes < 1) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            _started = true;
            CurrentSeason = SeasonForDay(dayIndex);
            var start = Absolute(dayIndex, minuteOfDay);
            Kind = kind;
            _segmentStart = start;
            _segmentEnd = start + durationMinutes;
        }

        public WeatherSnapshotV1 CaptureSnapshot() => new WeatherSnapshotV1
        {
            kind = Kind.ToString(),
            segmentEndDayIndex = SegmentEndDayIndex,
            segmentEndMinuteOfDay = SegmentEndMinuteOfDay,
            hangover = Hangover.ToString(),
            hangoverEndDayIndex = HangoverEndDayIndex
        };

        /// <summary>
        /// Restore from a save. <paramref name="snapshot"/> null (older saves) → a fresh Clear segment
        /// starting at the given time. Throws <see cref="ArgumentException"/> on invalid data.
        /// </summary>
        public void RestoreSnapshot(WeatherSnapshotV1 snapshot, int dayIndex, int minuteOfDay)
        {
            var now = Absolute(dayIndex, minuteOfDay);
            CurrentSeason = SeasonForDay(dayIndex);
            _started = true;

            if (IsAbsent(snapshot))
            {
                Kind = WeatherKind.Clear;
                Hangover = WeatherHangover.None;
                HangoverEndDayIndex = 0;
                _segmentStart = now;
                _segmentEnd = now + MinSegmentMinutes;
                return;
            }

            if (!TryValidateSnapshot(snapshot, out var error))
                throw new ArgumentException(error, nameof(snapshot));

            Enum.TryParse(snapshot.kind, false, out WeatherKind kind);
            Enum.TryParse(snapshot.hangover, false, out WeatherHangover hangover);

            Kind = kind;
            Hangover = hangover;
            HangoverEndDayIndex = hangover == WeatherHangover.None ? 0 : snapshot.hangoverEndDayIndex;
            _segmentEnd = Absolute(snapshot.segmentEndDayIndex, snapshot.segmentEndMinuteOfDay);
            _segmentStart = Math.Min(now, _segmentEnd);
        }

        /// <summary>
        /// True for older saves with no weather block. <c>JsonUtility</c> materializes a missing
        /// serializable class as an all-default object, so blank kind and hangover count as absent too.
        /// </summary>
        public static bool IsAbsent(WeatherSnapshotV1 snapshot) =>
            snapshot == null
            || (string.IsNullOrEmpty(snapshot.kind) && string.IsNullOrEmpty(snapshot.hangover));

        public static bool TryValidateSnapshot(WeatherSnapshotV1 snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Weather snapshot is missing.";
                return false;
            }

            if (!TryParseDefined(snapshot.kind, out WeatherKind _))
            {
                error = "Weather kind is unknown.";
                return false;
            }

            if (!TryParseDefined(snapshot.hangover, out WeatherHangover _))
            {
                error = "Weather hangover is unknown.";
                return false;
            }

            if (snapshot.segmentEndDayIndex < 0
                || snapshot.segmentEndMinuteOfDay < 0
                || snapshot.segmentEndMinuteOfDay >= MinutesPerDay)
            {
                error = "Weather segment end is out of range.";
                return false;
            }

            if (snapshot.hangoverEndDayIndex < 0)
            {
                error = "Weather hangover end day is out of range.";
                return false;
            }

            error = null;
            return true;
        }

        static bool TryParseDefined<T>(string value, out T result) where T : struct, Enum
        {
            result = default;
            return !string.IsNullOrEmpty(value)
                   && Enum.TryParse(value, false, out result)
                   && Enum.IsDefined(typeof(T), result)
                   && string.Equals(Enum.GetName(typeof(T), result), value, StringComparison.Ordinal);
        }

        /// <summary>Roll a bucket with Fair 75 / Cloudy 10 / Wet 10 / Severe 5.</summary>
        public static WeatherBucket RollBucket(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var roll = rng.Next(100);
            if (roll < FairWeightPercent)
                return WeatherBucket.Fair;
            if (roll < FairWeightPercent + CloudyWeightPercent)
                return WeatherBucket.Cloudy;
            if (roll < FairWeightPercent + CloudyWeightPercent + WetWeightPercent)
                return WeatherBucket.Wet;
            return WeatherBucket.Severe;
        }

        /// <summary>Map a bucket to a concrete kind (winter: Snow/Blizzard; otherwise Rain/Thunderstorm).</summary>
        public static WeatherKind KindForBucket(WeatherBucket bucket, Season season, Random rng)
        {
            switch (bucket)
            {
                case WeatherBucket.Fair:
                    if (rng == null) throw new ArgumentNullException(nameof(rng));
                    return rng.Next(100) < FairClearPercent ? WeatherKind.Clear : WeatherKind.PartlyCloudy;
                case WeatherBucket.Cloudy:
                    return WeatherKind.Cloudy;
                case WeatherBucket.Wet:
                    return season == Season.Winter ? WeatherKind.Snow : WeatherKind.Rain;
                default:
                    return season == Season.Winter ? WeatherKind.Blizzard : WeatherKind.Thunderstorm;
            }
        }

        public static WeatherKind RollKind(Season season, Random rng) =>
            KindForBucket(RollBucket(rng), season, rng);

        static float BaseStreet(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Cloudy: return CloudyStreet;
                case WeatherKind.Rain:
                case WeatherKind.Snow: return WetStreet;
                case WeatherKind.Thunderstorm:
                case WeatherKind.Blizzard: return SevereStreet;
                default: return FairStreet;
            }
        }

        static float BaseSpend(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Cloudy: return CloudySpend;
                case WeatherKind.Rain:
                case WeatherKind.Snow: return WetSpend;
                case WeatherKind.Thunderstorm:
                case WeatherKind.Blizzard: return SevereSpend;
                default: return FairSpend;
            }
        }

        static long Absolute(int dayIndex, int minuteOfDay) =>
            (long)dayIndex * MinutesPerDay + minuteOfDay;

        static Season SeasonForDay(int dayIndex) =>
            SeasonUtil.FromMonth(GameClock.DateForDayIndex(dayIndex).Month);

        void BeginRolledSegment(WeatherKind kind, long start)
        {
            Kind = kind;
            _segmentStart = start;

            switch (kind)
            {
                case WeatherKind.Thunderstorm:
                    _segmentEnd = start + MinThunderstormMinutes +
                                  _rng.Next(MaxThunderstormMinutes - MinThunderstormMinutes + 1);
                    break;
                case WeatherKind.Blizzard:
                {
                    var nextMidnight = (start / MinutesPerDay + 1) * MinutesPerDay;
                    _segmentEnd = nextMidnight - start < MinBlizzardMinutes
                        ? start + MinBlizzardMinutes
                        : nextMidnight;
                    break;
                }
                default:
                    _segmentEnd = start + MinSegmentMinutes +
                                  _rng.Next(MaxSegmentMinutes - MinSegmentMinutes + 1);
                    break;
            }
        }

        /// <summary>
        /// Blizzard ending exactly at midnight → whole next day at half street.
        /// Ending mid-day (late-roll minimum) → rest of that day crushed, then next day half.
        /// </summary>
        void BeginBlizzardHangover(long endedAt)
        {
            HangoverEndDayIndex = (int)(endedAt / MinutesPerDay);
            Hangover = endedAt % MinutesPerDay == 0
                ? WeatherHangover.BlizzardNextDayHalf
                : WeatherHangover.BlizzardSameDayCleanup;
        }

        void UpdateHangover(int dayIndex)
        {
            if (Hangover == WeatherHangover.BlizzardSameDayCleanup && dayIndex > HangoverEndDayIndex)
            {
                Hangover = WeatherHangover.BlizzardNextDayHalf;
                HangoverEndDayIndex++;
            }

            if (Hangover == WeatherHangover.BlizzardNextDayHalf && dayIndex > HangoverEndDayIndex)
            {
                Hangover = WeatherHangover.None;
                HangoverEndDayIndex = 0;
            }
        }
    }
}
