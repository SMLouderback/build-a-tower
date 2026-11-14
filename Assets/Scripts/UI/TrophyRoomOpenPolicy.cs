namespace BuildATower
{
    public static class TrophyRoomOpenPolicy
    {
        public static bool ShouldOpenGallery(
            string hitRoomTypeId,
            int hitInstanceId,
            string selectedTypeId,
            int selectedInstanceId,
            bool hasSelection)
        {
            if (hitRoomTypeId != TrophyRoomDisplay.RoomTypeId)
                return false;
            if (!hasSelection || selectedTypeId != TrophyRoomDisplay.RoomTypeId)
                return false;
            return selectedInstanceId == hitInstanceId;
        }
    }
}
