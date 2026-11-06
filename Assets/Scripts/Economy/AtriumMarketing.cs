namespace BuildATower
{
    public static class AtriumMarketing
    {
        public const float HotelFillBonus = 0.05f;
        public const float CondoDemandBonus = 0.05f;
        public const float StreetSpawnMultiplier = 1.15f;

        public static bool HasAtrium(TowerGrid grid)
        {
            if (grid == null) return false;

            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null || room.IsBroken) continue;
                if (room.Type.isAtrium) return true;
                if (room.Type.id == "leisure_atrium") return true;
            }

            return false;
        }

        public static float HotelFillBonusFor(TowerGrid grid) =>
            HasAtrium(grid) ? HotelFillBonus : 0f;

        public static float CondoDemandBonusFor(TowerGrid grid) =>
            HasAtrium(grid) ? CondoDemandBonus : 0f;

        public static float StreetSpawnMultiplierFor(TowerGrid grid) =>
            HasAtrium(grid) ? StreetSpawnMultiplier : 1f;
    }
}
