using System.Collections.Generic;

namespace BuildATower
{
    public sealed class ChallengeTracker
    {
        readonly Dictionary<string, int> _durationStreaks = new Dictionary<string, int>();
        readonly Dictionary<string, int> _durationLastDay = new Dictionary<string, int>();

        public int BuildLifetime { get; private set; }
        public int DemolishLifetime { get; private set; }

        public void RecordBuild(int count = 1)
        {
            if (count > 0)
                BuildLifetime += count;
        }

        public void RecordDemolish(int count = 1)
        {
            if (count > 0)
                DemolishLifetime += count;
        }

        public RewardsMetrics WithLifetimeCounters(RewardsMetrics metrics)
        {
            if (BuildLifetime > metrics.BuildLifetime)
                metrics.BuildLifetime = BuildLifetime;
            if (DemolishLifetime > metrics.DemolishLifetime)
                metrics.DemolishLifetime = DemolishLifetime;
            return metrics;
        }

        public static void EvaluateThresholds(
            RewardsMetrics metrics,
            ClaimLedger ledger,
            IReadOnlyList<ChallengeDef> catalog)
        {
            if (ledger == null || catalog == null)
                return;

            foreach (var def in catalog)
            {
                if (def.Kind == ChallengeKind.Contract)
                    continue;

                if (ledger.IsClaimed(def.Id) || ledger.IsPending(def.Id))
                    continue;

                var condition = def.Condition;
                if (condition.Kind != ChallengeConditionKind.Threshold)
                    continue;

                var current = metrics.GetMetric(condition.Metric);
                if (current >= condition.ThresholdValue)
                    ledger.EnqueuePending(def.Id);
            }
        }

        public void EvaluateDaily(
            RewardsMetrics metrics,
            ClaimLedger ledger,
            IReadOnlyList<ChallengeDef> catalog)
        {
            if (ledger == null || catalog == null)
                return;

            metrics = WithLifetimeCounters(metrics);
            EvaluateThresholds(metrics, ledger, catalog);

            foreach (var def in catalog)
            {
                if (def.Kind == ChallengeKind.Contract)
                    continue;

                if (ledger.IsClaimed(def.Id) || ledger.IsPending(def.Id))
                    continue;

                var condition = def.Condition;
                if (condition.Kind != ChallengeConditionKind.DurationHold)
                    continue;

                if (metrics.DayIndex > 0 &&
                    _durationLastDay.TryGetValue(def.Id, out var lastDay) &&
                    lastDay == metrics.DayIndex)
                {
                    continue;
                }

                var current = metrics.GetMetric(condition.Metric);
                var inBand = current >= condition.BandMin && current <= condition.BandMax;
                _durationStreaks[def.Id] = inBand
                    ? _durationStreaks.TryGetValue(def.Id, out var streak) ? streak + 1 : 1
                    : 0;

                if (metrics.DayIndex > 0)
                    _durationLastDay[def.Id] = metrics.DayIndex;

                if (_durationStreaks[def.Id] >= condition.HoldDays)
                    ledger.EnqueuePending(def.Id);
            }
        }
    }
}
