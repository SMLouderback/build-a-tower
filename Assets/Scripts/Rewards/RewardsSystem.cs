using System.Collections.Generic;

namespace BuildATower
{
    public sealed class RewardsSystem
    {
        readonly IReadOnlyList<ChallengeDef> _catalog;
        readonly object _board = new object();

        public RewardsSystem(
            ChallengeTracker tracker = null,
            ClaimLedger ledger = null,
            TrophyCase trophyCase = null,
            IReadOnlyList<ChallengeDef> catalog = null)
        {
            Tracker = tracker ?? new ChallengeTracker();
            Ledger = ledger ?? new ClaimLedger();
            TrophyCase = trophyCase ?? new TrophyCase();
            _catalog = catalog ?? ChallengeCatalog.All;
        }

        public ChallengeTracker Tracker { get; }
        public ClaimLedger Ledger { get; }
        public TrophyCase TrophyCase { get; }
        public object Board => _board;
        public bool RewardTutorialSeen { get; set; }

        public int Evaluate(RewardsMetrics metrics, TowerNews news) =>
            EvaluateInternal(metrics, news, daily: false);

        public int OnDayRolled(RewardsMetrics metrics, TowerNews news) =>
            EvaluateInternal(metrics, news, daily: true);

        int EvaluateInternal(RewardsMetrics metrics, TowerNews news, bool daily)
        {
            var wasPending = new HashSet<string>();
            for (var i = 0; i < _catalog.Count; i++)
            {
                var id = _catalog[i].Id;
                if (Ledger.IsPending(id))
                    wasPending.Add(id);
            }

            if (daily)
                Tracker.EvaluateDaily(metrics, Ledger, _catalog);
            else
                ChallengeTracker.EvaluateThresholds(Tracker.WithLifetimeCounters(metrics), Ledger, _catalog);

            var newlyPending = 0;
            for (var i = 0; i < _catalog.Count; i++)
            {
                var def = _catalog[i];
                if (!Ledger.IsPending(def.Id) || wasPending.Contains(def.Id))
                    continue;

                newlyPending++;
                var item = RewardsNews.Completed(def);
                item.CreatedDayIndex = metrics.DayIndex;
                item.ExpireDayIndex = metrics.DayIndex + 5;
                news?.Push(item);
            }

            return newlyPending;
        }
    }
}
