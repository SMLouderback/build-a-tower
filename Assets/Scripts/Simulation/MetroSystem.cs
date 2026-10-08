using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class MetroSystem
    {
        public const int MinLeftEdgeSpacingTiles = 24;
        public const float StreetBonusPerStation = 0.08f;
        public const float StreetBonusSoftCap = 0.20f;
        public const float TravelReliefPerStation = 0.05f;
        public const float TravelReliefSoftCap = 0.12f;

        readonly List<RectInt> _stations = new List<RectInt>();

        public int StationCount => _stations.Count;
        public bool HasTunnel => StationCount > 0;

        public float StreetTrafficMultiplier =>
            1f + Mathf.Min(StreetBonusSoftCap, StationCount * StreetBonusPerStation);

        public float TravelReliefMultiplier =>
            1f + Mathf.Min(TravelReliefSoftCap, StationCount * TravelReliefPerStation);

        public static int MaxStationsFor(int stars)
        {
            if (stars >= 5) return 3;
            if (stars >= 4) return 2;
            if (stars >= 3) return 1;
            return 0;
        }

        public bool CanPlace(IReadOnlyList<RectInt> existingLeftAnchors, RectInt candidate, int stars, out string reason)
        {
            reason = string.Empty;
            var existing = existingLeftAnchors ?? Array.Empty<RectInt>();
            var max = MaxStationsFor(stars);
            if (max <= 0)
            {
                reason = "Metro stations require at least 3 stars.";
                return false;
            }

            if (existing.Count >= max)
            {
                reason = $"Maximum metro stations ({max}) for current star rating.";
                return false;
            }

            var candidateLeft = candidate.x;
            for (var i = 0; i < existing.Count; i++)
            {
                if (Mathf.Abs(candidateLeft - existing[i].x) < MinLeftEdgeSpacingTiles)
                {
                    reason =
                        $"Metro stations must be at least {MinLeftEdgeSpacingTiles} tiles apart (left edge to left edge).";
                    return false;
                }
            }

            return true;
        }

        public void RegisterStation(RectInt footprint)
        {
            for (var i = 0; i < _stations.Count; i++)
            {
                if (_stations[i].Equals(footprint))
                    return;
            }

            _stations.Add(footprint);
        }

        public void UnregisterStation(RectInt footprint)
        {
            for (var i = _stations.Count - 1; i >= 0; i--)
            {
                if (_stations[i].Equals(footprint))
                    _stations.RemoveAt(i);
            }
        }

        public void Clear() => _stations.Clear();
    }
}
