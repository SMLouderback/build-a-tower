namespace BuildATower
{
    public enum TutorialStep
    {
        Inactive,
        /// <summary>Centered Skip / Continue gate before coaching begins.</summary>
        Intro,
        LobbyTool,
        PlaceLobby,
        PlaceLiving,
        PlaceStairs,
        WaitElevator,
        PlaceElevator,
        ExtendElevator,
        Done
    }

    public enum TutorialHighlightTarget
    {
        None,
        LobbyTool,
        LobbyFloorBand,
        LivingFamily,
        StairsTool,
        ElevatorTool,
        ElevatorShaft
    }
}
