using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Grid pathfinder: horizontal on occupied cells; vertical only on stairs.
    /// Journeys with |Δfloor| &gt; <see cref="MaxStairsFloorSpan"/> are rejected.
    /// </summary>
    public sealed class StairsPathfinder
    {
        public const int MaxStairsFloorSpan = 3;

        readonly HashSet<Vector2Int> _walkable = new();
        readonly HashSet<Vector2Int> _stairsCells = new();
        readonly Dictionary<Vector2Int, List<Vector2Int>> _edges = new();

        public void Rebuild(TowerGrid grid)
        {
            _walkable.Clear();
            _stairsCells.Clear();
            _edges.Clear();
            if (grid == null) return;

            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                var isStairs = room.Type.isStairs;
                foreach (var cell in room.OccupiedCells())
                {
                    _walkable.Add(cell);
                    if (isStairs) _stairsCells.Add(cell);
                }
            }

            foreach (var cell in _walkable)
            {
                var neighbors = new List<Vector2Int>(4);
                TryAddHorizontal(cell, new Vector2Int(cell.x - 1, cell.y), neighbors);
                TryAddHorizontal(cell, new Vector2Int(cell.x + 1, cell.y), neighbors);
                TryAddVertical(cell, new Vector2Int(cell.x, cell.y - 1), neighbors);
                TryAddVertical(cell, new Vector2Int(cell.x, cell.y + 1), neighbors);
                _edges[cell] = neighbors;
            }
        }

        public bool TryFindPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
        {
            path = null;
            if (!_walkable.Contains(start) || !_walkable.Contains(goal)) return false;

            var floorSpan = Mathf.Abs(goal.y - start.y);
            if (floorSpan > MaxStairsFloorSpan) return false;

            if (start == goal)
            {
                path = new List<Vector2Int> { start };
                return true;
            }

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            cameFrom[start] = start;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal) break;
                if (!_edges.TryGetValue(current, out var neighbors)) continue;
                foreach (var next in neighbors)
                {
                    if (cameFrom.ContainsKey(next)) continue;
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!cameFrom.ContainsKey(goal)) return false;

            path = new List<Vector2Int>();
            var node = goal;
            while (true)
            {
                path.Add(node);
                if (node == start) break;
                node = cameFrom[node];
            }

            path.Reverse();
            return true;
        }

        void TryAddHorizontal(Vector2Int from, Vector2Int to, List<Vector2Int> neighbors)
        {
            if (_walkable.Contains(to)) neighbors.Add(to);
        }

        void TryAddVertical(Vector2Int from, Vector2Int to, List<Vector2Int> neighbors)
        {
            if (!_walkable.Contains(to)) return;

            // Normal stairs shafts: both cells are stairs.
            if (_stairsCells.Contains(from) && _stairsCells.Contains(to))
            {
                neighbors.Add(to);
                return;
            }

            // Lobby link: stairs on floor LobbyFloor+1 connect down into lobby cells on LobbyFloor.
            if (from.x != to.x) return;
            var lowY = Mathf.Min(from.y, to.y);
            var highY = Mathf.Max(from.y, to.y);
            if (lowY != TowerGrid.LobbyFloor || highY != TowerGrid.LobbyFloor + 1) return;
            var stairsSide = from.y == TowerGrid.LobbyFloor + 1 ? from : to;
            if (_stairsCells.Contains(stairsSide))
                neighbors.Add(to);
        }
    }
}
