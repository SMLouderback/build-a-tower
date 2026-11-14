using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Determines whether a room's left/right dollhouse faces are exterior (no adjacent room on that edge).
    /// Shell envelope filler is visual-only and never occupies grid cells; scaffolding is not a room neighbor.
    /// </summary>
    public static class RoomExteriorFaces
    {
        public static void Evaluate(
            TowerGrid grid,
            RoomInstance room,
            out bool leftExterior,
            out bool rightExterior)
        {
            leftExterior = false;
            rightExterior = false;
            if (grid == null || room == null)
                return;

            leftExterior = IsExteriorSide(grid, room, left: true);
            rightExterior = IsExteriorSide(grid, room, left: false);
        }

        static bool IsExteriorSide(TowerGrid grid, RoomInstance room, bool left)
        {
            var edgeX = left ? room.Origin.x - 1 : room.Origin.x + room.Size.x;
            var yMin = room.Origin.y;
            var yMax = room.Origin.y + room.Size.y - 1;

            for (var y = yMin; y <= yMax; y++)
            {
                if (!grid.TryGetRoomAt(new Vector2Int(edgeX, y), out var neighbor))
                    continue;
                if (CountsAsAdjacentRoom(neighbor, room))
                    return false;
            }

            return true;
        }

        static bool CountsAsAdjacentRoom(RoomInstance neighbor, RoomInstance subject)
        {
            if (neighbor == null || neighbor.InstanceId == subject.InstanceId)
                return false;
            var type = neighbor.Type;
            if (type == null || type.isScaffolding)
                return false;
            return true;
        }
    }
}
