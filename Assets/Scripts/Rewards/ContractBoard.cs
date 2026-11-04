using System.Collections.Generic;

namespace BuildATower
{
    public readonly struct ActiveContract
    {
        public ChallengeDef Def { get; }
        public int StartedDayIndex { get; }
        public int ExpiresDayIndex { get; }
        public int ProgressValue { get; }

        public ActiveContract(ChallengeDef def, int startedDayIndex, int expiresDayIndex, int progressValue = 0)
        {
            Def = def;
            StartedDayIndex = startedDayIndex;
            ExpiresDayIndex = expiresDayIndex;
            ProgressValue = progressValue;
        }

        public ActiveContract WithProgress(int progressValue) =>
            new ActiveContract(Def, StartedDayIndex, ExpiresDayIndex, progressValue);
    }

    public sealed class ContractBoard
    {
        const int DefaultMinSlots = 3;
        const int DefaultMaxSlots = 5;
        const int DefaultOfferDays = 7;

        readonly int _minSlots;
        readonly int _maxSlots;
        readonly int _offerDays;
        readonly List<ActiveContract> _active = new List<ActiveContract>();

        public ContractBoard(int minSlots = DefaultMinSlots, int maxSlots = DefaultMaxSlots, int offerDays = DefaultOfferDays)
        {
            _minSlots = minSlots < 1 ? DefaultMinSlots : minSlots;
            _maxSlots = maxSlots < _minSlots ? _minSlots : maxSlots;
            _offerDays = offerDays < 1 ? DefaultOfferDays : offerDays;
        }

        public IReadOnlyList<ActiveContract> Active => _active;

        public bool Contains(string id)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Def.Id == id)
                    return true;
            }

            return false;
        }

        public int Refresh(int dayIndex, IReadOnlyList<ChallengeDef> catalog, ClaimLedger ledger)
        {
            if (catalog == null || ledger == null)
                return 0;

            var added = 0;
            for (var i = 0; i < catalog.Count && _active.Count < _minSlots && _active.Count < _maxSlots; i++)
            {
                var def = catalog[i];
                if (def.Kind != ChallengeKind.Contract)
                    continue;
                if (Contains(def.Id) || ledger.IsPending(def.Id) || ledger.IsClaimed(def.Id))
                    continue;

                _active.Add(new ActiveContract(def, dayIndex, dayIndex + _offerDays));
                added++;
            }

            return added;
        }

        public int Evaluate(RewardsMetrics metrics, ClaimLedger ledger, TowerNews news = null)
        {
            if (ledger == null)
                return 0;

            var completed = 0;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var offer = _active[i];
                if (metrics.DayIndex > offer.ExpiresDayIndex)
                {
                    _active.RemoveAt(i);
                    news?.Push(RewardsNews.ContractExpired(offer.Def, metrics.DayIndex));
                    continue;
                }

                var progress = GetProgress(offer.Def.Condition, metrics);
                _active[i] = offer.WithProgress(progress);
                if (!IsComplete(offer.Def.Condition, progress))
                    continue;

                ledger.EnqueuePending(offer.Def.Id);
                _active.RemoveAt(i);
                completed++;
            }

            return completed;
        }

        public int OnDayRolled(
            RewardsMetrics metrics,
            ClaimLedger ledger,
            IReadOnlyList<ChallengeDef> catalog,
            TowerNews news)
        {
            var completed = Evaluate(metrics, ledger, news);
            if (metrics.DayIndex > 0 && metrics.DayIndex % 7 == 0)
                Refresh(metrics.DayIndex, catalog, ledger);
            return completed;
        }

        static int GetProgress(ChallengeCondition condition, RewardsMetrics metrics) =>
            condition.Kind switch
            {
                ChallengeConditionKind.Counter => metrics.GetMetric(condition.Metric),
                _ => metrics.GetMetric(condition.Metric)
            };

        static bool IsComplete(ChallengeCondition condition, int progress) =>
            condition.Kind switch
            {
                ChallengeConditionKind.Counter => progress >= condition.CounterTarget,
                ChallengeConditionKind.Threshold => progress >= condition.ThresholdValue,
                ChallengeConditionKind.DurationHold => progress >= condition.HoldDays,
                _ => false
            };
    }
}
