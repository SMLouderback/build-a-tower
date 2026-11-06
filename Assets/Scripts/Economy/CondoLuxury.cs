using System;

namespace BuildATower
{
    public static class CondoLuxury
    {
        public const string StudioId = "condo_studio";
        public const string AlcoveId = "condo_alcove";
        public const string BaseId = "condo_base";
        public const string MidStandardId = "condo_mid_standard";
        public const string MidLoftId = "condo_mid_loft";
        public const string MidFamilyId = "condo_mid_family";
        public const string UpperStandardId = "condo_upper_standard";
        public const string UpperCornerId = "condo_upper_corner";
        public const string UpperPenthouseId = "condo_upper_penthouse";

        public static bool AcceptsBuyer(LuxuryBand roomBand, WealthBand buyer, string roomId = null)
        {
            switch (buyer)
            {
                case WealthBand.Basic:
                    return roomBand == LuxuryBand.Base;
                case WealthBand.Mid:
                    return roomBand == LuxuryBand.Mid;
                case WealthBand.Upper:
                    if (roomBand == LuxuryBand.Upper) return true;
                    return roomBand == LuxuryBand.Mid &&
                           string.Equals(roomId, MidFamilyId, StringComparison.Ordinal);
                case WealthBand.Premium:
                    return string.Equals(roomId, UpperCornerId, StringComparison.Ordinal) ||
                           string.Equals(roomId, UpperPenthouseId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary>Lower is better. Premium prefers Penthouse, then Corner Condo.</summary>
        public static int PremiumUnitPreferenceRank(WealthBand wealth, string roomId)
        {
            if (wealth != WealthBand.Premium) return 0;
            if (string.Equals(roomId, UpperPenthouseId, StringComparison.Ordinal)) return 0;
            if (string.Equals(roomId, UpperCornerId, StringComparison.Ordinal)) return 1;
            return 2;
        }
    }
}
