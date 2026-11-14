using NUnit.Framework;

namespace BuildATower.Tests
{
    public class RewardsSystemTests
    {
        [Test]
        public void Evaluate_population_100_enqueues_pending_and_pushes_reward_news()
        {
            var rewards = new RewardsSystem();
            var news = new TowerNews();
            var metrics = new RewardsMetrics { Population = 100, DayIndex = 3 };

            rewards.Evaluate(metrics, news);

            Assert.AreEqual(1, rewards.Ledger.PendingCount);
            Assert.IsTrue(rewards.Ledger.IsPending("pop_100"));
            Assert.AreEqual(1, news.Items.Count);
            Assert.IsFalse(string.IsNullOrWhiteSpace(news.Items[0].Text));
        }

        [Test]
        public void RecordDemolish_evaluates_demolish_milestone()
        {
            var rewards = new RewardsSystem();
            var news = new TowerNews();
            var metrics = new RewardsMetrics { DayIndex = 4 };

            rewards.RecordDemolish(metrics, news, 10);

            Assert.AreEqual(10, rewards.Tracker.DemolishLifetime);
            Assert.IsTrue(rewards.Ledger.IsPending("demolish_10"));
            Assert.AreEqual(1, news.Items.Count);
        }

        [Test]
        public void RecordBuild_tracks_lifetime_and_evaluates()
        {
            var rewards = new RewardsSystem();

            rewards.RecordBuild(new RewardsMetrics { Population = 100 }, new TowerNews(), 3);

            Assert.AreEqual(3, rewards.Tracker.BuildLifetime);
            Assert.IsTrue(rewards.Ledger.IsPending("pop_100"));
        }
    }
}
