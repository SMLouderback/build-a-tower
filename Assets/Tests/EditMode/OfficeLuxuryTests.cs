using NUnit.Framework;

namespace BuildATower.Tests
{
    public class OfficeLuxuryTests
    {
        [Test]
        public void Basic_accepts_base_only()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Base, WealthBand.Basic, OfficeLuxury.MicroId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Basic, OfficeLuxury.MidStandardId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Basic, OfficeLuxury.UpperFloorId));
        }

        [Test]
        public void Mid_accepts_mid_only()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Mid, OfficeLuxury.MidClinicId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Base, WealthBand.Mid, OfficeLuxury.BaseId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Mid, OfficeLuxury.UpperStandardId));
        }

        [Test]
        public void Upper_accepts_team_bay_and_all_upper()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Upper, OfficeLuxury.MidTeamId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Mid, WealthBand.Upper, OfficeLuxury.MidStandardId));
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Upper, OfficeLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_accepts_corner_and_corporate_only()
        {
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Premium, OfficeLuxury.UpperCornerId));
            Assert.IsTrue(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Premium, OfficeLuxury.UpperFloorId));
            Assert.IsFalse(OfficeLuxury.AcceptsWorker(LuxuryBand.Upper, WealthBand.Premium, OfficeLuxury.UpperStandardId));
        }

        [Test]
        public void Premium_prefers_corporate_over_corner()
        {
            Assert.Less(
                OfficeLuxury.PremiumDeskPreferenceRank(WealthBand.Premium, OfficeLuxury.UpperFloorId),
                OfficeLuxury.PremiumDeskPreferenceRank(WealthBand.Premium, OfficeLuxury.UpperCornerId));
        }
    }
}
