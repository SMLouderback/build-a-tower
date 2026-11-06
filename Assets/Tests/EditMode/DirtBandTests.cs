using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class DirtBandTests
    {
        [Test]
        public void IsCrownRow_OnlyNegativeOne()
        {
            Assert.IsTrue(DirtBand.IsCrownRow(-1));
            Assert.IsFalse(DirtBand.IsCrownRow(-2));
            Assert.IsFalse(DirtBand.IsCrownRow(0));
        }

        [Test]
        public void ShouldRestore_EmptyBasementInsideBand()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(DirtBand.ShouldRestore(new Vector2Int(0, -1), grid));
            Assert.IsFalse(DirtBand.ShouldRestore(new Vector2Int(0, 0), grid));
        }

        [Test]
        public void DirtTileResource_CrownVsFill()
        {
            Assert.AreEqual("dirt_crown", DirtBand.DirtTileResource(-1, 0));
            Assert.AreEqual("dirt_fill", DirtBand.DirtTileResource(-3, 0));
        }
    }
}
