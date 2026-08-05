namespace BuildATower
{
    /// <summary>
    /// Cross-scene run settings. Survives MainMenu → TowerSandbox via static state.
    /// </summary>
    public static class GameSession
    {
        static bool _hasDifficulty;
        static GameDifficulty _difficulty;

        public static bool HasDifficulty => _hasDifficulty;

        public static GameDifficulty Difficulty
        {
            get
            {
                EnsureDefault();
                return _difficulty;
            }
            set
            {
                _difficulty = value;
                _hasDifficulty = true;
            }
        }

        public static bool IsSandbox => Difficulty == GameDifficulty.Sandbox;

        public static void EnsureDefault()
        {
            if (_hasDifficulty) return;
            _difficulty = GameDifficulty.Normal;
            _hasDifficulty = true;
        }

        public static void StartNewGame(GameDifficulty difficulty)
        {
            _difficulty = difficulty;
            _hasDifficulty = true;
        }

        public static void ResetForTests()
        {
            _hasDifficulty = false;
            _difficulty = GameDifficulty.Normal;
        }
    }
}
