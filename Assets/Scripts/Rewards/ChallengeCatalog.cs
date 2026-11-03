using System.Collections.Generic;

namespace BuildATower
{
    public static class ChallengeCatalog
    {
        static readonly Dictionary<string, ChallengeDef> ById = BuildIndex();
        static readonly ChallengeDef[] AllDefs = BuildAll();

        public static IReadOnlyList<ChallengeDef> All => AllDefs;

        public static bool TryGet(string id, out ChallengeDef def) => ById.TryGetValue(id, out def);

        static ChallengeDef[] BuildAll() => new[]
        {
            new ChallengeDef(
                id: "pop_100",
                title: "Growing Community",
                description: "Reach 100 residents in your tower.",
                subject: ChallengeSubject.Population,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Milestone,
                condition: ChallengeCondition.Threshold(ChallengeMetric.Population, 100),
                cash: 5000),
            new ChallengeDef(
                id: "health_hold_7",
                title: "Clean Bill of Health",
                description: "Keep tower health at 80 or better for 7 days.",
                subject: ChallengeSubject.Operations,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Milestone,
                condition: ChallengeCondition.DurationHold(ChallengeMetric.EffectiveHealth, 80, 100, 7),
                cash: 3500),
            new ChallengeDef(
                id: "demolish_10",
                title: "Measured Remodel",
                description: "Demolish 10 rooms over the tower lifetime.",
                subject: ChallengeSubject.Demolition,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Milestone,
                condition: ChallengeCondition.Threshold(ChallengeMetric.DemolishLifetime, 10),
                cash: 2500)
        };

        static Dictionary<string, ChallengeDef> BuildIndex()
        {
            var map = new Dictionary<string, ChallengeDef>();
            foreach (var def in BuildAll())
                map[def.Id] = def;
            return map;
        }
    }
}
