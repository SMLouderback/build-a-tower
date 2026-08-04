using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Underground parking stall capacity and claims for valet arrivals.
    /// </summary>
    public static class ParkingStalls
    {
        public const string ParkingId = "parking_underground";
        public const string ValetId = "service_valet";
        public const string RampId = "parking_ramp";
        public const int ParkingDailyUpkeep = 500;
        public const int ValetDailyUpkeep = 1_000;
        public const int RampDailyUpkeep = 200;
        public const float ArrivalViaParkingChance = 0.25f;
        public const int FiveStarMinStalls = 6;

        public static bool IsParking(RoomTypeSO type) =>
            type != null && type.id == ParkingId;

        public static bool IsValet(RoomTypeSO type) =>
            type != null && type.id == ValetId;

        public static bool IsRamp(RoomTypeSO type) =>
            type != null && (type.isParkingRamp || type.id == RampId);

        public static bool IsParking(RoomInstance room) =>
            room?.Type != null && IsParking(room.Type);

        public static bool IsRamp(RoomInstance room) =>
            room?.Type != null && IsRamp(room.Type);

        public static bool HasOperationalValet(TowerGrid grid)
        {
            if (grid == null) return false;
            foreach (var room in grid.Rooms)
            {
                if (!IsValet(room?.Type)) continue;
                if (room.IsBroken) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// B1 (−1) is always accessible. Deeper floors need a ramp chain to B1 or Lobby.
        /// </summary>
        public static bool IsParkingFloorAccessible(TowerGrid grid, int floor)
        {
            if (floor >= TowerGrid.LobbyFloor) return false;
            if (floor == -1) return true;
            if (grid == null) return false;

            var reachable = new HashSet<int> { floor };
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var room in grid.Rooms)
                {
                    if (!IsRamp(room) || room.IsBroken) continue;
                    var lo = room.Origin.y;
                    var hi = room.Origin.y + room.Size.y - 1;
                    var touches = false;
                    for (var y = lo; y <= hi; y++)
                    {
                        if (reachable.Contains(y))
                        {
                            touches = true;
                            break;
                        }
                    }

                    if (!touches) continue;
                    for (var y = lo; y <= hi; y++)
                    {
                        if (reachable.Add(y))
                            changed = true;
                    }
                }
            }

            return reachable.Contains(-1) || reachable.Contains(TowerGrid.LobbyFloor);
        }

        public static int TotalStalls(TowerGrid grid)
        {
            if (grid == null) return 0;
            var n = 0;
            foreach (var room in grid.Rooms)
            {
                if (!IsParking(room)) continue;
                if (room.IsBroken) continue;
                if (!IsParkingFloorAccessible(grid, room.Origin.y)) continue;
                n += Mathf.Max(0, room.Type.maxOccupants);
            }

            return n;
        }

        public static int ClaimedStalls(IReadOnlyList<Agent> agents)
        {
            if (agents == null) return 0;
            var n = 0;
            foreach (var agent in agents)
            {
                if (agent?.ParkingRoom != null)
                    n++;
            }

            return n;
        }

        public static int FreeStalls(TowerGrid grid, IReadOnlyList<Agent> agents) =>
            Mathf.Max(0, TotalStalls(grid) - ClaimedStalls(agents));

        public static bool TryClaim(Agent agent, TowerGrid grid, IReadOnlyList<Agent> agents)
        {
            if (agent == null || grid == null) return false;
            if (agent.ParkingRoom != null) return true;
            if (!HasOperationalValet(grid)) return false;

            foreach (var room in grid.Rooms)
            {
                if (!IsParking(room) || room.IsBroken) continue;
                if (!IsParkingFloorAccessible(grid, room.Origin.y)) continue;
                var cap = Mathf.Max(0, room.Type.maxOccupants);
                var used = 0;
                if (agents != null)
                {
                    foreach (var other in agents)
                    {
                        if (other?.ParkingRoom != null && ReferenceEquals(other.ParkingRoom, room))
                            used++;
                    }
                }

                if (used >= cap) continue;

                agent.ParkingRoom = room;
                agent.ParkingSlot = used;
                agent.ArrivedViaParking = true;
                return true;
            }

            return false;
        }

        public static void Release(Agent agent)
        {
            if (agent == null) return;
            agent.ParkingRoom = null;
            agent.ParkingSlot = 0;
            agent.ArrivedViaParking = false;
        }

        public static Vector2Int StallCell(RoomInstance parking, int slot)
        {
            if (parking == null) return Vector2Int.zero;
            var x = parking.Origin.x + Mathf.Min(Mathf.Max(0, slot), Mathf.Max(0, parking.Size.x - 1));
            return new Vector2Int(x, parking.Origin.y);
        }

        /// <summary>Preferred exit cell for agents who arrived via parking (no stall claim required).</summary>
        public static bool TryParkingExitCell(TowerGrid grid, out Vector2Int cell)
        {
            cell = default;
            if (grid == null) return false;
            foreach (var room in grid.Rooms)
            {
                if (!IsParking(room) || room.IsBroken) continue;
                if (!IsParkingFloorAccessible(grid, room.Origin.y)) continue;
                cell = StallCell(room, 0);
                return true;
            }

            return false;
        }
    }
}
