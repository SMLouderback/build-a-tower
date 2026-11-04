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
                cash: 2500),
            new ChallengeDef(
                id: "contract_pop_150",
                title: "Lease-Up Sprint",
                description: "Reach 150 residents before this contract expires.",
                subject: ChallengeSubject.Population,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Contract,
                condition: ChallengeCondition.Threshold(ChallengeMetric.Population, 150),
                cash: 3000),
            new ChallengeDef(
                id: "contract_stars_2",
                title: "Two-Star Showcase",
                description: "Reach a 2-star tower before this contract expires.",
                subject: ChallengeSubject.Stars,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Contract,
                condition: ChallengeCondition.Threshold(ChallengeMetric.Stars, 2),
                cash: 2500),
            new ChallengeDef(
                id: "contract_build_6",
                title: "Construction Push",
                description: "Build 6 rooms before this contract expires.",
                subject: ChallengeSubject.Construction,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Contract,
                condition: ChallengeCondition.Counter(ChallengeMetric.BuildLifetime, 6),
                cash: 2200),
            new ChallengeDef(
                id: "contract_clean_80",
                title: "Spotless Week",
                description: "Reach 80 cleanliness before this contract expires.",
                subject: ChallengeSubject.Operations,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Contract,
                condition: ChallengeCondition.Threshold(ChallengeMetric.EffectiveCleanliness, 80),
                cash: 2000)
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
