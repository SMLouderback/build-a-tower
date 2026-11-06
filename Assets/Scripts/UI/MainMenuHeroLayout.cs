using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Shared layout helpers for main-menu presentation.
    /// Hit-target UV overlays were abandoned — root actions use normal plaque buttons.
    /// </summary>
    public static class MainMenuHeroLayout
    {
        public const float DefaultHeroAspect = 16f / 9f;

        /// <summary>
        /// Largest rectangle of <paramref name="imageAspect"/> (width/height) that fits
        /// inside a view of <paramref name="viewWidth"/> × <paramref name="viewHeight"/>,
        /// centered (letterbox / pillarbox as needed).
        /// </summary>
        public static Rect FitContain(float viewWidth, float viewHeight, float imageAspect)
        {
            if (viewWidth <= 0f || viewHeight <= 0f || imageAspect <= 0f)
                return Rect.zero;

            var viewAspect = viewWidth / viewHeight;
            float width;
            float height;
            if (viewAspect > imageAspect)
            {
                height = viewHeight;
                width = height * imageAspect;
            }
            else
            {
                width = viewWidth;
                height = width / imageAspect;
            }

            return new Rect(
                (viewWidth - width) * 0.5f,
                (viewHeight - height) * 0.5f,
                width,
                height);
        }

        public static float AspectOf(Texture tex, float fallback = DefaultHeroAspect)
        {
            if (tex == null || tex.height <= 0)
                return fallback;
            return tex.width / (float)tex.height;
        }
    }
}
