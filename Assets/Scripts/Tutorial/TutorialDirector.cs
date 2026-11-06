namespace BuildATower
{
    public sealed class TutorialDirector
    {
        TutorialStep _step = TutorialStep.Inactive;
        int _elevatorSpanSnapshot;
        bool _hasElevatorSpanSnapshot;

        public TutorialStep CurrentStep => _step;

        public bool IsActive =>
            _step is not (TutorialStep.Inactive or TutorialStep.Done);

        /// <summary>True while the centered Skip/Continue gate is showing.</summary>
        public bool AwaitingContinue => _step == TutorialStep.Intro;

        /// <summary>True after Continue — compact coach docked near build tools.</summary>
        public bool CoachDocked => IsActive && _step != TutorialStep.Intro;

        public void TryStart()
        {
            if (_step != TutorialStep.Inactive)
                return;
            _step = TutorialStep.Intro;
            _hasElevatorSpanSnapshot = false;
            _elevatorSpanSnapshot = 0;
        }

        public void Continue()
        {
            if (_step != TutorialStep.Intro)
                return;
            _step = TutorialStep.LobbyTool;
            _hasElevatorSpanSnapshot = false;
            _elevatorSpanSnapshot = 0;
        }

        public void Skip()
        {
            _step = TutorialStep.Inactive;
            _hasElevatorSpanSnapshot = false;
            _elevatorSpanSnapshot = 0;
        }

        public void Tick(TowerGrid grid, int currentStars, bool lobbyToolSelected)
        {
            if (_step is TutorialStep.Inactive or TutorialStep.Done or TutorialStep.Intro)
                return;
            if (grid == null)
                return;

            // Soft skip-ahead: keep advancing while the current step's goal is already met.
            var guard = 0;
            while (IsActive && guard++ < 16)
            {
                EnsureElevatorSnapshot(grid);
                if (!IsStepComplete(grid, currentStars, lobbyToolSelected))
                    break;
                Advance();
            }
        }

        public TutorialHighlight GetHighlight()
        {
            return _step switch
            {
                TutorialStep.Intro => new TutorialHighlight(
                    TutorialHighlightTarget.None,
                    "Welcome to Build-A-Tower",
                    "A short tutorial will walk you through your first lobby, rooms, stairs, and elevator. You can skip anytime."),
                TutorialStep.LobbyTool => new TutorialHighlight(
                    TutorialHighlightTarget.LobbyTool,
                    "Select the Lobby tool",
                    "Choose Lobby in the build menu to start your tower."),
                TutorialStep.PlaceLobby => new TutorialHighlight(
                    TutorialHighlightTarget.LobbyFloorBand,
                    "Place your lobby",
                    "Drag a lobby band across the ground floor."),
                TutorialStep.PlaceLiving => new TutorialHighlight(
                    TutorialHighlightTarget.LivingFamily,
                    "Add living space",
                    "Place an Office, Condo, or Hotel room above the lobby."),
                TutorialStep.PlaceStairs => new TutorialHighlight(
                    TutorialHighlightTarget.StairsTool,
                    "Connect with stairs",
                    "Place stairs so tenants can move between floors."),
                TutorialStep.WaitElevator => new TutorialHighlight(
                    TutorialHighlightTarget.ElevatorTool,
                    "Unlock elevators",
                    "Earn 1 star (or place a shaft) to unlock elevators."),
                TutorialStep.PlaceElevator => new TutorialHighlight(
                    TutorialHighlightTarget.ElevatorTool,
                    "Place an elevator",
                    "Build an elevator shaft next to your floors."),
                TutorialStep.ExtendElevator => new TutorialHighlight(
                    TutorialHighlightTarget.ElevatorShaft,
                    "Extend the elevator",
                    "Stretch the shaft so it serves more floors."),
                _ => new TutorialHighlight(TutorialHighlightTarget.None, string.Empty, string.Empty)
            };
        }

        void EnsureElevatorSnapshot(TowerGrid grid)
        {
            if (_step != TutorialStep.PlaceElevator || _hasElevatorSpanSnapshot)
                return;
            _elevatorSpanSnapshot = MaxElevatorSpan(grid);
            _hasElevatorSpanSnapshot = true;
        }

        void Advance()
        {
            _step = _step switch
            {
                TutorialStep.LobbyTool => TutorialStep.PlaceLobby,
                TutorialStep.PlaceLobby => TutorialStep.PlaceLiving,
                TutorialStep.PlaceLiving => TutorialStep.PlaceStairs,
                TutorialStep.PlaceStairs => TutorialStep.WaitElevator,
                TutorialStep.WaitElevator => TutorialStep.PlaceElevator,
                TutorialStep.PlaceElevator => TutorialStep.ExtendElevator,
                TutorialStep.ExtendElevator => TutorialStep.Done,
                _ => _step
            };

            if (_step == TutorialStep.PlaceElevator)
                _hasElevatorSpanSnapshot = false;
        }

        bool IsStepComplete(TowerGrid grid, int currentStars, bool lobbyToolSelected)
        {
            return _step switch
            {
                TutorialStep.LobbyTool => lobbyToolSelected || grid.HasLobby,
                TutorialStep.PlaceLobby => grid.HasLobby,
                TutorialStep.PlaceLiving => HasLivingRoom(grid),
                TutorialStep.PlaceStairs => HasStairs(grid),
                TutorialStep.WaitElevator => currentStars >= 1 || HasElevator(grid),
                TutorialStep.PlaceElevator => HasElevator(grid),
                TutorialStep.ExtendElevator => IsExtendComplete(grid),
                _ => false
            };
        }

        bool IsExtendComplete(TowerGrid grid)
        {
            var span = MaxElevatorSpan(grid);
            if (span >= 2)
                return true;
            return _hasElevatorSpanSnapshot && span > _elevatorSpanSnapshot;
        }

        static bool HasLivingRoom(TowerGrid grid)
        {
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                if (room.Type.category is RoomCategory.Office or RoomCategory.Condo or RoomCategory.Hotel)
                    return true;
            }

            return false;
        }

        static bool HasStairs(TowerGrid grid)
        {
            foreach (var room in grid.Rooms)
            {
                if (room?.Type != null && room.Type.isStairs)
                    return true;
            }

            return false;
        }

        static bool HasElevator(TowerGrid grid)
        {
            foreach (var room in grid.Rooms)
            {
                if (room?.Type != null && room.Type.isElevatorShaft)
                    return true;
            }

            return false;
        }

        static int MaxElevatorSpan(TowerGrid grid)
        {
            var max = 0;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null || !room.Type.isElevatorShaft) continue;
                if (room.Size.y > max)
                    max = room.Size.y;
            }

            return max;
        }
    }
}
