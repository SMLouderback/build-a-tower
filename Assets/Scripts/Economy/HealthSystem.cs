using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class HealthSystem
    {
        public const float MaxIllness = 100f;
        public const float GuestRaisePerMinute = 0.04f;
        public const float SickRoomRaisePerMinute = 0.15f;
        public const float NaturalDecayPerMinute = 0.05f;
        public const float ClinicBaselinePerMinute = 0.04f;
        public const float ClinicProximityPerMinute = 0.20f;
        public const int ClinicProximityFloors = 8;

        public const float QuarantineOn = 0.25f;
        public const float QuarantineOff = 0.22f;
        public const float HotelFillThreshold = 0.10f;
        public const float OfficeFillThreshold = 0.15f;
        public const float CondoFillThreshold = 0.20f;

        readonly Dictionary<int, float> _illness = new();
        int _towerFloorCount;

        public CleanMode CleanMode { get; set; } = CleanMode.Normal;
        public bool Quarantined { get; set; }

        public float GetIllness(int floor) =>
            _illness.TryGetValue(floor, out var value) ? value : 0f;

        public void SetIllness(int floor, float value)
        {
            var clamped = Clamp(value);
            if (Approximately(clamped, 0f))
                _illness.Remove(floor);
            else
                _illness[floor] = clamped;
            AverageIllness = ComputeAverage(_towerFloorCount);
        }

        public float AverageIllness { get; private set; }

        public void Tick(
            float deltaGameMinutes,
            IReadOnlyDictionary<int, int> hotelGuestsByFloor,
            IReadOnlyDictionary<int, bool> trashCoveredByFloor,
            IReadOnlyDictionary<int, int> sickRoomsByFloor,
            IReadOnlyList<int> workingClinicFloors,
            int towerFloorCount)
        {
            if (deltaGameMinutes <= 0f)
                return;

            var decayingFloors = new List<int>(_illness.Keys);

            if (hotelGuestsByFloor != null)
            {
                foreach (var kv in hotelGuestsByFloor)
                {
                    if (kv.Value <= 0)
                        continue;
                    if (IsTrashCovered(trashCoveredByFloor, kv.Key))
                        continue;
                    Add(kv.Key, GuestRaisePerMinute * kv.Value * deltaGameMinutes);
                }
            }

            if (sickRoomsByFloor != null)
            {
                foreach (var kv in sickRoomsByFloor)
                {
                    if (kv.Value <= 0)
                        continue;
                    Add(kv.Key, SickRoomRaisePerMinute * kv.Value * deltaGameMinutes);
                }
            }

            foreach (var floor in decayingFloors)
                Add(floor, -DecayPerFloor(floor, deltaGameMinutes, workingClinicFloors));

            _towerFloorCount = towerFloorCount;
            AverageIllness = ComputeAverage(towerFloorCount);
        }

        static bool IsTrashCovered(IReadOnlyDictionary<int, bool> trashCoveredByFloor, int floor)
        {
            if (trashCoveredByFloor == null)
                return false;
            return trashCoveredByFloor.TryGetValue(floor, out var covered) && covered;
        }

        static float DecayPerFloor(int floor, float deltaGameMinutes, IReadOnlyList<int> workingClinicFloors)
        {
            var decay = NaturalDecayPerMinute * deltaGameMinutes;
            if (workingClinicFloors == null)
                return decay;

            for (var i = 0; i < workingClinicFloors.Count; i++)
            {
                decay += ClinicBaselinePerMinute * deltaGameMinutes;
                var distance = Math.Abs(floor - workingClinicFloors[i]);
                if (distance > ClinicProximityFloors)
                    continue;
                decay += ClinicProximityPerMinute * (1f - distance / (float)ClinicProximityFloors) * deltaGameMinutes;
            }

            return decay;
        }

        float ComputeAverage(int towerFloorCount)
        {
            if (towerFloorCount <= 0)
                return 0f;

            var sum = 0f;
            foreach (var kv in _illness)
                sum += kv.Value;
            return sum / towerFloorCount;
        }

        void Add(int floor, float delta)
        {
            if (Approximately(delta, 0f))
                return;

            var next = Clamp(GetIllness(floor) + delta);
            if (Approximately(next, 0f))
                _illness.Remove(floor);
            else
                _illness[floor] = next;
        }

        static float Clamp(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > MaxIllness)
                return MaxIllness;
            return value;
        }

        static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), 1.1920928955078125e-7f * 8f);

        public static float FillMultiplier(float sickShare, float threshold)
        {
            if (sickShare < threshold)
                return 1f;

            var span = 0.25f - threshold;
            var t = (sickShare - threshold) / span;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;
            return 0.85f * (1f - t);
        }

        public static bool EvaluateQuarantine(bool currentlyOn, float sickShare)
        {
            if (currentlyOn)
            {
                if (sickShare <= QuarantineOff)
                    return false;
                return true;
            }

            return sickShare >= QuarantineOn;
        }

        public HealthSnapshotV1 CaptureSnapshot()
        {
            var count = _illness.Count;
            var floors = new float[count];
            var values = new float[count];
            var index = 0;
            foreach (var kv in _illness)
            {
                floors[index] = kv.Key;
                values[index] = kv.Value;
                index++;
            }

            return new HealthSnapshotV1
            {
                floorIndices = floors,
                illness = values,
                cleanMode = CleanMode.ToString(),
                quarantined = Quarantined
            };
        }

        /// <summary>
        /// Restore from a save. Null or an absent legacy block clears illness and resets clean mode and quarantine.
        /// Throws <see cref="ArgumentException"/> when the snapshot is present but invalid.
        /// </summary>
        public void RestoreSnapshot(HealthSnapshotV1 snapshot)
        {
            _illness.Clear();
            CleanMode = CleanMode.Normal;
            Quarantined = false;
            AverageIllness = 0f;

            if (IsAbsent(snapshot))
                return;

            if (!TryValidateSnapshot(snapshot, out var error))
                throw new ArgumentException(error, nameof(snapshot));

            Enum.TryParse(snapshot.cleanMode, false, out CleanMode mode);
            CleanMode = mode;
            Quarantined = snapshot.quarantined;

            for (var i = 0; i < snapshot.floorIndices.Length; i++)
                SetIllness((int)snapshot.floorIndices[i], snapshot.illness[i]);
        }

        /// <summary>
        /// True for older saves with no health block. <c>JsonUtility</c> materializes a missing
        /// serializable class as an all-default object, so blank clean mode and empty arrays count as absent too.
        /// </summary>
        public static bool IsAbsent(HealthSnapshotV1 snapshot) =>
            snapshot == null
            || (string.IsNullOrEmpty(snapshot.cleanMode)
                && IsNullOrEmpty(snapshot.floorIndices)
                && IsNullOrEmpty(snapshot.illness)
                && !snapshot.quarantined);

        public static bool TryValidateSnapshot(HealthSnapshotV1 snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Health snapshot is missing.";
                return false;
            }

            if (!TryParseCleanMode(snapshot.cleanMode, out _))
            {
                error = "Health clean mode is unknown.";
                return false;
            }

            if (snapshot.floorIndices == null || snapshot.illness == null)
            {
                error = "Health floor illness arrays are missing.";
                return false;
            }

            if (snapshot.floorIndices.Length != snapshot.illness.Length)
            {
                error = "Health floor and illness arrays differ in length.";
                return false;
            }

            for (var i = 0; i < snapshot.floorIndices.Length; i++)
            {
                var floor = snapshot.floorIndices[i];
                if (!IsWholeNumber(floor))
                {
                    error = "A health floor index is not a whole number.";
                    return false;
                }

                var illness = snapshot.illness[i];
                if (illness < 0f || illness > MaxIllness)
                {
                    error = "A health illness value is outside 0–100.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        static bool TryParseCleanMode(string value, out CleanMode mode)
        {
            if (!Enum.TryParse(value, false, out mode))
                return false;

            return mode is CleanMode.Quick or CleanMode.Normal or CleanMode.Deep;
        }

        static bool IsNullOrEmpty(float[] values) => values == null || values.Length == 0;

        static bool IsWholeNumber(float value) =>
            Math.Abs(value - MathF.Round(value)) < 0.0001f;
    }
}
