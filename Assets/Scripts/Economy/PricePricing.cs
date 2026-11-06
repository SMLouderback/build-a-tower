namespace BuildATower
{
    /// <summary>
    /// Discrete rent/sale price tiers (continuous slider comes after MVP).
    /// </summary>
    public static class PricePricing
    {
        public const int TierLow = 0;
        public const int TierNormal = 1;
        public const int TierHigh = 2;
        public const int TierMax = 3;

        public static readonly string[] Labels = { "Low", "Normal", "High", "Max" };

        public static int ClampTier(int tier) =>
            System.Math.Clamp(tier, TierLow, TierMax);

        public static float PayoutMultiplier(int tier)
        {
            return ClampTier(tier) switch
            {
                TierLow => 0.7f,
                TierHigh => 1.3f,
                TierMax => 1.6f,
                _ => 1f
            };
        }

        public static int ScaledIncome(int baseIncome, int tier) =>
            (int)System.Math.Round(baseIncome * PayoutMultiplier(tier));

        /// <summary>
        /// Highest tier the market comfortably supports at the given star count (0–5 band).
        /// </summary>
        public static int ComfortMaxTier(int stars) =>
            ComfortMaxTier(stars, climateOffset: 0);

        /// <summary>
        /// Stars baseline comfort plus climate offset, clamped to Low…Max.
        /// </summary>
        public static int ComfortMaxTier(int stars, int climateOffset)
        {
            stars = System.Math.Clamp(stars, 0, 5);
            var baseline = stars switch
            {
                0 => TierLow,
                1 => TierNormal,
                2 => TierNormal,
                3 => TierHigh,
                4 => TierHigh,
                _ => TierMax
            };
            return System.Math.Clamp(baseline + climateOffset, TierLow, TierMax);
        }

        public static int OverpriceSteps(int tier, int stars, int climateOffset = 0) =>
            System.Math.Max(0, ClampTier(tier) - ComfortMaxTier(stars, climateOffset));

        /// <summary>
        /// Chance a room earns / accepts occupancy, or a condo buyer spawns.
        /// </summary>
        public static float DemandChance(int tier, int stars) =>
            DemandChance(tier, stars, climateOffset: 0);

        public static float DemandChance(int tier, int stars, int climateOffset)
        {
            var steps = OverpriceSteps(tier, stars, climateOffset);
            return steps switch
            {
                0 => 1f,
                1 => 0.4f,
                _ => 0.1f
            };
        }

        public static string MarketHint(int tier, int stars) =>
            MarketHint(tier, stars, climateOffset: 0);

        public static string MarketHint(int tier, int stars, int climateOffset)
        {
            tier = ClampTier(tier);
            var comfort = ComfortMaxTier(stars, climateOffset);
            var baseHint = tier <= comfort
                ? $"Market: OK for {stars}★"
                : $"Market: Overpriced for {stars}★";

            if (climateOffset == 0)
                return baseHint;

            var step = System.Math.Clamp(
                MarketClimate.Normal + climateOffset,
                MarketClimate.Recession,
                MarketClimate.Boom);
            return $"{baseHint} · {MarketClimate.Labels[step]} economy";
        }

        public static bool IsPricedRoom(RoomTypeSO type)
        {
            if (type == null) return false;
            return type.incomeModel is IncomeModel.QuarterlyRent
                or IncomeModel.NightlyRate
                or IncomeModel.UpfrontSale;
        }
    }
}
