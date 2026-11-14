using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Pure path math for the metro train: loops the tunnel and dwells at station centers
    /// for a fixed span of game minutes.
    /// </summary>
    public static class MetroTrainMotion
    {
        public const float StationDwellGameMinutes = 5f;

        public struct State
        {
            /// <summary>Distance along the loop in [0, travel). Left edge = PathDistance - carTiles.</summary>
            public float PathDistance;
            public float DwellGameMinutesRemaining;
            public int ServedStopMask;
        }

        public static float TravelLength(float spanTiles, float carTiles) =>
            Mathf.Max(0.01f, spanTiles + carTiles);

        public static float LeftOffset(float pathDistance, float carTiles) =>
            pathDistance - carTiles;

        /// <summary>
        /// Stop path-distance where the train center matches the station footprint center.
        /// Relative to tunnel minX; only stations whose stop lies on the open track are kept.
        /// </summary>
        public static void CollectStopPathDistances(
            IReadOnlyList<RectInt> stations,
            int tunnelMinX,
            float spanTiles,
            float carTiles,
            List<float> into)
        {
            into.Clear();
            if (stations == null || stations.Count == 0)
                return;

            var travel = TravelLength(spanTiles, carTiles);
            for (var i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                var centerX = station.x + station.width * 0.5f;
                var stopLeft = centerX - tunnelMinX - carTiles * 0.5f;
                var pathDist = stopLeft + carTiles;
                if (pathDist < 0f || pathDist >= travel)
                    continue;
                into.Add(pathDist);
            }

            into.Sort();
        }

        public static State Advance(
            State state,
            float deltaGameMinutes,
            float spanTiles,
            float carTiles,
            float tilesPerGameMinute,
            float dwellGameMinutes,
            IReadOnlyList<float> stopPathDistances)
        {
            if (deltaGameMinutes <= 0f || tilesPerGameMinute <= 0f)
                return state;

            var travel = TravelLength(spanTiles, carTiles);
            state.PathDistance = Mathf.Repeat(Mathf.Max(0f, state.PathDistance), travel);

            var remaining = deltaGameMinutes;
            var guard = 0;
            while (remaining > 0f && guard++ < 64)
            {
                if (state.DwellGameMinutesRemaining > 0f)
                {
                    var used = Mathf.Min(remaining, state.DwellGameMinutesRemaining);
                    state.DwellGameMinutesRemaining -= used;
                    remaining -= used;
                    if (state.DwellGameMinutesRemaining > 1e-4f)
                        return state;
                    state.DwellGameMinutesRemaining = 0f;
                    continue;
                }

                var nextStop = FindNextUnservedStop(state.PathDistance, state.ServedStopMask, stopPathDistances);
                float eventDist;
                var isStop = nextStop >= 0;
                if (isStop)
                    eventDist = stopPathDistances[nextStop];
                else
                    eventDist = travel;

                var distToEvent = eventDist - state.PathDistance;
                if (distToEvent < 1e-4f)
                {
                    if (isStop)
                    {
                        state.ServedStopMask |= 1 << nextStop;
                        state.DwellGameMinutesRemaining = Mathf.Max(0f, dwellGameMinutes);
                        continue;
                    }

                    state.PathDistance = 0f;
                    state.ServedStopMask = 0;
                    continue;
                }

                var moveDist = tilesPerGameMinute * remaining;
                if (moveDist < distToEvent)
                {
                    state.PathDistance += moveDist;
                    return state;
                }

                var minutesToEvent = distToEvent / tilesPerGameMinute;
                remaining -= minutesToEvent;
                state.PathDistance = eventDist;
                if (isStop)
                {
                    state.ServedStopMask |= 1 << nextStop;
                    state.DwellGameMinutesRemaining = Mathf.Max(0f, dwellGameMinutes);
                }
                else
                {
                    state.PathDistance = 0f;
                    state.ServedStopMask = 0;
                }
            }

            return state;
        }

        static int FindNextUnservedStop(
            float pathDistance,
            int servedMask,
            IReadOnlyList<float> stops)
        {
            if (stops == null || stops.Count == 0)
                return -1;

            var best = -1;
            var bestDist = float.PositiveInfinity;
            for (var i = 0; i < stops.Count && i < 31; i++)
            {
                if ((servedMask & (1 << i)) != 0)
                    continue;
                var d = stops[i];
                if (d <= pathDistance + 1e-4f)
                    continue;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }
    }
}
