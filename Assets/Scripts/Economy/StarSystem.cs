namespace BuildATower
{
    public sealed class StarSystem
    {
        public const int QuarterDays = 90;
        public const int OneStarPopulation = 10;
        public const float OneStarMaxStress = 40f;
        public const int TwoStarPopulation = 30;
        public const float TwoStarMaxStress = 25f;
        public const int MaxStars = 2;

        public int CurrentStars { get; private set; }
        public string LastResult { get; private set; }

        public void Evaluate(TowerGrid grid, float averageStress, int population)
        {
            if (CurrentStars > 0 && !MeetsCriteria(CurrentStars, grid, averageStress, population))
            {
                CurrentStars--;
                LastResult = "Star tier demoted.";
            }

            if (CurrentStars < MaxStars &&
                MeetsCriteria(CurrentStars + 1, grid, averageStress, population))
            {
                CurrentStars++;
                LastResult = "Star tier promoted.";
            }
            else if (LastResult == null)
            {
                LastResult = "Star tier unchanged.";
            }
        }

        public bool CanBuild(RoomTypeSO type) =>
            type == null || CurrentStars >= type.requiredStars;

        static bool MeetsCriteria(int stars, TowerGrid grid, float averageStress, int population)
        {
            if (grid == null || !grid.HasLobby) return false;

            if (stars == 1)
                return population >= OneStarPopulation && averageStress <= OneStarMaxStress;

            if (stars == 2)
            {
                if (population < TwoStarPopulation || averageStress > TwoStarMaxStress)
                    return false;

                foreach (var room in grid.Rooms)
                {
                    if (room?.Type != null && room.Type.isElevatorShaft)
                        return true;
                }
            }

            return false;
        }
    }
}
