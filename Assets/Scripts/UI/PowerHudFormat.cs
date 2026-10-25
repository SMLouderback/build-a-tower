using UnityEngine;

namespace BuildATower
{
    public static class PowerHudFormat
    {
        static readonly string[] LevelNumerals = { "I", "II", "III", "IV", "V", "VI" };

        public static string FormatPower(int use, int capacity) => $"Power {use}/{capacity}";

        public static string FormatWater(float pressurePercent) =>
            $"Water {Mathf.RoundToInt(pressurePercent)}%";

        public static string FormatRoomCost(int cost) => $"Power {cost}";

        public static string FormatPlant(int level, int output) =>
            $"Power plant {LevelLabel(level)} · {output}";

        public static string FormatPump(int level, int supply) =>
            $"Pump {LevelLabel(level)} · {supply}";

        public static string FormatUpgrade(int nextLevel, int days, int fee) =>
            $"Upgrade to {LevelLabel(nextLevel)} · {days} days · ${fee:N0}";

        static string LevelLabel(int level)
        {
            if (level <= 0)
                return "Built";
            if (level >= 1 && level <= LevelNumerals.Length)
                return LevelNumerals[level - 1];
            return level.ToString();
        }
    }
}
