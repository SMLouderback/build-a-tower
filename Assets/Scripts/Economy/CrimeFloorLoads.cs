using System;
using System.Collections.Generic;

namespace BuildATower
{
    public static class CrimeFloorLoads
    {
        public static float VisitorCrimeWeight(RoomTypeSO type)
        {
            if (type == null || string.IsNullOrEmpty(type.id)) return 1f;
            var id = type.id;
            if (id.StartsWith("leisure_casino", StringComparison.OrdinalIgnoreCase)) return 1.75f;
            if (id.StartsWith("leisure_nightclub", StringComparison.OrdinalIgnoreCase)) return 1.5f;
            if (id.StartsWith("leisure_chapel", StringComparison.OrdinalIgnoreCase)) return 0.5f;
            return 1f;
        }

        public static Dictionary<int, float> ShopLoadByFloor(TowerGrid grid)
        {
            var loads = new Dictionary<int, float>();
            if (grid == null) return loads;

            foreach (var room in grid.Rooms)
            {
                if (!ShopVisitRules.IsTrafficVenue(room.Type)) continue;
                if (room.ConcurrentVisitors <= 0) continue;

                var visitors = room.ConcurrentVisitors * VisitorCrimeWeight(room.Type);
                var minY = room.Origin.y;
                var maxY = room.Origin.y + room.Size.y - 1;
                for (var floor = minY; floor <= maxY; floor++)
                {
                    loads.TryGetValue(floor, out var existing);
                    loads[floor] = existing + visitors;
                }
            }

            return loads;
        }

        public static Dictionary<int, float> HotelLoadByFloor(TowerGrid grid, IReadOnlyList<Agent> agents)
        {
            var loads = new Dictionary<int, float>();
            if (agents == null) return loads;

            foreach (var agent in agents)
            {
                if (agent == null) continue;
                if (agent.Role != AgentRole.HotelGuest &&
                    !(agent.Role == AgentRole.EventVisitor && IsHotelHome(agent)))
                    continue;
                if (agent.Phase == AgentPhase.Outside) continue;

                var floor = agent.Cell.y;
                loads.TryGetValue(floor, out var existing);
                loads[floor] = existing + 1f;
            }

            return loads;
        }

        static bool IsHotelHome(Agent agent) =>
            agent?.HomeRoom?.Type != null &&
            agent.HomeRoom.Type.category == RoomCategory.Hotel;
    }
}
