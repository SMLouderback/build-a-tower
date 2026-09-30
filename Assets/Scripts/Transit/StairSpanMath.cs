using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Stair comfort span helpers. Transfer floors (lobby, sky lobby, atrium floors)
    /// reset the comfort chain, so only the longest segment between them counts.
    /// </summary>
    public static class StairSpanMath
    {
        /// <summary>
        /// Longest floor gap between consecutive stops on a stairs journey from
        /// <paramref name="startY"/> to <paramref name="goalY"/>, where every transfer floor
        /// strictly between them (inclusive of either end) splits the journey.
        /// With no transfer floors in range this equals |goalY - startY|.
        /// </summary>
        public static int MaxSegmentSpan(int startY, int goalY, IReadOnlyList<int> transferFloorsSorted)
        {
            var lo = Mathf.Min(startY, goalY);
            var hi = Mathf.Max(startY, goalY);
            if (lo == hi) return 0;

            var max = 0;
            var prev = lo;
            if (transferFloorsSorted != null)
            {
                for (var i = 0; i < transferFloorsSorted.Count; i++)
                {
                    var floor = transferFloorsSorted[i];
                    if (floor <= lo || floor >= hi) continue;
                    max = Mathf.Max(max, floor - prev);
                    prev = floor;
                }
            }

            return Mathf.Max(max, hi - prev);
        }
    }
}
