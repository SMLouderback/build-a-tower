using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorQueueLaneTests
    {
        [Test]
        public void ComputeQueueLaneX_prefers_walkable_side_toward_rooms()
        {
            // Layout: empty sky | shaft@5 | rooms 6..12 on floor 17
            var walkable = new HashSet<Vector2Int>
            {
                new Vector2Int(5, 17)
            };
            for (var x = 6; x <= 12; x++)
                walkable.Add(new Vector2Int(x, 17));

            bool IsWalkable(Vector2Int cell) => walkable.Contains(cell);

            // Preferred side is left (empty) — must flip to the walkable right side.
            var x0 = AgentSystem.ComputeQueueLaneX(5, 1, 17, slot: 0, preferredSide: -1, IsWalkable);
            Assert.Greater(x0, 5.5f, "Waiter should stand on the room side of the shaft, not in open sky.");

            var xFar = AgentSystem.ComputeQueueLaneX(5, 1, 17, slot: 20, preferredSide: -1, IsWalkable);
            Assert.LessOrEqual(xFar, 12.5f, "Long queues must clamp to the walkable strip.");
            Assert.Greater(xFar, 5.5f);
        }

        [Test]
        public void ComputeQueueLaneX_stacks_on_shaft_when_no_adjacent_floor()
        {
            bool IsWalkable(Vector2Int cell) => cell.x == 5 && cell.y == 17;

            var x = AgentSystem.ComputeQueueLaneX(5, 1, 17, slot: 3, preferredSide: -1, IsWalkable);
            Assert.AreEqual(5.5f, x, 0.01f);
        }

        [Test]
        public void ComputeQueueLaneX_uses_preferred_side_when_walkable()
        {
            var walkable = new HashSet<Vector2Int>();
            for (var x = 0; x <= 10; x++)
                walkable.Add(new Vector2Int(x, 3));

            bool IsWalkable(Vector2Int cell) => walkable.Contains(cell);

            var left = AgentSystem.ComputeQueueLaneX(5, 1, 3, slot: 0, preferredSide: -1, IsWalkable);
            var right = AgentSystem.ComputeQueueLaneX(5, 1, 3, slot: 0, preferredSide: 1, IsWalkable);
            Assert.Less(left, 5.5f);
            Assert.Greater(right, 5.5f);
        }
    }
}
