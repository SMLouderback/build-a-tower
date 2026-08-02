namespace BuildATower
{
    /// <summary>
    /// Maps completed research levels to gameplay multipliers (spec §4.1).
    /// </summary>
    public static class ResearchEffects
    {
        public static float ShopSpendMultiplier(ResearchSystem r) =>
            ResearchCatalog.ShopSpendMultiplier(Level(r, ResearchBranch.Marketing));

        public static float ElevatorSpeedMultiplier(ResearchSystem r) =>
            ResearchCatalog.ElevatorSpeedMultiplier(Level(r, ResearchBranch.Elevator));

        public static float ElevatorRoutingWaitWeightScale(ResearchSystem r) =>
            ResearchCatalog.ElevatorRoutingWaitWeightScale(Level(r, ResearchBranch.Elevator));

        public static float CrimeSuppressionMultiplier(ResearchSystem r) =>
            ResearchCatalog.CrimeSuppressionMultiplier(Level(r, ResearchBranch.Security));

        public static float CleanMinutesMultiplier(ResearchSystem r) =>
            ResearchCatalog.CleanMinutesMultiplier(Level(r, ResearchBranch.Housekeeping));

        public static float RepairMinutesMultiplier(ResearchSystem r) =>
            ResearchCatalog.RepairMinutesMultiplier(Level(r, ResearchBranch.Maintenance));

        public static float RepairChunkMultiplier(ResearchSystem r) =>
            ResearchCatalog.RepairChunkMultiplier(Level(r, ResearchBranch.Maintenance));

        static int Level(ResearchSystem r, ResearchBranch branch) =>
            r == null ? 0 : r.HighestCompleted(branch);
    }
}
