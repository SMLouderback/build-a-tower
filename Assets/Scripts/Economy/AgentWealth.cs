using System;

namespace BuildATower
{
    public enum WealthBand
    {
        Street,
        Basic,
        Premium
    }

    public static class AgentWealth
    {
        public static WealthBand ResolveBand(AgentRole role, RoomTypeSO homeType)
        {
            if (role == AgentRole.StreetVisitor)
                return WealthBand.Street;

            // Convention / event attendees spend more like office workers than street walk-ins.
            if (role == AgentRole.EventVisitor)
                return WealthBand.Basic;

            if (IsPremiumLiving(homeType))
                return WealthBand.Premium;

            return WealthBand.Basic;
        }

        public static int RollDailyDisposable(WealthBand band, float climateMult, Random rng)
        {
            var (lo, hi) = BandRange(band);
            var rolled = lo + rng.Next(0, hi - lo + 1);
            var scaled = (int)Math.Round(rolled * climateMult);
            return Math.Max(0, scaled);
        }

        /// <summary>
        /// Soft gate: visitors can enter if they cover a meaningful fraction of list price.
        /// Actual spend is still <see cref="RollSpend"/> (1 … min(price, remaining)).
        /// </summary>
        public static bool CanAfford(int remaining, RoomTypeSO shop)
        {
            var price = ShopVisitRules.PayPerVisit(shop);
            if (price <= 0 || remaining <= 0) return false;
            var gate = Math.Min(price, Math.Max(25, price / 2));
            return remaining >= gate;
        }

        public static int RollSpend(int remaining, RoomTypeSO shop, Random rng)
        {
            var price = ShopVisitRules.PayPerVisit(shop);
            var max = Math.Min(price, remaining);
            if (max < 1) return 0;
            return rng.Next(1, max + 1);
        }

        static (int lo, int hi) BandRange(WealthBand band) => band switch
        {
            WealthBand.Street => (35, 90),
            WealthBand.Premium => (120, 280),
            _ => (70, 160)
        };

        static bool IsPremiumLiving(RoomTypeSO homeType)
        {
            if (homeType == null) return false;
            if (!IsLivingCategory(homeType.category)) return false;
            if (homeType.requiredStars >= 2) return true;
            if (ContainsPremium(homeType.id) || ContainsPremium(homeType.displayName))
                return true;
            return false;
        }

        static bool IsLivingCategory(RoomCategory category) =>
            category == RoomCategory.Office ||
            category == RoomCategory.Hotel ||
            category == RoomCategory.Condo;

        static bool ContainsPremium(string value) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf("premium", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
