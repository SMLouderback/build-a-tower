using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public static class CondoEmployment
    {
        public const int CommuteMinMinutes = 15;
        public const int CommuteMaxMinutes = 60;
        public const int CommuteModeMinutes = 30;

        public static int InTowerWanted(int officeDesks, int condoResidents)
        {
            var residents = Mathf.Max(0, condoResidents);
            var ratio = officeDesks / (float)Mathf.Max(1, residents);
            var share = Mathf.Min(0.5f, ratio);
            return Mathf.FloorToInt(share * residents);
        }

        public static int RollCommuteOneWayMinutes(System.Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var sample = SampleTriangular(
                rng,
                CommuteMinMinutes,
                CommuteMaxMinutes,
                CommuteModeMinutes);
            return Mathf.Clamp(Mathf.RoundToInt(sample), CommuteMinMinutes, CommuteMaxMinutes);
        }

        public static Dictionary<int, int> DistributeReservedDesks(
            IReadOnlyList<RoomInstance> offices,
            int reserveCount)
        {
            var result = new Dictionary<int, int>();
            if (offices == null || reserveCount <= 0)
                return result;

            var remaining = reserveCount;
            foreach (var office in SortedOffices(offices))
            {
                if (remaining <= 0)
                    break;

                var capacity = office?.Type?.maxOccupants ?? 0;
                if (capacity <= 0)
                    continue;

                var take = Mathf.Min(capacity, remaining);
                if (take <= 0)
                    continue;

                result[office.InstanceId] = take;
                remaining -= take;
            }

            return result;
        }

        static IEnumerable<RoomInstance> SortedOffices(IReadOnlyList<RoomInstance> offices)
        {
            var sorted = new List<RoomInstance>(offices.Count);
            for (var i = 0; i < offices.Count; i++)
            {
                if (offices[i] != null)
                    sorted.Add(offices[i]);
            }

            sorted.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
            return sorted;
        }

        static float SampleTriangular(System.Random rng, float min, float max, float mode)
        {
            var u = (float)rng.NextDouble();
            var span = max - min;
            var fc = (mode - min) / span;
            if (u < fc)
                return min + Mathf.Sqrt(u * span * (mode - min));
            return max - Mathf.Sqrt((1f - u) * span * (max - mode));
        }
    }
}
