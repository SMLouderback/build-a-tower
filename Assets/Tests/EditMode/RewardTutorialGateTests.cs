using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class RewardTutorialGateTests
    {
        static ChallengeDef MakeDef(string id, ChallengeSubject subject, TrophyMetal metal) =>
            new ChallengeDef(
                id,
                title: id,
                description: id,
                subject: subject,
                metal: metal,
                kind: ChallengeKind.Milestone,
                condition: ChallengeCondition.Threshold(ChallengeMetric.Population, 1),
                cash: 100);

        [Test]
        public void TryOpen_pauses_once_for_first_unseen_trophy()
        {
            var rewards = new RewardsSystem();
            var gate = new RewardTutorialGate();
            var clock = new GameClock(60f);
            var speed = 0f;
            var paused = false;

            var opened = gate.TryOpen(rewards, firstTrophyOnTower: true, clock, (s, p) =>
            {
                speed = s;
                paused = p;
            });

            Assert.IsTrue(opened);
            Assert.IsTrue(gate.IsModalOpen);
            Assert.AreEqual(60f, speed);
            Assert.IsTrue(paused);
        }

        [Test]
        public void Dismiss_restores_speed_and_marks_tutorial_seen()
        {
            var rewards = new RewardsSystem();
            var gate = new RewardTutorialGate();
            var clock = new GameClock(5f);
            float speed = -1f;
            var paused = true;

            gate.TryOpen(rewards, firstTrophyOnTower: true, clock, (s, p) => { });
            gate.Dismiss(rewards, (s, p) =>
            {
                speed = s;
                paused = p;
            });

            Assert.IsFalse(gate.IsModalOpen);
            Assert.IsTrue(rewards.RewardTutorialSeen);
            Assert.AreEqual(5f, speed);
            Assert.IsFalse(paused);
        }

        [Test]
        public void Second_metal_upgrade_never_needs_tutorial_after_first_dismiss()
        {
            var bronze = MakeDef("bronze", ChallengeSubject.Population, TrophyMetal.Bronze);
            var gold = MakeDef("gold", ChallengeSubject.Population, TrophyMetal.Gold);
            var rewards = new RewardsSystem(catalog: new[] { bronze, gold });
            var wallet = new FundsWallet(0);
            var gate = new RewardTutorialGate();

            rewards.Ledger.EnqueuePending("bronze");
            var first = RewardBookHud.ClaimReady(rewards, wallet, bronze);
            Assert.IsTrue(first.FirstTrophyOnTower);
            Assert.IsTrue(first.NeedsTutorial);

            Assert.IsTrue(gate.TryOpen(rewards, first.NeedsTutorial, new GameClock(1f), (s, p) => { }));
            gate.Dismiss(rewards, (s, p) => { });

            rewards.Ledger.EnqueuePending("gold");
            var second = RewardBookHud.ClaimReady(rewards, wallet, gold);

            Assert.IsTrue(second.MetalRaised);
            Assert.IsFalse(second.FirstTrophyOnTower);
            Assert.IsFalse(second.NeedsTutorial);
            Assert.IsFalse(gate.TryOpen(rewards, second.NeedsTutorial, new GameClock(1f), (s, p) => { }));
        }
    }
}
