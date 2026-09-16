using UnityEngine;

namespace BuildATower
{
    public static class AmenitySystem
    {
        public const int MaxFloorDelta = 2;
        public const int MaxHorizontalCells = 12;
        public const float HotelDemandBonusAmount = 0.08f;

        public static float ReliefForId(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0f;
            if (id.StartsWith("leisure_spa", System.StringComparison.Ordinal)) return 5f;
            if (id.StartsWith("leisure_pool", System.StringComparison.Ordinal)) return 4f;
            if (id.StartsWith("leisure_gym", System.StringComparison.Ordinal)) return 3f;
            if (id.StartsWith("leisure_theater", System.StringComparison.Ordinal)) return 3f;
            if (id.StartsWith("leisure_bowling", System.StringComparison.Ordinal)) return 2f;
            return 0f;
        }

        public static float MaxReliefInRange(TowerGrid grid, RoomInstance home)
        {
            if (grid == null || home == null) return 0f;
            var max = 0f;
            foreach (var room in grid.Rooms)
            {
                if (!IsEligibleAmenity(room, home)) continue;
                var relief = ReliefForId(room.Type.id);
                if (relief > max) max = relief;
            }

            return max;
        }

        public static bool TryApplyDailyRelief(Agent agent, TowerGrid grid, int dayIndex)
        {
            if (agent == null || grid == null) return false;
            if (agent.Role is not (AgentRole.HotelGuest or AgentRole.CondoResident)) return false;
            if (agent.HomeRoom == null) return false;
            if (agent.AmenityReliefDay == dayIndex) return false;

            var relief = MaxReliefInRange(grid, agent.HomeRoom);
            if (relief <= 0f) return false;

            agent.RelieveStress(relief);
            agent.AmenityReliefDay = dayIndex;
            return true;
        }

        public static float HotelDemandBonus(TowerGrid grid, RoomInstance hotel)
        {
            if (grid == null || hotel == null) return 0f;
            foreach (var room in grid.Rooms)
            {
                if (!IsEligibleAmenity(room, hotel)) continue;
                var id = room.Type.id;
                if (id != null &&
                    (id.StartsWith("leisure_spa", System.StringComparison.Ordinal) ||
                     id.StartsWith("leisure_gym", System.StringComparison.Ordinal)))
                    return HotelDemandBonusAmount;
            }

            return 0f;
        }

        static bool IsEligibleAmenity(RoomInstance amenity, RoomInstance home)
        {
            if (amenity?.Type == null || home == null) return false;
            if (amenity.IsBroken) return false;
            if (amenity.Type.ResolvedBuildFamily() != BuildFamily.Leisure) return false;
            return IsInRange(amenity.Origin, home.Origin);
        }

        /// <summary>Origin-to-origin: ±2 floors, ≤12 cells horizontal.</summary>
        static bool IsInRange(Vector2Int amenityOrigin, Vector2Int homeOrigin)
        {
            if (Mathf.Abs(amenityOrigin.y - homeOrigin.y) > MaxFloorDelta) return false;
            return Mathf.Abs(amenityOrigin.x - homeOrigin.x) <= MaxHorizontalCells;
        }
    }
}
