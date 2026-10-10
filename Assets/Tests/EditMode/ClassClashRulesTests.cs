using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ClassClashRulesTests
    {
        [Test]
        public void Upper_clashes_with_adjacent_Lower_living()
        {
            Assert.IsTrue(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Condo,
                TenantClass.Lower,
                floorDelta: 1));
            Assert.IsTrue(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Office,
                TenantClass.Lower,
                floorDelta: -1));
            Assert.IsTrue(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Hotel,
                TenantClass.Lower,
                floorDelta: 0));
            Assert.Less(ClassClashRules.PenaltyMult(
                TenantClass.Upper,
                EconomicFamily.Condo,
                TenantClass.Lower,
                floorDelta: 1), ClassClashRules.NoPenalty);
        }

        [Test]
        public void Mid_does_not_clash_with_adjacent_Lower()
        {
            Assert.IsFalse(ClassClashRules.HasClash(
                TenantClass.Mid,
                EconomicFamily.Condo,
                TenantClass.Lower,
                floorDelta: 0));
            Assert.AreEqual(ClassClashRules.NoPenalty, ClassClashRules.PenaltyMult(
                TenantClass.Mid,
                EconomicFamily.Condo,
                TenantClass.Lower,
                floorDelta: 1), 0.001f);
        }

        [Test]
        public void Upper_clashes_with_nearby_noisy_service_or_leisure()
        {
            Assert.IsTrue(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Service,
                TenantClass.Mid,
                floorDelta: 0));
            Assert.IsTrue(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Leisure,
                TenantClass.Mid,
                floorDelta: 1));
        }

        [Test]
        public void Upper_does_not_clash_when_neighbor_is_far()
        {
            Assert.IsFalse(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Condo,
                TenantClass.Lower,
                floorDelta: 2));
            Assert.IsFalse(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Service,
                TenantClass.Mid,
                floorDelta: -2));
        }

        [Test]
        public void Upper_does_not_clash_with_adjacent_Mid_living()
        {
            Assert.IsFalse(ClassClashRules.HasClash(
                TenantClass.Upper,
                EconomicFamily.Condo,
                TenantClass.Mid,
                floorDelta: 0));
        }

        [Test]
        public void Upper_elev_wait_stresses_above_15s_not_at_14s()
        {
            Assert.IsFalse(TenantClassStress.ElevWaitStress(TenantClass.Upper, waitSeconds: 14f));
            Assert.IsFalse(TenantClassStress.ElevWaitStress(TenantClass.Upper, waitSeconds: 15f));
            Assert.IsTrue(TenantClassStress.ElevWaitStress(TenantClass.Upper, waitSeconds: 16f));
        }

        [Test]
        public void Lower_elev_wait_is_resilient()
        {
            Assert.IsFalse(TenantClassStress.ElevWaitStress(TenantClass.Lower, waitSeconds: 60f));
        }

        [Test]
        public void Mid_elev_wait_threshold_is_between_Upper_and_Lower()
        {
            Assert.Greater(
                TenantClassStress.ElevWaitThresholdSeconds(TenantClass.Mid),
                TenantClassStress.ElevWaitThresholdSeconds(TenantClass.Upper));
            Assert.IsFalse(TenantClassStress.ElevWaitStress(TenantClass.Mid, waitSeconds: 16f));
            Assert.IsTrue(TenantClassStress.ElevWaitStress(
                TenantClass.Mid,
                TenantClassStress.ElevWaitThresholdSeconds(TenantClass.Mid) + 1f));
        }
    }
}
