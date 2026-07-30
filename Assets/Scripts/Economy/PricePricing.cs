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
        public static int ComfortMaxTier(int stars)
        {
            stars = System.Math.Clamp(stars, 0, 5);
            return stars switch
            {
                0 => TierLow,
                1 => TierNormal,
                2 => TierNormal,
                3 => TierHigh,
                4 => TierHigh,
                _ => TierMax
            };
        }

        public static int OverpriceSteps(int tier, int stars) =>
            System.Math.Max(0, ClampTier(tier) - ComfortMaxTier(stars));

        /// <summary>
        /// Chance a room earns / accepts occupancy, or a condo buyer spawns.
        /// </summary>
        public static float DemandChance(int tier, int stars)
        {
            var steps = OverpriceSteps(tier, stars);
            return steps switch
            {
                0 => 1f,
                1 => 0.4f,
                _ => 0.1f
            };
        }

        public static string MarketHint(int tier, int stars)
        {
            tier = ClampTier(tier);
            var comfort = ComfortMaxTier(stars);
            if (tier <= comfort)
                return $"Market: OK for {stars}★";
            return $"Market: Overpriced for {stars}★";
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
