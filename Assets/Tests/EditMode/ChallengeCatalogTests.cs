using System.Collections.Generic;
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

        [Test]
        public void Catalog_has_launch_volume_and_contract_pool()
        {
            var contractCount = 0;

            foreach (var def in ChallengeCatalog.All)
            {
                if (def.Kind == ChallengeKind.Contract)
                    contractCount++;
            }

            Assert.GreaterOrEqual(ChallengeCatalog.All.Count, 40);
            Assert.LessOrEqual(ChallengeCatalog.All.Count, 60);
            Assert.GreaterOrEqual(contractCount, 8);
        }

        [Test]
        public void Catalog_ids_are_unique()
        {
            var ids = new HashSet<string>();

            foreach (var def in ChallengeCatalog.All)
                Assert.IsTrue(ids.Add(def.Id), $"Duplicate challenge id: {def.Id}");
        }

        [Test]
        public void Catalog_covers_every_subject()
        {
            var subjects = new HashSet<ChallengeSubject>();
            foreach (var def in ChallengeCatalog.All)
                subjects.Add(def.Subject);

            foreach (ChallengeSubject subject in System.Enum.GetValues(typeof(ChallengeSubject)))
                Assert.IsTrue(subjects.Contains(subject), $"Missing challenge subject: {subject}");
        }

        [Test]
        public void Catalog_uses_every_trophy_metal()
        {
            var metals = new HashSet<TrophyMetal>();
            foreach (var def in ChallengeCatalog.All)
                metals.Add(def.Metal);

            foreach (TrophyMetal metal in System.Enum.GetValues(typeof(TrophyMetal)))
            {
                if (metal == TrophyMetal.None)
                    continue;
                Assert.IsTrue(metals.Contains(metal), $"Missing trophy metal: {metal}");
            }
        }
    }
}
