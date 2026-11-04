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
    }
}
