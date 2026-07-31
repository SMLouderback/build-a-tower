using UnityEngine;

namespace BuildATower
{
    public static class RoomConditionRules
    {
        public const int PauseBelow = 40;
        public const int StressBelow = 70;
        public const int RepairChunk = 10;
        public const float RepairMinutesPerChunk = 60f;
        public const float CleanBasicMinutes = 15f;
        public const float CleanPremiumMinutes = 30f;

        public static bool CanDegrade(RoomTypeSO t) =>
            t != null && !t.isLobby && !t.isElevatorShaft && !t.isStairs;

        public static bool IncomePaused(RoomInstance room) =>
            room != null && room.Condition < PauseBelow;

        public static float CleanMinutes(RoomTypeSO hotelType)
        {
            if (hotelType != null && hotelType.requiredStars >= 2)
                return CleanPremiumMinutes;
            return CleanBasicMinutes;
        }

        public static void ApplyMidnightDecay(RoomInstance room)
        {
            if (room == null || !CanDegrade(room.Type)) return;
            room.Condition = Mathf.Max(0, room.Condition - 1);
        }

        public static void ApplyRepairTick(RoomInstance room)
        {
            if (room == null) return;
            room.Condition = Mathf.Min(100, room.Condition + RepairChunk);
        }
    }
}
