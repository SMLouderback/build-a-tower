using System;
using UnityEngine;

namespace BuildATower
{
    public static class ShopVisitRules
    {
        public static bool IsShop(RoomTypeSO type) =>
            type != null && type.incomeModel == IncomeModel.TrafficVariable;

        public static bool IsOpen(RoomTypeSO type, int minuteOfDay)
        {
            if (!IsShop(type)) return false;
            if (!type.hasActiveHours) return true;
            var m = ((minuteOfDay % (24 * 60)) + 24 * 60) % (24 * 60);
            if (type.activeHoursStart <= type.activeHoursEnd)
                return m >= type.activeHoursStart && m < type.activeHoursEnd;
            return m >= type.activeHoursStart || m < type.activeHoursEnd;
        }

        public static int SlotCount(RoomTypeSO type) =>
            type == null ? 0 : Mathf.Max(1, type.maxOccupants);

        public static int PayPerVisit(RoomTypeSO type) =>
            type == null ? 0 : Math.Max(0, type.baseIncome);

        public static int PickDwellMinutes(RoomTypeSO type, System.Random rng)
        {
            var (lo, hi) = DwellRange(type);
            return lo + rng.Next(0, hi - lo + 1);
        }

        static (int lo, int hi) DwellRange(RoomTypeSO type)
        {
            if (type != null && !string.IsNullOrEmpty(type.id))
            {
                if (type.id.IndexOf("food_fast", StringComparison.OrdinalIgnoreCase) >= 0)
                    return (15, 25);
                if (type.id.IndexOf("food_restaurant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    type.id.IndexOf("restaurant", StringComparison.OrdinalIgnoreCase) >= 0)
                    return (40, 60);
                if (type.id.IndexOf("retail", StringComparison.OrdinalIgnoreCase) >= 0)
                    return (20, 40);
            }

            if (type != null)
            {
                var subgroup = type.ResolvedBuildSubgroup();
                if (subgroup == BuildSubgroup.Food)
                    return (15, 25);
                if (subgroup == BuildSubgroup.Retail)
                    return (20, 40);
            }

            return (20, 40);
        }
    }
}
