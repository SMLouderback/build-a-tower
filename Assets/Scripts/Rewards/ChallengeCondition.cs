namespace BuildATower
{
    public enum ChallengeConditionKind
    {
        Threshold,
        DurationHold,
        Counter,
        Flag,
        Compound
    }

    /// <summary>Metrics referenced by challenge condition evaluators.</summary>
    public enum ChallengeMetric
    {
        Population,
        Stars,
        RoomCount,
        BuildLifetime,
        DemolishLifetime,
        CondemnCount,
        QuarantineEver,
        EffectiveHealth,
        EffectiveCleanliness,
        DayIndex,
        ElevatorShaftCells,
        StairCells
    }

    public readonly struct ChallengeCondition
    {
        public ChallengeConditionKind Kind { get; }
        public ChallengeMetric Metric { get; }
        public int ThresholdValue { get; }
        public int HoldDays { get; }
        public int BandMin { get; }
        public int BandMax { get; }
        public int CounterTarget { get; }
        public string FlagId { get; }

        ChallengeCondition(
            ChallengeConditionKind kind,
            ChallengeMetric metric = default,
            int thresholdValue = 0,
            int holdDays = 0,
            int bandMin = 0,
            int bandMax = 0,
            int counterTarget = 0,
            string flagId = null)
        {
            Kind = kind;
            Metric = metric;
            ThresholdValue = thresholdValue;
            HoldDays = holdDays;
            BandMin = bandMin;
            BandMax = bandMax;
            CounterTarget = counterTarget;
            FlagId = flagId;
        }

        public static ChallengeCondition Threshold(ChallengeMetric metric, int value) =>
            new(ChallengeConditionKind.Threshold, metric, thresholdValue: value);

        public static ChallengeCondition DurationHold(ChallengeMetric metric, int bandMin, int bandMax, int days) =>
            new(ChallengeConditionKind.DurationHold, metric, holdDays: days, bandMin: bandMin, bandMax: bandMax);

        public static ChallengeCondition Counter(ChallengeMetric metric, int target) =>
            new(ChallengeConditionKind.Counter, metric, counterTarget: target);

        public static ChallengeCondition Flag(string flagId) =>
            new(ChallengeConditionKind.Flag, flagId: flagId);
    }
}
