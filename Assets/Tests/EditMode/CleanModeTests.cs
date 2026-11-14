using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class CleanModeTests
    {
        [Test]
        public void Quick_Normal_Deep_time_multipliers_are_0_6_1_and_1_6()
        {
            Assert.AreEqual(0.6f, CleanModeRules.TimeMultiplier(CleanMode.Quick), 0.0001f);
            Assert.AreEqual(1.0f, CleanModeRules.TimeMultiplier(CleanMode.Normal), 0.0001f);
            Assert.AreEqual(1.6f, CleanModeRules.TimeMultiplier(CleanMode.Deep), 0.0001f);
        }

        [Test]
        public void Quick_Normal_Deep_illness_chances_are_0_25_0_08_and_0_02()
        {
            Assert.AreEqual(0.25f, CleanModeRules.IllnessChance(CleanMode.Quick), 0.0001f);
            Assert.AreEqual(0.08f, CleanModeRules.IllnessChance(CleanMode.Normal), 0.0001f);
            Assert.AreEqual(0.02f, CleanModeRules.IllnessChance(CleanMode.Deep), 0.0001f);
        }

        [Test]
        public void MarksTenantSick_only_for_hotel_office_and_condo()
        {
            Assert.IsTrue(CleanModeRules.MarksTenantSick(RoomCategory.Hotel));
            Assert.IsTrue(CleanModeRules.MarksTenantSick(RoomCategory.Office));
            Assert.IsTrue(CleanModeRules.MarksTenantSick(RoomCategory.Condo));

            Assert.IsFalse(CleanModeRules.MarksTenantSick(RoomCategory.Structure));
            Assert.IsFalse(CleanModeRules.MarksTenantSick(RoomCategory.Commercial));
            Assert.IsFalse(CleanModeRules.MarksTenantSick(RoomCategory.Transit));
            Assert.IsFalse(CleanModeRules.MarksTenantSick(RoomCategory.Parking));
            Assert.IsFalse(CleanModeRules.MarksTenantSick(RoomCategory.Service));
        }

        [Test]
        public void RedirtiesInstead_for_public_and_office_restrooms_conference_and_event_hall()
        {
            Assert.IsTrue(CleanModeRules.RedirtiesInstead("service_public_restroom"));
            Assert.IsTrue(CleanModeRules.RedirtiesInstead("service_office_restroom"));
            Assert.IsTrue(CleanModeRules.RedirtiesInstead("service_conference"));
            Assert.IsTrue(CleanModeRules.RedirtiesInstead("service_event_hall"));
        }

        [Test]
        public void RedirtiesInstead_false_for_other_room_type_ids()
        {
            Assert.IsFalse(CleanModeRules.RedirtiesInstead("service_trash"));
            Assert.IsFalse(CleanModeRules.RedirtiesInstead("hotel_standard"));
            Assert.IsFalse(CleanModeRules.RedirtiesInstead(null));
            Assert.IsFalse(CleanModeRules.RedirtiesInstead(""));
        }
    }
}
