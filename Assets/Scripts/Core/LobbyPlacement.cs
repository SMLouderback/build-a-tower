namespace BuildATower
{
    /// <summary>
    /// Floor rules for the merged Lobby tool: ground lobby on G, sky lobby above.
    /// </summary>
    public static class LobbyPlacement
    {
        /// <summary>True when the merged Lobby tool should place/extend a sky lobby on this floor.</summary>
        public static bool UsesSkyLobby(int floor) =>
            floor > TowerGrid.LobbyFloor;

        /// <summary>Picks ground vs sky room type for the merged Lobby tool.</summary>
        public static RoomTypeSO ResolveType(int floor, RoomTypeSO groundLobby, RoomTypeSO skyLobby) =>
            UsesSkyLobby(floor) ? skyLobby : groundLobby;
    }
}
