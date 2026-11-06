using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Finds dollhouse rooms whose context art can change when another room's footprint changes.
    /// </summary>
    public static class DollhouseRepaintSet
    {
        static readonly Vector2Int[] Cardinal =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down
        };

        public static IReadOnlyList<RoomInstance> Collect(TowerGrid grid, RoomInstance changedRoom)
        {
            var result = new List<RoomInstance>();
            if (grid == null || changedRoom == null)
                return result;

            var seen = new HashSet<int>();
            foreach (var cell in changedRoom.OccupiedCells())
            {
                foreach (var delta in Cardinal)
                {
                    if (!grid.TryGetRoomAt(cell + delta, out var neighbor))
                        continue;
                    if (!ShouldRepaint(neighbor, changedRoom))
                        continue;
                    if (!seen.Add(neighbor.InstanceId))
                        continue;
                    result.Add(neighbor);
                }
            }

            return result;
        }

        static bool ShouldRepaint(RoomInstance neighbor, RoomInstance changedRoom)
        {
            if (neighbor?.Type == null || changedRoom == null)
                return false;
            if (neighbor.InstanceId == changedRoom.InstanceId)
                return false;
            if (neighbor.Type.isScaffolding)
                return false;
            return RoomDollhouseArt.IsMapped(neighbor.Type);
        }
    }
}
