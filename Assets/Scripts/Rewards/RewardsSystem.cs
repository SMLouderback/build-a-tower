using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class RewardsSystem
    {
        readonly IReadOnlyList<ChallengeDef> _catalog;

        public RewardsSystem(
            ChallengeTracker tracker = null,
            ClaimLedger ledger = null,
            TrophyCase trophyCase = null,
            ContractBoard board = null,
            IReadOnlyList<ChallengeDef> catalog = null,
            RewardExtras extras = null)
        {
            Tracker = tracker ?? new ChallengeTracker();
            Ledger = ledger ?? new ClaimLedger();
            TrophyCase = trophyCase ?? new TrophyCase();
            _catalog = catalog ?? ChallengeCatalog.All;
            Extras = extras ?? new RewardExtras();
            Board = board ?? new ContractBoard();
            Board.Refresh(0, _catalog, Ledger);
        }

        public ChallengeTracker Tracker { get; }
        public ClaimLedger Ledger { get; }
        public TrophyCase TrophyCase { get; }
        public ContractBoard Board { get; }
        public RewardExtras Extras { get; }
        public bool RewardTutorialSeen { get; set; }

        public event Action<ChallengeSubject, TrophyMetal> OnTrophyUpgraded;

        public static bool IsAbsent(RewardsSnapshotV1 snapshot) =>
            snapshot == null || !snapshot.present;

        public RewardsSnapshotV1 CaptureSnapshot() =>
            new RewardsSnapshotV1
            {
                present = true,
                pendingIds = Ledger.CapturePendingIds(),
                claimedIds = Ledger.CaptureClaimedIds(),
                trophies = TrophyCase.CaptureSnapshot(),
                rewardTutorialSeen = RewardTutorialSeen,
                contracts = Board.CaptureSnapshot(),
                extras = Extras.CaptureSnapshot()
            };

        public void RestoreSnapshot(RewardsSnapshotV1 snapshot, int dayIndex = 0)
        {
            Ledger.RestoreSnapshot(null, null);
            TrophyCase.RestoreSnapshot(null);
            Extras.RestoreSnapshot(null);
            RewardTutorialSeen = false;

            if (IsAbsent(snapshot))
            {
                Board.RestoreDefault(dayIndex < 0 ? 0 : dayIndex, _catalog, Ledger);
                return;
            }

            Ledger.RestoreSnapshot(snapshot.pendingIds, snapshot.claimedIds);
            TrophyCase.RestoreSnapshot(snapshot.trophies);
            Extras.RestoreSnapshot(snapshot.extras);
            RewardTutorialSeen = snapshot.rewardTutorialSeen;
            Board.RestoreSnapshot(snapshot.contracts, _catalog);
        }

        public int Evaluate(RewardsMetrics metrics, TowerNews news) =>
            EvaluateInternal(metrics, news, daily: false);

        public int RecordBuild(RewardsMetrics metrics, TowerNews news, int count = 1)
        {
            Tracker.RecordBuild(count);
            return Evaluate(metrics, news);
        }

        public int RecordDemolish(RewardsMetrics metrics, TowerNews news, int count = 1)
        {
            Tracker.RecordDemolish(count);
            return Evaluate(metrics, news);
        }

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

            if (daily)
                Board.OnDayRolled(Tracker.WithLifetimeCounters(metrics), Ledger, _catalog, news);
            else
                Board.Evaluate(Tracker.WithLifetimeCounters(metrics), Ledger);

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

        public ClaimResult TryClaim(string id, FundsWallet wallet, ChallengeDef def)
        {
            var result = Ledger.TryClaim(id, wallet, TrophyCase, Extras, def);
            if (result.Ok && result.MetalRaised)
                OnTrophyUpgraded?.Invoke(def.Subject, def.Metal);
            return result;
        }
    }
}
