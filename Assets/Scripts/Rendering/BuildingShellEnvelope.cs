using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Fills the cutaway tower silhouette so dollhouse gaps and floor steps do not show sky.
    /// Each floor uses the horizontal span of all rooms at or above that floor.
    /// </summary>
    public static class BuildingShellEnvelope
    {
        public static HashSet<Vector2Int> ComputeCells(IReadOnlyList<RoomInstance> rooms)
        {
            var occupied = new HashSet<Vector2Int>();
            if (rooms == null) return occupied;

            foreach (var room in rooms)
            {
                if (room?.Type == null) continue;
                foreach (var cell in room.OccupiedCells())
                    occupied.Add(cell);
            }

            if (occupied.Count == 0) return new HashSet<Vector2Int>();

            var yMin = int.MaxValue;
            var yMax = int.MinValue;
            foreach (var cell in occupied)
            {
                if (cell.y < yMin) yMin = cell.y;
                if (cell.y > yMax) yMax = cell.y;
            }

            var shell = new HashSet<Vector2Int>();
            for (var y = yMin; y <= yMax; y++)
            {
                var minX = int.MaxValue;
                var maxX = int.MinValue;
                foreach (var cell in occupied)
                {
                    if (cell.y < y) continue;
                    if (cell.x < minX) minX = cell.x;
                    if (cell.x > maxX) maxX = cell.x;
                }

                if (minX > maxX) continue;

                for (var x = minX; x <= maxX; x++)
                    shell.Add(new Vector2Int(x, y));
            }

            return shell;
        }

        public static bool ShouldSkipShellCell(Vector2Int cell, TowerGrid grid)
        {
            if (grid == null || !grid.TryGetRoomAt(cell, out var room) || room?.Type == null)
                return false;

            return room.Type.isLobby
                || room.Type.isSkyLobby
                || room.Type.isScaffolding
                || room.Type.isStairs
                || room.Type.isElevatorShaft;
        }
    }
}
