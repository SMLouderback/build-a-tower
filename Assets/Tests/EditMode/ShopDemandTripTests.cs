using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ShopDemandTripTests
    {
        [TestCase(AgentRole.OfficeWorker, 0.00, ShopDemandFamily.Food)]
        [TestCase(AgentRole.OfficeWorker, 0.82, ShopDemandFamily.Food)]
        [TestCase(AgentRole.OfficeWorker, 0.84, ShopDemandFamily.Retail)]
        [TestCase(AgentRole.CondoResident, 0.56, ShopDemandFamily.Food)]
        [TestCase(AgentRole.CondoResident, 0.58, ShopDemandFamily.Retail)]
        [TestCase(AgentRole.HotelGuest, 0.73, ShopDemandFamily.Food)]
        [TestCase(AgentRole.HotelGuest, 0.75, ShopDemandFamily.Retail)]
        public void Occupant_family_choice_uses_role_weight(
            AgentRole role,
            double roll,
            ShopDemandFamily expected) =>
            Assert.AreEqual(
                expected,
                ShopDemandBalance.PickFamily(
                    role,
                    foodAvailable: 5,
                    retailAvailable: 5,
                    roll: roll));

        [Test]
        public void Choice_falls_back_to_only_serviceable_family()
        {
            Assert.AreEqual(
                ShopDemandFamily.Retail,
                ShopDemandBalance.PickFamily(
                    AgentRole.OfficeWorker,
                    foodAvailable: 0,
                    retailAvailable: 3,
                    roll: 0.01));
        }

        [Test]
        public void Choice_returns_null_when_no_family_is_serviceable()
        {
            Assert.IsNull(
                ShopDemandBalance.PickFamily(
                    AgentRole.OfficeWorker,
                    foodAvailable: 0,
                    retailAvailable: 0,
                    roll: 0.5));
        }

        [Test]
        public void Street_and_event_choice_is_proportional_to_remaining_demand()
        {
            Assert.AreEqual(
                ShopDemandFamily.Food,
                ShopDemandBalance.PickFamily(
                    AgentRole.StreetVisitor,
                    foodAvailable: 3,
                    retailAvailable: 1,
                    roll: 0.70));
            Assert.AreEqual(
                ShopDemandFamily.Retail,
                ShopDemandBalance.PickFamily(
                    AgentRole.EventVisitor,
                    foodAvailable: 3,
                    retailAvailable: 1,
                    roll: 0.80));
        }
    }
}
