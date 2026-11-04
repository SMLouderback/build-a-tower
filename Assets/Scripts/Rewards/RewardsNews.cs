namespace BuildATower
{
    public static class RewardsNews
    {
        public static TowerNewsItem Completed(ChallengeDef def) =>
            new TowerNewsItem
            {
                Category = TowerNewsCategory.MajorEvent,
                Priority = 6,
                Text = $"Reward ready: {def.Title}. Claim ${def.Cash:n0} in the Reward Book.",
                ExpireDayIndex = 5
            };
    }
}
