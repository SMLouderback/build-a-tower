using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public static class CathedralPlacement
    {
        public const int MinLeftEdgeSpacingTiles = 16;

        public static bool CanPlace(IReadOnlyList<RectInt> existing, RectInt candidate, out string reason)
        {
            reason = string.Empty;
            if (existing == null || existing.Count == 0)
                return true;

            var candidateLeft = candidate.x;
            for (var i = 0; i < existing.Count; i++)
            {
                if (Mathf.Abs(candidateLeft - existing[i].x) < MinLeftEdgeSpacingTiles)
                {
                    reason =
                        $"Cathedrals must be at least {MinLeftEdgeSpacingTiles} tiles apart (left edge to left edge).";
                    return false;
                }
            }

            return true;
        }
    }
}
