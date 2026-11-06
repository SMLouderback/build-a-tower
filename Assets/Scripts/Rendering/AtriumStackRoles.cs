using System.Collections.Generic;

namespace BuildATower
{
    public enum AtriumStackRole
    {
        Solo,
        Roof,
        Mid
    }

    /// <summary>
    /// Classifies atrium rooms for dollhouse skylight vs open-well art based on vertical stacks
    /// (same origin X + width, contiguous in Y).
    /// </summary>
    public static class AtriumStackRoles
    {
        public static AtriumStackRole RoleFor(TowerGrid grid, RoomInstance room)
        {
            if (grid == null || room == null || !IsAtriumRoom(room))
                return AtriumStackRole.Solo;

            var roles = RolesByInstanceId(grid);
            return roles.TryGetValue(room.InstanceId, out var role)
                ? role
                : AtriumStackRole.Solo;
        }

        public static Dictionary<int, AtriumStackRole> RolesByInstanceId(TowerGrid grid)
        {
            var result = new Dictionary<int, AtriumStackRole>();
            if (grid == null)
                return result;

            var byFootprint = new Dictionary<(int originX, int width), List<RoomInstance>>();
            foreach (var room in grid.Rooms)
            {
                if (!IsAtriumRoom(room))
                    continue;

                var key = (room.Origin.x, room.Size.x);
                if (!byFootprint.TryGetValue(key, out var list))
                {
                    list = new List<RoomInstance>();
                    byFootprint[key] = list;
                }

                list.Add(room);
            }

            foreach (var list in byFootprint.Values)
                AssignRolesForFootprint(list, result);

            return result;
        }

        static void AssignRolesForFootprint(List<RoomInstance> rooms, Dictionary<int, AtriumStackRole> result)
        {
            if (rooms == null || rooms.Count == 0)
                return;

            var parent = new int[rooms.Count];
            for (var i = 0; i < parent.Length; i++)
                parent[i] = i;

            for (var i = 0; i < rooms.Count; i++)
            {
                for (var j = i + 1; j < rooms.Count; j++)
                {
                    if (YRangesTouchOrOverlap(rooms[i], rooms[j]))
                        Union(parent, i, j);
                }
            }

            var components = new Dictionary<int, List<RoomInstance>>();
            for (var i = 0; i < rooms.Count; i++)
            {
                var root = Find(parent, i);
                if (!components.TryGetValue(root, out var component))
                {
                    component = new List<RoomInstance>();
                    components[root] = component;
                }

                component.Add(rooms[i]);
            }

            foreach (var component in components.Values)
                AssignRolesForComponent(component, result);
        }

        static void AssignRolesForComponent(List<RoomInstance> component, Dictionary<int, AtriumStackRole> result)
        {
            if (component.Count == 1)
            {
                result[component[0].InstanceId] = AtriumStackRole.Solo;
                return;
            }

            var roof = component[0];
            var roofTop = TopY(roof);
            for (var i = 1; i < component.Count; i++)
            {
                var top = TopY(component[i]);
                if (top > roofTop)
                {
                    roofTop = top;
                    roof = component[i];
                }
            }

            foreach (var room in component)
            {
                result[room.InstanceId] = room.InstanceId == roof.InstanceId
                    ? AtriumStackRole.Roof
                    : AtriumStackRole.Mid;
            }
        }

        static bool IsAtriumRoom(RoomInstance room)
        {
            var type = room?.Type;
            if (type == null)
                return false;
            if (type.isAtrium)
                return true;
            return type.id == "leisure_atrium";
        }

        static int TopY(RoomInstance room) => room.Origin.y + room.Size.y - 1;

        static bool YRangesTouchOrOverlap(RoomInstance a, RoomInstance b)
        {
            var aTop = TopY(a);
            var bTop = TopY(b);
            var aBottom = a.Origin.y;
            var bBottom = b.Origin.y;
            return !(aTop + 1 < bBottom || bTop + 1 < aBottom);
        }

        static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        static void Union(int[] parent, int a, int b)
        {
            var rootA = Find(parent, a);
            var rootB = Find(parent, b);
            if (rootA != rootB)
                parent[rootB] = rootA;
        }
    }
}
