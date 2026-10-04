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
        public const string MailId = "service_mail";
        public const string RecyclingId = "service_recycling";
        public const string LoadingDockId = "service_loading_dock";
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

        public static bool RequiresVehicleAccess(RoomTypeSO type) =>
            type != null && (type.id == RecyclingId || type.id == LoadingDockId);

        public static bool IsVehicleService(RoomTypeSO type) =>
            type != null && (type.id == RecyclingId || type.id == LoadingDockId);

        public static bool WouldBeVehicleAccessible(TowerGrid grid, RoomTypeSO type, Vector2Int origin)
        {
            if (!RequiresVehicleAccess(type)) return true;
            if (grid == null || type == null) return false;
            var hypothetical = new RoomInstance(0, type, origin, type.size);
            return IsVehicleAccessible(grid, hypothetical, includeExtra: hypothetical);
        }

        public static bool IsVehicleAccessible(TowerGrid grid, RoomInstance room) =>
            IsVehicleAccessible(grid, room, includeExtra: null);

        static bool IsVehicleAccessible(TowerGrid grid, RoomInstance room, RoomInstance includeExtra)
        {
            if (room?.Type == null || !RequiresVehicleAccess(room.Type)) return true;
            if (room.IsBroken) return false;
            if (room.Origin.y >= TowerGrid.LobbyFloor) return false;
            if (room.Origin.y == -1) return true;
            if (grid == null) return false;

            var vehicleRooms = new List<RoomInstance>();
            var parkingLots = new List<RoomInstance>();
            foreach (var candidate in EnumerateRooms(grid, includeExtra))
            {
                if (candidate.IsBroken) continue;
                if (IsVehicleService(candidate.Type))
                {
                    if (candidate.Origin.y >= TowerGrid.LobbyFloor) continue;
                    vehicleRooms.Add(candidate);
                }
                else if (IsParking(candidate) && candidate.Origin.y < TowerGrid.LobbyFloor)
                    parkingLots.Add(candidate);
            }

            var targetIdx = IndexOfRoom(vehicleRooms, room);
            if (targetIdx < 0) return false;

            var accessibleParking = new List<RoomInstance>();
            foreach (var lot in parkingLots)
            {
                if (IsParkingAccessible(grid, lot))
                    accessibleParking.Add(lot);
            }

            var nodes = new List<RoomInstance>();
            var isParkingNode = new List<bool>();
            foreach (var v in vehicleRooms)
            {
                nodes.Add(v);
                isParkingNode.Add(false);
            }

            foreach (var lot in accessibleParking)
            {
                nodes.Add(lot);
                isParkingNode.Add(true);
            }

            var seeds = new HashSet<int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (isParkingNode[i])
                {
                    if (node.Origin.y == -1 || TouchesLobbyReachingRamp(grid, node))
                        seeds.Add(i);
                    continue;
                }

                if (node.Origin.y == -1 || TouchesLobbyReachingRamp(grid, node))
                    seeds.Add(i);
            }

            if (seeds.Contains(targetIdx)) return true;

            var reachable = new HashSet<int>(seeds);
            var queue = new Queue<int>(seeds);
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                if (i == targetIdx) return true;
                for (var j = 0; j < nodes.Count; j++)
                {
                    if (reachable.Contains(j)) continue;
                    if (!VehicleAccessNodesLink(nodes[i], isParkingNode[i], nodes[j], isParkingNode[j]))
                        continue;
                    if (!RoomsHorizontallyAdjacent(nodes[i], nodes[j])) continue;
                    reachable.Add(j);
                    queue.Enqueue(j);
                }
            }

            return reachable.Contains(targetIdx);
        }

        static bool VehicleAccessNodesLink(
            RoomInstance a,
            bool aIsParking,
            RoomInstance b,
            bool bIsParking)
        {
            if (a.Origin.y != b.Origin.y) return false;
            if (aIsParking && bIsParking) return true;
            if (aIsParking || bIsParking)
            {
                var vehicle = aIsParking ? b : a;
                return vehicle.Type.id == LoadingDockId;
            }

            return a.Type.id == b.Type.id;
        }

        static int IndexOfRoom(List<RoomInstance> rooms, RoomInstance room)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                if (ReferenceEquals(rooms[i], room)) return i;
                if (rooms[i].Type.id == room.Type.id &&
                    rooms[i].Origin == room.Origin &&
                    rooms[i].Size == room.Size)
                    return i;
            }

            return -1;
        }

        static IEnumerable<RoomInstance> EnumerateRooms(TowerGrid grid, RoomInstance includeExtra)
        {
            foreach (var room in grid.Rooms)
                yield return room;
            if (includeExtra == null) yield break;
            var seen = false;
            foreach (var room in grid.Rooms)
            {
                if (ReferenceEquals(room, includeExtra))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
                yield return includeExtra;
        }

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
        /// True when a ramp chain from this floor reaches B1 (−1) or Lobby (0).
        /// B1 is always a valid egress floor.
        /// </summary>
        public static bool IsParkingFloorAccessible(TowerGrid grid, int floor)
        {
            if (floor >= TowerGrid.LobbyFloor) return false;
            if (floor == -1) return true;
            if (grid == null) return false;

            var reachable = new HashSet<int> { floor };
            ExpandRampFloors(grid, reachable);
            return reachable.Contains(-1) || reachable.Contains(TowerGrid.LobbyFloor);
        }

        /// <summary>
        /// A parking lot counts when it is on B1, edge-touches a lobby-reaching ramp,
        /// or is linked through edge-adjacent parking lots to either of those.
        /// </summary>
        public static bool IsParkingAccessible(TowerGrid grid, RoomInstance parking)
        {
            if (!IsParking(parking) || parking.IsBroken) return false;
            if (grid == null) return false;
            if (parking.Origin.y >= TowerGrid.LobbyFloor) return false;

            var lots = new List<RoomInstance>();
            foreach (var room in grid.Rooms)
            {
                if (!IsParking(room) || room.IsBroken) continue;
                if (room.Origin.y >= TowerGrid.LobbyFloor) continue;
                lots.Add(room);
            }

            if (lots.Count == 0) return false;

            var seeds = new HashSet<int>();
            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot.Origin.y == -1 || TouchesLobbyReachingRamp(grid, lot))
                    seeds.Add(i);
            }

            if (seeds.Count == 0) return false;

            var adj = BuildParkingAdjacency(lots);
            var reachable = new HashSet<int>(seeds);
            var queue = new Queue<int>(seeds);
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                if (!adj.TryGetValue(i, out var neighbors)) continue;
                foreach (var j in neighbors)
                {
                    if (!reachable.Add(j)) continue;
                    queue.Enqueue(j);
                }
            }

            for (var i = 0; i < lots.Count; i++)
            {
                if (ReferenceEquals(lots[i], parking))
                    return reachable.Contains(i);
            }

            return false;
        }

        public static int TotalStalls(TowerGrid grid)
        {
            if (grid == null) return 0;
            var n = 0;
            foreach (var room in grid.Rooms)
            {
                if (!IsParkingAccessible(grid, room)) continue;
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
                if (!IsParkingAccessible(grid, room)) continue;
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
                if (!IsParkingAccessible(grid, room)) continue;
                cell = StallCell(room, 0);
                return true;
            }

            return false;
        }

        static void ExpandRampFloors(TowerGrid grid, HashSet<int> reachable)
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var room in grid.Rooms)
                {
                    if (!IsRamp(room) || room.IsBroken) continue;
                    var lo = room.Origin.y;
                    var hi = room.Origin.y + room.Size.y - 1;
                    if (!RampTouchesReachable(grid, room, lo, hi, reachable)) continue;

                    for (var y = lo; y <= hi; y++)
                    {
                        if (reachable.Add(y))
                            changed = true;
                    }
                }
            }
        }

        /// <summary>
        /// A ramp joins the flood when it overlaps a reachable floor, or when an
        /// exact-X vertically abutting ramp (3×1 stack) already reaches those floors.
        /// </summary>
        static bool RampTouchesReachable(
            TowerGrid grid, RoomInstance ramp, int lo, int hi, HashSet<int> reachable)
        {
            for (var y = lo; y <= hi; y++)
            {
                if (reachable.Contains(y))
                    return true;
            }

            foreach (var other in grid.Rooms)
            {
                if (!IsRamp(other) || other.IsBroken) continue;
                if (ReferenceEquals(other, ramp)) continue;
                if (other.Origin.x != ramp.Origin.x || other.Size.x != ramp.Size.x) continue;

                var oLo = other.Origin.y;
                var oHi = other.Origin.y + other.Size.y - 1;
                // Exact-X stack: floors abut (no gap), same column span.
                if (oHi + 1 != lo && hi + 1 != oLo) continue;

                for (var y = oLo; y <= oHi; y++)
                {
                    if (reachable.Contains(y))
                        return true;
                }
            }

            return false;
        }

        static bool TouchesLobbyReachingRamp(TowerGrid grid, RoomInstance parking)
        {
            foreach (var room in grid.Rooms)
            {
                if (!IsRamp(room) || room.IsBroken) continue;
                if (!RoomsEdgeAdjacent(parking, room)) continue;

                var floors = new HashSet<int>();
                var lo = room.Origin.y;
                var hi = room.Origin.y + room.Size.y - 1;
                for (var y = lo; y <= hi; y++)
                    floors.Add(y);
                ExpandRampFloors(grid, floors);
                if (floors.Contains(-1) || floors.Contains(TowerGrid.LobbyFloor))
                    return true;
            }

            return false;
        }

        static Dictionary<int, List<int>> BuildParkingAdjacency(List<RoomInstance> lots)
        {
            var adj = new Dictionary<int, List<int>>();
            for (var i = 0; i < lots.Count; i++)
                adj[i] = new List<int>();

            for (var i = 0; i < lots.Count; i++)
            {
                for (var j = i + 1; j < lots.Count; j++)
                {
                    // Same-floor bridges only — deeper floors still need their own ramp link.
                    if (!RoomsHorizontallyAdjacent(lots[i], lots[j])) continue;
                    adj[i].Add(j);
                    adj[j].Add(i);
                }
            }

            return adj;
        }

        /// <summary>True when any occupied cell of a is 4-adjacent to any occupied cell of b.</summary>
        static bool RoomsEdgeAdjacent(RoomInstance a, RoomInstance b)
        {
            if (a == null || b == null) return false;
            foreach (var ca in a.OccupiedCells())
            {
                foreach (var cb in b.OccupiedCells())
                {
                    var dx = Mathf.Abs(ca.x - cb.x);
                    var dy = Mathf.Abs(ca.y - cb.y);
                    if (dx + dy == 1) return true;
                }
            }

            return false;
        }

        /// <summary>True when lots share an edge on the same floor (gap-free garage run).</summary>
        static bool RoomsHorizontallyAdjacent(RoomInstance a, RoomInstance b)
        {
            if (a == null || b == null) return false;
            foreach (var ca in a.OccupiedCells())
            {
                foreach (var cb in b.OccupiedCells())
                {
                    if (ca.y != cb.y) continue;
                    if (Mathf.Abs(ca.x - cb.x) == 1) return true;
                }
            }

            return false;
        }
    }
}
