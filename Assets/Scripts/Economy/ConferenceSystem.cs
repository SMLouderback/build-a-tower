using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public enum MajorEventPhase
    {
        None,
        Upcoming,
        Live,
        Ended
    }

    public sealed class MajorEventState
    {
        public string Name;
        public MajorEventPhase Phase;
        public int StartDayIndex;
        public int EndDayIndex;
        public List<int> BookedHallInstanceIds = new List<int>();
    }

    public sealed class ConferenceSystem
    {
        public const string ConferenceId = "service_conference";
        public const string EventHallId = "service_event_hall";

        public const int EventForeshadowDays = 2;
        public const int EventMinGapDays = 14;
        public const int EventMaxGapDays = 21;
        public const float EventPayMult = 8f;
        public const float EventDailyWhileLiveMult = 0.15f;

        const string DefaultEventName = "TowerCon";

        int _liveLumpPayout;

        public HashSet<int> BookedHallInstanceIds { get; } = new HashSet<int>();

        public MajorEventState Active { get; } = new MajorEventState
        {
            Name = string.Empty,
            Phase = MajorEventPhase.None,
            StartDayIndex = -1,
            EndDayIndex = -1,
            BookedHallInstanceIds = new List<int>()
        };

        public bool IsHallBooked(RoomInstance room) =>
            room != null && BookedHallInstanceIds.Contains(room.InstanceId);

        public static int MajorEventLumpPayout(
            int hotelGuests,
            int stars,
            int bookedCapacity,
            float climateMult)
        {
            return Mathf.RoundToInt(hotelGuests * stars * bookedCapacity * EventPayMult * climateMult);
        }

        public void TickDay(
            int dayIndex,
            TowerGrid grid,
            int hotelGuestCount,
            int stars,
            float climateSpendMult,
            FundsWallet wallet,
            TowerNews news,
            System.Random rng)
        {
            var endedThisTick = false;
            if (Active.Phase == MajorEventPhase.Live && dayIndex > Active.EndDayIndex)
            {
                EndEvent(dayIndex, news);
                endedThisTick = true;
            }

            // Reschedule on a later day so end-of-event news is not mixed with a new roll.
            if (!endedThisTick
                && Active.Phase == MajorEventPhase.None
                && Active.StartDayIndex < 0)
            {
                TrySchedule(dayIndex, grid, rng);
            }

            if (Active.StartDayIndex >= 0
                && Active.Phase == MajorEventPhase.None
                && dayIndex == Active.StartDayIndex - EventForeshadowDays)
            {
                Active.Phase = MajorEventPhase.Upcoming;
                PushMajorNews(
                    news,
                    dayIndex,
                    $"{Active.Name} is upcoming — opens in {EventForeshadowDays} days.",
                    priority: 10);
            }

            if (Active.StartDayIndex >= 0
                && (Active.Phase == MajorEventPhase.Upcoming || Active.Phase == MajorEventPhase.None)
                && dayIndex == Active.StartDayIndex)
            {
                BeginEvent(dayIndex, grid, hotelGuestCount, stars, climateSpendMult, wallet, news);
            }

            if (Active.Phase == MajorEventPhase.Live
                && dayIndex > Active.StartDayIndex
                && dayIndex <= Active.EndDayIndex
                && wallet != null
                && _liveLumpPayout > 0)
            {
                wallet.Add(Mathf.RoundToInt(_liveLumpPayout * EventDailyWhileLiveMult));
            }
        }

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

        void TrySchedule(int dayIndex, TowerGrid grid, System.Random rng)
        {
            if (grid == null || rng == null)
                return;
            if (!HasNonBrokenEventHall(grid))
                return;

            var gap = rng.Next(EventMinGapDays, EventMaxGapDays + 1);
            var duration = rng.Next(1, 4);
            Active.Name = DefaultEventName;
            Active.Phase = MajorEventPhase.None;
            Active.StartDayIndex = dayIndex + gap;
            Active.EndDayIndex = Active.StartDayIndex + duration - 1;
            Active.BookedHallInstanceIds.Clear();
            _liveLumpPayout = 0;
        }

        void BeginEvent(
            int dayIndex,
            TowerGrid grid,
            int hotelGuestCount,
            int stars,
            float climateSpendMult,
            FundsWallet wallet,
            TowerNews news)
        {
            var hall = FindHighestCapacityEventHall(grid);
            Active.BookedHallInstanceIds.Clear();
            BookedHallInstanceIds.Clear();
            _liveLumpPayout = 0;

            if (hall != null)
            {
                Active.BookedHallInstanceIds.Add(hall.InstanceId);
                BookedHallInstanceIds.Add(hall.InstanceId);
                var capacity = ResolveCapacity(hall);
                _liveLumpPayout = MajorEventLumpPayout(
                    hotelGuestCount,
                    stars,
                    capacity,
                    climateSpendMult);
                wallet?.Add(_liveLumpPayout);
            }

            Active.Phase = MajorEventPhase.Live;
            if (string.IsNullOrEmpty(Active.Name))
                Active.Name = DefaultEventName;

            PushMajorNews(
                news,
                dayIndex,
                $"{Active.Name} is live in the Event Hall.",
                priority: 20);
        }

        void EndEvent(int dayIndex, TowerNews news)
        {
            PushMajorNews(
                news,
                dayIndex,
                $"{Active.Name} has ended.",
                priority: 15);

            BookedHallInstanceIds.Clear();
            Active.BookedHallInstanceIds.Clear();
            Active.Phase = MajorEventPhase.None;
            Active.StartDayIndex = -1;
            Active.EndDayIndex = -1;
            Active.Name = string.Empty;
            _liveLumpPayout = 0;
        }

        static void PushMajorNews(TowerNews news, int dayIndex, string text, int priority)
        {
            if (news == null) return;
            news.Push(new TowerNewsItem
            {
                Category = TowerNewsCategory.MajorEvent,
                Priority = priority,
                Text = text,
                CreatedDayIndex = dayIndex,
                ExpireDayIndex = dayIndex + 7
            });
        }

        static bool HasNonBrokenEventHall(TowerGrid grid)
        {
            return FindHighestCapacityEventHall(grid) != null;
        }

        static RoomInstance FindHighestCapacityEventHall(TowerGrid grid)
        {
            if (grid == null) return null;

            RoomInstance best = null;
            var bestCapacity = -1;
            foreach (var room in grid.Rooms)
            {
                if (!IsEligibleEventHall(room))
                    continue;
                var capacity = ResolveCapacity(room);
                if (capacity <= bestCapacity)
                    continue;
                bestCapacity = capacity;
                best = room;
            }

            return best;
        }

        static bool IsEligibleEventHall(RoomInstance room)
        {
            if (room == null || ReferenceEquals(room.Type, null))
                return false;
            if (room.Type.id != EventHallId)
                return false;
            if (room.IsBroken)
                return false;
            return true;
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
