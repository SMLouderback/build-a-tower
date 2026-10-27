using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Shared build-family accent palette (HUD build menu, dollhouse accents).
    /// </summary>
    public static class BuildFamilyColors
    {
        public static Color For(BuildFamily family) => family switch
        {
            BuildFamily.Office => new Color(0.35f, 0.55f, 0.85f),
            BuildFamily.Hotel => new Color(0.62f, 0.35f, 0.85f),
            BuildFamily.Condo => new Color(0.35f, 0.75f, 0.45f),
            BuildFamily.Shops => new Color(0.9f, 0.6f, 0.25f),
            BuildFamily.Leisure => new Color(0.25f, 0.65f, 0.70f),
            BuildFamily.Utility => new Color(0.45f, 0.7f, 0.75f),
            BuildFamily.Transit => new Color(0.7f, 0.7f, 0.35f),
            _ => Color.gray
        };
    }
}
