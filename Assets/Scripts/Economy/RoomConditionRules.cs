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

        public static float CleanMinutes(RoomTypeSO hotelType, float minutesMultiplier = 1f)
        {
            var baseMinutes = hotelType != null && hotelType.requiredStars >= 2
                ? CleanPremiumMinutes
                : CleanBasicMinutes;
            return baseMinutes * minutesMultiplier;
        }

        public static float RepairMinutes(float minutesMultiplier = 1f) =>
            RepairMinutesPerChunk * minutesMultiplier;

        public static int EffectiveRepairChunk(float chunkMultiplier = 1f) =>
            Mathf.Max(1, Mathf.RoundToInt(RepairChunk * chunkMultiplier));

        public static void ApplyMidnightDecay(RoomInstance room)
        {
            if (room == null || !CanDegrade(room.Type)) return;
            room.Condition = Mathf.Max(0, room.Condition - 1);
        }

        /// <summary>
        /// Applies +RepairChunk (scaled by <paramref name="chunkMultiplier"/>) to Condition (cap 100).
        /// No-op if room is null or Broken (Condition &lt; 1).
        /// </summary>
        /// <returns>True if the room was repairable (not null/Broken); false for no-op.</returns>
        public static bool ApplyRepairTick(RoomInstance room, float chunkMultiplier = 1f)
        {
            if (room == null || room.IsBroken || room.Condition < 1)
                return false;
            room.Condition = Mathf.Min(100, room.Condition + EffectiveRepairChunk(chunkMultiplier));
            return true;
        }
    }
}
