using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public static class TransferFloorProvider
    {
        public static List<int> GetSortedTransferFloors(TowerGrid grid)
        {
            if (grid == null) return new List<int>();
            return grid.GetLobbyFloors();
        }

        public static IEnumerable<int> TransferFloorsBetween(int y0, int y1, TowerGrid grid)
        {
            var lo = Mathf.Min(y0, y1);
            var hi = Mathf.Max(y0, y1);
            foreach (var floor in GetSortedTransferFloors(grid))
            {
                if (floor >= lo && floor <= hi)
                    yield return floor;
            }
        }
    }
}
