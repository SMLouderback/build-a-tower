using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class CrimeSystem
    {
        public const float MaxCrime = 100f;
        public const float ShopRaisePerVisitorPerMinute = 0.8f;
        public const float HotelRaisePerGuestPerMinute = 0.35f;
        public const float NaturalDecayPerMinute = 0.05f;
        public const float BaselineDecayPerStaffPerMinute = 0.08f;
        public const float PatrolDecayPerMinute = 1.2f;
        public const float PatrolAdjacentFactor = 0.5f;
        public const float CriminalRaisePerMinute = 2.5f;
        public const float CaptureCrimeDrop = 10f;

        readonly Dictionary<int, float> _crime = new();

        public float GetCrime(int floor) =>
            _crime.TryGetValue(floor, out var value) ? value : 0f;

        public void SetCrime(int floor, float value) =>
            _crime[floor] = Clamp(value);

        public float AverageCrime
        {
            get
            {
                if (_crime.Count == 0) return 0f;
                var sum = 0f;
                foreach (var kv in _crime)
                    sum += kv.Value;
                return sum / _crime.Count;
            }
        }

        public void Tick(
            float deltaGameMinutes,
            IReadOnlyDictionary<int, float> shopLoadByFloor,
            IReadOnlyDictionary<int, float> hotelLoadByFloor,
            int totalStaffedSecurityWorkers,
            IReadOnlyList<int> patrolFloors,
            IReadOnlyList<int> criminalFloors,
            float crimeSuppressionMultiplier = 1f)
        {
            if (deltaGameMinutes <= 0f) return;

            if (shopLoadByFloor != null)
            {
                foreach (var kv in shopLoadByFloor)
                    Add(kv.Key, ShopRaisePerVisitorPerMinute * kv.Value * deltaGameMinutes);
            }

            if (hotelLoadByFloor != null)
            {
                foreach (var kv in hotelLoadByFloor)
                    Add(kv.Key, HotelRaisePerGuestPerMinute * kv.Value * deltaGameMinutes);
            }

            if (criminalFloors != null)
            {
                foreach (var floor in criminalFloors)
                    Add(floor, CriminalRaisePerMinute * deltaGameMinutes);
            }

            var suppression = crimeSuppressionMultiplier <= 0f ? 1f : crimeSuppressionMultiplier;
            var baselineDecay = totalStaffedSecurityWorkers * BaselineDecayPerStaffPerMinute * deltaGameMinutes * suppression;
            var patrolDecay = new Dictionary<int, float>();
            if (patrolFloors != null)
            {
                foreach (var floor in patrolFloors)
                {
                    AddPatrolDecay(patrolDecay, floor, PatrolDecayPerMinute * deltaGameMinutes * suppression);
                    AddPatrolDecay(patrolDecay, floor - 1, PatrolDecayPerMinute * PatrolAdjacentFactor * deltaGameMinutes * suppression);
                    AddPatrolDecay(patrolDecay, floor + 1, PatrolDecayPerMinute * PatrolAdjacentFactor * deltaGameMinutes * suppression);
                }
            }

            var floors = new List<int>(_crime.Keys);
            foreach (var floor in floors)
            {
                var decay = NaturalDecayPerMinute * deltaGameMinutes + baselineDecay;
                if (patrolDecay.TryGetValue(floor, out var patrol))
                    decay += patrol;
                Add(floor, -decay);
            }
        }

        public void ApplyCaptureDrop(int floor) =>
            Add(floor, -CaptureCrimeDrop);

        void Add(int floor, float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;
            var next = Clamp(GetCrime(floor) + delta);
            if (Mathf.Approximately(next, 0f))
                _crime.Remove(floor);
            else
                _crime[floor] = next;
        }

        static void AddPatrolDecay(Dictionary<int, float> patrolDecay, int floor, float amount)
        {
            if (Mathf.Approximately(amount, 0f)) return;
            patrolDecay.TryGetValue(floor, out var existing);
            patrolDecay[floor] = existing + amount;
        }

        static float Clamp(float value) => Mathf.Clamp(value, 0f, MaxCrime);
    }
}
