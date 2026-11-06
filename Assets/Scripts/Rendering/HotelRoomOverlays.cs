using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Dirty / broken tile tints for hotel cutaway panoramas.
    /// Caution-tape footprint overlay reuses <see cref="OfficeCondemnedOverlay"/>.
    /// </summary>
    public static class HotelRoomOverlays
    {
        /// <summary>Warm brown matching <see cref="TilemapTowerView.RoomPaintColor"/> dirty lerp intent.</summary>
        public static readonly Color DirtyBrown = new(0.45f, 0.28f, 0.12f, 1f);

        const float DirtyLerp = 0.55f;
        /// <summary>How much dirty brown to blend into the broken grey wash so both cues read.</summary>
        const float DirtyIntoBrokenLerp = 0.4f;

        public static Color DirtyOnlyTint => Color.Lerp(Color.white, DirtyBrown, DirtyLerp);

        public static Color DirtyAndBrokenTint =>
            Color.Lerp(OfficeCondemnedOverlay.BrokenTileTint, DirtyBrown, DirtyIntoBrokenLerp);

        /// <summary>
        /// Tilemap color for hotel cutaway cells: clean white, dirty brown wash,
        /// broken grey wash, or broken+dirty (grey wash with stronger brown).
        /// </summary>
        public static Color CutawayTileTint(bool dirty, bool broken)
        {
            if (broken && dirty)
                return DirtyAndBrokenTint;
            if (broken)
                return OfficeCondemnedOverlay.BrokenTileTint;
            if (dirty)
                return DirtyOnlyTint;
            return Color.white;
        }

        public static Color CutawayTileTint(RoomInstance room) =>
            room == null
                ? Color.white
                : CutawayTileTint(room.Dirty, room.IsBroken);
    }
}
