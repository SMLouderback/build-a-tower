namespace BuildATower
{
    public static class FloorLabels
    {
        public static string Format(int floor, TowerGrid grid)
        {
            if (grid != null && grid.TryGetSkyLobbyOnFloor(floor, out _))
                return "SL" + floor;
            if (floor == TowerGrid.LobbyFloor)
                return "L";
            if (floor < 0)
                return "B" + (-floor);
            return floor.ToString();
        }

        public static bool IsActive(ElevatorShaftRuntime shaft, int floor) =>
            shaft != null && shaft.Serves(floor);
    }
}
