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

        public MetroSnapshotV1 CaptureSnapshot()
        {
            var stations = new MetroStationFootprintV1[_stations.Count];
            for (var i = 0; i < _stations.Count; i++)
            {
                var footprint = _stations[i];
                stations[i] = new MetroStationFootprintV1
                {
                    x = footprint.x,
                    y = footprint.y,
                    width = footprint.width,
                    height = footprint.height
                };
            }

            return new MetroSnapshotV1
            {
                stations = stations,
                hasTunnel = HasTunnel
            };
        }

        /// <summary>
        /// Restore station footprints. Null or an absent legacy block clears the metro.
        /// Throws <see cref="ArgumentException"/> when the snapshot is present but invalid.
        /// </summary>
        public void RestoreSnapshot(MetroSnapshotV1 snapshot)
        {
            Clear();
            if (IsAbsent(snapshot))
                return;

            if (!TryValidateSnapshot(snapshot, out var error))
                throw new ArgumentException(error ?? "Metro snapshot is invalid.", nameof(snapshot));

            for (var i = 0; i < snapshot.stations.Length; i++)
            {
                var station = snapshot.stations[i];
                RegisterStation(new RectInt(station.x, station.y, station.width, station.height));
            }
        }

        /// <summary>
        /// True for older saves with no metro block. <c>JsonUtility</c> materializes a missing
        /// serializable class as an all-default object, so a null station list and no tunnel count as absent.
        /// </summary>
        public static bool IsAbsent(MetroSnapshotV1 snapshot) =>
            snapshot == null || (snapshot.stations == null && !snapshot.hasTunnel);

        public static bool TryValidateSnapshot(MetroSnapshotV1 snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Metro snapshot is missing.";
                return false;
            }

            if (snapshot.stations == null)
            {
                error = "Metro station list is missing.";
                return false;
            }

            for (var i = 0; i < snapshot.stations.Length; i++)
            {
                var station = snapshot.stations[i];
                if (station == null)
                {
                    error = "A metro station footprint is missing.";
                    return false;
                }

                if (station.width <= 0 || station.height <= 0)
                {
                    error = "A metro station footprint has a non-positive size.";
                    return false;
                }
            }

            if (snapshot.hasTunnel != (snapshot.stations.Length > 0))
            {
                error = "Metro tunnel flag does not match the station list.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
