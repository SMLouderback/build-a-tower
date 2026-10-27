using System;

namespace BuildATower
{
    /// <summary>
    /// Soft class-clash penalty when Upper is near Lower living/work or noisy Service/Leisure
    /// (same floor or vertically adjacent). Pure helpers — multiply fill/income; never block place.
    /// </summary>
    public static class ClassClashRules
    {
        public const float NoPenalty = 1f;
        public const float ClashPenalty = 0.85f;

        public static bool IsNear(int floorDelta) => Math.Abs(floorDelta) <= 1;

        public static bool IsLivingOrWork(EconomicFamily family) =>
            family == EconomicFamily.Office
            || family == EconomicFamily.Hotel
            || family == EconomicFamily.Condo;

        public static bool IsNoisyServiceOrLeisure(EconomicFamily family) =>
            family == EconomicFamily.Service || family == EconomicFamily.Leisure;

        public static bool HasClash(
            TenantClass subjectClass,
            EconomicFamily neighborFamily,
            TenantClass neighborClass,
            int floorDelta)
        {
            if (subjectClass != TenantClass.Upper) return false;
            if (!IsNear(floorDelta)) return false;

            if (IsLivingOrWork(neighborFamily) && neighborClass == TenantClass.Lower)
                return true;

            if (IsNoisyServiceOrLeisure(neighborFamily))
                return true;

            return false;
        }

        public static float PenaltyMult(
            TenantClass subjectClass,
            EconomicFamily neighborFamily,
            TenantClass neighborClass,
            int floorDelta) =>
            HasClash(subjectClass, neighborFamily, neighborClass, floorDelta)
                ? ClashPenalty
                : NoPenalty;
    }
}
