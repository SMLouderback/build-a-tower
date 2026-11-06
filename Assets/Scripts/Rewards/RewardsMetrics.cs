namespace BuildATower
{
    /// <summary>Snapshot of tower stats for challenge condition evaluation.</summary>
    public struct RewardsMetrics
    {
        public int Population;
        public int Stars;
        public int RoomCount;
        public int BuildLifetime;
        public int DemolishLifetime;
        public int CondemnCount;
        public bool QuarantineEver;
        public int EffectiveHealth;
        public int EffectiveCleanliness;
        public int DayIndex;
        public int ElevatorShaftCells;
        public int StairCells;

        public int GetMetric(ChallengeMetric metric) =>
            metric switch
            {
                ChallengeMetric.Population => Population,
                ChallengeMetric.Stars => Stars,
                ChallengeMetric.RoomCount => RoomCount,
                ChallengeMetric.BuildLifetime => BuildLifetime,
                ChallengeMetric.DemolishLifetime => DemolishLifetime,
                ChallengeMetric.CondemnCount => CondemnCount,
                ChallengeMetric.QuarantineEver => QuarantineEver ? 1 : 0,
                ChallengeMetric.EffectiveHealth => EffectiveHealth,
                ChallengeMetric.EffectiveCleanliness => EffectiveCleanliness,
                ChallengeMetric.DayIndex => DayIndex,
                ChallengeMetric.ElevatorShaftCells => ElevatorShaftCells,
                ChallengeMetric.StairCells => StairCells,
                _ => 0
            };
    }
}
