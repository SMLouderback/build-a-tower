using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildATower
{
    public enum PowerBand
    {
        Elevator = 0,
        Occupied = 1,
        Other = 2,
        ShedFirst = 3
    }

    public readonly struct PowerNeed
    {
        public int InstanceId { get; }
        public int Cost { get; }
        public PowerBand Band { get; }

        public PowerNeed(int instanceId, int cost, PowerBand band)
        {
            InstanceId = instanceId;
            Cost = cost;
            Band = band;
        }
    }

    public static class PowerSystem
    {
        public const int FreeCapacity = 20;

        static readonly int[] PlantOutputs = { 50, 80, 120, 170, 230, 300, 380 };
        static readonly int[] PlantUpgradeFees = { 40000, 90000, 180000, 310000, 480000, 690000 };
        static readonly int[] PlantUpgradeDurationsMinutes = { 1440, 4320, 10080, 18720, 30240, 44640 };

        public static int Cost(RoomTypeSO type)
        {
            if (type.isLobby || type.isSkyLobby || type.isStairs)
                return 0;
            if (type.id == "service_power")
                return 0;
            if (type.isElevatorShaft)
                return 2;
            return type.size.y;
        }

        public static int PlantOutput(int level)
        {
            if (level < 0 || level > 6)
                return 0;
            return PlantOutputs[level];
        }

        public static int PlantUpgradeFee(int nextLevel)
        {
            if (nextLevel < 1 || nextLevel > 6)
                return 0;
            return PlantUpgradeFees[nextLevel - 1];
        }

        public static int PlantUpgradeMinutes(int nextLevel)
        {
            if (nextLevel < 1 || nextLevel > 6)
                return 0;
            return PlantUpgradeDurationsMinutes[nextLevel - 1];
        }

        public static int Capacity(int[] plantLevels, bool[] plantBroken)
        {
            var total = FreeCapacity;
            if (plantLevels == null || plantBroken == null)
                return total;

            var count = Math.Min(plantLevels.Length, plantBroken.Length);
            for (var i = 0; i < count; i++)
            {
                if (!plantBroken[i])
                    total += PlantOutput(plantLevels[i]);
            }

            return total;
        }

        public static int[] Assign(IReadOnlyList<PowerNeed> needs, int capacity)
        {
            if (needs == null || needs.Count == 0)
                return Array.Empty<int>();

            var remaining = capacity;
            var powered = new List<int>();
            foreach (var need in needs.OrderBy(n => n.Band).ThenBy(n => n.InstanceId))
            {
                if (remaining < need.Cost)
                    continue;
                remaining -= need.Cost;
                powered.Add(need.InstanceId);
            }

            return powered.ToArray();
        }
    }
}
