using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public static class ShopVisitRules
    {
        public static bool IsTrafficVenue(RoomTypeSO type) =>
            type != null && type.incomeModel == IncomeModel.TrafficVariable;

        public static bool IsShop(RoomTypeSO type) =>
            IsTrafficVenue(type) && type.ResolvedBuildFamily() == BuildFamily.Shops;

        public static bool IsOpen(RoomTypeSO type, int minuteOfDay)
        {
            if (!IsTrafficVenue(type)) return false;
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

        public static RoomInstance PickWeightedShop(IReadOnlyList<RoomInstance> shops, System.Random rng)
        {
            if (shops == null || shops.Count == 0) return null;
            if (shops.Count == 1) return shops[0];

            var total = 0f;
            foreach (var shop in shops)
            {
                var w = shop?.Type?.streetVisitWeight ?? 1f;
                if (w > 0f) total += w;
            }

            if (total <= 0f)
                return shops[rng.Next(shops.Count)];

            var roll = (float)(rng.NextDouble() * total);
            foreach (var shop in shops)
            {
                var w = shop?.Type?.streetVisitWeight ?? 1f;
                if (w <= 0f) continue;
                roll -= w;
                if (roll < 0f) return shop;
            }

            return shops[shops.Count - 1];
        }

        public static float DemandWeight(RoomInstance shop, bool streetOrigin)
        {
            if (shop?.Type == null) return 0f;
            var free = Mathf.Max(1, SlotCount(shop.Type) - shop.ConcurrentVisitors);
            var fairness = 1f + 1f / (1f + Mathf.Max(0, shop.VisitsToday));
            var origin = streetOrigin ? Mathf.Max(0.1f, shop.Type.streetVisitWeight) : 1f;
            return free * fairness * origin;
        }

        public static float DemandWeight(RoomInstance shop, bool streetOrigin, TowerGrid grid)
        {
            var dockBonus = IsShop(shop?.Type)
                ? AmenitySystem.LoadingDockDemandBonus(grid, shop)
                : 0f;
            return DemandWeight(shop, streetOrigin) * (1f + dockBonus);
        }

        public static RoomInstance PickDemandWeightedShop(
            IReadOnlyList<RoomInstance> shops,
            System.Random rng,
            bool streetOrigin) =>
            PickDemandWeightedShop(shops, rng, streetOrigin, null);

        public static RoomInstance PickDemandWeightedShop(
            IReadOnlyList<RoomInstance> shops,
            System.Random rng,
            bool streetOrigin,
            TowerGrid grid)
        {
            if (shops == null || shops.Count == 0) return null;
            if (shops.Count == 1) return shops[0];

            var total = 0f;
            foreach (var shop in shops)
                total += DemandWeight(shop, streetOrigin, grid);

            if (total <= 0f)
                return shops[rng.Next(shops.Count)];

            var roll = (float)(rng.NextDouble() * total);
            foreach (var shop in shops)
            {
                roll -= DemandWeight(shop, streetOrigin, grid);
                if (roll < 0f) return shop;
            }

            return shops[shops.Count - 1];
        }

        public static (int lo, int hi) DwellRangeForId(string id)
        {
            if (TryIdSpecificDwellRange(id, out var range))
                return range;
            return (20, 40);
        }

        static (int lo, int hi) DwellRange(RoomTypeSO type)
        {
            if (type != null && TryIdSpecificDwellRange(type.id, out var range))
                return range;

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

        static bool TryIdSpecificDwellRange(string id, out (int lo, int hi) range)
        {
            range = default;
            if (string.IsNullOrEmpty(id))
                return false;

            if (id.IndexOf("food_fine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (40, 60);
                return true;
            }

            if (id.IndexOf("food_mexican", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (35, 50);
                return true;
            }

            if (id.IndexOf("food_taco", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (12, 20);
                return true;
            }

            if (id.IndexOf("food_chicken", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (15, 25);
                return true;
            }

            if (id.IndexOf("food_fast", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (15, 25);
                return true;
            }

            if (id.IndexOf("food_restaurant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (40, 60);
                return true;
            }

            if (id.IndexOf("retail_gifts", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (15, 30);
                return true;
            }

            if (id.IndexOf("retail_department", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (30, 50);
                return true;
            }

            if (id.IndexOf("retail_shoes", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (25, 40);
                return true;
            }

            if (id.IndexOf("retail", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                range = (20, 40);
                return true;
            }

            if (id.StartsWith("leisure_gym", StringComparison.OrdinalIgnoreCase))
            {
                range = (30, 50);
                return true;
            }

            if (id.StartsWith("leisure_spa", StringComparison.OrdinalIgnoreCase))
            {
                range = (45, 75);
                return true;
            }

            if (id.StartsWith("leisure_pool", StringComparison.OrdinalIgnoreCase))
            {
                range = (40, 70);
                return true;
            }

            if (id.StartsWith("leisure_bowling", StringComparison.OrdinalIgnoreCase))
            {
                range = (50, 80);
                return true;
            }

            if (id.StartsWith("leisure_theater", StringComparison.OrdinalIgnoreCase))
            {
                range = (90, 130);
                return true;
            }

            if (id.StartsWith("leisure_casino", StringComparison.OrdinalIgnoreCase))
            {
                range = (40, 70);
                return true;
            }

            if (id.StartsWith("leisure_nightclub", StringComparison.OrdinalIgnoreCase))
            {
                range = (50, 90);
                return true;
            }

            if (id.StartsWith("leisure_chapel", StringComparison.OrdinalIgnoreCase))
            {
                range = (20, 40);
                return true;
            }

            return false;
        }
    }
}
