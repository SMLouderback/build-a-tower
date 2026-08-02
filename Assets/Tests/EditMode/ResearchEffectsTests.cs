using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ResearchEffectsTests
    {
        static void CompleteUpTo(ResearchSystem research, ResearchBranch branch, int level)
        {
            for (var L = 1; L <= level; L++)
            {
                if (research.IsComplete(branch, L))
                    continue;
                Assert.IsTrue(research.TryStart(branch, L));
                research.TickProgress(ResearchCatalog.BaseWorkMinutes(L), researcherPool: 1);
                Assert.IsTrue(research.IsComplete(branch, L));
            }
        }

        [Test]
        public void Null_research_returns_identity_multipliers()
        {
            Assert.AreEqual(1f, ResearchEffects.ShopSpendMultiplier(null));
            Assert.AreEqual(1f, ResearchEffects.ElevatorSpeedMultiplier(null));
            Assert.AreEqual(1f, ResearchEffects.ElevatorRoutingWaitWeightScale(null));
            Assert.AreEqual(1f, ResearchEffects.CrimeSuppressionMultiplier(null));
            Assert.AreEqual(1f, ResearchEffects.CleanMinutesMultiplier(null));
            Assert.AreEqual(1f, ResearchEffects.RepairMinutesMultiplier(null));
            Assert.AreEqual(1f, ResearchEffects.RepairChunkMultiplier(null));
        }

        [Test]
        public void Level_0_returns_identity_multipliers()
        {
            var research = new ResearchSystem();
            Assert.AreEqual(1f, ResearchEffects.ShopSpendMultiplier(research));
            Assert.AreEqual(1f, ResearchEffects.ElevatorSpeedMultiplier(research));
            Assert.AreEqual(1f, ResearchEffects.ElevatorRoutingWaitWeightScale(research));
            Assert.AreEqual(1f, ResearchEffects.CrimeSuppressionMultiplier(research));
            Assert.AreEqual(1f, ResearchEffects.CleanMinutesMultiplier(research));
            Assert.AreEqual(1f, ResearchEffects.RepairMinutesMultiplier(research));
            Assert.AreEqual(1f, ResearchEffects.RepairChunkMultiplier(research));
        }

        [Test]
        public void Marketing_shop_spend_1_10_1_20_1_35()
        {
            var research = new ResearchSystem();
            CompleteUpTo(research, ResearchBranch.Marketing, 1);
            Assert.AreEqual(1.10f, ResearchEffects.ShopSpendMultiplier(research), 0.0001f);
            CompleteUpTo(research, ResearchBranch.Marketing, 2);
            Assert.AreEqual(1.20f, ResearchEffects.ShopSpendMultiplier(research), 0.0001f);
            CompleteUpTo(research, ResearchBranch.Marketing, 3);
            Assert.AreEqual(1.35f, ResearchEffects.ShopSpendMultiplier(research), 0.0001f);
        }

        [Test]
        public void Elevator_speed_1_10_1_20_1_35_and_wait_scale_II_III()
        {
            var research = new ResearchSystem();
            CompleteUpTo(research, ResearchBranch.Elevator, 1);
            Assert.AreEqual(1.10f, ResearchEffects.ElevatorSpeedMultiplier(research), 0.0001f);
            Assert.AreEqual(1f, ResearchEffects.ElevatorRoutingWaitWeightScale(research), 0.0001f);

            CompleteUpTo(research, ResearchBranch.Elevator, 2);
            Assert.AreEqual(1.20f, ResearchEffects.ElevatorSpeedMultiplier(research), 0.0001f);
            Assert.AreEqual(0.85f, ResearchEffects.ElevatorRoutingWaitWeightScale(research), 0.0001f);

            CompleteUpTo(research, ResearchBranch.Elevator, 3);
            Assert.AreEqual(1.35f, ResearchEffects.ElevatorSpeedMultiplier(research), 0.0001f);
            Assert.AreEqual(0.70f, ResearchEffects.ElevatorRoutingWaitWeightScale(research), 0.0001f);
        }

        [Test]
        public void Security_crime_suppression_1_15_1_30_1_50()
        {
            var research = new ResearchSystem();
            CompleteUpTo(research, ResearchBranch.Security, 1);
            Assert.AreEqual(1.15f, ResearchEffects.CrimeSuppressionMultiplier(research), 0.0001f);
            CompleteUpTo(research, ResearchBranch.Security, 2);
            Assert.AreEqual(1.30f, ResearchEffects.CrimeSuppressionMultiplier(research), 0.0001f);
            CompleteUpTo(research, ResearchBranch.Security, 3);
            Assert.AreEqual(1.50f, ResearchEffects.CrimeSuppressionMultiplier(research), 0.0001f);
        }

        [Test]
        public void Housekeeping_clean_minutes_0_90_0_80_0_65()
        {
            var research = new ResearchSystem();
            CompleteUpTo(research, ResearchBranch.Housekeeping, 1);
            Assert.AreEqual(0.90f, ResearchEffects.CleanMinutesMultiplier(research), 0.0001f);
            CompleteUpTo(research, ResearchBranch.Housekeeping, 2);
            Assert.AreEqual(0.80f, ResearchEffects.CleanMinutesMultiplier(research), 0.0001f);
            CompleteUpTo(research, ResearchBranch.Housekeeping, 3);
            Assert.AreEqual(0.65f, ResearchEffects.CleanMinutesMultiplier(research), 0.0001f);
        }

        [Test]
        public void Maintenance_repair_minutes_and_chunk()
        {
            var research = new ResearchSystem();
            CompleteUpTo(research, ResearchBranch.Maintenance, 1);
            Assert.AreEqual(0.90f, ResearchEffects.RepairMinutesMultiplier(research), 0.0001f);
            Assert.AreEqual(1.10f, ResearchEffects.RepairChunkMultiplier(research), 0.0001f);

            CompleteUpTo(research, ResearchBranch.Maintenance, 2);
            Assert.AreEqual(0.80f, ResearchEffects.RepairMinutesMultiplier(research), 0.0001f);
            Assert.AreEqual(1.25f, ResearchEffects.RepairChunkMultiplier(research), 0.0001f);

            CompleteUpTo(research, ResearchBranch.Maintenance, 3);
            Assert.AreEqual(0.65f, ResearchEffects.RepairMinutesMultiplier(research), 0.0001f);
            Assert.AreEqual(1.45f, ResearchEffects.RepairChunkMultiplier(research), 0.0001f);
        }

        [Test]
        public void Branches_are_independent()
        {
            var research = new ResearchSystem();
            CompleteUpTo(research, ResearchBranch.Marketing, 3);
            Assert.AreEqual(1.35f, ResearchEffects.ShopSpendMultiplier(research), 0.0001f);
            Assert.AreEqual(1f, ResearchEffects.ElevatorSpeedMultiplier(research), 0.0001f);
            Assert.AreEqual(1f, ResearchEffects.CrimeSuppressionMultiplier(research), 0.0001f);
        }
    }
}
