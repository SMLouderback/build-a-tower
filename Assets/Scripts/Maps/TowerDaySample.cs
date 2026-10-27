namespace BuildATower
{
    /// <summary>One midnight snapshot for the Maps analytics graph (~90 days).</summary>
    public readonly struct TowerDaySample
    {
        public readonly int DayIndex;
        public readonly int ClimateStep;
        public readonly float SpendMult;
        public readonly float Vacancy;
        public readonly int Population;
        public readonly int DailyIncome;
        public readonly int DailyExpense;
        public readonly int Savings;
        public readonly int Stars;

        public TowerDaySample(
            int dayIndex,
            int climateStep,
            float spendMult,
            float vacancy,
            int population,
            int dailyIncome,
            int dailyExpense,
            int savings,
            int stars)
        {
            DayIndex = dayIndex;
            ClimateStep = climateStep;
            SpendMult = spendMult;
            Vacancy = vacancy;
            Population = population;
            DailyIncome = dailyIncome;
            DailyExpense = dailyExpense;
            Savings = savings;
            Stars = stars;
        }
    }

    /// <summary>Day index when a star tier was first earned (for timeline markers).</summary>
    public readonly struct StarEarnEvent
    {
        public readonly int DayIndex;
        public readonly int Stars;

        public StarEarnEvent(int dayIndex, int stars)
        {
            DayIndex = dayIndex;
            Stars = stars;
        }
    }
}
