using System.Collections.Generic;

namespace BuildATower
{
    public static class ChallengeTracker
    {
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
    }
}
