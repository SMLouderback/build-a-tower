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

        readonly Dictionary<int, float> _illness = new();
        int _towerFloorCount;

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
    }
}
