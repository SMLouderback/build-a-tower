using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class LandEdgeMarkersTests
    {
        [Test]
        public void LeftSignPosition_AtMinXMinusHalf()
        {
            Assert.AreEqual(new Vector3(-80.5f, 0f, 0f), LandEdgeMarkers.LeftSignPosition());
        }

        [Test]
        public void RightSignPosition_AtMaxXPlusHalf()
        {
            Assert.AreEqual(new Vector3(100.5f, 0f, 0f), LandEdgeMarkers.RightSignPosition());
        }
    }
}
