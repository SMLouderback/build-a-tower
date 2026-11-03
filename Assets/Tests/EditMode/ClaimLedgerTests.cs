using System;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ClaimLedgerTests
    {
        static ChallengeDef MakeDef(
            string id,
            ChallengeSubject subject,
            TrophyMetal metal,
            int cash) =>
            new ChallengeDef(
                id,
                title: id,
                description: id,
                subject,
                metal,
                ChallengeKind.Milestone,
                ChallengeCondition.Threshold(ChallengeMetric.Population, 1),
                cash);

        static Func<string, ChallengeDef?> Resolver(params ChallengeDef[] defs)
        {
            var map = new System.Collections.Generic.Dictionary<string, ChallengeDef>();
            foreach (var d in defs)
                map[d.Id] = d;
            return id => map.TryGetValue(id, out var d) ? d : null;
        }

        [Test]
        public void Enqueue_then_TryClaim_pays_cash_and_clears_pending()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(100);
            var trophyCase = new TrophyCase();
            var def = MakeDef("a", ChallengeSubject.Population, TrophyMetal.Bronze, 5000);

            ledger.EnqueuePending("a");
            var result = ledger.TryClaim("a", wallet, trophyCase, NoOpRewardExtras.Instance, def);

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(5000, result.CashPaid);
            Assert.AreEqual(5100, wallet.Balance);
            Assert.IsFalse(ledger.IsPending("a"));
            Assert.IsTrue(ledger.IsClaimed("a"));
        }

        [Test]
        public void Second_TryClaim_same_id_fails_with_no_cash()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var def = MakeDef("a", ChallengeSubject.Population, TrophyMetal.Bronze, 5000);

            ledger.EnqueuePending("a");
            Assert.IsTrue(ledger.TryClaim("a", wallet, trophyCase, NoOpRewardExtras.Instance, def).Ok);

            var second = ledger.TryClaim("a", wallet, trophyCase, NoOpRewardExtras.Instance, def);

            Assert.IsFalse(second.Ok);
            Assert.AreEqual(0, second.CashPaid);
            Assert.AreEqual(5000, wallet.Balance);
        }

        [Test]
        public void EnqueuePending_ignores_duplicate_and_already_claimed()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var def = MakeDef("a", ChallengeSubject.Population, TrophyMetal.Bronze, 100);

            ledger.EnqueuePending("a");
            ledger.EnqueuePending("a");
            Assert.IsTrue(ledger.TryClaim("a", wallet, trophyCase, NoOpRewardExtras.Instance, def).Ok);

            ledger.EnqueuePending("a");
            Assert.IsFalse(ledger.IsPending("a"));
        }

        [Test]
        public void ClaimAll_sums_cash_from_pending_snapshot()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var defA = MakeDef("a", ChallengeSubject.Population, TrophyMetal.Bronze, 3000);
            var defB = MakeDef("b", ChallengeSubject.Stars, TrophyMetal.Silver, 2000);

            ledger.EnqueuePending("a");
            ledger.EnqueuePending("b");

            var all = ledger.ClaimAll(
                wallet,
                trophyCase,
                NoOpRewardExtras.Instance,
                Resolver(defA, defB));

            Assert.AreEqual(5000, all.TotalCashPaid);
            Assert.AreEqual(5000, wallet.Balance);
            Assert.IsFalse(ledger.IsPending("a"));
            Assert.IsFalse(ledger.IsPending("b"));
        }

        [Test]
        public void TryClaim_upgrades_metal_when_higher()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var bronze = MakeDef("bronze", ChallengeSubject.Population, TrophyMetal.Bronze, 100);
            var gold = MakeDef("gold", ChallengeSubject.Population, TrophyMetal.Gold, 100);

            ledger.EnqueuePending("bronze");
            ledger.TryClaim("bronze", wallet, trophyCase, NoOpRewardExtras.Instance, bronze);
            Assert.AreEqual(TrophyMetal.Bronze, trophyCase.GetMetal(ChallengeSubject.Population));

            ledger.EnqueuePending("gold");
            var result = ledger.TryClaim("gold", wallet, trophyCase, NoOpRewardExtras.Instance, gold);

            Assert.IsTrue(result.MetalRaised);
            Assert.AreEqual(TrophyMetal.Gold, trophyCase.GetMetal(ChallengeSubject.Population));
        }

        [Test]
        public void TryClaim_does_not_downgrade_trophy_metal()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var gold = MakeDef("gold", ChallengeSubject.Population, TrophyMetal.Gold, 400);
            var bronze = MakeDef("bronze", ChallengeSubject.Population, TrophyMetal.Bronze, 100);

            ledger.EnqueuePending("gold");
            ledger.TryClaim("gold", wallet, trophyCase, NoOpRewardExtras.Instance, gold);

            ledger.EnqueuePending("bronze");
            var result = ledger.TryClaim("bronze", wallet, trophyCase, NoOpRewardExtras.Instance, bronze);

            Assert.IsFalse(result.MetalRaised);
            Assert.AreEqual(TrophyMetal.Gold, trophyCase.GetMetal(ChallengeSubject.Population));
            Assert.AreEqual(500, wallet.Balance);
        }

        [Test]
        public void First_trophy_on_tower_flag_set_once()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var pop = MakeDef("pop", ChallengeSubject.Population, TrophyMetal.Bronze, 10);
            var stars = MakeDef("stars", ChallengeSubject.Stars, TrophyMetal.Silver, 10);

            ledger.EnqueuePending("pop");
            var first = ledger.TryClaim("pop", wallet, trophyCase, NoOpRewardExtras.Instance, pop);
            Assert.IsTrue(first.FirstTrophyOnTower);

            ledger.EnqueuePending("stars");
            var second = ledger.TryClaim("stars", wallet, trophyCase, NoOpRewardExtras.Instance, stars);
            Assert.IsFalse(second.FirstTrophyOnTower);
        }

        [Test]
        public void ClaimAll_firstTrophyOnTower_when_any_claim_awards_first_metal()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var pop = MakeDef("pop", ChallengeSubject.Population, TrophyMetal.Bronze, 10);
            var stars = MakeDef("stars", ChallengeSubject.Stars, TrophyMetal.Silver, 10);

            ledger.EnqueuePending("pop");
            ledger.EnqueuePending("stars");

            var all = ledger.ClaimAll(
                wallet,
                trophyCase,
                NoOpRewardExtras.Instance,
                Resolver(pop, stars));

            Assert.IsTrue(all.FirstTrophyOnTower);
        }
    }
}
