namespace BuildATower
{
    public static class ResearchHudPanel
    {
        public static string StatusCaption(ResearchSystem research)
        {
            if (research == null || !research.IsRunning)
                return "Research · Idle";

            if (research.IsPaused)
                return "Research · Paused";

            var branch = research.ActiveBranch!.Value;
            return $"Research · {ResearchCatalog.BranchDisplayName(branch)} L{research.ActiveLevel}";
        }
    }
}
