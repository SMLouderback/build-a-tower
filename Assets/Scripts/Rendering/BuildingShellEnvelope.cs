using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Fills narrow cracks between room cells with structure paint (cement / dirt).
    /// Does not span large empty spans — that created flat grey “slabs” between towers.
    /// </summary>
    public static class BuildingShellEnvelope
    {
        /// <summary>Only bridge empty runs of this many cells (1 = single-cell seams).</summary>
        public const int MaxBridgeGap = 1;

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

            var byFloor = new Dictionary<int, List<int>>();
            foreach (var cell in occupied)
            {
                if (!byFloor.TryGetValue(cell.y, out var xs))
                {
                    xs = new List<int>();
                    byFloor[cell.y] = xs;
                }

                xs.Add(cell.x);
            }

            var shell = new HashSet<Vector2Int>();
            foreach (var (y, xs) in byFloor)
            {
                xs.Sort();
                // Unique
                var unique = new List<int>(xs.Count);
                var last = int.MinValue;
                foreach (var x in xs)
                {
                    if (x == last) continue;
                    unique.Add(x);
                    last = x;
                }

                for (var i = 0; i < unique.Count - 1; i++)
                {
                    var a = unique[i];
                    var b = unique[i + 1];
                    var gap = b - a - 1;
                    if (gap < 1 || gap > MaxBridgeGap) continue;
                    for (var x = a + 1; x < b; x++)
                        shell.Add(new Vector2Int(x, y));
                }
            }

            return shell;
        }

        public static bool ShouldSkipShellCell(Vector2Int cell, TowerGrid grid)
        {
            if (grid == null || !grid.TryGetRoomAt(cell, out var room) || room?.Type == null)
                return false;

            // Structure-owned rooms keep their own tiles; dollhouse rooms skip shell
            // so authored transparent window panes show sky/parallax instead of grey fill.
            if (OwnsStructurePaint(room.Type))
                return true;
            return RoomDollhouseArt.IsMapped(room.Type);
        }

        /// <summary>
        /// Rooms that paint on the structure tilemap (or must keep transit tiles).
        /// Shell clear/repaint must not erase these cells' tiles.
        /// </summary>
        public static bool OwnsStructurePaint(RoomTypeSO type) =>
            type != null &&
            (type.isLobby
             || type.isSkyLobby
             || type.isScaffolding
             || type.isStairs
             || type.isElevatorShaft
             || type.isParkingRamp);
    }
}
