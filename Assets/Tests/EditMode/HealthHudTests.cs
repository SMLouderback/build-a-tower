using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class HealthHudTests
    {
        [Test]
        public void FormatBar_renders_illness_and_sick_share_without_quarantine()
        {
            Assert.AreEqual(
                "Illness 12 · Sick 10%",
                HealthHudFormat.FormatBar(12f, 0.10f, false));
            Assert.AreEqual(
                "Illness 40 · Sick 25%",
                HealthHudFormat.FormatBar(40f, 0.25f, false));
        }

        [Test]
        public void FormatBar_appends_quarantine_when_latched()
        {
            Assert.AreEqual(
                "Illness 40 · Sick 25% · Quarantine",
                HealthHudFormat.FormatBar(40f, 0.25f, true));
        }

        [Test]
        public void StaffLabel_uses_doctors_for_clinic()
        {
            Assert.AreEqual("Doctors (2/4)", HealthHudFormat.StaffLabel("service_clinic", 2));
            Assert.AreEqual("Staff (1/4)", HealthHudFormat.StaffLabel("service_security", 1));
        }

        [Test]
        public void WaitingOnDoctor_is_fixed_copy()
        {
            Assert.AreEqual("Waiting on a doctor.", HealthHudFormat.WaitingOnDoctor);
        }
    }
}
