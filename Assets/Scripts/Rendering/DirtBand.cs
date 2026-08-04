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
        public static readonly Color Color = new(0.45f, 0.32f, 0.22f, 1f);

        public static bool Contains(Vector2Int cell) =>
            cell.y < TowerGrid.LobbyFloor &&
            cell.y >= -Depth &&
            cell.x >= MinX &&
            cell.x <= MaxX;

        /// <summary>
        /// True when a vacated basement cell should show dirt again (no room remains on the grid).
        /// </summary>
        public static bool ShouldRestore(Vector2Int cell, TowerGrid grid) =>
            Contains(cell) && (grid == null || !grid.TryGetRoomAt(cell, out _));
    }
}
