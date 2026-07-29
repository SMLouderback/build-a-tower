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

        /// <summary>
        /// Grants the next star as soon as its criteria are met. Never demotes.
        /// </summary>
        public bool TryPromote(TowerGrid grid, float averageStress, int population)
        {
            if (CurrentStars >= MaxStars) return false;
            if (!MeetsCriteria(CurrentStars + 1, grid, averageStress, population)) return false;

            CurrentStars++;
            LastResult = $"Earned {CurrentStars}★.";
            return true;
        }

        /// <summary>
        /// Quarterly review. This is the only path that can take a star away.
        /// </summary>
        public void EvaluateQuarterly(TowerGrid grid, float averageStress, int population)
        {
            if (CurrentStars > 0 && !MeetsCriteria(CurrentStars, grid, averageStress, population))
            {
                CurrentStars--;
                LastResult = $"Quarterly review: demoted to {CurrentStars}★.";
                return;
            }

            if (TryPromote(grid, averageStress, population))
                return;

            LastResult = $"Quarterly review: kept {CurrentStars}★.";
        }

        public bool CanBuild(RoomTypeSO type) =>
            type == null || CurrentStars >= type.requiredStars;

        public void ForceStars(int stars)
        {
            CurrentStars = System.Math.Clamp(stars, 0, MaxStars);
        }

        /// <summary>
        /// Multi-line requirement summary for the next star tier, newline separated.
        /// </summary>
        public string FormatNextStarGoal(TowerGrid grid, float averageStress, int population)
        {
            if (CurrentStars >= MaxStars)
                return $"Next ★: max tier ({MaxStars}★) reached";

            var target = CurrentStars + 1;
            var neededPopulation = RequiredPopulation(target);
            var allowedStress = AllowedStress(target);

            var lines = $"Next ★ ({target}★) needs:";
            lines += $"\n  Pop {population}/{neededPopulation} {Mark(population >= neededPopulation)}";
            lines += $"\n  Stress {averageStress:0}/{allowedStress:0} max {Mark(averageStress <= allowedStress)}";
            lines += $"\n  Lobby {Mark(grid != null && grid.HasLobby)}";

            if (target >= 2)
                lines += $"\n  Elevator {Mark(HasElevator(grid))}";

            return lines;
        }

        public static int RequiredPopulation(int stars) =>
            stars >= 2 ? TwoStarPopulation : OneStarPopulation;

        public static float AllowedStress(int stars) =>
            stars >= 2 ? TwoStarMaxStress : OneStarMaxStress;

        static string Mark(bool met) => met ? "✓" : "✗";

        static bool MeetsCriteria(int stars, TowerGrid grid, float averageStress, int population)
        {
            if (grid == null || !grid.HasLobby) return false;
            if (population < RequiredPopulation(stars) || averageStress > AllowedStress(stars))
                return false;

            return stars < 2 || HasElevator(grid);
        }

        static bool HasElevator(TowerGrid grid)
        {
            if (grid == null) return false;

            foreach (var room in grid.Rooms)
            {
                if (room?.Type != null && room.Type.isElevatorShaft)
                    return true;
            }

            return false;
        }
    }
}
