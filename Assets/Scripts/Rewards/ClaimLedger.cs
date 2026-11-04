using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildATower
{
    public interface IRewardExtras
    {
        void OnClaim(ChallengeDef def);
    }

    public sealed class NoOpRewardExtras : IRewardExtras
    {
        public static readonly NoOpRewardExtras Instance = new NoOpRewardExtras();

        NoOpRewardExtras() { }

        public void OnClaim(ChallengeDef def) { }
    }

    public readonly struct ClaimResult
    {
        public bool Ok { get; }
        public int CashPaid { get; }
        public bool MetalRaised { get; }
        public bool FirstTrophyOnTower { get; }

        public ClaimResult(bool ok, int cashPaid, bool metalRaised, bool firstTrophyOnTower)
        {
            Ok = ok;
            CashPaid = cashPaid;
            MetalRaised = metalRaised;
            FirstTrophyOnTower = firstTrophyOnTower;
        }

        public static ClaimResult Failed => new ClaimResult(false, 0, false, false);
    }

    public readonly struct ClaimAllResult
    {
        public int TotalCashPaid { get; }
        public bool FirstTrophyOnTower { get; }

        public ClaimAllResult(int totalCashPaid, bool firstTrophyOnTower)
        {
            TotalCashPaid = totalCashPaid;
            FirstTrophyOnTower = firstTrophyOnTower;
        }
    }

    public sealed class ClaimLedger
    {
        readonly HashSet<string> _pending = new HashSet<string>();
        readonly HashSet<string> _claimed = new HashSet<string>();

        public int PendingCount => _pending.Count;

        public bool IsPending(string id) => _pending.Contains(id);

        public bool IsClaimed(string id) => _claimed.Contains(id);

        public void EnqueuePending(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (_claimed.Contains(id))
                return;
            if (_pending.Contains(id))
                return;
            _pending.Add(id);
        }

        public ClaimResult TryClaim(
            string id,
            FundsWallet wallet,
            TrophyCase trophyCase,
            IRewardExtras extras,
            ChallengeDef def)
        {
            if (!_pending.Contains(id) || _claimed.Contains(id))
                return ClaimResult.Failed;

            _pending.Remove(id);
            _claimed.Add(id);

            wallet.Add(def.Cash);
            extras?.OnClaim(def);
            var (metalRaised, firstTrophy) = trophyCase.ApplyClaimMetal(def.Subject, def.Metal);

            return new ClaimResult(true, def.Cash, metalRaised, firstTrophy);
        }

        public ClaimAllResult ClaimAll(
            FundsWallet wallet,
            TrophyCase trophyCase,
            IRewardExtras extras,
            Func<string, ChallengeDef?> resolveDef = null)
        {
            resolveDef ??= ResolveFromCatalog;
            var snapshot = _pending.ToArray();
            var totalCash = 0;
            var firstTrophy = false;

            foreach (var id in snapshot)
            {
                if (!resolveDef(id).HasValue)
                    continue;

                var result = TryClaim(id, wallet, trophyCase, extras, resolveDef(id).Value);
                if (!result.Ok)
                    continue;

                totalCash += result.CashPaid;
                if (result.FirstTrophyOnTower)
                    firstTrophy = true;
            }

            return new ClaimAllResult(totalCash, firstTrophy);
        }

        static ChallengeDef? ResolveFromCatalog(string id) =>
            ChallengeCatalog.TryGet(id, out var def) ? def : (ChallengeDef?)null;
    }
}
