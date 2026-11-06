using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Visual dirt band under Floor G (matches <see cref="BuildController"/> starter guides).
    /// </summary>
    public static class DirtBand
    {
        public const int MinX = -80;
        public const int MaxX = 100;
        public const int Depth = 10;
        // Richer earth brown so dirt reads against sky / rooms.
        public static readonly Color Color = new(0.55f, 0.36f, 0.18f, 1f);

        public static bool Contains(Vector2Int cell) =>
            cell.y < TowerGrid.LobbyFloor &&
            cell.y >= -Depth &&
            cell.x >= MinX &&
            cell.x <= MaxX;

        public static bool IsCrownRow(int cellY) => cellY == -1;

        public static bool IsFillRow(int cellY) => cellY <= -2 && cellY >= -Depth;

        /// <summary>
        /// Resource leaf name for parcel dirt paint (crown under Floor G; fill below).
        /// </summary>
        public static string DirtTileResource(int cellY, int cellX) =>
            IsCrownRow(cellY) ? "dirt_crown" :
            (HashFillVariant(cellX, cellY) == 0 ? "dirt_fill" : "dirt_fill");

        static int HashFillVariant(int cellX, int cellY) =>
            (cellX * 73856093) ^ (cellY * 19349663);

        /// <summary>
        /// True when a vacated basement cell should show dirt again (no room remains on the grid).
        /// </summary>
        public static bool ShouldRestore(Vector2Int cell, TowerGrid grid) =>
            Contains(cell) && (grid == null || !grid.TryGetRoomAt(cell, out _));
    }
}
