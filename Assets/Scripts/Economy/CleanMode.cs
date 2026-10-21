namespace BuildATower
{
    public enum CleanMode
    {
        Quick,
        Normal,
        Deep,
    }

    public static class CleanModeRules
    {
        public static float TimeMultiplier(CleanMode mode) =>
            mode switch
            {
                CleanMode.Quick => 0.6f,
                CleanMode.Normal => 1.0f,
                CleanMode.Deep => 1.6f,
            };

        public static float IllnessChance(CleanMode mode) =>
            mode switch
            {
                CleanMode.Quick => 0.25f,
                CleanMode.Normal => 0.08f,
                CleanMode.Deep => 0.02f,
            };

        public static bool MarksTenantSick(RoomCategory category) =>
            category == RoomCategory.Hotel
            || category == RoomCategory.Office
            || category == RoomCategory.Condo;

        public static bool RedirtiesInstead(string roomTypeId) =>
            roomTypeId == "service_public_restroom"
            || roomTypeId == "service_office_restroom"
            || roomTypeId == "service_conference"
            || roomTypeId == "service_event_hall";
    }
}
