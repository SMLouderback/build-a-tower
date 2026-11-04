using NUnit.Framework;

namespace BuildATower.Tests
{
    public class RewardClaimFxTests
    {
        [Test]
        public void BurstGlyphCount_scales_with_cash_and_claim_count()
        {
            Assert.AreEqual(0, RewardClaimFx.BurstGlyphCount(0, 0));
            Assert.Greater(RewardClaimFx.BurstGlyphCount(10_000, 1), RewardClaimFx.BurstGlyphCount(100, 1));
            Assert.Greater(RewardClaimFx.BurstGlyphCount(100, 3), RewardClaimFx.BurstGlyphCount(100, 1));
            Assert.LessOrEqual(RewardClaimFx.BurstGlyphCount(999_999, 20), 56);
            Assert.GreaterOrEqual(RewardClaimFx.BurstGlyphCount(100, 1), 12);
        }

        [Test]
        public void BurstDurationSeconds_stays_in_reward_rain_window()
        {
            Assert.AreEqual(0f, RewardClaimFx.BurstDurationSeconds(0, 0));
            var single = RewardClaimFx.BurstDurationSeconds(500, 1);
            var bulk = RewardClaimFx.BurstDurationSeconds(50_000, 8);
            Assert.GreaterOrEqual(single, 0.8f);
            Assert.LessOrEqual(single, 1.2f);
            Assert.GreaterOrEqual(bulk, 0.8f);
            Assert.LessOrEqual(bulk, 1.2f);
            Assert.Greater(bulk, single);
        }
    }
}
