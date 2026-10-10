using System;

namespace BuildATower
{
    /// <summary>
    /// Owns weekly Market Pulse multipliers (living vs commercial). Advances about every
    /// <see cref="PulsePeriodDays"/> game days. Independent of monthly <see cref="MarketClimate"/>
    /// (climate is read later at balancer wire-up; this state does not mutate climate).
    /// </summary>
    public sealed class MacroEconomicController
    {
        public const int PulsePeriodDays = 7;
        public const float NeutralPulseMult = 1f;
        public const float MinPulseMult = 0.70f;
        public const float MaxPulseMult = 1.30f;

        /// <summary>Absolute max step applied to a pulse mult in one weekly roll.</summary>
        public const float MaxStepAbs = 0.10f;

        /// <summary>Chance (0–100) after the walk to nudge one step toward neutral.</summary>
        public const int MeanReversionChancePercent = 20;

        // Weight bands for Next(100): stay 0–39, −step 40–61, +step 62–84, −2× 85–91, +2× 92–99
        const int StayEnd = 40;
        const int MinusEnd = 62;
        const int PlusEnd = 85;
        const int Minus2End = 92;

        readonly Random _rng;
        int _nextPulseDayIndex = PulsePeriodDays;

        public MacroEconomicController(Random rng = null)
        {
            _rng = rng ?? new Random();
            LivingPulseMult = NeutralPulseMult;
            CommercialPulseMult = NeutralPulseMult;
        }

        public float LivingPulseMult { get; private set; }
        public float CommercialPulseMult { get; private set; }
        public int NextPulseDayIndex => _nextPulseDayIndex;

        /// <summary>
        /// Advance pulse state to match <paramref name="clock"/>.DayIndex.
        /// Rolls once per elapsed pulse period (catch-up when days jump).
        /// </summary>
        public void Tick(GameClock clock)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            while (clock.DayIndex >= _nextPulseDayIndex)
            {
                LivingPulseMult = Clamp(ApplyStep(LivingPulseMult));
                CommercialPulseMult = Clamp(ApplyStep(CommercialPulseMult));
                _nextPulseDayIndex += PulsePeriodDays;
            }
        }

        public MarketPulseSnapshotV1 CaptureSnapshot() => new MarketPulseSnapshotV1
        {
            livingPulseMult = LivingPulseMult,
            commercialPulseMult = CommercialPulseMult,
            nextPulseDayIndex = _nextPulseDayIndex
        };

        /// <summary>
        /// Restore from a save. Null/absent (older saves) → neutral pulse and next pulse
        /// aligned to <paramref name="dayIndex"/>. Throws on invalid present data.
        /// </summary>
        public void RestoreSnapshot(MarketPulseSnapshotV1 snapshot, int dayIndex)
        {
            if (IsAbsent(snapshot))
            {
                LivingPulseMult = NeutralPulseMult;
                CommercialPulseMult = NeutralPulseMult;
                _nextPulseDayIndex = NextAlignedPulseDay(dayIndex);
                return;
            }

            if (!TryValidateSnapshot(snapshot, out var error))
                throw new ArgumentException(error, nameof(snapshot));

            LivingPulseMult = snapshot.livingPulseMult;
            CommercialPulseMult = snapshot.commercialPulseMult;
            _nextPulseDayIndex = snapshot.nextPulseDayIndex;
        }

        /// <summary>Test/debug seam: set multipliers (clamped) and next pulse day.</summary>
        public void ForcePulse(float living, float commercial, int nextPulseDayIndex)
        {
            LivingPulseMult = Clamp(living);
            CommercialPulseMult = Clamp(commercial);
            _nextPulseDayIndex = Math.Max(0, nextPulseDayIndex);
        }

        public static bool IsAbsent(MarketPulseSnapshotV1 snapshot) =>
            snapshot == null
            || (snapshot.livingPulseMult == 0f
                && snapshot.commercialPulseMult == 0f
                && snapshot.nextPulseDayIndex == 0);

        public static bool TryValidateSnapshot(MarketPulseSnapshotV1 snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Market pulse snapshot is missing.";
                return false;
            }

            if (!IsFinite(snapshot.livingPulseMult)
                || snapshot.livingPulseMult < MinPulseMult
                || snapshot.livingPulseMult > MaxPulseMult)
            {
                error = "Living pulse multiplier is out of range.";
                return false;
            }

            if (!IsFinite(snapshot.commercialPulseMult)
                || snapshot.commercialPulseMult < MinPulseMult
                || snapshot.commercialPulseMult > MaxPulseMult)
            {
                error = "Commercial pulse multiplier is out of range.";
                return false;
            }

            if (snapshot.nextPulseDayIndex < 0)
            {
                error = "Next pulse day index is out of range.";
                return false;
            }

            error = null;
            return true;
        }

        public static float Clamp(float mult) =>
            Math.Clamp(mult, MinPulseMult, MaxPulseMult);

        float ApplyStep(float current)
        {
            var roll = _rng.Next(100);
            float delta;
            if (roll < StayEnd)
                delta = 0f;
            else if (roll < MinusEnd)
                delta = -MaxStepAbs;
            else if (roll < PlusEnd)
                delta = MaxStepAbs;
            else if (roll < Minus2End)
                delta = -2f * MaxStepAbs;
            else
                delta = 2f * MaxStepAbs;

            var next = current + delta;

            // Soft pull toward neutral so long boom/bust runs drift back.
            if (!Approximately(next, NeutralPulseMult) && _rng.Next(100) < MeanReversionChancePercent)
                next += Math.Sign(NeutralPulseMult - next) * MaxStepAbs;

            return next;
        }

        static int NextAlignedPulseDay(int dayIndex)
        {
            if (dayIndex < 0) dayIndex = 0;
            var completed = dayIndex / PulsePeriodDays;
            return (completed + 1) * PulsePeriodDays;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;
    }
}
