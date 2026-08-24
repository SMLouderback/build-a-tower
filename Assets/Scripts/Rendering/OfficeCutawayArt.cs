using System;
using UnityEngine;

namespace BuildATower
{
    public static class OfficeCutawayArt
    {
        public const int CellPixels = 128;

        public static Func<int> RollArtVariantForTests;

        public static bool IsOffice(RoomTypeSO type) =>
            type != null && type.category == RoomCategory.Office;

        public static string ResourceLeaf(string typeId, int variant) =>
            $"{typeId}_v{ClampArtVariant(variant):D2}";

        public static string ResourcePath(string typeId, int variant) =>
            "Art/Offices/" + ResourceLeaf(typeId, variant);

        public static Vector2Int ExpectedPixelSize(Vector2Int cellSize) =>
            new Vector2Int(cellSize.x * CellPixels, CellPixels);

        public static int ClampArtVariant(int v) => v == 2 ? 2 : 1;

        public static int RollArtVariant()
        {
            if (RollArtVariantForTests != null)
                return ClampArtVariant(RollArtVariantForTests());
            return UnityEngine.Random.Range(0, 2) == 0 ? 1 : 2;
        }

        public static void AssignArtVariantIfUnset(RoomInstance room)
        {
            if (room?.Type == null || room.Type.category != RoomCategory.Office) return;
            if (room.ArtVariant != 0) return;
            room.ArtVariant = RollArtVariant();
        }

        public static void ResetForTests() => RollArtVariantForTests = null;
    }
}
