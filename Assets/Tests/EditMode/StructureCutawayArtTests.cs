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
            Assert.AreEqual(0, StructureCutawayArt.LobbyPanIndex(25)); // 5 pans * 5 cells
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
            Assert.AreEqual(4, StructureCutawayArt.LobbyPanIndex(-1));
            Assert.AreEqual(4, StructureCutawayArt.LobbyPanIndex(-5));
            Assert.AreEqual(3, StructureCutawayArt.LobbyPanIndex(-6));
        }

        [Test]
        public void LobbyStarIndex_ClampsToSetRange()
        {
            Assert.AreEqual(0, StructureCutawayArt.LobbyStarIndex(-3));
            Assert.AreEqual(0, StructureCutawayArt.LobbyStarIndex(0));
            Assert.AreEqual(3, StructureCutawayArt.LobbyStarIndex(3));
            Assert.AreEqual(5, StructureCutawayArt.LobbyStarIndex(5));
            Assert.AreEqual(5, StructureCutawayArt.LobbyStarIndex(9));
        }

        [Test]
        public void LobbyPanResource_UsesStarAndPanNumbering()
        {
            Assert.AreEqual("lobby_s00_pan_01", StructureCutawayArt.LobbyPanResource(0, 0));
            Assert.AreEqual("lobby_s03_pan_02", StructureCutawayArt.LobbyPanResource(3, 1));
            Assert.AreEqual("lobby_s05_pan_05", StructureCutawayArt.LobbyPanResource(5, 4));
        }

        [Test]
        public void LobbyStarSets_CoversZeroThroughFiveStars()
        {
            Assert.AreEqual(6, StructureCutawayArt.LobbyStarSets);
        }

        [Test]
        public void TryLobbyTile_ResolvesArtForEveryStarRating()
        {
            // Whichever lobby_s{SS}_pan_{PP} sets have shipped, the fallback chain
            // must still land on art for every star.
            for (var stars = 0; stars <= 5; stars++)
            {
                StructureCutawayArt.ResetCache();
                StructureCutawayArt.SetStarRating(stars);
                Assert.IsTrue(
                    StructureCutawayArt.TryLobbyTile(0, out var tile),
                    $"no lobby tile for {stars} stars");
                Assert.IsNotNull(tile);
            }
        }

        [Test]
        public void SetStarRating_ReportsLobbyStarChangeIndependentOfStairsTier()
        {
            StructureCutawayArt.ResetCache();
            StructureCutawayArt.TryLobbyTile(0, out _);

            // 4★ and 5★ share a stairs tier but must be different lobby sets.
            Assert.IsTrue(StructureCutawayArt.SetStarRating(4));
            Assert.IsFalse(StructureCutawayArt.SetStarRating(4));
            Assert.IsTrue(StructureCutawayArt.SetStarRating(5));
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
