using System;

namespace BuildATower
{
    /// <summary>
    /// Starting funds and economy multipliers by <see cref="GameDifficulty"/>.
    /// Normal catalog assets are authored at 1.0 / 1.0; other difficulties scale at runtime.
    /// </summary>
    public static class DifficultyProfile
    {
        public static int StartingFunds(GameDifficulty difficulty) =>
            difficulty switch
            {
                GameDifficulty.Sandbox => 1_125_000,
                GameDifficulty.Easy => 1_500_000,
                GameDifficulty.Normal => 1_125_000,
                GameDifficulty.Hard => 900_000,
                GameDifficulty.Extreme => 600_000,
                _ => 1_125_000
            };

        public static float BuildCostMultiplier(GameDifficulty difficulty) =>
            difficulty switch
            {
                GameDifficulty.Sandbox => 0f,
                GameDifficulty.Easy => 0.75f,
                GameDifficulty.Normal => 1f,
                GameDifficulty.Hard => 1.25f,
                GameDifficulty.Extreme => 1.5f,
                _ => 1f
            };

        public static float IncomeMultiplier(GameDifficulty difficulty) =>
            difficulty switch
            {
                GameDifficulty.Sandbox => 1f,
                GameDifficulty.Easy => 1.25f,
                GameDifficulty.Normal => 1f,
                GameDifficulty.Hard => 0.8f,
                GameDifficulty.Extreme => 0.65f,
                _ => 1f
            };

        public static int EffectiveBuildCost(int nominalCost, GameDifficulty? difficulty = null)
        {
            var d = difficulty ?? GameSession.Difficulty;
            if (d == GameDifficulty.Sandbox) return 0;
            if (nominalCost <= 0) return 0;
            var mult = BuildCostMultiplier(d);
            return Math.Max(0, (int)Math.Ceiling(nominalCost * (double)mult));
        }

        public static int ApplyIncome(int nominalAmount, GameDifficulty? difficulty = null)
        {
            if (nominalAmount <= 0) return 0;
            var d = difficulty ?? GameSession.Difficulty;
            var mult = IncomeMultiplier(d);
            return Math.Max(0, (int)Math.Round(nominalAmount * (double)mult));
        }
    }
}
