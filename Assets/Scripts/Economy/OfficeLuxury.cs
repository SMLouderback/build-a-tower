using System;

namespace BuildATower
{
    public static class OfficeLuxury
    {
        public const string MicroId = "office_micro";
        public const string StudioId = "office_studio";
        public const string BaseId = "office_base";
        public const string MidStandardId = "office_mid_standard";
        public const string MidClinicId = "office_mid_clinic";
        public const string MidTeamId = "office_mid_team";
        public const string UpperStandardId = "office_upper_standard";
        public const string UpperCornerId = "office_upper_corner";
        public const string UpperFloorId = "office_upper_floor";

        public static bool AcceptsWorker(LuxuryBand roomBand, WealthBand worker, string roomId = null)
        {
            switch (worker)
            {
                case WealthBand.Basic:
                    return roomBand == LuxuryBand.Base;
                case WealthBand.Mid:
                    return roomBand == LuxuryBand.Mid;
                case WealthBand.Upper:
                    if (roomBand == LuxuryBand.Upper) return true;
                    return roomBand == LuxuryBand.Mid &&
                           string.Equals(roomId, MidTeamId, StringComparison.Ordinal);
                case WealthBand.Premium:
                    return string.Equals(roomId, UpperCornerId, StringComparison.Ordinal) ||
                           string.Equals(roomId, UpperFloorId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary>Lower is better. Premium prefers Corporate Floor, then Corner Suite.</summary>
        public static int PremiumDeskPreferenceRank(WealthBand wealth, string roomId)
        {
            if (wealth != WealthBand.Premium) return 0;
            if (string.Equals(roomId, UpperFloorId, StringComparison.Ordinal)) return 0;
            if (string.Equals(roomId, UpperCornerId, StringComparison.Ordinal)) return 1;
            return 2;
        }
    }
}
