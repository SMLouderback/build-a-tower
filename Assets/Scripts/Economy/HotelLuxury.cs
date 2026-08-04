using System;

namespace BuildATower
{
    /// <summary>
    /// Hotel guest mix, room acceptance, climate bias/fill, and clean-time fallbacks.
    /// </summary>
    public static class HotelLuxury
    {
        /// <summary>Average crime at or above this zeros Premium mix and strongly cuts Upper.</summary>
        public const float HighCrimeThreshold = 40f;

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

        public static WealthBand RollGuestBand(int stars, float averageCrime, int climateStep, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var basic = 0.40f;
            var mid = 0.30f;
            var upper = 0.20f;
            var premium = 0.10f;

            ApplyStarWeights(stars, ref basic, ref mid, ref upper, ref premium);
            ApplyCrimeWeights(averageCrime, ref upper, ref premium);
            ApplyClimateWeights(climateStep, ref basic, ref mid, ref upper, ref premium);

            var sum = basic + mid + upper + premium;
            if (sum <= 0f)
                return WealthBand.Basic;

            basic /= sum;
            mid /= sum;
            upper /= sum;
            // premium is remainder after renormalize via roll thresholds

            var roll = (float)rng.NextDouble();
            if (roll < basic)
                return WealthBand.Basic;
            if (roll < basic + mid)
                return WealthBand.Mid;
            if (roll < basic + mid + upper)
                return WealthBand.Upper;
            return WealthBand.Premium;
        }

        public static int LuxuryClimateBias(LuxuryBand band, int climateStep)
        {
            return band switch
            {
                LuxuryBand.Mid => climateStep == MarketClimate.Recession ? -1 : 0,
                LuxuryBand.Upper => climateStep switch
                {
                    MarketClimate.Recession => -2,
                    MarketClimate.Slow => -1,
                    MarketClimate.Boom => 1,
                    _ => 0
                },
                _ => 0
            };
        }

        public static float CheckInFillMultiplier(LuxuryBand band, int climateStep)
        {
            return band switch
            {
                LuxuryBand.Base => climateStep switch
                {
                    MarketClimate.Recession => 1.1f,
                    MarketClimate.Strong => 0.95f,
                    MarketClimate.Boom => 0.9f,
                    _ => 1.0f
                },
                LuxuryBand.Mid => climateStep switch
                {
                    MarketClimate.Recession => 0.55f,
                    MarketClimate.Slow => 0.8f,
                    MarketClimate.Strong => 1.05f,
                    MarketClimate.Boom => 1.1f,
                    _ => 1.0f
                },
                LuxuryBand.Upper => climateStep switch
                {
                    MarketClimate.Recession => 0.2f,
                    MarketClimate.Slow => 0.5f,
                    MarketClimate.Strong => 1.15f,
                    MarketClimate.Boom => 1.25f,
                    _ => 1.0f
                },
                _ => 1.0f
            };
        }

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

        /// <summary>
        /// Minimum demand chance for hotel nightly payout. Base rooms in Recession keep
        /// a high floor when only mildly overpriced (≤1 step). Returns 0 when no floor applies.
        /// </summary>
        public static float DemandChanceFloor(LuxuryBand band, int climateStep, int overpriceSteps)
        {
            if (band == LuxuryBand.Base &&
                climateStep == MarketClimate.Recession &&
                overpriceSteps <= 1)
                return 0.85f;
            return 0f;
        }

        static void ApplyStarWeights(
            int stars,
            ref float basic,
            ref float mid,
            ref float upper,
            ref float premium)
        {
            if (stars <= 1)
            {
                basic *= 1.25f;
                mid *= 1.15f;
                upper *= 0.55f;
                premium *= 0.35f;
            }
            else if (stars >= 4)
            {
                basic *= 0.7f;
                upper *= 1.25f;
                premium *= 1.4f;
            }
        }

        static void ApplyCrimeWeights(float averageCrime, ref float upper, ref float premium)
        {
            if (averageCrime < HighCrimeThreshold)
                return;

            premium = 0f;
            upper *= 0.25f;
        }

        static void ApplyClimateWeights(
            int climateStep,
            ref float basic,
            ref float mid,
            ref float upper,
            ref float premium)
        {
            if (climateStep == MarketClimate.Recession || climateStep == MarketClimate.Slow)
            {
                basic *= 1.3f;
                mid *= 1.1f;
                upper *= 0.5f;
                premium *= 0.35f;
            }
            else if (climateStep == MarketClimate.Strong || climateStep == MarketClimate.Boom)
            {
                basic *= 0.85f;
                upper *= 1.2f;
                premium *= 1.35f;
            }
        }
    }
}
