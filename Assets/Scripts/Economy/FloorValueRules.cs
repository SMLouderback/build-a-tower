using System;

namespace BuildATower
{
    /// <summary>
    /// Soft vertical zoning: FloorFit01 ∈ [FitMin…FitMax] and warning copy when below threshold.
    /// Never blocks placement — income/fill consumers multiply by Fit01 only.
    /// Street/podium favors Shops/Leisure; prestige favors Upper living/work; mid-cap scales with stars.
    /// </summary>
    public static class FloorValueRules
    {
        public const float FitMin = 0.55f;
        public const float FitMax = 1f;
        public const float WarningThreshold = 0.85f;

        public const int StreetPodiumMaxY = 3;
        public const int MidCapBase = 8;
        public const int MidCapPerStar = 2;

        enum FloorBand
        {
            Street,
            Mid,
            Prestige
        }

        public static int MidCap(int stars) =>
            MidCapBase + Math.Max(0, stars) * MidCapPerStar;

        public static float Fit01(EconomicFamily family, TenantClass cls, int floorY, int stars)
        {
            var band = ResolveBand(floorY, stars);
            var raw = Preference(family, cls, band);
            if (raw < FitMin) return FitMin;
            if (raw > FitMax) return FitMax;
            return raw;
        }

        public static bool TryWarning(
            EconomicFamily family,
            TenantClass cls,
            int floorY,
            int stars,
            out string reason)
        {
            var fit = Fit01(family, cls, floorY, stars);
            if (fit >= WarningThreshold)
            {
                reason = null;
                return false;
            }

            reason = WarningCopy(family, cls, floorY, stars);
            return true;
        }

        static FloorBand ResolveBand(int floorY, int stars)
        {
            if (floorY <= StreetPodiumMaxY)
                return FloorBand.Street;
            if (floorY <= MidCap(stars))
                return FloorBand.Mid;
            return FloorBand.Prestige;
        }

        static bool IsStreetReliant(EconomicFamily family) =>
            family == EconomicFamily.Shops || family == EconomicFamily.Leisure;

        static bool IsLivingOrWork(EconomicFamily family) =>
            family == EconomicFamily.Office
            || family == EconomicFamily.Hotel
            || family == EconomicFamily.Condo;

        static float Preference(EconomicFamily family, TenantClass cls, FloorBand band)
        {
            if (IsStreetReliant(family))
            {
                return band switch
                {
                    FloorBand.Street => FitMax,
                    FloorBand.Mid => 0.85f,
                    _ => FitMin
                };
            }

            if (IsLivingOrWork(family))
            {
                return cls switch
                {
                    TenantClass.Upper => band switch
                    {
                        FloorBand.Prestige => FitMax,
                        FloorBand.Mid => 0.85f,
                        _ => FitMin
                    },
                    TenantClass.Lower => band switch
                    {
                        FloorBand.Street => 0.95f,
                        FloorBand.Mid => FitMax,
                        _ => 0.70f
                    },
                    _ => band switch // Mid
                    {
                        FloorBand.Mid => FitMax,
                        FloorBand.Prestige => 0.95f,
                        _ => 0.75f
                    }
                };
            }

            // Service / Transit / Infrastructure / None — neutral soft fit
            return band switch
            {
                FloorBand.Street when family == EconomicFamily.Service => 0.95f,
                FloorBand.Mid => FitMax,
                FloorBand.Prestige when family == EconomicFamily.Service => 0.80f,
                _ => 0.95f
            };
        }

        static string WarningCopy(EconomicFamily family, TenantClass cls, int floorY, int stars)
        {
            var band = ResolveBand(floorY, stars);
            var midCap = MidCap(stars);

            if (IsStreetReliant(family) && band != FloorBand.Street)
                return "Street-facing shops earn more on lower floors";

            if (IsLivingOrWork(family) && cls == TenantClass.Upper && band != FloorBand.Prestige)
                return $"High-floor premium works better above floor {midCap}";

            if (IsLivingOrWork(family) && cls == TenantClass.Lower && band == FloorBand.Prestige)
                return "Lower-class tenants prefer mid and street floors";

            return "This floor is a poor fit for this use";
        }
    }
}
