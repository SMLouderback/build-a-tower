using System;

namespace BuildATower
{
    public enum WealthBand
    {
        Street,
        Basic,
        Mid,
        Upper,
        Premium
    }

    public static class AgentWealth
    {
        /// <summary>
        /// Resolves disposable-income wealth band from role and home room.
        /// Call sites should pass the simulation <paramref name="rng"/> so office/condo
        /// and suite mixes stay deterministic with the rest of the sim.
        /// No 2-arg overload: always supply rng (avoids a hidden <c>new Random(0)</c> fallback).
        /// </summary>
        public static WealthBand ResolveBand(AgentRole role, RoomTypeSO homeType, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            if (role == AgentRole.StreetVisitor)
                return WealthBand.Street;

            // Convention / event attendees spend like Mid living guests.
            if (role == AgentRole.EventVisitor)
                return WealthBand.Mid;

            if (homeType != null && homeType.category == RoomCategory.Hotel)
                return ResolveHotelBand(homeType, rng);

            if (homeType != null &&
                (homeType.category == RoomCategory.Office || homeType.category == RoomCategory.Condo))
                return ResolveOfficeCondoBand(homeType, rng);

            return WealthBand.Mid;
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
            WealthBand.Basic => (55, 110),
            WealthBand.Mid => (90, 160),
            WealthBand.Upper => (140, 220),
            WealthBand.Premium => (200, 320),
            _ => (55, 110)
        };

        static WealthBand ResolveHotelBand(RoomTypeSO homeType, Random rng)
        {
            switch (homeType.luxuryBand)
            {
                case LuxuryBand.Base:
                    return WealthBand.Basic;
                case LuxuryBand.Mid:
                    return WealthBand.Mid;
                case LuxuryBand.Upper:
                    if (ContainsIgnoreCase(homeType.id, "suite"))
                        return rng.Next(2) == 0 ? WealthBand.Upper : WealthBand.Premium;
                    return WealthBand.Upper;
                default:
                    // Legacy hotel_premium / name premium without band → Mid.
                    if (ContainsIgnoreCase(homeType.id, "premium") ||
                        ContainsIgnoreCase(homeType.displayName, "premium"))
                        return WealthBand.Mid;
                    return WealthBand.Basic;
            }
        }

        static WealthBand ResolveOfficeCondoBand(RoomTypeSO homeType, Random rng)
        {
            if (homeType.category == RoomCategory.Office && homeType.luxuryBand != LuxuryBand.None)
            {
                return homeType.luxuryBand switch
                {
                    LuxuryBand.Base => WealthBand.Basic,
                    LuxuryBand.Mid => WealthBand.Mid,
                    LuxuryBand.Upper =>
                        string.Equals(homeType.id, OfficeLuxury.UpperFloorId, StringComparison.Ordinal) ||
                        string.Equals(homeType.id, OfficeLuxury.UpperCornerId, StringComparison.Ordinal)
                            ? (rng.Next(2) == 0 ? WealthBand.Upper : WealthBand.Premium)
                            : WealthBand.Upper,
                    _ => WealthBand.Mid
                };
            }

            // Condo + legacy office without luxuryBand:
            if (homeType.requiredStars < 2)
                // 30% Basic / 70% Mid
                return rng.NextDouble() < 0.30 ? WealthBand.Basic : WealthBand.Mid;

            // 70% Upper / 30% Premium
            return rng.NextDouble() < 0.70 ? WealthBand.Upper : WealthBand.Premium;
        }

        static bool ContainsIgnoreCase(string value, string needle) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
