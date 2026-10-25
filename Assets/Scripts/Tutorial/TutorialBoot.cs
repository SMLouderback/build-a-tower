namespace BuildATower
{
    /// <summary>
    /// Pure boot seam for the guided coach: consume the one-shot
    /// <see cref="GameSession.ConsumeTutorialMayStart"/> flag and start a director.
    /// Runtime ownership/tick lives on <see cref="TowerSimulation"/>; HUD reads
    /// <see cref="TowerSimulation.TutorialDirector"/>.
    /// </summary>
    public static class TutorialBoot
    {
        /// <summary>
        /// If the session allows a tutorial start, consumes the flag and calls
        /// <see cref="TutorialDirector.TryStart"/>. Returns whether TryStart ran.
        /// </summary>
        public static bool TryStartFromSession(TutorialDirector director)
        {
            if (director == null)
                return false;
            if (!GameSession.ConsumeTutorialMayStart())
                return false;
            director.TryStart();
            return true;
        }

        /// <summary>
        /// Lobby build-menu tool is selected (same rule as HUD Lobby icon highlight).
        /// </summary>
        public static bool IsLobbyToolSelected(BuildController build) =>
            build != null
            && build.CurrentTool == BuildTool.PlaceRoom
            && build.SelectedRoomType != null
            && build.SelectedRoomType.isLobby;
    }
}
