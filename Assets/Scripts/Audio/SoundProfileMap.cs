namespace BuildATower
{
    public static class SoundProfileMap
    {
        public static SoundProfile ForRoom(RoomTypeSO type)
        {
            if (type == null) return SoundProfile.None;
            if (type.isLobby || type.isSkyLobby) return SoundProfile.Lobby;
            if (type.isStairs) return SoundProfile.Stairs;
            if (type.isElevatorShaft) return SoundProfile.Elevator;
            if (type.isParkingRamp ||
                type.id == ParkingStalls.ParkingId ||
                type.id == ParkingStalls.ValetId ||
                type.id == ParkingStalls.RampId ||
                type.category == RoomCategory.Parking)
                return SoundProfile.Parking;
            if (type.id == "service_conference") return SoundProfile.Conference;
            if (type.id == "service_event_hall") return SoundProfile.Event;
            return type.ResolvedBuildFamily() switch
            {
                BuildFamily.Office => SoundProfile.Office,
                BuildFamily.Hotel => SoundProfile.Hotel,
                BuildFamily.Condo => SoundProfile.Condo,
                BuildFamily.Shops => type.ResolvedBuildSubgroup() == BuildSubgroup.Food
                    ? SoundProfile.Restaurant
                    : SoundProfile.Retail,
                BuildFamily.Utility => SoundProfile.Utility,
                _ => SoundProfile.None
            };
        }
    }
}
