namespace BuildATower
{
    public sealed class StarSystem
    {
        public const int QuarterDays = 90;
        public const int OneStarPopulation = 10;
        public const float OneStarMaxStress = 40f;
        public const int TwoStarPopulation = 30;
        public const float TwoStarMaxStress = 25f;
        public const int ThreeStarPopulation = 60;
        public const float ThreeStarMaxStress = 20f;
        public const int MaxStars = 3;
        /// <summary>HUD star track length (4–5★ content comes later; unearned slots stay grey).</summary>
        public const int StarSlots = 5;

        const string HousekeepingId = "service_housekeeping";
        const string MaintenanceId = "service_maintenance";

        public int CurrentStars { get; private set; }
        public string LastResult { get; private set; }

        /// <summary>
        /// Grants every consecutive star whose criteria are currently met. Never demotes.
        /// Cascades (e.g. 0→2) so a single eligibility check cannot leave Goals all-✓ while stuck mid-tier.
        /// </summary>
        public bool TryPromote(TowerGrid grid, float averageStress, int population)
        {
            if (CurrentStars >= MaxStars) return false;

            var promoted = false;
            while (CurrentStars < MaxStars &&
                   MeetsCriteria(CurrentStars + 1, grid, averageStress, population))
            {
                CurrentStars++;
                promoted = true;
                LastResult = $"Earned {CurrentStars}★.";
            }

            return promoted;
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
            // Show one decimal so HUD ✓ matches MeetsCriteria (≤) when avg is e.g. 25.4 vs max 25.
            lines += $"\n  Stress {averageStress:0.#}/{allowedStress:0} max {Mark(averageStress <= allowedStress)}";
            lines += $"\n  Lobby {Mark(grid != null && grid.HasLobby)}";

            if (target >= 2)
                lines += $"\n  Elevator {Mark(HasElevator(grid))}";

            if (target >= 3)
            {
                // Security is a 3★ unlock — do not require it to earn 3★ (chicken-and-egg).
                lines += $"\n  Housekeeping {Mark(HasOperationalFacility(grid, HousekeepingId))}";
                lines += $"\n  Maintenance {Mark(HasOperationalFacility(grid, MaintenanceId))}";
            }

            return lines;
        }

        public static int RequiredPopulation(int stars) =>
            stars >= 3 ? ThreeStarPopulation :
            stars >= 2 ? TwoStarPopulation :
            OneStarPopulation;

        public static float AllowedStress(int stars) =>
            stars >= 3 ? ThreeStarMaxStress :
            stars >= 2 ? TwoStarMaxStress :
            OneStarMaxStress;

        static string Mark(bool met) => met ? "✓" : "✗";

        static bool MeetsCriteria(int stars, TowerGrid grid, float averageStress, int population)
        {
            if (grid == null || !grid.HasLobby) return false;
            if (population < RequiredPopulation(stars) || averageStress > AllowedStress(stars))
                return false;

            if (stars >= 2 && !HasElevator(grid)) return false;
            if (stars >= 3 && !HasOperationalServiceFacilities(grid)) return false;
            return true;
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

        static bool HasOperationalServiceFacilities(TowerGrid grid) =>
            HasOperationalFacility(grid, HousekeepingId) &&
            HasOperationalFacility(grid, MaintenanceId);

        static bool HasOperationalFacility(TowerGrid grid, string typeId)
        {
            if (grid == null || string.IsNullOrEmpty(typeId)) return false;

            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                if (room.Type.id != typeId) continue;
                if (IsBroken(room)) continue;
                return true;
            }

            return false;
        }

        static bool IsBroken(RoomInstance room)
        {
            if (room == null) return false;
            return room.IsBroken;
        }
    }
}
