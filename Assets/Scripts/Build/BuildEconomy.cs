namespace BuildATower
{
    /// <summary>
    /// Placement spend/afford gated by <see cref="GameSession"/> difficulty.
    /// Applies <see cref="DifficultyProfile"/> build-cost multipliers; Sandbox is free.
    /// </summary>
    public static class BuildEconomy
    {
        public static int EffectiveBuildCost(int nominalCost) =>
            DifficultyProfile.EffectiveBuildCost(nominalCost);

        public static int ApplyIncome(int nominalAmount) =>
            DifficultyProfile.ApplyIncome(nominalAmount);

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
