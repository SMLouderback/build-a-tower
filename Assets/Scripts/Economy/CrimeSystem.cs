using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class CrimeSystem
    {
        public const float MaxCrime = 100f;
        /// <summary>
        /// Per concurrent shop/leisure visitor on a floor.
        /// Retuned 2026-10: 3 full fast-food (12 visitors) must lose to ~4 staffed guards.
        /// </summary>
        public const float ShopRaisePerVisitorPerMinute = 0.08f;
        /// <summary>Per in-tower hotel guest / hotel-home event visitor on a floor.</summary>
        public const float HotelRaisePerGuestPerMinute = 0.035f;
        public const float NaturalDecayPerMinute = 0.08f;
        /// <summary>Applied to each floor with crime, per staffed security worker.</summary>
        public const float BaselineDecayPerStaffPerMinute = 0.25f;
        public const float PatrolDecayPerMinute = 0.7f;
        public const float PatrolAdjacentFactor = 0.5f;
        public const float CriminalRaisePerMinute = 1.2f;
        public const float CaptureCrimeDrop = 8f;
        /// <summary>EMA rate toward raw average (~12–15 game minutes to mostly catch up).</summary>
        public const float SentimentAlphaPerMinute = 0.08f;

        readonly Dictionary<int, float> _crime = new();
        float _sentiment;

        public float GetCrime(int floor) =>
            _crime.TryGetValue(floor, out var value) ? value : 0f;

        public void SetCrime(int floor, float value)
        {
            var clamped = Clamp(value);
            if (Mathf.Approximately(clamped, 0f))
                _crime.Remove(floor);
            else
                _crime[floor] = clamped;
            AverageCrime = ComputeAverage(0);
        }

        /// <summary>
        /// Mean crime across the tower. When <see cref="Tick"/> is given
        /// <c>towerFloorCount</c>, zero-crime floors dilute the average so a few
        /// hot floors cannot report as “100% tower crime.”
        /// </summary>
        public float AverageCrime { get; private set; }

        /// <summary>Smoothed tower crime for HUD / “sentiment” (lags raw <see cref="AverageCrime"/>).</summary>
        public float DisplayCrime => _sentiment;

        public void Tick(
            float deltaGameMinutes,
            IReadOnlyDictionary<int, float> shopLoadByFloor,
            IReadOnlyDictionary<int, float> hotelLoadByFloor,
            int totalStaffedSecurityWorkers,
            IReadOnlyList<int> patrolFloors,
            IReadOnlyList<int> criminalFloors,
            float crimeSuppressionMultiplier = 1f,
            int towerFloorCount = 0)
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

            AverageCrime = ComputeAverage(towerFloorCount);

            // Smooth HUD sentiment toward the current tower average.
            var blend = 1f - Mathf.Exp(-SentimentAlphaPerMinute * deltaGameMinutes);
            _sentiment = Mathf.Lerp(_sentiment, AverageCrime, blend);
        }

        float ComputeAverage(int towerFloorCount)
        {
            var sum = 0f;
            foreach (var kv in _crime)
                sum += kv.Value;
            if (towerFloorCount > 0)
                return sum / towerFloorCount;
            if (_crime.Count == 0) return 0f;
            return sum / _crime.Count;
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
