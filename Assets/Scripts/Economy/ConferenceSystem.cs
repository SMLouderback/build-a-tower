using System.Collections.Generic;

namespace BuildATower
{
    public sealed class ConferenceSystem
    {
        public const string ConferenceId = "service_conference";
        public const string EventHallId = "service_event_hall";

        public HashSet<int> BookedHallInstanceIds { get; } = new();

        public bool IsHallBooked(RoomInstance room) =>
            room != null && BookedHallInstanceIds.Contains(room.InstanceId);

        public int ComputeDailyMeetings(
            TowerGrid grid,
            int officeWorkerCount,
            int stars,
            float climateSpendMult)
        {
            if (grid == null)
                return 0;

            var capacities = new List<int>();
            var totalEligibleCapacity = 0;
            foreach (var room in grid.Rooms)
            {
                if (!IsEligibleDailyConference(room))
                    continue;
                var capacity = ResolveCapacity(room);
                if (capacity <= 0)
                    continue;
                capacities.Add(capacity);
                totalEligibleCapacity += capacity;
            }

            if (totalEligibleCapacity <= 0)
                return 0;

            var total = 0;
            foreach (var capacity in capacities)
            {
                total += ConferenceMath.DailyMeetingPayout(
                    officeWorkerCount,
                    capacity,
                    totalEligibleCapacity,
                    stars,
                    climateSpendMult);
            }

            return total;
        }

        bool IsEligibleDailyConference(RoomInstance room)
        {
            // Use ReferenceEquals: Unity's overloaded == treats unconstructed
            // ScriptableObjects (EditMode/test hosts) as fake-null.
            if (room == null || ReferenceEquals(room.Type, null))
                return false;
            if (room.Type.id != ConferenceId)
                return false;
            if (room.IsBroken)
                return false;
            if (IsHallBooked(room))
                return false;
            return true;
        }

        static int ResolveCapacity(RoomInstance room)
        {
            if (ReferenceEquals(room.Type, null))
                return 0;
            if (room.Type.eventCapacity > 0)
                return room.Type.eventCapacity;
            return room.Size.x * room.Size.y * 5;
        }
    }
}
