using System;
using UnityEngine;

namespace BuildATower
{
    public static class WaterSystem
    {
        static readonly int[] PumpOutputs = { 80, 120, 180, 260 };
        static readonly int[] PumpUpgradeFees = { 30000, 70000, 140000 };
        static readonly int[] PumpUpgradeDurationsMinutes = { 1440, 4320, 10080 };

        public static int Height(int lobbyFloor, int highestFloorAtOrAboveLobby)
        {
            return Math.Max(1, highestFloorAtOrAboveLobby - lobbyFloor + 1);
        }

        public static int Demand(int height, int upperHotels, int upperCondos)
        {
            return Math.Max(1, height * 2 + upperHotels * 4 + upperCondos * 4);
        }

        public static float PressurePercent(int supply, int demand)
        {
            return supply / (float)demand * 100f;
        }

        public static int PumpSupply(int level)
        {
            if (level < 0 || level > 3)
                return 0;
            return PumpOutputs[level];
        }

        public static int PumpUpgradeFee(int nextLevel)
        {
            if (nextLevel < 1 || nextLevel > 3)
                return 0;
            return PumpUpgradeFees[nextLevel - 1];
        }

        public static int PumpUpgradeMinutes(int nextLevel)
        {
            if (nextLevel < 1 || nextLevel > 3)
                return 0;
            return PumpUpgradeDurationsMinutes[nextLevel - 1];
        }

        public static float UpperFillMultiplier(float pressurePercent)
        {
            if (pressurePercent >= 100f)
                return 1f;
            return 0.25f + 0.75f * (pressurePercent / 100f);
        }

        public static int UpperStressGain(float pressurePercent)
        {
            if (pressurePercent >= 100f)
                return 0;
            return Mathf.RoundToInt(8f * (1f - pressurePercent / 100f));
        }
    }
}
