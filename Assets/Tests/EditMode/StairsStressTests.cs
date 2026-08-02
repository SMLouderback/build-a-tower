using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class StairsStressTests
    {
        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO Stairs()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.category = RoomCategory.Transit;
            so.isStairs = true;
            so.size = new Vector2Int(2, 2);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        RoomTypeSO Pad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "pad";
            so.category = RoomCategory.Office;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        [Test]
        public void StairsOverCapPenalty_zero_within_comfort()
        {
            Assert.AreEqual(0f, ElevatorRouting.StairsOverCapPenalty(3));
            Assert.AreEqual(40f, ElevatorRouting.StairsOverCapPenalty(4));
            Assert.AreEqual(80f, ElevatorRouting.StairsOverCapPenalty(5));
        }

        [Test]
        public void MaxAffordableOverCapFloors_respects_stress()
        {
            Assert.AreEqual(4, ElevatorRouting.MaxAffordableOverCapFloors(0f));
            Assert.AreEqual(0, ElevatorRouting.MaxAffordableOverCapFloors(100f));
            Assert.AreEqual(1, ElevatorRouting.MaxAffordableOverCapFloors(90f));
            Assert.AreEqual(1, ElevatorRouting.MaxAffordableOverCapFloors(75f));
            Assert.AreEqual(1, ElevatorRouting.MaxAffordableOverCapFloors(76f));
        }

        [Test]
        public void Pathfinder_comfort_span_rejects_long_stairs_unlimited_succeeds()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 2), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 3), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, 4), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 2), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 3), out _));

            var pf = new StairsPathfinder();
            pf.Rebuild(grid);

            // Walkable stair-column endpoints (2×2 shaft at x=0..1); |Δfloor|=4 > comfort span 3.
            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(0, 4);

            Assert.IsFalse(pf.TryFindPath(start, goal, out _));
            Assert.IsTrue(pf.TryFindPath(start, goal, -1, out var path));
            Assert.Greater(path.Count, 1);
        }
    }
}
