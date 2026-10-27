using System;

namespace BuildATower
{
    /// <summary>
    /// Tower-wide market climate on a 5-step scale. Advances with a weighted
    /// random walk each Gregorian month (caller invokes <see cref="OnMonthRolled"/>).
    /// Extremes reflect instead of absorbing, with a soft pull toward Normal and a
    /// hard cap on consecutive months at Recession/Boom.
    /// </summary>
    public sealed class MarketClimate
    {
        public const int Recession = 0;
        public const int Slow = 1;
        public const int Normal = 2;
        public const int Strong = 3;
        public const int Boom = 4;

        /// <summary>After this many consecutive months at Recession or Boom, force a step toward Normal.</summary>
        public const int MaxConsecutiveExtremeMonths = 2;

        /// <summary>Chance (0–100) after the walk to nudge one step toward Normal when not already there.</summary>
        public const int MeanReversionChancePercent = 18;

        public static readonly string[] Labels =
        {
            "Recession",
            "Slow",
            "Normal",
            "Strong",
            "Boom"
        };

        public int Step { get; private set; } = Normal;

        /// <summary>Consecutive months spent at the current step (for extreme escape).</summary>
        public int MonthsAtCurrentStep { get; private set; }

        public string Name => Labels[Step];

        public float SpendMultiplier => Step switch
        {
            Recession => 0.7f,
            Slow => 0.85f,
            Strong => 1.15f,
            Boom => 1.3f,
            _ => 1f
        };

        public int ComfortTierOffset => Step - Normal;

        /// <summary>
        /// Weighted monthly step: stay ~40%, ±1 ~45%, ±2 ~15%.
        /// Out-of-range moves reflect (Recession −1 → Slow). Soft mean reversion toward Normal.
        /// Cannot remain at Recession/Boom more than <see cref="MaxConsecutiveExtremeMonths"/> months.
        /// </summary>
        public void OnMonthRolled(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var roll = rng.Next(100);
            int delta;
            if (roll < 40)
                delta = 0;
            else if (roll < 62)
                delta = -1;
            else if (roll < 85)
                delta = 1;
            else if (roll < 92)
                delta = -2;
            else
                delta = 2;

            var next = Reflect(Step + delta);

            // Soft pull toward Normal so long Slow/Strong runs also drift back.
            if (next != Normal && rng.Next(100) < MeanReversionChancePercent)
                next = Reflect(next + Math.Sign(Normal - next));

            // Hard escape: don't linger at Recession/Boom for months on end.
            if (next == Step && IsExtreme(Step) &&
                MonthsAtCurrentStep >= MaxConsecutiveExtremeMonths)
            {
                next = Reflect(Step + Math.Sign(Normal - Step));
            }

            if (next == Step)
                MonthsAtCurrentStep++;
            else
                MonthsAtCurrentStep = 1;

            Step = next;
        }

        static bool IsExtreme(int step) =>
            step == Recession || step == Boom;

        /// <summary>Bounce off 0/4 so downward rolls at Recession become recovery.</summary>
        public static int Reflect(int step)
        {
            while (step < Recession || step > Boom)
            {
                if (step < Recession)
                    step = Recession + (Recession - step);
                if (step > Boom)
                    step = Boom - (step - Boom);
            }

            return step;
        }
    }
}
