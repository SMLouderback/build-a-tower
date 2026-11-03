using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ChallengeCatalogTests
    {
        [Test]
        public void TrophyMetalCompare_Max_returns_higher_metal()
        {
            Assert.AreEqual(TrophyMetal.Gold, TrophyMetalCompare.Max(TrophyMetal.Bronze, TrophyMetal.Gold));
            Assert.AreEqual(TrophyMetal.Gold, TrophyMetalCompare.Max(TrophyMetal.Gold, TrophyMetal.Bronze));
            Assert.AreEqual(TrophyMetal.Galactic, TrophyMetalCompare.Max(TrophyMetal.Platinum, TrophyMetal.Galactic));
        }

        [Test]
        public void TryGet_missing_id_returns_false()
        {
            Assert.IsFalse(ChallengeCatalog.TryGet("missing", out _));
        }

        [Test]
        public void Pop_100_stub_is_seed_milestone_in_catalog()
        {
            Assert.IsTrue(ChallengeCatalog.TryGet("pop_100", out var def));
            Assert.AreEqual("pop_100", def.Id);
            Assert.AreEqual(ChallengeSubject.Population, def.Subject);
            Assert.AreEqual(TrophyMetal.Bronze, def.Metal);
            Assert.AreEqual(ChallengeKind.Milestone, def.Kind);
            Assert.AreEqual(5000, def.Cash);
            Assert.AreEqual(ChallengeConditionKind.Threshold, def.Condition.Kind);
            Assert.AreEqual(ChallengeMetric.Population, def.Condition.Metric);
            Assert.AreEqual(100, def.Condition.ThresholdValue);
        }
    }
}
