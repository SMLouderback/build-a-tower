using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Warm industrial colors + pulse math for the guided tutorial coach (IMGUI).
    /// Magenta is key-plate only — not used here.
    /// </summary>
    public static class TutorialHudChrome
    {
        public static readonly Color CardFill = new Color(0.18f, 0.15f, 0.11f, 0.94f);
        public static readonly Color CardBorder = new Color(0.72f, 0.58f, 0.36f, 0.95f);
        public static readonly Color TitleText = new Color(1f, 0.86f, 0.47f, 1f);
        public static readonly Color BodyText = new Color(0.95f, 0.92f, 0.86f, 1f);
        public static readonly Color PulseWarm = new Color(0.92f, 0.72f, 0.38f, 1f);

        public const float IntroCardWidth = 420f;
        public const float IntroCardHeight = 168f;
        public const float DockedCardWidth = 248f;
        public const float DockedCardHeight = 112f;
        public const float DockSlideSeconds = 0.35f;

        /// <summary>Centered intro modal.</summary>
        public static Rect IntroCardRect(float screenW, float screenH)
        {
            return new Rect(
                (screenW - IntroCardWidth) * 0.5f,
                (screenH - IntroCardHeight) * 0.5f,
                IntroCardWidth,
                IntroCardHeight);
        }

        /// <summary>Compact coach near the left build-tool strip.</summary>
        public static Rect DockedCardRect(float screenW, float screenH)
        {
            var x = Mathf.Clamp(168f, 8f, screenW - DockedCardWidth - 8f);
            var y = Mathf.Clamp(screenH * 0.28f, 72f, screenH - DockedCardHeight - 24f);
            return new Rect(x, y, DockedCardWidth, DockedCardHeight);
        }

        public static Rect LerpCardRect(Rect from, Rect to, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            // Ease-out for a soft settle next to the tools.
            var t = 1f - (1f - t01) * (1f - t01);
            return new Rect(
                Mathf.Lerp(from.x, to.x, t),
                Mathf.Lerp(from.y, to.y, t),
                Mathf.Lerp(from.width, to.width, t),
                Mathf.Lerp(from.height, to.height, t));
        }

        public static float PulseAlpha(float timeSeconds)
        {
            // Soft breathe between ~0.35 and ~0.80
            return 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(timeSeconds * 4f));
        }

        public static Color PulseOutlineColor(float timeSeconds)
        {
            var c = PulseWarm;
            c.a = PulseAlpha(timeSeconds);
            return c;
        }

        /// <summary>Returns tip when hover tips are on; otherwise empty.</summary>
        public static string GateHoverTip(string tip, bool hoverTipsEnabled)
        {
            if (!hoverTipsEnabled || string.IsNullOrEmpty(tip))
                return string.Empty;
            return tip;
        }

        /// <summary>
        /// Resolves the string drawn by the hover tip overlay.
        /// When tips are off, always empty (ignores both HUD and GUI.tooltip sources).
        /// </summary>
        public static string ResolveHoverTip(string hoverTooltip, string guiTooltip, bool hoverTipsEnabled)
        {
            if (!hoverTipsEnabled)
                return string.Empty;
            return string.IsNullOrEmpty(hoverTooltip) ? (guiTooltip ?? string.Empty) : hoverTooltip;
        }
    }
}
