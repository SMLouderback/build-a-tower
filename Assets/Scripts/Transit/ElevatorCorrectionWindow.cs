namespace BuildATower
{
    /// <summary>
    /// Tracks a brief post-extension undo window so players can shrink back toward
    /// the pre-extension bounds without entering maintenance mode.
    /// </summary>
    public sealed class ElevatorCorrectionWindow
    {
        public const float DurationSeconds = 10f;

        public int ShaftInstanceId { get; }
        public int PreviousMinY { get; }
        public int PreviousMaxY { get; }
        public float DeadlineRealtime { get; private set; }

        public ElevatorCorrectionWindow(
            int shaftInstanceId,
            int previousMinY,
            int previousMaxY,
            float nowRealtime)
        {
            ShaftInstanceId = shaftInstanceId;
            PreviousMinY = previousMinY;
            PreviousMaxY = previousMaxY;
            DeadlineRealtime = nowRealtime + DurationSeconds;
        }

        public bool IsActive(float nowRealtime) => nowRealtime < DeadlineRealtime;

        public float SecondsRemaining(float nowRealtime) =>
            System.Math.Max(0f, DeadlineRealtime - nowRealtime);

        /// <summary>
        /// After an extension, allowed shrinks keep previousBounds ⊆ newBounds ⊆ currentBounds.
        /// </summary>
        public bool AllowsResize(
            int currentMinY,
            int currentMaxY,
            int newMinY,
            int newMaxY,
            float nowRealtime)
        {
            if (!IsActive(nowRealtime)) return false;
            if (newMinY == currentMinY && newMaxY == currentMaxY) return false;
            if (newMinY < currentMinY || newMaxY > currentMaxY) return false;
            if (newMinY > PreviousMinY || newMaxY < PreviousMaxY) return false;
            return true;
        }

        public void RefreshDeadline(float nowRealtime) =>
            DeadlineRealtime = nowRealtime + DurationSeconds;
    }
}
