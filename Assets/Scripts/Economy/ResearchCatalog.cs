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
    }
}
