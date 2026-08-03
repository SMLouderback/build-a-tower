using System.Collections.Generic;

namespace BuildATower
{
    public sealed class StairCapacity
    {
        public const int DefaultCap = 5;

        readonly Dictionary<int, HashSet<int>> _occupants = new();

        public int Cap { get; }

        public StairCapacity(int cap = DefaultCap)
        {
            Cap = cap;
        }

        public bool TryEnter(int stairsRoomId, int agentId)
        {
            var set = GetOrCreateSet(stairsRoomId);
            if (set.Contains(agentId))
                return true;

            if (set.Count >= Cap)
                return false;

            set.Add(agentId);
            return true;
        }

        public void Leave(int stairsRoomId, int agentId)
        {
            if (!_occupants.TryGetValue(stairsRoomId, out var set))
                return;

            set.Remove(agentId);
            if (set.Count == 0)
                _occupants.Remove(stairsRoomId);
        }

        public int Occupancy(int stairsRoomId)
        {
            return _occupants.TryGetValue(stairsRoomId, out var set) ? set.Count : 0;
        }

        public void Clear()
        {
            _occupants.Clear();
        }

        HashSet<int> GetOrCreateSet(int stairsRoomId)
        {
            if (!_occupants.TryGetValue(stairsRoomId, out var set))
            {
                set = new HashSet<int>();
                _occupants[stairsRoomId] = set;
            }

            return set;
        }
    }
}
