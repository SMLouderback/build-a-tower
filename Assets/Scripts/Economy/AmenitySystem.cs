using UnityEngine;

namespace BuildATower
{
    public static class AmenitySystem
    {
        public const int MaxFloorDelta = 2;
        public const int MaxHorizontalCells = 12;
        public const int AtriumMaxFloorDelta = 6;
        public const int AtriumMaxHorizontalCells = 24;
        public const float HotelDemandBonusAmount = 0.08f;
        public const float LoadingDockDemandBonusAmount = 0.05f;

        public static float ReliefForId(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0f;
            if (id.StartsWith("leisure_spa", System.StringComparison.Ordinal)) return 5f;
            if (id.StartsWith("leisure_pool", System.StringComparison.Ordinal)) return 4f;
            if (id.StartsWith("leisure_gym", System.StringComparison.Ordinal)) return 3f;
            if (id.StartsWith("leisure_theater", System.StringComparison.Ordinal)) return 3f;
            if (id.StartsWith("leisure_bowling", System.StringComparison.Ordinal)) return 2f;
            if (id.StartsWith("leisure_casino", System.StringComparison.Ordinal)) return 3f;
            if (id.StartsWith("leisure_nightclub", System.StringComparison.Ordinal)) return 2f;
            if (id.StartsWith("leisure_chapel", System.StringComparison.Ordinal)) return 4f;
            if (id.StartsWith("leisure_atrium", System.StringComparison.Ordinal)) return 5f;
            if (id == ParkingStalls.MailId) return 1f;
            if (id == ParkingStalls.RecyclingId) return 2f;
            return 0f;
        }

        public static float MaxReliefInRange(TowerGrid grid, RoomInstance home)
        {
            return MaxReliefInRange(grid, home, null);
        }

        static float MaxReliefInRange(TowerGrid grid, RoomInstance home, AgentRole? role)
        {
            if (grid == null || home == null) return 0f;
            var max = 0f;
            foreach (var room in grid.Rooms)
            {
                if (!IsEligibleReliefAmenity(grid, room, home, role)) continue;
                var relief = ReliefForId(room.Type.id);
                if (relief > max) max = relief;
            }

            return max;
        }

        public static bool TryApplyDailyRelief(Agent agent, TowerGrid grid, int dayIndex)
        {
            if (agent == null || grid == null) return false;
            if (agent.Role is not (AgentRole.HotelGuest or AgentRole.CondoResident or AgentRole.OfficeWorker))
                return false;
            if (agent.HomeRoom == null) return false;
            if (agent.AmenityReliefDay == dayIndex) return false;

            var relief = MaxReliefInRange(grid, agent.HomeRoom, agent.Role);
            if (relief <= 0f) return false;

            agent.RelieveStress(relief);
            agent.AmenityReliefDay = dayIndex;
            return true;
        }

        public static float HotelDemandBonus(TowerGrid grid, RoomInstance hotel)
        {
            if (grid == null || hotel == null) return 0f;
            var bonus = LoadingDockDemandBonus(grid, hotel);
            foreach (var room in grid.Rooms)
            {
                if (!IsEligibleReliefAmenity(grid, room, hotel, AgentRole.HotelGuest)) continue;
                var id = room.Type.id;
                if (id != null &&
                    (id.StartsWith("leisure_spa", System.StringComparison.Ordinal) ||
                     id.StartsWith("leisure_gym", System.StringComparison.Ordinal)))
                {
                    bonus += HotelDemandBonusAmount;
                    break;
                }
            }

            return bonus;
        }

        public static float LoadingDockDemandBonus(TowerGrid grid, RoomInstance beneficiary)
        {
            if (grid == null || beneficiary == null) return 0f;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type?.id != ParkingStalls.LoadingDockId) continue;
                if (room.IsBroken || !ParkingStalls.IsVehicleAccessible(grid, room)) continue;
                if (IsInRange(room, beneficiary.Origin))
                    return LoadingDockDemandBonusAmount;
            }

            return 0f;
        }

        static bool IsEligibleReliefAmenity(
            TowerGrid grid,
            RoomInstance amenity,
            RoomInstance home,
            AgentRole? role)
        {
            if (amenity?.Type == null || home == null) return false;
            if (amenity.IsBroken) return false;
            var id = amenity.Type.id;
            if (amenity.Type.ResolvedBuildFamily() == BuildFamily.Leisure)
            {
                if (role == AgentRole.OfficeWorker) return false;
            }
            else if (id == ParkingStalls.MailId)
            {
                if (role.HasValue &&
                    role != AgentRole.CondoResident &&
                    role != AgentRole.OfficeWorker)
                    return false;
            }
            else if (id == ParkingStalls.RecyclingId)
            {
                if (role.HasValue &&
                    role != AgentRole.CondoResident &&
                    role != AgentRole.HotelGuest)
                    return false;
                if (!ParkingStalls.IsVehicleAccessible(grid, amenity))
                    return false;
            }
            else
            {
                return false;
            }

            return IsInRange(amenity, home.Origin);
        }

        /// <summary>Origin-to-origin: ±2 floors (±6 atrium), ≤12 cells horizontal (≤24 atrium).</summary>
        static bool IsInRange(RoomInstance amenity, Vector2Int homeOrigin)
        {
            var amenityOrigin = amenity.Origin;
            var atrium = amenity.Type != null &&
                (amenity.Type.isAtrium ||
                 amenity.Type.id.StartsWith("leisure_atrium", System.StringComparison.Ordinal));
            var maxFloor = atrium ? AtriumMaxFloorDelta : MaxFloorDelta;
            var maxHoriz = atrium ? AtriumMaxHorizontalCells : MaxHorizontalCells;
            if (Mathf.Abs(amenityOrigin.y - homeOrigin.y) > maxFloor) return false;
            return Mathf.Abs(amenityOrigin.x - homeOrigin.x) <= maxHoriz;
        }
    }
}
