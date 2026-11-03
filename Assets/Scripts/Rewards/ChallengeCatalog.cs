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
                cash: 5000)
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
