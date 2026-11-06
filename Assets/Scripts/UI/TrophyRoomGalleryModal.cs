namespace BuildATower
{
    public sealed class TrophyRoomGalleryModal
    {
        public bool IsOpen { get; private set; }
        public bool BlocksWorld => IsOpen;

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public bool HandleEsc()
        {
            if (!IsOpen)
                return false;
            Close();
            return true;
        }

        /// <summary>Mirrors HUD <c>BlocksWorldInput</c> so EditMode can lock the gallery clause.</summary>
        public static bool ComposeBlocksWorld(
            bool escPause,
            bool restoreNotice,
            bool rewardTutorial,
            bool gallery,
            bool celebrationActive) =>
            escPause || restoreNotice || rewardTutorial || gallery || celebrationActive;
    }
}
