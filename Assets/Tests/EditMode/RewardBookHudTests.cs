using System.Collections.Generic;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class RewardBookHudTests
    {
        static ChallengeDef MakeDef(string id, int cash) =>
            new ChallengeDef(
                id,
                title: id,
                description: id,
                subject: ChallengeSubject.Population,
                metal: TrophyMetal.Bronze,
                kind: ChallengeKind.Milestone,
                condition: ChallengeCondition.Threshold(ChallengeMetric.Population, 1),
                cash: cash);

        [Test]
        public void ShouldShowButton_unlocks_with_lobby_or_pending_rewards()
        {
            Assert.IsFalse(RewardBookHud.ShouldShowButton(hasLobby: false, pendingCount: 0));
            Assert.IsTrue(RewardBookHud.ShouldShowButton(hasLobby: true, pendingCount: 0));
            Assert.IsTrue(RewardBookHud.ShouldShowButton(hasLobby: false, pendingCount: 2));
        }

        [Test]
        public void BadgeText_is_empty_for_zero_and_caps_large_counts()
        {
            Assert.AreEqual(string.Empty, RewardBookHud.BadgeText(0));
            Assert.AreEqual("7", RewardBookHud.BadgeText(7));
            Assert.AreEqual("99+", RewardBookHud.BadgeText(100));
        }

        [Test]
        public void TabLabels_match_reward_book_sections()
        {
            CollectionAssert.AreEqual(
                new[] { "Ready", "Catalog", "Contracts", "Trophies" },
                RewardBookHud.TabLabels());
        }

        [Test]
        public void ReadyRewards_follow_catalog_order_not_enqueue_order()
        {
            var first = MakeDef("first", 100);
            var second = MakeDef("second", 200);
            var rewards = new RewardsSystem(catalog: new[] { first, second });
            rewards.Ledger.EnqueuePending("second");
            rewards.Ledger.EnqueuePending("first");

            var ready = RewardBookHud.ReadyRewards(rewards, new[] { first, second });

            Assert.AreEqual("first", ready[0].Id);
            Assert.AreEqual("second", ready[1].Id);
        }

        [Test]
        public void ClaimAllReady_claims_pending_rewards_in_catalog_order()
        {
            var first = MakeDef("first", 100);
            var second = MakeDef("second", 200);
            var rewards = new RewardsSystem(catalog: new[] { first, second });
            var extras = new RecordingExtras();
            var wallet = new FundsWallet(0);
            rewards.Ledger.EnqueuePending("second");
            rewards.Ledger.EnqueuePending("first");

            var result = RewardBookHud.ClaimAllReady(rewards, wallet, new[] { first, second }, extras);

            Assert.AreEqual(300, result.TotalCashPaid);
            CollectionAssert.AreEqual(new[] { "first", "second" }, extras.ClaimedIds);
            Assert.IsFalse(rewards.Ledger.IsPending("first"));
            Assert.IsFalse(rewards.Ledger.IsPending("second"));
        }

        sealed class RecordingExtras : IRewardExtras
        {
            public readonly List<string> ClaimedIds = new List<string>();

            public void OnClaim(ChallengeDef def, TrophyCase trophyCase) =>
                ClaimedIds.Add(def.Id);
        }
    }
}
