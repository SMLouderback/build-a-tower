using System;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Living identity map + placeholder VPSF rate tables for the economic balancer.
    /// Money numbers live only as named constants here (never scattered in place/midnight code).
    /// </summary>
    public static class VpsfCatalog
    {
        public const int MinTier = 1;
        public const int MaxTier = 9;

        // --- Placeholder base $/cell (Task 2 consumes these) ---
        public const float OfficeBasePerCell = 120f;
        public const float HotelBasePerCell = 140f;
        public const float CondoBasePerCell = 160f;
        public const float ShopsBasePerCell = 110f;
        public const float LeisureBasePerCell = 100f;
        public const float ServiceBasePerCell = 80f;
        public const float TransitBasePerCell = 90f;
        public const float InfrastructureBasePerCell = 70f;

        // --- Class income multipliers ---
        public const float ClassMultLower = 0.85f;
        public const float ClassMultMid = 1.00f;
        public const float ClassMultUpper = 1.35f;

        // --- Upkeep as fraction of period rent/income ---
        public const float UpkeepRatioLower = 0.40f;
        public const float UpkeepRatioMid = 0.25f;
        public const float UpkeepRatioUpper = 0.15f;

        // --- Build-cost factor vs income base (placeholder) ---
        public const float BuildCostPerIncomeCell = 18f;

        static readonly float[] TierMult =
        {
            0f, // unused (tiers are 1–9)
            0.70f,
            0.80f,
            0.90f,
            1.00f,
            1.10f,
            1.20f,
            1.35f,
            1.50f,
            1.70f
        };

        const string HotelBaseId = "hotel_base";
        const string HotelAccessibleId = "hotel_accessible";
        const string HotelMidStandardId = "hotel_mid_standard";
        const string HotelStudioId = "hotel_studio";
        const string HotelJuniorSuiteId = "hotel_junior_suite";
        const string HotelUpperStandardId = "hotel_upper_standard";

        public static bool TryIdentity(
            RoomTypeSO type,
            out EconomicFamily family,
            out int tier,
            out TenantClass tenantClass)
        {
            family = EconomicFamily.None;
            tier = 0;
            tenantClass = TenantClass.Mid;

            if (type == null || string.IsNullOrEmpty(type.id))
                return false;

            if (!TryMapLivingId(type.id, out family, out tier))
                return false;

            tenantClass = ResolveTenantClass(type.luxuryBand, type.id);
            return true;
        }

        public static int Cells(RoomTypeSO type)
        {
            if (type == null) return 0;
            var w = type.size.x;
            var h = type.size.y;
            if (w <= 0 || h <= 0) return 0;
            return w * h;
        }

        public static float BasePerCell(EconomicFamily family) => family switch
        {
            EconomicFamily.Office => OfficeBasePerCell,
            EconomicFamily.Hotel => HotelBasePerCell,
            EconomicFamily.Condo => CondoBasePerCell,
            EconomicFamily.Shops => ShopsBasePerCell,
            EconomicFamily.Leisure => LeisureBasePerCell,
            EconomicFamily.Service => ServiceBasePerCell,
            EconomicFamily.Transit => TransitBasePerCell,
            EconomicFamily.Infrastructure => InfrastructureBasePerCell,
            _ => 0f
        };

        public static float TierMultiplier(int tier)
        {
            if (tier < MinTier || tier > MaxTier) return 0f;
            return TierMult[tier];
        }

        public static float ClassMultiplier(TenantClass tenantClass) => tenantClass switch
        {
            TenantClass.Lower => ClassMultLower,
            TenantClass.Upper => ClassMultUpper,
            _ => ClassMultMid
        };

        public static float UpkeepRatio(TenantClass tenantClass) => tenantClass switch
        {
            TenantClass.Lower => UpkeepRatioLower,
            TenantClass.Upper => UpkeepRatioUpper,
            _ => UpkeepRatioMid
        };

        static bool TryMapLivingId(string id, out EconomicFamily family, out int tier)
        {
            family = EconomicFamily.None;
            tier = 0;

            if (TryOfficeTier(id, out tier))
            {
                family = EconomicFamily.Office;
                return true;
            }

            if (TryHotelTier(id, out tier))
            {
                family = EconomicFamily.Hotel;
                return true;
            }

            if (TryCondoTier(id, out tier))
            {
                family = EconomicFamily.Condo;
                return true;
            }

            return false;
        }

        static bool TryOfficeTier(string id, out int tier)
        {
            if (Eq(id, OfficeLuxury.MicroId)) { tier = 1; return true; }
            if (Eq(id, OfficeLuxury.StudioId)) { tier = 2; return true; }
            if (Eq(id, OfficeLuxury.BaseId)) { tier = 3; return true; }
            if (Eq(id, OfficeLuxury.MidStandardId)) { tier = 4; return true; }
            if (Eq(id, OfficeLuxury.MidClinicId)) { tier = 5; return true; }
            if (Eq(id, OfficeLuxury.MidTeamId)) { tier = 6; return true; }
            if (Eq(id, OfficeLuxury.UpperStandardId)) { tier = 7; return true; }
            if (Eq(id, OfficeLuxury.UpperCornerId)) { tier = 8; return true; }
            if (Eq(id, OfficeLuxury.UpperFloorId)) { tier = 9; return true; }
            tier = 0;
            return false;
        }

        static bool TryHotelTier(string id, out int tier)
        {
            if (Eq(id, HotelBaseId)) { tier = 1; return true; }
            if (Eq(id, HotelAccessibleId)) { tier = 2; return true; }
            if (Eq(id, HotelMidStandardId)) { tier = 3; return true; }
            if (Eq(id, HotelLuxury.MidExtendedId)) { tier = 4; return true; }
            if (Eq(id, HotelStudioId)) { tier = 5; return true; }
            if (Eq(id, HotelJuniorSuiteId)) { tier = 6; return true; }
            if (Eq(id, HotelUpperStandardId)) { tier = 7; return true; }
            if (Eq(id, HotelLuxury.UpperKingId)) { tier = 8; return true; }
            if (Eq(id, HotelLuxury.UpperSuiteId)) { tier = 9; return true; }
            tier = 0;
            return false;
        }

        static bool TryCondoTier(string id, out int tier)
        {
            if (Eq(id, CondoLuxury.StudioId)) { tier = 1; return true; }
            if (Eq(id, CondoLuxury.AlcoveId)) { tier = 2; return true; }
            if (Eq(id, CondoLuxury.BaseId)) { tier = 3; return true; }
            if (Eq(id, CondoLuxury.MidStandardId)) { tier = 4; return true; }
            if (Eq(id, CondoLuxury.MidLoftId)) { tier = 5; return true; }
            if (Eq(id, CondoLuxury.MidFamilyId)) { tier = 6; return true; }
            if (Eq(id, CondoLuxury.UpperStandardId)) { tier = 7; return true; }
            if (Eq(id, CondoLuxury.UpperCornerId)) { tier = 8; return true; }
            if (Eq(id, CondoLuxury.UpperPenthouseId)) { tier = 9; return true; }
            tier = 0;
            return false;
        }

        /// <summary>
        /// LuxuryBand → TenantClass. Mid/Upper map directly; None/Base → Mid.
        /// When band is None, id tokens (_upper / _mid / _lower) may refine the class.
        /// </summary>
        static TenantClass ResolveTenantClass(LuxuryBand band, string id)
        {
            switch (band)
            {
                case LuxuryBand.Upper:
                    return TenantClass.Upper;
                case LuxuryBand.Mid:
                    return TenantClass.Mid;
                case LuxuryBand.Base:
                    return TenantClass.Mid;
                case LuxuryBand.None:
                    return InferClassFromId(id);
                default:
                    // Future-proof if LuxuryBand gains Lower.
                    if (string.Equals(band.ToString(), "Lower", StringComparison.Ordinal))
                        return TenantClass.Lower;
                    return InferClassFromId(id);
            }
        }

        static TenantClass InferClassFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return TenantClass.Mid;
            if (id.IndexOf("_upper", StringComparison.OrdinalIgnoreCase) >= 0)
                return TenantClass.Upper;
            if (id.IndexOf("_lower", StringComparison.OrdinalIgnoreCase) >= 0)
                return TenantClass.Lower;
            if (id.IndexOf("_mid", StringComparison.OrdinalIgnoreCase) >= 0)
                return TenantClass.Mid;
            return TenantClass.Mid;
        }

        static bool Eq(string a, string b) =>
            string.Equals(a, b, StringComparison.Ordinal);
    }
}
