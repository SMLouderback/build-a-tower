using System;

namespace BuildATower
{
    /// <summary>
    /// Placement spend/afford gated by <see cref="GameSession"/> difficulty.
    /// Room money routes through <see cref="EconomicBalancingManager"/> (VPSF).
    /// Integer overloads remain for legacy nominal amounts (tests / non-room charges).
    /// </summary>
    public static class BuildEconomy
    {
        public static int EffectiveBuildCost(int nominalCost) =>
            DifficultyProfile.EffectiveBuildCost(nominalCost);

        public static int ApplyIncome(int nominalAmount) =>
            DifficultyProfile.ApplyIncome(nominalAmount);

        /// <summary>Full-footprint build charge (difficulty already applied).</summary>
        public static int BuildCost(RoomTypeSO type) =>
            EconomicBalancingManager.BuildCost(type, GameSession.Difficulty);

        /// <summary>
        /// Per billable unit already difficulty-scaled: elevator floor strip, lobby/sky/scaffold cell,
        /// otherwise full footprint.
        /// </summary>
        public static int UnitBuildCost(RoomTypeSO type)
        {
            if (type == null) return 0;
            if (type.isElevatorShaft)
                return EconomicBalancingManager.BuildCost(
                    type, GameSession.Difficulty, Math.Max(1, type.size.x));
            if (type.isLobby || type.isSkyLobby || type.isScaffolding)
                return EconomicBalancingManager.BuildCost(type, GameSession.Difficulty, 1);
            return EconomicBalancingManager.BuildCost(type, GameSession.Difficulty);
        }

        /// <summary>
        /// Placement charge for <paramref name="units"/> billable units (floors/cells).
        /// Already difficulty-scaled — do not pass through <see cref="EffectiveBuildCost"/>.
        /// </summary>
        public static int PlacementCost(RoomTypeSO type, int units = 1)
        {
            if (type == null || units <= 0) return 0;
            if (type.isElevatorShaft || type.isLobby || type.isSkyLobby || type.isScaffolding)
                return UnitBuildCost(type) * units;
            return UnitBuildCost(type);
        }

        public static bool CanAffordCharged(FundsWallet wallet, int chargedAmount)
        {
            if (GameSession.IsSandbox) return true;
            if (chargedAmount <= 0) return true;
            return wallet != null && wallet.CanAfford(chargedAmount);
        }

        public static bool TrySpendCharged(FundsWallet wallet, int chargedAmount)
        {
            if (GameSession.IsSandbox) return true;
            if (chargedAmount <= 0) return true;
            return wallet != null && wallet.TrySpend(chargedAmount);
        }

        public static void RefundCharged(FundsWallet wallet, int chargedAmount)
        {
            if (GameSession.IsSandbox) return;
            if (chargedAmount <= 0) return;
            wallet?.Add(chargedAmount);
        }

        public static int RecordedChargedSpend(int chargedAmount) =>
            GameSession.IsSandbox ? 0 : Math.Max(0, chargedAmount);

        public static bool CanAffordBuild(FundsWallet wallet, int nominalCost)
        {
            if (GameSession.IsSandbox) return true;
            var charged = EffectiveBuildCost(nominalCost);
            return wallet != null && wallet.CanAfford(charged);
        }

        public static bool TrySpendForBuild(FundsWallet wallet, int nominalCost)
        {
            if (GameSession.IsSandbox) return true;
            var charged = EffectiveBuildCost(nominalCost);
            if (charged <= 0) return true;
            return wallet != null && wallet.TrySpend(charged);
        }

        public static void RefundBuild(FundsWallet wallet, int nominalCost)
        {
            if (GameSession.IsSandbox) return;
            var charged = EffectiveBuildCost(nominalCost);
            if (charged <= 0) return;
            wallet?.Add(charged);
        }

        /// <summary>
        /// Amount recorded for grace-refund tracking (what was actually charged).
        /// </summary>
        public static int RecordedSpend(int nominalCost) =>
            GameSession.IsSandbox ? 0 : EffectiveBuildCost(nominalCost);
    }
}
