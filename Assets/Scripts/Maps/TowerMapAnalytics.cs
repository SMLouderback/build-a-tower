using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Rolling samples and 0–1 per-cell scores for Maps heatmaps / graph history.
    /// </summary>
    public sealed class TowerMapAnalytics
    {
        public const int TrafficHistoryDays = 30;
        public const int ClimateHistoryDays = 90;

        readonly Dictionary<Vector2Int, float> _trafficToday = new();
        readonly Dictionary<Vector2Int, float> _waitToday = new();
        readonly List<Dictionary<Vector2Int, float>> _trafficDayHistory = new();

        readonly Dictionary<Vector2Int, float> _crime = new();
        readonly Dictionary<Vector2Int, float> _noise = new();
        readonly Dictionary<Vector2Int, float> _traffic = new();
        readonly Dictionary<Vector2Int, float> _econProfit = new();
        readonly Dictionary<Vector2Int, float> _econDemand = new();
        readonly Dictionary<Vector2Int, float> _econBlend = new();

        readonly List<(int climateStep, float spendMult, float demandProxy)> _climateHistory = new();

        public IReadOnlyList<(int climateStep, float spendMult, float demandProxy)> ClimateHistory =>
            _climateHistory;

        public static float Clamp01(float v) =>
            v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Blend(float profit, float demand, float profitWeight = 0.5f)
        {
            var w = Clamp01(profitWeight);
            return Clamp01(profit * w + demand * (1f - w));
        }

        public void ClearAllScores()
        {
            _crime.Clear();
            _noise.Clear();
            _traffic.Clear();
            _econProfit.Clear();
            _econDemand.Clear();
            _econBlend.Clear();
        }

        public float GetScore(TowerMapMode mode, Vector2Int cell, EconomicMapView econView = EconomicMapView.Blend)
        {
            switch (mode)
            {
                case TowerMapMode.Crime:
                    return _crime.TryGetValue(cell, out var c) ? c : 0f;
                case TowerMapMode.Noise:
                    return _noise.TryGetValue(cell, out var n) ? n : 0f;
                case TowerMapMode.Traffic:
                    return _traffic.TryGetValue(cell, out var t) ? t : 0f;
                case TowerMapMode.Economic:
                    return econView switch
                    {
                        EconomicMapView.Profit => _econProfit.TryGetValue(cell, out var p) ? p : 0f,
                        EconomicMapView.Demand => _econDemand.TryGetValue(cell, out var d) ? d : 0f,
                        _ => _econBlend.TryGetValue(cell, out var b) ? b : 0f
                    };
                default:
                    return 0f;
            }
        }

        public IEnumerable<KeyValuePair<Vector2Int, float>> EnumerateScores(
            TowerMapMode mode,
            EconomicMapView econView = EconomicMapView.Blend)
        {
            Dictionary<Vector2Int, float> src = mode switch
            {
                TowerMapMode.Crime => _crime,
                TowerMapMode.Noise => _noise,
                TowerMapMode.Traffic => _traffic,
                TowerMapMode.Economic => econView switch
                {
                    EconomicMapView.Profit => _econProfit,
                    EconomicMapView.Demand => _econDemand,
                    _ => _econBlend
                },
                _ => null
            };
            if (src == null) yield break;
            foreach (var kv in src)
                yield return kv;
        }

        public void RecordTraversal(Vector2Int cell, float weight = 1f)
        {
            if (weight <= 0f) return;
            _trafficToday.TryGetValue(cell, out var cur);
            _trafficToday[cell] = cur + weight;
        }

        public void RecordWait(Vector2Int cell, float weight = 1f)
        {
            if (weight <= 0f) return;
            _waitToday.TryGetValue(cell, out var cur);
            _waitToday[cell] = cur + weight;
        }

        public void ArchiveTrafficDay()
        {
            var day = new Dictionary<Vector2Int, float>();
            foreach (var kv in _trafficToday)
                day[kv.Key] = kv.Value;
            foreach (var kv in _waitToday)
            {
                day.TryGetValue(kv.Key, out var cur);
                day[kv.Key] = cur + kv.Value * 1.5f;
            }

            _trafficDayHistory.Add(day);
            while (_trafficDayHistory.Count > TrafficHistoryDays)
                _trafficDayHistory.RemoveAt(0);

            _trafficToday.Clear();
            _waitToday.Clear();
        }

        public void RebuildTraffic(TrafficMapWindow window, Dictionary<Vector2Int, float> capacityStress = null)
        {
            _traffic.Clear();
            if (window == TrafficMapWindow.Today)
            {
                MergeInto(_traffic, _trafficToday, 1f);
                MergeInto(_traffic, _waitToday, 1.5f);
            }
            else
            {
                if (_trafficDayHistory.Count == 0)
                {
                    MergeInto(_traffic, _trafficToday, 1f);
                    MergeInto(_traffic, _waitToday, 1.5f);
                }
                else
                {
                    var counts = new Dictionary<Vector2Int, int>();
                    foreach (var day in _trafficDayHistory)
                    {
                        foreach (var kv in day)
                        {
                            _traffic.TryGetValue(kv.Key, out var sum);
                            _traffic[kv.Key] = sum + kv.Value;
                            counts.TryGetValue(kv.Key, out var n);
                            counts[kv.Key] = n + 1;
                        }
                    }

                    var keys = new List<Vector2Int>(_traffic.Keys);
                    foreach (var key in keys)
                        _traffic[key] /= Math.Max(1, counts[key]);
                }
            }

            if (capacityStress != null)
            {
                foreach (var kv in capacityStress)
                {
                    _traffic.TryGetValue(kv.Key, out var cur);
                    _traffic[kv.Key] = cur + kv.Value;
                }
            }

            NormalizeMap(_traffic);
        }

        public void SetCrimeScores(Dictionary<Vector2Int, float> scores)
        {
            _crime.Clear();
            if (scores == null) return;
            foreach (var kv in scores)
                _crime[kv.Key] = Clamp01(kv.Value);
        }

        public void SetNoiseScores(Dictionary<Vector2Int, float> scores)
        {
            _noise.Clear();
            if (scores == null) return;
            foreach (var kv in scores)
                _noise[kv.Key] = Clamp01(kv.Value);
        }

        public void SetEconomicScores(
            Dictionary<Vector2Int, float> profit,
            Dictionary<Vector2Int, float> demand)
        {
            _econProfit.Clear();
            _econDemand.Clear();
            _econBlend.Clear();
            if (profit != null)
            {
                foreach (var kv in profit)
                    _econProfit[kv.Key] = ClampSigned01(kv.Value);
            }

            if (demand != null)
            {
                foreach (var kv in demand)
                    _econDemand[kv.Key] = Clamp01(kv.Value);
            }

            var cells = new HashSet<Vector2Int>();
            foreach (var k in _econProfit.Keys) cells.Add(k);
            foreach (var k in _econDemand.Keys) cells.Add(k);
            foreach (var cell in cells)
            {
                _econProfit.TryGetValue(cell, out var p);
                _econDemand.TryGetValue(cell, out var d);
                // Blend stays on 0–1 risk scale; losses contribute no “profit intensity”.
                _econBlend[cell] = Blend(Math.Max(0f, p), d);
            }
        }

        public static float ClampSigned01(float v) =>
            v < -1f ? -1f : v > 1f ? 1f : v;

        public void RecordClimateSample(int climateStep, float spendMult, float demandProxy)
        {
            _climateHistory.Add((climateStep, spendMult, Clamp01(demandProxy)));
            while (_climateHistory.Count > ClimateHistoryDays)
                _climateHistory.RemoveAt(0);
        }

        public static float CrimeScore(float traffic, float criminal, float eventBoost, float patrol)
        {
            return Clamp01(traffic * 0.45f + criminal * 0.4f + eventBoost * 0.25f - patrol * 0.35f);
        }

        public static float TrafficCapacityStress(float occupancy, float capacity, float researchEfficiencyMult = 1f)
        {
            if (capacity <= 0f) return 1f;
            var effCap = capacity * Math.Max(0.25f, researchEfficiencyMult);
            var ratio = occupancy / effCap;
            if (ratio < 0.7f) return 0f;
            if (ratio >= 1.2f) return 1f;
            return Clamp01((ratio - 0.7f) / 0.5f);
        }

        /// <summary>
        /// Economic demand heat: empty living/office space is hot. Condo unsold ≈ full vacancy.
        /// </summary>
        public static float LivingDemandStress(
            RoomCategory category,
            int occupants,
            int maxOccupants,
            bool condoSold,
            int overpriceSteps = 0)
        {
            if (maxOccupants <= 0) return 0f;
            if (category is not (RoomCategory.Office or RoomCategory.Hotel or RoomCategory.Condo))
                return 0.2f;

            if (category == RoomCategory.Condo && !condoSold)
                return Clamp01(0.9f + overpriceSteps * 0.05f);

            var fill = occupants / (float)Math.Max(1, maxOccupants);
            var stress = Clamp01(1f - fill);
            if (overpriceSteps > 0)
                stress = Clamp01(stress + 0.08f * overpriceSteps);
            return stress;
        }

        /// <summary>Tower-wide vacant seats / capacity among Office, Hotel, and Condo rooms.</summary>
        public static float TowerVacancyPressure(int vacantSeats, int totalCapacity) =>
            totalCapacity <= 0 ? 0f : Clamp01(vacantSeats / (float)totalCapacity);

        static void MergeInto(Dictionary<Vector2Int, float> dst, Dictionary<Vector2Int, float> src, float weight)
        {
            if (src == null) return;
            foreach (var kv in src)
            {
                dst.TryGetValue(kv.Key, out var cur);
                dst[kv.Key] = cur + kv.Value * weight;
            }
        }

        static void NormalizeMap(Dictionary<Vector2Int, float> map)
        {
            if (map == null || map.Count == 0) return;
            var max = 0f;
            foreach (var kv in map)
                if (kv.Value > max) max = kv.Value;
            if (max <= 0f)
            {
                map.Clear();
                return;
            }

            var keys = new List<Vector2Int>(map.Keys);
            foreach (var key in keys)
                map[key] = Clamp01(map[key] / max);
        }
    }
}
