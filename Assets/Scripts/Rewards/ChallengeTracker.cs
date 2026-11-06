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

        public DurationStreakSnapshotV1[] CaptureDurationStreaks()
        {
            if (_durationStreaks.Count == 0 && _durationLastDay.Count == 0)
                return null;

            var ids = new HashSet<string>();
            foreach (var key in _durationStreaks.Keys)
                ids.Add(key);
            foreach (var key in _durationLastDay.Keys)
                ids.Add(key);

            var snapshot = new DurationStreakSnapshotV1[ids.Count];
            var index = 0;
            foreach (var id in ids)
            {
                _durationStreaks.TryGetValue(id, out var streak);
                _durationLastDay.TryGetValue(id, out var lastDay);
                snapshot[index++] = new DurationStreakSnapshotV1
                {
                    challengeId = id,
                    streak = streak,
                    lastDayIndex = lastDay
                };
            }

            return snapshot;
        }

        public void RestoreDurationStreaks(DurationStreakSnapshotV1[] snapshots)
        {
            _durationStreaks.Clear();
            _durationLastDay.Clear();
            if (snapshots == null)
                return;

            for (var i = 0; i < snapshots.Length; i++)
            {
                var entry = snapshots[i];
                if (entry == null || string.IsNullOrEmpty(entry.challengeId))
                    continue;

                if (entry.streak > 0)
                    _durationStreaks[entry.challengeId] = entry.streak;
                if (entry.lastDayIndex > 0)
                    _durationLastDay[entry.challengeId] = entry.lastDayIndex;
            }
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
