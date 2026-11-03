namespace BuildATower
{
    public readonly struct ChallengeDef
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public ChallengeSubject Subject { get; }
        public TrophyMetal Metal { get; }
        public ChallengeKind Kind { get; }
        public ChallengeCondition Condition { get; }
        public int Cash { get; }

        public ChallengeDef(
            string id,
            string title,
            string description,
            ChallengeSubject subject,
            TrophyMetal metal,
            ChallengeKind kind,
            ChallengeCondition condition,
            int cash)
        {
            Id = id;
            Title = title;
            Description = description;
            Subject = subject;
            Metal = metal;
            Kind = kind;
            Condition = condition;
            Cash = cash;
        }
    }
}
