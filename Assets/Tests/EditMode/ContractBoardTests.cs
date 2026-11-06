using System.Collections.Generic;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ContractBoardTests
    {
        [Test]
        public void Refresh_fills_three_to_five_contract_slots()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();

            board.Refresh(0, ChallengeCatalog.All, ledger);

            Assert.GreaterOrEqual(board.Active.Count, 3);
            Assert.LessOrEqual(board.Active.Count, 5);
            foreach (var offer in board.Active)
                Assert.AreEqual(ChallengeKind.Contract, offer.Def.Kind);
        }

        [Test]
        public void Refresh_skips_claimed_contracts()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            Assert.IsTrue(ChallengeCatalog.TryGet("contract_pop_150", out var claimedContract));

            ledger.EnqueuePending(claimedContract.Id);
            ledger.TryClaim(claimedContract.Id, new FundsWallet(0), new TrophyCase(), NoOpRewardExtras.Instance, claimedContract);

            board.Refresh(0, ChallengeCatalog.All, ledger);

            foreach (var offer in board.Active)
                Assert.AreNotEqual("contract_pop_150", offer.Def.Id);
        }

        [Test]
        public void Evaluate_success_enqueues_pending_and_removes_contract()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            board.Refresh(0, ChallengeCatalog.All, ledger);

            board.Evaluate(
                new RewardsMetrics { Population = 150, DayIndex = 2 },
                ledger,
                catalog: ChallengeCatalog.All);

            Assert.IsTrue(ledger.IsPending("contract_pop_150"));
            Assert.IsFalse(board.Contains("contract_pop_150"));
        }

        [Test]
        public void Counter_contract_uses_progress_since_offer_not_lifetime_total()
        {
            Assert.IsTrue(ChallengeCatalog.TryGet("contract_demo_10", out var demo));
            var catalog = new List<ChallengeDef> { demo };
            var board = new ContractBoard(minSlots: 1, maxSlots: 1);
            var ledger = new ClaimLedger();
            var armedMetrics = new RewardsMetrics { DemolishLifetime = 50, DayIndex = 0 };

            board.Refresh(0, catalog, ledger, armedMetrics);
            Assert.IsTrue(board.Contains("contract_demo_10"));

            board.Evaluate(new RewardsMetrics { DemolishLifetime = 59, DayIndex = 1 }, ledger, catalog: catalog);
            Assert.IsFalse(ledger.IsPending("contract_demo_10"));

            board.Evaluate(new RewardsMetrics { DemolishLifetime = 60, DayIndex = 2 }, ledger, catalog: catalog);
            Assert.IsTrue(ledger.IsPending("contract_demo_10"));
        }

        [Test]
        public void Expired_contract_pushes_failure_news_without_pending_reward()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            var news = new TowerNews();
            board.Refresh(0, ChallengeCatalog.All, ledger);

            board.Evaluate(
                new RewardsMetrics { Population = 0, DayIndex = 8 },
                ledger,
                news,
                ChallengeCatalog.All);

            Assert.IsFalse(ledger.IsPending("contract_pop_150"));
            Assert.GreaterOrEqual(board.Active.Count, 3);
            Assert.GreaterOrEqual(news.Items.Count, 1);
            var foundExpiredNews = false;
            for (var i = 0; i < news.Items.Count; i++)
                foundExpiredNews |= news.Items[i].Text.ToLowerInvariant().Contains("expired");
            Assert.IsTrue(foundExpiredNews);
        }

        [Test]
        public void Success_or_expiry_refills_board_before_weekly_roll()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            board.Refresh(0, ChallengeCatalog.All, ledger);

            board.Evaluate(
                new RewardsMetrics { Population = 150, DayIndex = 2 },
                ledger,
                catalog: ChallengeCatalog.All);
            Assert.GreaterOrEqual(board.Active.Count, 3);

            board.OnDayRolled(new RewardsMetrics { DayIndex = 3 }, ledger, ChallengeCatalog.All, null);
            Assert.GreaterOrEqual(board.Active.Count, 3);
        }

        [Test]
        public void Weekly_refresh_fills_empty_slots()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            board.Refresh(0, ChallengeCatalog.All, ledger);

            board.Evaluate(
                new RewardsMetrics { Population = 150, DayIndex = 2 },
                ledger,
                catalog: ChallengeCatalog.All);
            Assert.GreaterOrEqual(board.Active.Count, 3);

            board.OnDayRolled(new RewardsMetrics { DayIndex = 7 }, ledger, ChallengeCatalog.All, null);

            Assert.GreaterOrEqual(board.Active.Count, 3);
        }

        [Test]
        public void RewardsSystem_exposes_contract_board_and_evaluates_success()
        {
            var rewards = new RewardsSystem();
            var news = new TowerNews();

            rewards.Board.Refresh(0, ChallengeCatalog.All, rewards.Ledger);
            rewards.Evaluate(new RewardsMetrics { Population = 150, DayIndex = 1 }, news);

            Assert.IsTrue(rewards.Ledger.IsPending("contract_pop_150"));
            Assert.IsFalse(rewards.Board.Contains("contract_pop_150"));
            Assert.GreaterOrEqual(news.Items.Count, 1);
        }
    }
}
