namespace BuildATower
{
    public enum ResearchBranch
    {
        Marketing,
        Elevator,
        Security,
        Housekeeping,
        Maintenance
    }

    public static class ResearchCatalog
    {
        public const int MaxLevel = 3;
        public const float ResearcherSpeedBonus = 0.35f;
        public const int IdlePerLabPerDay = 500;
        public const int ActivePerDay = 2000;
        public const float DecayFractionPerDay = 0.05f;

        public static int BaseWorkMinutes(int level) => level switch
        {
            1 => 1440,
            2 => 4320,
            3 => 10080,
            _ => 0
        };

        public static string BranchDisplayName(ResearchBranch branch) => branch switch
        {
            ResearchBranch.Marketing => "Marketing",
            ResearchBranch.Elevator => "Elevator Ops",
            ResearchBranch.Security => "Security Training",
            ResearchBranch.Housekeeping => "Housekeeping",
            ResearchBranch.Maintenance => "Maintenance",
            _ => branch.ToString()
        };

        // Spec §4.1 — cumulative from highest completed level (0 = identity).

        public static float ShopSpendMultiplier(int level) => level switch
        {
            1 => 1.10f,
            2 => 1.20f,
            3 => 1.35f,
            _ => 1f
        };

        public static float ElevatorSpeedMultiplier(int level) => level switch
        {
            1 => 1.10f,
            2 => 1.20f,
            3 => 1.35f,
            _ => 1f
        };

        /// <summary>I = identity; II/III reduce WaitWeight for better queue scoring.</summary>
        public static float ElevatorRoutingWaitWeightScale(int level) => level switch
        {
            2 => 0.85f,
            3 => 0.70f,
            _ => 1f
        };

        public static float CrimeSuppressionMultiplier(int level) => level switch
        {
            1 => 1.15f,
            2 => 1.30f,
            3 => 1.50f,
            _ => 1f
        };

        public static float CleanMinutesMultiplier(int level) => level switch
        {
            1 => 0.90f,
            2 => 0.80f,
            3 => 0.65f,
            _ => 1f
        };

        public static float RepairMinutesMultiplier(int level) => level switch
        {
            1 => 0.90f,
            2 => 0.80f,
            3 => 0.65f,
            _ => 1f
        };

        public static float RepairChunkMultiplier(int level) => level switch
        {
            1 => 1.10f,
            2 => 1.25f,
            3 => 1.45f,
            _ => 1f
        };
    }
}
