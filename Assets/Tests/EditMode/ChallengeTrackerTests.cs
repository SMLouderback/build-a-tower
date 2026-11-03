using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ChallengeTrackerTests
    {
        static RewardsMetrics WithPopulation(int population) =>
            new RewardsMetrics { Population = population };

        static RewardsMetrics WithHealth(int health, int day) =>
            new RewardsMetrics { EffectiveHealth = health, DayIndex = day };

        static RewardsMetrics WithDemolishLifetime(int demolishLifetime) =>
            new RewardsMetrics { DemolishLifetime = demolishLifetime };

        [Test]
        public void Population_at_100_enqueues_pop_100()
        {
            var ledger = new ClaimLedger();
            var metrics = WithPopulation(100);

            ChallengeTracker.EvaluateThresholds(metrics, ledger, ChallengeCatalog.All);

            Assert.IsTrue(ledger.IsPending("pop_100"));
        }

        [Test]
        public void Population_below_threshold_does_not_enqueue()
        {
            var ledger = new ClaimLedger();
            var metrics = WithPopulation(99);

            ChallengeTracker.EvaluateThresholds(metrics, ledger, ChallengeCatalog.All);

            Assert.IsFalse(ledger.IsPending("pop_100"));
        }

        [Test]
        public void EvaluateThresholds_does_not_touch_wallet()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(12345);
            var metrics = WithPopulation(200);

            ChallengeTracker.EvaluateThresholds(metrics, ledger, ChallengeCatalog.All);

            Assert.IsTrue(ledger.IsPending("pop_100"));
            Assert.AreEqual(12345, wallet.Balance);
        }

        [Test]
        public void Already_pending_not_duplicated_on_second_evaluate()
        {
            var ledger = new ClaimLedger();
            var metrics = WithPopulation(150);

            ChallengeTracker.EvaluateThresholds(metrics, ledger, ChallengeCatalog.All);
            ChallengeTracker.EvaluateThresholds(metrics, ledger, ChallengeCatalog.All);

            Assert.IsTrue(ledger.IsPending("pop_100"));
        }

        [Test]
        public void Already_claimed_not_enqueued_again()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            Assert.IsTrue(ChallengeCatalog.TryGet("pop_100", out var def));

            ledger.EnqueuePending("pop_100");
            Assert.IsTrue(ledger.TryClaim("pop_100", wallet, trophyCase, NoOpRewardExtras.Instance, def).Ok);

            var metrics = WithPopulation(500);
            ChallengeTracker.EvaluateThresholds(metrics, ledger, ChallengeCatalog.All);

            Assert.IsFalse(ledger.IsPending("pop_100"));
            Assert.IsTrue(ledger.IsClaimed("pop_100"));
        }

        [Test]
        public void Duration_hold_enqueues_after_7_healthy_days()
        {
            var tracker = new ChallengeTracker();
            var ledger = new ClaimLedger();

            for (var day = 1; day <= 7; day++)
                tracker.EvaluateDaily(WithHealth(90, day), ledger, ChallengeCatalog.All);

            Assert.IsTrue(ledger.IsPending("health_hold_7"));
        }

        [Test]
        public void Duration_hold_breach_on_day_3_resets_streak()
        {
            var tracker = new ChallengeTracker();
            var ledger = new ClaimLedger();

            tracker.EvaluateDaily(WithHealth(90, 1), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 2), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(70, 3), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 4), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 5), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 6), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 7), ledger, ChallengeCatalog.All);

            Assert.IsFalse(ledger.IsPending("health_hold_7"));

            tracker.EvaluateDaily(WithHealth(90, 8), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 9), ledger, ChallengeCatalog.All);
            tracker.EvaluateDaily(WithHealth(90, 10), ledger, ChallengeCatalog.All);

            Assert.IsTrue(ledger.IsPending("health_hold_7"));
        }

        [Test]
        public void Demolish_lifetime_counter_enqueues_at_threshold()
        {
            var ledger = new ClaimLedger();

            ChallengeTracker.EvaluateThresholds(WithDemolishLifetime(9), ledger, ChallengeCatalog.All);
            Assert.IsFalse(ledger.IsPending("demolish_10"));

            ChallengeTracker.EvaluateThresholds(WithDemolishLifetime(10), ledger, ChallengeCatalog.All);
            Assert.IsTrue(ledger.IsPending("demolish_10"));
        }
    }
}
