using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public static class HotelCutawayArt
    {
        public const int CellPixels = 128;

        static readonly Dictionary<string, Vector2Int> ExpectedSizesByKey = new()
        {
            ["hotel_3_base"] = new Vector2Int(384, 128),
            ["hotel_4_mid"] = new Vector2Int(512, 128),
            ["hotel_5_mid"] = new Vector2Int(640, 128),
            ["hotel_6_mid"] = new Vector2Int(768, 128),
            ["hotel_5_upper"] = new Vector2Int(640, 128),
            ["hotel_8_upper"] = new Vector2Int(1024, 128),
        };

        public static bool IsHotel(RoomTypeSO type) =>
            type != null && type.category == RoomCategory.Hotel;

        public static string ResolveArtKey(RoomTypeSO type)
        {
            if (!IsHotel(type)) return string.Empty;

            if (type.id == "hotel_premium" &&
                (type.luxuryBand == LuxuryBand.None || type.luxuryBand == LuxuryBand.Mid))
                return "hotel_4_mid";

            var bandSuffix = BandSuffix(type.luxuryBand);
            if (bandSuffix == null) return string.Empty;

            var key = $"hotel_{type.size.x}_{bandSuffix}";
            return ExpectedSizesByKey.ContainsKey(key) ? key : string.Empty;
        }

        public static string ResourcePath(string artKey) => "Art/Hotels/" + artKey;

        public static Vector2Int ExpectedPixelSize(string artKey)
        {
            if (string.IsNullOrEmpty(artKey)) return Vector2Int.zero;
            return ExpectedSizesByKey.TryGetValue(artKey, out var size) ? size : Vector2Int.zero;
        }

        public static void ResetForTests()
        {
        }

        static string BandSuffix(LuxuryBand band) => band switch
        {
            LuxuryBand.Base => "base",
            LuxuryBand.Mid => "mid",
            LuxuryBand.Upper => "upper",
            _ => null
        };
    }
}
