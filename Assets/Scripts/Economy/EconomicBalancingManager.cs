using System;

namespace BuildATower
{
    /// <summary>
    /// Pure VPSF money API: build cost, period income/sale payout, and upkeep.
    /// Call sites stay dumb — difficulty, price tier, climate, pulse, and floor-fit apply here.
    /// </summary>
    public static class EconomicBalancingManager
    {
        public static int BuildCost(RoomTypeSO type, GameDifficulty difficulty)
        {
            if (!TryRentEquivalent(type, out var rentEq))
                return 0;

            var nominal = (int)Math.Round(rentEq * VpsfCatalog.BuildCostPerIncomeCell);
            return DifficultyProfile.EffectiveBuildCost(nominal, difficulty);
        }

        public static int PeriodIncome(
            RoomTypeSO type,
            int priceTier,
            GameDifficulty difficulty,
            float climateSpendMult,
            float pulseMult,
            float floorFit01)
        {
            if (!TryRentEquivalent(type, out var rentEq))
                return 0;

            var scaled = rentEq
                * PricePricing.PayoutMultiplier(priceTier)
                * DifficultyProfile.IncomeMultiplier(difficulty)
                * climateSpendMult
                * pulseMult
                * floorFit01;

            if (scaled <= 0f) return 0;
            return Math.Max(0, (int)Math.Round(scaled));
        }

        public static int PeriodUpkeep(RoomTypeSO type, GameDifficulty difficulty, float pulseMult)
        {
            if (!TryIdentityRent(type, out var rentEq, out var tenantClass))
                return 0;

            var scaled = rentEq
                * VpsfCatalog.UpkeepRatio(tenantClass)
                * DifficultyProfile.IncomeMultiplier(difficulty)
                * pulseMult;

            if (scaled <= 0f) return 0;
            return Math.Max(0, (int)Math.Round(scaled));
        }

        static bool TryRentEquivalent(RoomTypeSO type, out float rentEq) =>
            TryIdentityRent(type, out rentEq, out _);

        static bool TryIdentityRent(RoomTypeSO type, out float rentEq, out TenantClass tenantClass)
        {
            rentEq = 0f;
            tenantClass = TenantClass.Mid;

            if (!VpsfCatalog.TryIdentity(type, out var family, out var tier, out tenantClass))
                return false;

            var cells = VpsfCatalog.Cells(type);
            if (cells <= 0) return false;

            var basePerCell = VpsfCatalog.BasePerCell(family);
            if (basePerCell <= 0f) return false;

            var tierMult = VpsfCatalog.TierMultiplier(tier);
            if (tierMult <= 0f) return false;

            rentEq = basePerCell * cells * tierMult * VpsfCatalog.ClassMultiplier(tenantClass);
            return rentEq > 0f;
        }
    }
}
