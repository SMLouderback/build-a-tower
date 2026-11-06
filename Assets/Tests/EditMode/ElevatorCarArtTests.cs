using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ElevatorCarArtTests
    {
        [Test]
        public void ElevatorCarResource_UsesZeroPaddedStar()
        {
            Assert.AreEqual("elevator_car_s00", ElevatorView.ElevatorCarResource(0));
            Assert.AreEqual("elevator_car_s03", ElevatorView.ElevatorCarResource(3));
            Assert.AreEqual("elevator_car_s05", ElevatorView.ElevatorCarResource(5));
            Assert.AreEqual("elevator_car_s05", ElevatorView.ElevatorCarResource(9));
        }
    }
}
