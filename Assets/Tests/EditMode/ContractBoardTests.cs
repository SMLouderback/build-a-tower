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

            board.Evaluate(new RewardsMetrics { Population = 150, DayIndex = 2 }, ledger);

            Assert.IsTrue(ledger.IsPending("contract_pop_150"));
            Assert.IsFalse(board.Contains("contract_pop_150"));
        }

        [Test]
        public void Expired_contract_pushes_failure_news_without_pending_reward()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            var news = new TowerNews();
            board.Refresh(0, ChallengeCatalog.All, ledger);

            board.Evaluate(new RewardsMetrics { Population = 0, DayIndex = 8 }, ledger, news);

            Assert.IsFalse(ledger.IsPending("contract_pop_150"));
            Assert.IsFalse(board.Contains("contract_pop_150"));
            Assert.GreaterOrEqual(news.Items.Count, 1);
            var foundExpiredNews = false;
            for (var i = 0; i < news.Items.Count; i++)
                foundExpiredNews |= news.Items[i].Text.ToLowerInvariant().Contains("expired");
            Assert.IsTrue(foundExpiredNews);
        }

        [Test]
        public void Weekly_refresh_fills_empty_slots()
        {
            var board = new ContractBoard();
            var ledger = new ClaimLedger();
            board.Refresh(0, ChallengeCatalog.All, ledger);

            board.Evaluate(new RewardsMetrics { Population = 150, DayIndex = 2 }, ledger);
            Assert.Less(board.Active.Count, 3);

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
