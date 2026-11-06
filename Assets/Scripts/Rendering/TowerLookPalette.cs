using System;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Cutaway-inspired procedural room colors by category, luxury, and id hints.
    /// Paint-time override — does not require rewriting every RoomTypeSO asset.
    /// </summary>
    public static class TowerLookPalette
    {
        public static readonly Color Lobby = new(0.88f, 0.82f, 0.70f, 1f);
        public static readonly Color Scaffold = new(0.70f, 0.58f, 0.32f, 1f);
        /// <summary>Structure filler behind dollhouse overlays (frames / floor seams).</summary>
        public static readonly Color BuildingShell = new(0.42f, 0.42f, 0.45f, 1f);

        public static Color ForRoom(RoomTypeSO type)
        {
            if (type == null) return Color.magenta;

            if (type.isLobby)
                return Lobby;

            if (type.isScaffolding)
                return Scaffold;

            if (type.isElevatorShaft)
                return new Color(0.22f, 0.25f, 0.32f, 1f);

            if (type.isStairs)
                return new Color(0.45f, 0.45f, 0.48f, 1f);

            if (type.isParkingRamp ||
                ParkingStalls.IsParking(type) ||
                ParkingStalls.IsValet(type) ||
                ParkingStalls.IsRamp(type) ||
                type.category == RoomCategory.Parking)
                return new Color(0.28f, 0.28f, 0.30f, 1f);

            var id = type.id ?? string.Empty;

            if (IdContains(id, "security"))
                return new Color(0.48f, 0.55f, 0.62f, 1f);

            if (IdContains(id, "maintenance"))
                return new Color(0.72f, 0.58f, 0.38f, 1f);

            if (IdContains(id, "housekeeping"))
                return new Color(0.62f, 0.58f, 0.48f, 1f);

            if (IdContains(id, "conference"))
                return new Color(0.55f, 0.50f, 0.62f, 1f);

            if (IdContains(id, "event"))
                return new Color(0.58f, 0.42f, 0.55f, 1f);

            return type.category switch
            {
                RoomCategory.Office => OfficeColor(type.luxuryBand),
                RoomCategory.Hotel => HotelColor(type.luxuryBand),
                RoomCategory.Condo => CondoColor(type.luxuryBand),
                RoomCategory.Commercial => CommercialColor(type),
                RoomCategory.Structure => type.placeholderColor,
                RoomCategory.Transit => type.placeholderColor,
                RoomCategory.Service => type.placeholderColor,
                _ => type.placeholderColor
            };
        }

        static Color OfficeColor(LuxuryBand band) => band switch
        {
            LuxuryBand.Upper => new Color(0.30f, 0.48f, 0.72f, 1f),
            LuxuryBand.Mid => new Color(0.38f, 0.55f, 0.78f, 1f),
            _ => new Color(0.45f, 0.62f, 0.82f, 1f)
        };

        static Color HotelColor(LuxuryBand band) => band switch
        {
            LuxuryBand.Upper => new Color(0.52f, 0.35f, 0.68f, 1f),
            LuxuryBand.Mid => new Color(0.62f, 0.48f, 0.75f, 1f),
            _ => new Color(0.72f, 0.62f, 0.82f, 1f)
        };

        static Color CondoColor(LuxuryBand band) => band switch
        {
            LuxuryBand.Upper => new Color(0.42f, 0.62f, 0.55f, 1f),
            LuxuryBand.Mid => new Color(0.48f, 0.68f, 0.58f, 1f),
            _ => new Color(0.55f, 0.72f, 0.62f, 1f)
        };

        static Color CommercialColor(RoomTypeSO type)
        {
            var dining = type.ResolvedBuildSubgroup() == BuildSubgroup.Food ||
                         IdContains(type.id, "food") ||
                         IdContains(type.id, "restaurant") ||
                         IdContains(type.id, "dining");
            return dining
                ? new Color(0.88f, 0.42f, 0.28f, 1f)
                : new Color(0.82f, 0.55f, 0.35f, 1f);
        }

        static bool IdContains(string id, string token) =>
            !string.IsNullOrEmpty(id) &&
            id.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
