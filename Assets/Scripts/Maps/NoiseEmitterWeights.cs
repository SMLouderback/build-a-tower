using System;

namespace BuildATower
{
    /// <summary>
    /// Relative noise emission weights for heatmap v1. Future amenities hook here.
    /// </summary>
    public static class NoiseEmitterWeights
    {
        public const int NightStartMinute = 22 * 60;
        public const int NightEndMinute = 7 * 60;

        public static bool IsNight(int minuteOfDay)
        {
            var m = ((minuteOfDay % (24 * 60)) + 24 * 60) % (24 * 60);
            return m >= NightStartMinute || m < NightEndMinute;
        }

        public static float Emit(
            RoomTypeSO type,
            bool occupied,
            bool crimeActiveNear,
            bool eventOrConferenceBusy)
        {
            if (type == null) return 0f;

            var baseEmit = Math.Max(type.noiseOutput, CategoryFloor(type));
            if (type.isLobby || type.isScaffolding)
                return 0.05f;

            if (type.isElevatorShaft || type.isStairs)
                return occupied ? 0.25f : 0.08f;

            if (type.category == RoomCategory.Commercial || IsParking(type))
                return Math.Max(baseEmit, 0.65f) * (occupied ? 1f : 0.35f);

            if (type.id != null &&
                (type.id.IndexOf("event", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 type.id.IndexOf("conference", StringComparison.OrdinalIgnoreCase) >= 0))
                return eventOrConferenceBusy ? Math.Max(baseEmit, 0.85f) : Math.Max(baseEmit, 0.2f);

            if (type.id != null && type.id.IndexOf("security", StringComparison.OrdinalIgnoreCase) >= 0)
                return crimeActiveNear ? 0.55f : 0.08f;

            if (type.id != null && type.id.IndexOf("housekeeping", StringComparison.OrdinalIgnoreCase) >= 0)
                return occupied ? 0.35f : 0.1f;

            if (type.id != null && type.id.IndexOf("maintenance", StringComparison.OrdinalIgnoreCase) >= 0)
                return occupied ? 0.5f : 0.2f;

            if (type.category == RoomCategory.Office)
                return occupied ? Math.Max(baseEmit, 0.35f) : 0.05f;

            if (type.category == RoomCategory.Hotel || type.category == RoomCategory.Condo)
                return occupied ? Math.Max(baseEmit, 0.15f) : 0.05f;

            return occupied ? Math.Max(baseEmit, 0.2f) : baseEmit * 0.25f;
        }

        public static float ResidentialBotherFactor(RoomTypeSO type, int minuteOfDay)
        {
            if (type == null) return 1f;
            if (type.category != RoomCategory.Hotel && type.category != RoomCategory.Condo)
                return 1f;
            return IsNight(minuteOfDay) ? 1.45f : 0.75f;
        }

        static float CategoryFloor(RoomTypeSO type)
        {
            if (type.category == RoomCategory.Commercial) return 0.55f;
            if (IsParking(type)) return 0.7f;
            return 0f;
        }

        static bool IsParking(RoomTypeSO type) =>
            type != null && type.id != null &&
            (type.id.IndexOf("parking", StringComparison.OrdinalIgnoreCase) >= 0 ||
             type.id.IndexOf("valet", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
