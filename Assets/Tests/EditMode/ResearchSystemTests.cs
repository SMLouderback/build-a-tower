using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ResearchSystemTests
    {
        [Test]
        public void CanStart_level_I_yes_level_II_without_I_no()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.CanStart(ResearchBranch.Marketing, 1));
            Assert.IsFalse(research.CanStart(ResearchBranch.Marketing, 2));
            Assert.IsFalse(research.CanStart(ResearchBranch.Marketing, 3));
        }

        [Test]
        public void TryStart_level_I_sets_active_project()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Elevator, 1));
            Assert.AreEqual(ResearchBranch.Elevator, research.ActiveBranch);
            Assert.AreEqual(1, research.ActiveLevel);
            Assert.AreEqual(0f, research.ActiveProgress);
            Assert.IsFalse(research.IsPaused);
        }

        [Test]
        public void TickProgress_with_4_researchers_fills_faster_than_1()
        {
            var slow = new ResearchSystem();
            var fast = new ResearchSystem();
            Assert.IsTrue(slow.TryStart(ResearchBranch.Security, 1));
            Assert.IsTrue(fast.TryStart(ResearchBranch.Security, 1));

            const float dt = 60f;
            slow.TickProgress(dt, researcherPool: 1);
            fast.TickProgress(dt, researcherPool: 4);

            Assert.Greater(fast.ActiveProgress, slow.ActiveProgress);
            Assert.AreEqual(1f * dt, slow.ActiveProgress, 0.0001f);
            Assert.AreEqual((1f + 3f * ResearchCatalog.ResearcherSpeedBonus) * dt, fast.ActiveProgress, 0.0001f);
        }

        [Test]
        public void TickDayDecay_while_paused_reduces_progress()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Housekeeping, 1));
            research.TickProgress(200f, researcherPool: 1);
            var before = research.ActiveProgress;
            Assert.Greater(before, 0f);

            research.Pause();
            research.TickDayDecay();

            var expected = before - ResearchCatalog.DecayFractionPerDay * ResearchCatalog.BaseWorkMinutes(1);
            Assert.AreEqual(expected, research.ActiveProgress, 0.0001f);
            Assert.Less(research.ActiveProgress, before);
        }

        [Test]
        public void TickDayDecay_decays_stored_progress_after_branch_switch()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Marketing, 1));
            research.TickProgress(200f, researcherPool: 1);
            var storedBefore = research.ActiveProgress;
            Assert.Greater(storedBefore, 0f);

            Assert.IsTrue(research.TryStart(ResearchBranch.Elevator, 1));
            research.TickProgress(100f, researcherPool: 1);
            var activeBefore = research.ActiveProgress;
            Assert.IsFalse(research.IsPaused);

            research.TickDayDecay();

            Assert.AreEqual(activeBefore, research.ActiveProgress, 0.0001f);

            Assert.IsTrue(research.TryStart(ResearchBranch.Marketing, 1));
            var expectedStored = storedBefore
                - ResearchCatalog.DecayFractionPerDay * ResearchCatalog.BaseWorkMinutes(1);
            Assert.AreEqual(expectedStored, research.ActiveProgress, 0.0001f);
        }

        [Test]
        public void Completing_level_I_unlocks_level_II()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Maintenance, 1));
            research.TickProgress(ResearchCatalog.BaseWorkMinutes(1), researcherPool: 1);

            Assert.IsTrue(research.IsComplete(ResearchBranch.Maintenance, 1));
            Assert.AreEqual(1, research.HighestCompleted(ResearchBranch.Maintenance));
            Assert.IsNull(research.ActiveBranch);
            Assert.IsTrue(research.CanStart(ResearchBranch.Maintenance, 2));
            Assert.IsFalse(research.CanStart(ResearchBranch.Maintenance, 3));
        }

        [Test]
        public void Zero_researcher_pool_auto_pauses()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Marketing, 1));
            research.TickProgress(30f, researcherPool: 2);
            var before = research.ActiveProgress;

            research.TickProgress(30f, researcherPool: 0);

            Assert.IsTrue(research.IsPaused);
            Assert.AreEqual(before, research.ActiveProgress, 0.0001f);
        }

        [Test]
        public void Catalog_base_work_and_display_names()
        {
            Assert.AreEqual(1440, ResearchCatalog.BaseWorkMinutes(1));
            Assert.AreEqual(4320, ResearchCatalog.BaseWorkMinutes(2));
            Assert.AreEqual(10080, ResearchCatalog.BaseWorkMinutes(3));
            Assert.AreEqual("Marketing", ResearchCatalog.BranchDisplayName(ResearchBranch.Marketing));
            Assert.AreEqual("Elevator Ops", ResearchCatalog.BranchDisplayName(ResearchBranch.Elevator));
            Assert.AreEqual("Security Training", ResearchCatalog.BranchDisplayName(ResearchBranch.Security));
            Assert.AreEqual("Housekeeping", ResearchCatalog.BranchDisplayName(ResearchBranch.Housekeeping));
            Assert.AreEqual("Maintenance", ResearchCatalog.BranchDisplayName(ResearchBranch.Maintenance));
        }
    }
}
