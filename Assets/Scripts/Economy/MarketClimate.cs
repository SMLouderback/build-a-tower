using System;

namespace BuildATower
{
    /// <summary>
    /// Tower-wide market climate on a 5-step scale. Advances with a weighted
    /// random walk each Gregorian month (caller invokes <see cref="OnMonthRolled"/>).
    /// </summary>
    public sealed class MarketClimate
    {
        public const int Recession = 0;
        public const int Slow = 1;
        public const int Normal = 2;
        public const int Strong = 3;
        public const int Boom = 4;

        public static readonly string[] Labels =
        {
            "Recession",
            "Slow",
            "Normal",
            "Strong",
            "Boom"
        };

        public int Step { get; private set; } = Normal;

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
        /// Weighted monthly step: stay ~40%, ±1 ~45%, ±2 ~15%. Clamped to 0–4.
        /// Uses <see cref="Random.Next(int)"/> with max 100.
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

            Step = Math.Clamp(Step + delta, Recession, Boom);
        }
    }
}
