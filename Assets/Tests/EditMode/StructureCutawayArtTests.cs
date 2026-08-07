using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class StructureCutawayArtTests
    {
        [Test]
        public void LobbyPanIndex_CyclesEveryFiveCells()
        {
            Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(0));
            Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(4));
            Assert.AreEqual(1, StructureCutawayArt.LobbyPanIndex(5));
            Assert.AreEqual(1, StructureCutawayArt.LobbyPanIndex(9));
            Assert.AreEqual(2, StructureCutawayArt.LobbyPanIndex(10));
            Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(30));
        }

        [Test]
        public void LobbySliceIndex_IsColumnWithinPan()
        {
            Assert.AreEqual(0, StructureCutawayArt.LobbySliceIndex(0));
            Assert.AreEqual(4, StructureCutawayArt.LobbySliceIndex(4));
            Assert.AreEqual(0, StructureCutawayArt.LobbySliceIndex(5));
            Assert.AreEqual(3, StructureCutawayArt.LobbySliceIndex(-2));
        }

        [Test]
        public void LobbyPanIndex_HandlesNegativeX()
        {
            Assert.AreEqual(5, StructureCutawayArt.LobbyPanIndex(-1));
            Assert.AreEqual(5, StructureCutawayArt.LobbyPanIndex(-5));
            Assert.AreEqual(4, StructureCutawayArt.LobbyPanIndex(-6));
        }

        [Test]
        public void TryElevatorTile_RuntimeArt_DoesNotThrow()
        {
            StructureCutawayArt.ResetCache();
            Assert.DoesNotThrow(() =>
            {
                Assert.IsTrue(StructureCutawayArt.TryElevatorTile(1, 0, 3, out var tile));
                Assert.IsNotNull(tile);
            });
        }

        [Test]
        public void TryLobbyTile_RuntimeArt_DoesNotThrow()
        {
            StructureCutawayArt.ResetCache();
            Assert.DoesNotThrow(() =>
            {
                Assert.IsTrue(StructureCutawayArt.TryLobbyTile(0, out var tile));
                Assert.IsNotNull(tile);
            });
        }
    }
}
