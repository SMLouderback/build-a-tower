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

        public static TowerNewsItem ContractExpired(ChallengeDef def, int dayIndex) =>
            new TowerNewsItem
            {
                Category = TowerNewsCategory.Quirk,
                Priority = 2,
                Text = $"Contract expired: {def.Title}. The tower missed the bonus window, but the board has fresh work lined up.",
                CreatedDayIndex = dayIndex,
                ExpireDayIndex = dayIndex + 5
            };
    }
}
