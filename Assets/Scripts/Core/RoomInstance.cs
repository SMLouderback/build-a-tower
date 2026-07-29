using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class RoomInstance
    {
        public int InstanceId { get; }
        public RoomTypeSO Type { get; }
        public Vector2Int Origin { get; }
        public Vector2Int Size { get; }
        public int Evaluation { get; set; } = 100;
        public bool CondoSold { get; set; }

        public RoomInstance(int instanceId, RoomTypeSO type, Vector2Int origin, Vector2Int size)
        {
            InstanceId = instanceId;
            Type = type;
            Origin = origin;
            Size = size;
        }

        public IEnumerable<Vector2Int> OccupiedCells()
        {
            for (var dy = 0; dy < Size.y; dy++)
            for (var dx = 0; dx < Size.x; dx++)
                yield return new Vector2Int(Origin.x + dx, Origin.y + dy);
        }
    }
}
