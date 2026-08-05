using System;

namespace BuildATower
{
    /// <summary>
    /// Hotel guest acceptance, clean-time fallbacks, and thin wrappers over shared living luxury helpers.
    /// </summary>
    public static class HotelLuxury
    {
        /// <summary>Alias for <see cref="LivingLuxury.HighCrimeThreshold"/>.</summary>
        public const float HighCrimeThreshold = LivingLuxury.HighCrimeThreshold;

        public const string MidExtendedId = "hotel_mid_extended";
        public const string UpperKingId = "hotel_upper_king";
        public const string UpperSuiteId = "hotel_upper_suite";

        public const float CleanFallbackBaseMinutes = 12f;
        public const float CleanFallbackMidMinutes = 22f;
        public const float CleanFallbackUpperMinutes = 35f;

        public static bool AcceptsGuest(LuxuryBand roomBand, WealthBand guest, string roomId = null)
        {
            switch (guest)
            {
                case WealthBand.Basic:
                    return roomBand == LuxuryBand.Base;
                case WealthBand.Mid:
                    return roomBand == LuxuryBand.Mid;
                case WealthBand.Upper:
                    if (roomBand == LuxuryBand.Upper)
                        return true;
                    return roomBand == LuxuryBand.Mid &&
                           string.Equals(roomId, MidExtendedId, StringComparison.Ordinal);
                case WealthBand.Premium:
                    return string.Equals(roomId, UpperKingId, StringComparison.Ordinal) ||
                           string.Equals(roomId, UpperSuiteId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public static WealthBand RollGuestBand(int stars, float averageCrime, int climateStep, Random rng) =>
            LivingLuxury.RollLivingBand(stars, averageCrime, climateStep, rng);

        public static int LuxuryClimateBias(LuxuryBand band, int climateStep) =>
            LivingLuxury.LuxuryClimateBias(band, climateStep);

        public static float CheckInFillMultiplier(LuxuryBand band, int climateStep) =>
            LivingLuxury.CheckInFillMultiplier(band, climateStep);

        public static float ResolveCleanMinutes(RoomTypeSO type)
        {
            if (type != null && type.cleanMinutes > 0f)
                return type.cleanMinutes;

            if (type != null)
            {
                switch (type.luxuryBand)
                {
                    case LuxuryBand.Base:
                        return CleanFallbackBaseMinutes;
                    case LuxuryBand.Mid:
                        return CleanFallbackMidMinutes;
                    case LuxuryBand.Upper:
                        return CleanFallbackUpperMinutes;
                }
            }

            return RoomConditionRules.CleanBasicMinutes;
        }

        public static float DemandChanceFloor(LuxuryBand band, int climateStep, int overpriceSteps) =>
            LivingLuxury.DemandChanceFloor(band, climateStep, overpriceSteps);
    }
}
